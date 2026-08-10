using System.Text.RegularExpressions;
using Lokad.Lython.Frontend;
using System.Numerics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object EvaluateConditional(ConditionalExpressionSyntax conditional, ExecutionContext context)
    {
        return IsTruthy(EvaluateExpression(conditional.Condition, context), context, conditional.Condition.Span)
            ? EvaluateExpression(conditional.Consequent, context)
            : EvaluateExpression(conditional.Alternative, context);
    }

    private static void ExecuteMatchStatement(MatchStatementSyntax statement, ExecutionContext context)
    {
        var subject = EvaluateExpression(statement.Subject, context);
        ExecuteMatch(statement, subject, context);
    }

    private static void ExecuteMatch(MatchStatementSyntax statement, object subject, ExecutionContext context)
    {
        foreach (var matchCase in statement.Cases)
        {
            var bindings = new Dictionary<string, object>(StringComparer.Ordinal);
            if (!TryMatchPattern(matchCase.Pattern, subject, context, bindings))
            {
                continue;
            }

            if (matchCase.Guard is not null)
            {
                var guardContext = new ExecutionContext(context);
                foreach (var pair in bindings)
                {
                    guardContext.Variables[pair.Key] = pair.Value;
                }

                if (!IsTruthy(EvaluateExpression(matchCase.Guard, guardContext)))
                {
                    continue;
                }
            }

            foreach (var pair in bindings)
            {
                StoreName(pair.Key, pair.Value, context, matchCase.Span);
            }

            var signal = ExecuteStatements(matchCase.Body, context);
            if (signal is not null)
            {
                throw signal;
            }

            return;
        }
    }

    private static bool TryMatchPattern(PatternSyntax pattern, object subject, ExecutionContext context, Dictionary<string, object> bindings)
    {
        switch (pattern)
        {
            case MatchValuePatternSyntax valuePattern:
                return AreEqual(subject, EvaluateExpression(valuePattern.Expression, context));

            case MatchSingletonPatternSyntax singletonPattern:
                return singletonPattern.Value switch
                {
                    MatchSingletonKind.None => subject is PyNone,
                    MatchSingletonKind.True => subject is bool boolean && boolean,
                    MatchSingletonKind.False => subject is bool boolean && !boolean,
                    _ => false
                };

            case MatchCapturePatternSyntax capturePattern:
                return TryBindPatternName(capturePattern.Name, subject, bindings);

            case MatchWildcardPatternSyntax:
                return true;

            case MatchSequencePatternSyntax sequencePattern:
                return TryMatchSequencePattern(sequencePattern, subject, context, bindings);

            case MatchMappingPatternSyntax mappingPattern:
                return TryMatchMappingPattern(mappingPattern, subject, context, bindings);

            case MatchClassPatternSyntax classPattern:
                return TryMatchClassPattern(classPattern, subject, context, bindings);

            case MatchStarPatternSyntax starPattern:
                return starPattern.Name is null || TryBindPatternName(starPattern.Name, subject, bindings);

            case MatchAsPatternSyntax asPattern:
                return TryMatchPattern(asPattern.Pattern, subject, context, bindings)
                    && TryBindPatternName(asPattern.Name, subject, bindings);

            case MatchOrPatternSyntax orPattern:
                foreach (var candidate in orPattern.Patterns)
                {
                    var branchBindings = new Dictionary<string, object>(bindings, StringComparer.Ordinal);
                    if (!TryMatchPattern(candidate, subject, context, branchBindings))
                    {
                        continue;
                    }

                    bindings.Clear();
                    foreach (var pair in branchBindings)
                    {
                        bindings[pair.Key] = pair.Value;
                    }

                    return true;
                }

                return false;

            default:
                throw new InvalidOperationException($"Unknown pattern type: {pattern.GetType().Name}");
        }
    }

    private static bool TryMatchSequencePattern(
        MatchSequencePatternSyntax pattern,
        object subject,
        ExecutionContext context,
        Dictionary<string, object> bindings)
    {
        if (!TryGetPatternSequence(subject, out var items))
        {
            return false;
        }

        var starIndex = pattern.Items
            .Select((item, index) => (item, index))
            .FirstOrDefault(pair => pair.item is MatchStarPatternSyntax).index;
        var hasStar = pattern.Items.Any(item => item is MatchStarPatternSyntax);

        if (!hasStar)
        {
            if (items.Count != pattern.Items.Count)
            {
                return false;
            }

            for (var i = 0; i < pattern.Items.Count; i++)
            {
                if (!TryMatchPattern(pattern.Items[i], items[i], context, bindings))
                {
                    return false;
                }
            }

            return true;
        }

        var beforeCount = starIndex;
        var afterCount = pattern.Items.Count - starIndex - 1;
        if (items.Count < beforeCount + afterCount)
        {
            return false;
        }

        for (var i = 0; i < beforeCount; i++)
        {
            if (!TryMatchPattern(pattern.Items[i], items[i], context, bindings))
            {
                return false;
            }
        }

        var starPattern = (MatchStarPatternSyntax)pattern.Items[starIndex];
        var starItems = items.Skip(beforeCount).Take(items.Count - beforeCount - afterCount).ToArray();
        if (starPattern.Name is not null &&
            !TryBindPatternName(
                starPattern.Name,
                new PyList(starItems, context.MemoryGovernor, pattern.Span),
                bindings))
        {
            return false;
        }

        for (var i = 0; i < afterCount; i++)
        {
            var patternIndex = starIndex + 1 + i;
            var itemIndex = items.Count - afterCount + i;
            if (!TryMatchPattern(pattern.Items[patternIndex], items[itemIndex], context, bindings))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryMatchMappingPattern(
        MatchMappingPatternSyntax pattern,
        object subject,
        ExecutionContext context,
        Dictionary<string, object> bindings)
    {
        if (subject is not PyDict dict)
        {
            return false;
        }

        var matchedKeys = new HashSet<object>(new PyValueComparer());
        foreach (var item in pattern.Items)
        {
            var key = ValidateDictionaryKey(EvaluateExpression(item.Key, context), item.Key.Span, context.MemoryGovernor);
            if (!dict.TryGetValue(key, out var value))
            {
                return false;
            }

            matchedKeys.Add(key);
            if (!TryMatchPattern(item.Pattern, value, context, bindings))
            {
                return false;
            }
        }

        if (pattern.RestName is not null)
        {
            var rest = new PyDict(context.MemoryGovernor, pattern.Span);
            foreach (var pair in dict)
            {
                if (!matchedKeys.Contains(pair.Key))
                {
                    rest.SetItem(pair.Key, pair.Value);
                }
            }

            if (!TryBindPatternName(pattern.RestName, rest, bindings))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryMatchClassPattern(
        MatchClassPatternSyntax pattern,
        object subject,
        ExecutionContext context,
        Dictionary<string, object> bindings)
    {
        var classValue = TryResolvePatternClassValue(pattern.ClassExpression, context);
        if (classValue is PyType runtimeType)
        {
            if (subject is not PyInstance instance || !instance.Type.IsSubtypeOf(runtimeType))
            {
                return false;
            }

            if (pattern.PositionalPatterns.Count != 0)
            {
                if (!runtimeType.TryGetMatchArgs(out var matchArgs))
                {
                    throw new LythonRuntimeException(
                        "TypeError",
                        $"Class '{runtimeType.Name}' does not define __match_args__ for positional class patterns.",
                        pattern.Span);
                }

                if (pattern.PositionalPatterns.Count > matchArgs.Count)
                {
                    throw new LythonRuntimeException(
                        "TypeError",
                        $"Class '{runtimeType.Name}' accepts {matchArgs.Count} positional class pattern argument(s), {pattern.PositionalPatterns.Count} given.",
                        pattern.Span);
                }

                for (var i = 0; i < pattern.PositionalPatterns.Count; i++)
                {
                    if (!PyMemberAccess.TryResolve(subject, matchArgs[i], context, pattern.Span, out var memberValue) ||
                        !TryMatchPattern(pattern.PositionalPatterns[i], RuntimeValue(memberValue), context, bindings))
                    {
                        return false;
                    }
                }
            }

            foreach (var keyword in pattern.KeywordPatterns)
            {
                if (!PyMemberAccess.TryResolve(subject, keyword.Name, context, keyword.Pattern.Span, out var memberValue))
                {
                    return false;
                }

                if (!TryMatchPattern(keyword.Pattern, RuntimeValue(memberValue), context, bindings))
                {
                    return false;
                }
            }

            return true;
        }

        if (!TryGetPatternClassName(pattern.ClassExpression, out var className))
        {
            throw new LythonRuntimeException(
                "TypeError",
                "match class patterns require a simple class name or dotted class name.",
                pattern.Span);
        }

        if (!DoesSubjectMatchPatternClass(className, subject))
        {
            return false;
        }

        if (pattern.PositionalPatterns.Count != 0)
        {
            return className switch
            {
                "list" when subject is PyList => TryMatchSequencePattern(
                    new MatchSequencePatternSyntax(pattern.PositionalPatterns, pattern.Span),
                    subject,
                    context,
                    bindings),
                "tuple" when subject is PyTuple => TryMatchSequencePattern(
                    new MatchSequencePatternSyntax(pattern.PositionalPatterns, pattern.Span),
                    subject,
                    context,
                    bindings),
                _ => throw new LythonRuntimeException(
                    "TypeError",
                    $"match class pattern '{className}(...)' does not support positional subpatterns in Lython.",
                    pattern.Span)
            };
        }

        foreach (var keyword in pattern.KeywordPatterns)
        {
            if (!PyMemberAccess.TryResolve(subject, keyword.Name, context, keyword.Pattern.Span, out var memberValue))
            {
                return false;
            }

            memberValue = RuntimeValue(memberValue);
            if (!TryMatchPattern(keyword.Pattern, memberValue, context, bindings))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetPatternSequence(object subject, out IReadOnlyList<object> items)
    {
        switch (subject)
        {
            case PyList list:
                items = list.ToArray();
                return true;
            case PyTuple tuple:
                items = tuple.ToArray();
                return true;
            default:
                items = Array.Empty<object>();
                return false;
        }
    }

    private static bool TryBindPatternName(string name, object value, Dictionary<string, object> bindings)
    {
        value = RuntimeValue(value);
        if (bindings.TryGetValue(name, out var existing))
        {
            return AreEqual(existing, value);
        }

        bindings[name] = value;
        return true;
    }

    private static bool TryGetPatternClassName(ExpressionSyntax expression, out string className)
    {
        switch (expression)
        {
            case IdentifierExpressionSyntax identifier:
                className = identifier.Name;
                return true;
            case MemberExpressionSyntax member when TryGetPatternClassName(member.Target, out var prefix):
                className = prefix + "." + member.MemberName;
                return true;
            default:
                className = string.Empty;
                return false;
        }
    }

    private static object? TryResolvePatternClassValue(ExpressionSyntax expression, ExecutionContext context)
    {
        return expression switch
        {
            IdentifierExpressionSyntax identifier => ResolveIdentifier(identifier, context),
            MemberExpressionSyntax member => ResolveMember(member, context),
            _ => null
        };
    }

    private static bool DoesSubjectMatchPatternClass(string className, object subject)
    {
        return className switch
        {
            "list" => subject is PyList,
            "tuple" => subject is PyTuple,
            "dict" => subject is PyDict,
            "set" => subject is PySet,
            "str" => subject is PyString,
            "bytes" => subject is PyBytes,
            "bool" => subject is bool,
            "int" => subject is BigInteger or int or bool,
            "float" => subject is double,
            "pathlib.Path" => subject is PyPath,
            "datetime.timedelta" => subject is PyTimedelta,
            "datetime.date" => subject is PyDate,
            "datetime.time" => subject is PyTime,
            "datetime.datetime" => subject is PyDateTime,
            "datetime.timezone" => subject is PyTimezone,
            "statistics.NormalDist" => subject is StatisticsModule.PyNormalDist,
            "random.Random" => subject is RandomModule.PyRandom,
            "re.Match" => subject is ReMatchObject,
            "re.Pattern" => subject is RePatternObject,
            _ => false
        };
    }

    private static IReadOnlyList<PyType> ResolveClassBases(object[] baseValues, LythonSourceSpan span, ExecutionContext context)
    {
        if (baseValues.Length == 0)
        {
            return context.TryGetBuiltinType("object", out var rootType)
                ? [rootType]
                : Array.Empty<PyType>();
        }

        var bases = new List<PyType>(baseValues.Length);
        foreach (var baseValue in baseValues)
        {
            if (baseValue is PyTypingAlias { IsInertClassBase: true })
            {
                continue;
            }

            if (baseValue is not PyType type)
            {
                throw new LythonRuntimeException("TypeError", "Class bases must be user-defined Lython classes.", span);
            }

            bases.Add(type);
        }

        if (bases.Count == 0)
        {
            return context.TryGetBuiltinType("object", out var rootType)
                ? [rootType]
                : Array.Empty<PyType>();
        }

        return bases;
    }
}
