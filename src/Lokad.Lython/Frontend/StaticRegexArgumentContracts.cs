using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using static Lokad.Lython.Frontend.StaticKnownCallArgumentChecks;

namespace Lokad.Lython.Frontend;

internal static class StaticRegexArgumentContracts
{
    public static bool AnalyzeKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;
        if (string.Equals(targetName, LythonKnownCallableSignatures.ReCompile.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeRegexPatternArgument(arguments, 0, "pattern", "re.compile(pattern[, flags]) expects pattern to be a string or compiled regex pattern.", diagnostics, bindings);
            if (IsCompiledRegexPattern(arguments, 0, "pattern", bindings) &&
                TryGetArgument(arguments, 1, "flags", bindings, out var compiledFlagsExpression, out var compiledFlagsValue) &&
                IsKnownTruthyFlags(compiledFlagsValue))
            {
                AddDiagnostic(diagnostics, "LA3158", "re.compile(pattern[, flags]) expects integer flags and no flags when pattern is compiled.", compiledFlagsExpression.Span);
                emitted = true;
            }
            if (TryGetArgument(arguments, 1, "flags", bindings, out var flagsExpression, out var flagsValue))
                emitted |= AnalyzeUnsupportedFlags(flagsExpression, flagsValue, diagnostics);

            emitted |= AnalyzeIntegerOrNoneArgument(arguments, 1, "flags", "re.compile(pattern[, flags]) expects flags to be an integer or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ReSearch.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.ReMatch.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.ReFullMatch.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.ReFindAll.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.ReFindIter.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeRegexPatternArgument(arguments, 0, "pattern", $"{targetName}(pattern, string[, flags][, pos][, endpos]) expects pattern to be a string or compiled regex pattern.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "string", $"{targetName}(pattern, string[, flags][, pos][, endpos]) expects string to be a string.", diagnostics, bindings);
            emitted |= AnalyzeRegexFlagsArgument(arguments, 0, "pattern", 2, "flags", $"{targetName}(pattern, string[, flags][, pos][, endpos]) expects integer flags and no flags when pattern is compiled.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 3, "pos", $"{targetName}(pattern, string[, flags][, pos][, endpos]) expects pos to be an integer.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 4, "endpos", $"{targetName}(pattern, string[, flags][, pos][, endpos]) expects endpos to be an integer.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ReSub.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.ReSubn.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeRegexPatternArgument(arguments, 0, "pattern", $"{targetName}(pattern, repl, string[, count][, flags][, pos][, endpos]) expects pattern to be a string or compiled regex pattern.", diagnostics, bindings);
            emitted |= AnalyzeStringOrCallableArgument(arguments, 1, "repl", $"{targetName}(pattern, repl, string[, count][, flags][, pos][, endpos]) expects repl to be a string or callable.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 2, "string", $"{targetName}(pattern, repl, string[, count][, flags][, pos][, endpos]) expects string to be a string.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 3, "count", $"{targetName}(pattern, repl, string[, count][, flags][, pos][, endpos]) expects count to be an integer.", diagnostics, bindings);
            emitted |= AnalyzeRegexFlagsArgument(arguments, 0, "pattern", 4, "flags", $"{targetName}(pattern, repl, string[, count][, flags][, pos][, endpos]) expects integer flags and no flags when pattern is compiled.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 5, "pos", $"{targetName}(pattern, repl, string[, count][, flags][, pos][, endpos]) expects pos to be an integer.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 6, "endpos", $"{targetName}(pattern, repl, string[, count][, flags][, pos][, endpos]) expects endpos to be an integer.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ReSplit.Name, StringComparison.Ordinal))
        {
            emitted |= AnalyzeRegexPatternArgument(arguments, 0, "pattern", "re.split(pattern, string[, maxsplit][, flags][, pos][, endpos]) expects pattern to be a string or compiled regex pattern.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "string", "re.split(pattern, string[, maxsplit][, flags][, pos][, endpos]) expects string to be a string.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 2, "maxsplit", "re.split(pattern, string[, maxsplit][, flags][, pos][, endpos]) expects maxsplit to be an integer.", diagnostics, bindings);
            emitted |= AnalyzeRegexFlagsArgument(arguments, 0, "pattern", 3, "flags", "re.split(pattern, string[, maxsplit][, flags][, pos][, endpos]) expects integer flags and no flags when pattern is compiled.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 4, "pos", "re.split(pattern, string[, maxsplit][, flags][, pos][, endpos]) expects pos to be an integer.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 5, "endpos", "re.split(pattern, string[, maxsplit][, flags][, pos][, endpos]) expects endpos to be an integer.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ReEscape.Name, StringComparison.Ordinal))
        {
            return AnalyzeStringArgument(arguments, 0, "string", "re.escape(string) expects a string argument.", diagnostics, bindings);
        }

        return false;
    }

    public static bool AnalyzeCallableSemanticContract(
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        AbstractValue receiver,
        string memberName)
    {
        var emitted = false;
        if (receiver.Kind == AbstractValueKind.RegexMatch &&
            string.Equals(memberName, "group", StringComparison.Ordinal))
        {
            emitted |= AnalyzeRegexMatchGroupContract(arguments, diagnostics, bindings, receiver);
        }

        if (receiver.Kind == AbstractValueKind.RegexMatch &&
            memberName is "start" or "end" or "span")
        {
            emitted |= AnalyzeRegexMatchSingleGroupContract(memberName, arguments, diagnostics, bindings, receiver);
        }

        if (receiver.Kind == AbstractValueKind.RegexMatch &&
            string.Equals(memberName, "expand", StringComparison.Ordinal))
        {
            emitted |= AnalyzeStringArgument(arguments, 0, "template", "match.expand(template) expects template to be a string.", diagnostics, bindings);
        }

        if (receiver.Kind == AbstractValueKind.RegexPattern)
        {
            emitted |= AnalyzeRegexPatternMemberArgumentTypes(memberName, arguments, diagnostics, bindings);
        }

        return emitted;
    }

    public static bool AnalyzeRegexFlagsArgument(
        ConcreteCallArguments arguments,
        int patternPosition,
        string patternKeyword,
        int flagsPosition,
        string flagsKeyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!TryGetArgument(arguments, flagsPosition, flagsKeyword, bindings, out var flagsExpression, out var flagsValue))
        {
            return false;
        }

        if (AnalyzeUnsupportedFlags(flagsExpression, flagsValue, diagnostics)) return true;

        if (TryGetArgument(arguments, patternPosition, patternKeyword, bindings, out _, out var patternValue) &&
            patternValue.Kind == AbstractValueKind.RegexPattern &&
            IsKnownTruthyFlags(flagsValue))
        {
            AddDiagnostic(diagnostics, "LA3158", message, flagsExpression.Span);
            return true;
        }

        return AnalyzeKnownArgumentValue(flagsExpression, flagsValue, message, diagnostics, static value => value.Kind == AbstractValueKind.None || StaticAbstractFacts.IsStrictIntegerLike(value));
    }

    private static bool IsCompiledRegexPattern(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        AbstractState bindings)
        => TryGetArgument(arguments, position, keyword, bindings, out _, out var value) &&
            value.Kind == AbstractValueKind.RegexPattern;

    // Only resolved values belong to regex flag policy. An unrelated receiver
    // called re, or a shadowed flag member, does not identify the regex module.
    private static bool AnalyzeUnsupportedFlags(ExpressionSyntax expression, AbstractValue value, List<LythonDiagnostic> diagnostics)
    {
        if (!TryGetInt32(value, out var flags)) return false;
        var emitted = false;
        if ((flags & 4) != 0)
        {
            AddDiagnostic(diagnostics, "LA3045", "re.LOCALE is unsupported.", expression.Span);
            emitted = true;
        }
        if ((flags & 128) != 0)
        {
            AddDiagnostic(diagnostics, "LA3045", "re.DEBUG is unsupported.", expression.Span);
            emitted = true;
        }
        return emitted;
    }

    private static bool IsKnownTruthyFlags(AbstractValue value)
        => value.Kind switch
        {
            AbstractValueKind.Boolean => value.RequireBoolean(),
            AbstractValueKind.Integer => TryGetInt32(value, out var integer) && integer != 0,
            _ => false,
        };

    public static bool AnalyzeRegexPatternMemberArgumentTypes(
        string memberName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;
        if (memberName is "search" or "match" or "fullmatch" or "findall" or "finditer")
        {
            emitted |= AnalyzeStringArgument(arguments, 0, "string", $"pattern.{memberName}(string[, pos[, endpos]]) expects a string argument.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 1, "pos", $"pattern.{memberName}(string[, pos[, endpos]]) expects pos to be an integer.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 2, "endpos", $"pattern.{memberName}(string[, pos[, endpos]]) expects endpos to be an integer.", diagnostics, bindings);
            return emitted;
        }

        if (memberName is "sub" or "subn")
        {
            emitted |= AnalyzeStringOrCallableArgument(arguments, 0, "repl", $"pattern.{memberName}(repl, string[, count[, pos[, endpos]]]) expects repl to be a string or callable.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "string", $"pattern.{memberName}(repl, string[, count[, pos[, endpos]]]) expects string to be a string.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 2, "count", $"pattern.{memberName}(repl, string[, count[, pos[, endpos]]]) expects count to be an integer.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 3, "pos", $"pattern.{memberName}(repl, string[, count[, pos[, endpos]]]) expects pos to be an integer.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 4, "endpos", $"pattern.{memberName}(repl, string[, count[, pos[, endpos]]]) expects endpos to be an integer.", diagnostics, bindings);
            return emitted;
        }

        if (memberName == "split")
        {
            emitted |= AnalyzeStringArgument(arguments, 0, "string", "pattern.split(string[, maxsplit[, pos[, endpos]]]) expects string to be a string.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 1, "maxsplit", "pattern.split(string[, maxsplit[, pos[, endpos]]]) expects maxsplit to be an integer.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 2, "pos", "pattern.split(string[, maxsplit[, pos[, endpos]]]) expects pos to be an integer.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 3, "endpos", "pattern.split(string[, maxsplit[, pos[, endpos]]]) expects endpos to be an integer.", diagnostics, bindings);
            return emitted;
        }

        return false;
    }

    public static bool AnalyzeRegexMatchGroupContract(
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        AbstractValue receiver)
    {
        var summary = receiver.RequireRegexMatchSummary();
        var emitted = false;
        for (var i = 0; i < arguments.Positional.Count; i++)
        {
            var expression = arguments.Positional[i];
            var value = arguments.ResolvePositionalValue(i, bindings);
            if (IsUnknown(value) || value.Kind == AbstractValueKind.UserInstance)
            {
                continue;
            }

            if (TryGetGroupIndex(value, out var index))
            {
                if (summary.CaptureSlotCount.HasValue &&
                    (index < 0 || index >= summary.CaptureSlotCount.Value))
                {
                    AddDiagnostic(diagnostics, "LA3159", "Regex group index is out of range.", DiagnosticSpan(expression, value));
                    emitted = true;
                }

                continue;
            }

            if (value.Kind is AbstractValueKind.IntegerType or AbstractValueKind.BooleanType)
            {
                continue;
            }

            if (value.Kind == AbstractValueKind.String)
            {
                var name = value.RequireText();
                if (summary.CaptureSlotCount.HasValue &&
                    !summary.NamedGroups.ContainsKey(PyString.FromString(name)))
                {
                    AddDiagnostic(diagnostics, "LA3159", $"Regex group '{name}' is not defined.", DiagnosticSpan(expression, value));
                    emitted = true;
                }

                continue;
            }

            if (value.Kind == AbstractValueKind.StringType)
            {
                continue;
            }

            AddDiagnostic(diagnostics, "LA3159", "match.group(index) expects an integer or group name.", DiagnosticSpan(expression, value));
            emitted = true;
        }

        return emitted;
    }

    public static bool AnalyzeRegexMatchSingleGroupContract(
        string memberName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        AbstractValue receiver)
    {
        ExpressionSyntax expression;
        AbstractValue value;
        if (arguments.Positional.Count > 0)
        {
            expression = arguments.Positional[0];
            value = arguments.ResolvePositionalValue(0, bindings);
        }
        else if (arguments.Keywords.TryGetValue("group", out var keywordExpression))
        {
            expression = keywordExpression;
            value = arguments.ResolveKeywordValue("group", bindings);
        }
        else
        {
            return false;
        }

        if (IsUnknown(value) || value.Kind == AbstractValueKind.UserInstance)
        {
            return false;
        }

        var summary = receiver.RequireRegexMatchSummary();
        if (TryGetGroupIndex(value, out var index))
        {
            if (summary.CaptureSlotCount.HasValue &&
                (index < 0 || index >= summary.CaptureSlotCount.Value))
            {
                AddDiagnostic(diagnostics, "LA3159", "Regex group index is out of range.", DiagnosticSpan(expression, value));
                return true;
            }

            return false;
        }

        if (value.Kind is AbstractValueKind.IntegerType or AbstractValueKind.BooleanType)
        {
            return false;
        }

        if (value.Kind == AbstractValueKind.String)
        {
            var name = value.RequireText();
            if (summary.CaptureSlotCount.HasValue &&
                !summary.NamedGroups.ContainsKey(PyString.FromString(name)))
            {
                AddDiagnostic(diagnostics, "LA3159", $"Regex group '{name}' is not defined.", DiagnosticSpan(expression, value));
                return true;
            }

            return false;
        }

        if (value.Kind == AbstractValueKind.StringType)
        {
            return false;
        }

        AddDiagnostic(diagnostics, "LA3159", $"match.{memberName}(group=0) expects an integer or group name.", DiagnosticSpan(expression, value));
        return true;
    }

    private static bool TryGetGroupIndex(AbstractValue value, out int index)
    {
        if (value.Kind == AbstractValueKind.Boolean)
        {
            index = value.RequireBoolean() ? 1 : 0;
            return true;
        }
        return TryGetInt32(value, out index);
    }
}
