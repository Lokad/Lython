using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class PyRegexFindIterator : PyIteratorBase
    {
        private readonly RePatternObject _pattern;
        private readonly RegexSubjectRange _range;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        private readonly System.Collections.IEnumerator? _matches;

        public PyRegexFindIterator(RePatternObject pattern, RegexSubjectRange range, ExecutionContext context, LythonSourceSpan span)
        {
            _pattern = pattern;
            _range = range;
            _context = context;
            _span = span;
            _matches = RegexMatcher.CreateDetailedFindMatches(pattern, range).GetEnumerator();
        }

        public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            if (_matches is null || !_matches.MoveNext())
            {
                value = PyNone.Instance;
                return false;
            }

            _context.CheckExecutionBudget(_span);
            value = RegexMatcher.CreateMatchObject(_pattern, _range, (Utf8PythonDetailedMatchData)_matches.Current.RequireNotNull(), _context, _span);
            return true;
        }

        public override PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<callable_iterator object>");
        }
    }

    internal static class ReMatchMembers
    {
        private readonly record struct RegexGroupBounds(BigInteger Start, BigInteger End);

        public static bool TryGetMember(ReMatchObject match, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "re" => match.Pattern,
                "string" => match.String,
                "pos" => match.Pos,
                "endpos" => match.EndPos,
                "lastindex" => LastIndex(match),
                "lastgroup" => LastGroup(match),
                "group" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length == 0)
                    {
                        return match.Value;
                    }

                    if (arguments.Length == 1)
                    {
                        return arguments[0] switch
                        {
                            BigInteger integer => ResolveIndexedGroup(match, (int)integer, span),
                            int integer => ResolveIndexedGroup(match, integer, span),
                            _ when PyStringOps.TryAsString(arguments[0], out var nameText) => ResolveNamedGroup(match, nameText.AsString(), span),
                            _ => throw new LythonRuntimeException("TypeError", "match.group(index) expects an integer or group name.", span)
                        };
                    }

                    var groups = new object[arguments.Length];
                    for (var i = 0; i < arguments.Length; i++)
                    {
                        var argument = arguments[i];
                        groups[i] = argument switch
                        {
                            BigInteger integer => ResolveIndexedGroup(match, (int)integer, span),
                            int integer => ResolveIndexedGroup(match, integer, span),
                            _ when PyStringOps.TryAsString(argument, out var nameText) => ResolveNamedGroup(match, nameText.AsString(), span),
                            _ => throw new LythonRuntimeException("TypeError", "match.group(index) expects an integer or group name.", span)
                        };
                    }

                    return new PyTuple(groups, context.MemoryGovernor, span);
                }),
                "groups" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "match.groups(default=None) expects zero or one argument.", span);
                    }

                    var defaultValue = arguments.Length == 1 ? arguments[0] : PyNone.Instance;
                    var groups = new object[Math.Max(0, match.CaptureSlotCount - 1)];
                    for (var i = 1; i < match.CaptureSlotCount; i++)
                    {
                        groups[i - 1] = ResolveIndexedGroup(match, i, span, defaultValue);
                    }

                    return new PyTuple(groups, context.MemoryGovernor, span);
                }, "match.groups", ["default"], 0),
                "groupdict" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "match.groupdict(default=None) expects zero or one argument.", span);
                    }

                    var defaultValue = arguments.Length == 1 ? arguments[0] : PyNone.Instance;
                    var dict = new PyDict(context.MemoryGovernor, span);
                    foreach (var entry in match.NamedGroups.OrderBy(entry => entry.Value))
                    {
                        dict.SetItem(PyString.FromString(entry.Key, context.MemoryGovernor, span), ResolveIndexedGroup(match, entry.Value, span, defaultValue));
                    }

                    return dict;
                }, "match.groupdict", ["default"], 0),
                "expand" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var template))
                    {
                        throw new LythonRuntimeException("TypeError", "match.expand(template) expects one string argument.", span);
                    }

                    return ExpandReplacementTemplate(match, template, context, span);
                }, "match.expand", ["template"]),
                "start" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "match.start(group=0) expects zero or one group identifier.", span);
                    }

                    return ResolveGroupBounds(match, arguments.Length == 0 ? BigInteger.Zero : arguments[0], span).Start;
                }, "match.start", ["group"], 0),
                "end" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "match.end(group=0) expects zero or one group identifier.", span);
                    }

                    return ResolveGroupBounds(match, arguments.Length == 0 ? BigInteger.Zero : arguments[0], span).End;
                }, "match.end", ["group"], 0),
                "span" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "match.span(group=0) expects zero or one group identifier.", span);
                    }

                    var bounds = ResolveGroupBounds(match, arguments.Length == 0 ? BigInteger.Zero : arguments[0], span);
                    return CreateTuple(2, i => i == 0 ? bounds.Start : bounds.End, context, span);
                }, "match.span", ["group"], 0),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object ResolveIndexedGroup(ReMatchObject match, int index, LythonSourceSpan span)
            => ResolveIndexedGroup(match, index, span, PyNone.Instance);

        private static object ResolveIndexedGroup(ReMatchObject match, int index, LythonSourceSpan span, object defaultValue)
        {
            if (index < 0 || index >= match.CaptureSlotCount)
            {
                throw new LythonRuntimeException("IndexError", "Regex group index is out of range.", span);
            }

            if (index == 0)
            {
                return match.Value;
            }

            return (object?)match.Captures[index - 1]?.Value ?? defaultValue;
        }

        private static object ResolveNamedGroup(ReMatchObject match, string groupName, LythonSourceSpan span)
        {
            if (!match.NamedGroups.TryGetValue(groupName, out var number))
            {
                throw new LythonRuntimeException("IndexError", $"Regex group '{groupName}' is not defined.", span);
            }

            return ResolveIndexedGroup(match, number, span);
        }

        private static RegexGroupBounds ResolveGroupBounds(ReMatchObject match, object group, LythonSourceSpan span)
        {
            if (TryResolveGroupIndex(match, group, span, out var index))
            {
                if (index == 0)
                {
                    return new RegexGroupBounds(match.Start, match.End);
                }

                var capture = match.Captures[index - 1];
                return capture is null
                    ? new RegexGroupBounds(new BigInteger(-1), new BigInteger(-1))
                    : new RegexGroupBounds(capture.Start, capture.End);
            }

            throw new LythonRuntimeException("TypeError", "Regex group identifier must be an integer or group name.", span);
        }

        private static bool TryResolveGroupIndex(ReMatchObject match, object group, LythonSourceSpan span, out int index)
        {
            index = group switch
            {
                BigInteger integer => integer < int.MinValue || integer > int.MaxValue
                    ? throw new LythonRuntimeException("IndexError", "Regex group index is out of range.", span)
                    : (int)integer,
                int integer => integer,
                _ when PyStringOps.TryAsString(group, out var nameText) => match.NamedGroups.TryGetValue(nameText.AsString(), out var namedIndex)
                    ? namedIndex
                    : throw new LythonRuntimeException("IndexError", $"Regex group '{nameText.AsString()}' is not defined.", span),
                _ => int.MinValue
            };

            if (index == int.MinValue)
            {
                return false;
            }

            if (index < 0 || index >= match.CaptureSlotCount)
            {
                throw new LythonRuntimeException("IndexError", "Regex group index is out of range.", span);
            }

            return true;
        }

        private static object LastIndex(ReMatchObject match)
        {
            for (var i = match.Captures.Count - 1; i >= 0; i--)
            {
                if (match.Captures[i] is not null)
                {
                    return new BigInteger(i + 1);
                }
            }

            return PyNone.Instance;
        }

        private static object LastGroup(ReMatchObject match)
        {
            var lastIndex = LastIndex(match);
            if (lastIndex is not BigInteger index)
            {
                return PyNone.Instance;
            }

            foreach (var entry in match.NamedGroups)
            {
                if (entry.Value == (int)index)
                {
                    return PyString.FromString(entry.Key);
                }
            }

            return PyNone.Instance;
        }

        internal static PyString ExpandReplacementTemplate(ReMatchObject match, PyString template, ExecutionContext context, LythonSourceSpan span)
        {
            var text = template.AsString();
            var builder = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (ch != '\\')
                {
                    builder.Append(ch);
                    continue;
                }

                if (++i >= text.Length)
                {
                    builder.Append('\\');
                    break;
                }

                var escaped = text[i];
                if (char.IsDigit(escaped))
                {
                    var start = i;
                    while (i + 1 < text.Length && char.IsDigit(text[i + 1]))
                    {
                        i++;
                    }

                    AppendExpandedGroup(match, text[start..(i + 1)], builder, span);
                    continue;
                }

                if (escaped == 'g' && i + 1 < text.Length && text[i + 1] == '<')
                {
                    var end = text.IndexOf('>', i + 2);
                    if (end < 0)
                    {
                        throw new LythonRuntimeException("error", "missing > in regex replacement group reference.", span);
                    }

                    AppendExpandedGroup(match, text[(i + 2)..end], builder, span);
                    i = end;
                    continue;
                }

                builder.Append(escaped switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    'f' => '\f',
                    'v' => '\v',
                    'a' => '\a',
                    'b' => '\b',
                    _ => escaped
                });
            }

            return CreateString(builder.ToString(), context, span);
        }

        private static void AppendExpandedGroup(ReMatchObject match, string reference, StringBuilder builder, LythonSourceSpan span)
        {
            object group = int.TryParse(reference, out var index)
                ? new BigInteger(index)
                : PyString.FromString(reference);

            if (!TryResolveGroupIndex(match, group, span, out var groupIndex))
            {
                throw new LythonRuntimeException("error", "invalid regex replacement group reference.", span);
            }

            var value = ResolveIndexedGroup(match, groupIndex, span, PyString.Empty);
            if (PyStringOps.TryAsString(value, out var groupText))
            {
                builder.Append(groupText.AsString());
            }
        }
    }

    internal static class RePatternMembers
    {
        public static bool TryGetMember(RePatternObject pattern, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "pattern" => pattern.Pattern,
                "flags" => new BigInteger(pattern.Flags),
                "groups" => new BigInteger(Math.Max(0, pattern.CaptureSlotCount - 1)),
                "groupindex" => CreateGroupIndex(pattern),
                "search" => BoundCallable.Create((arguments, span, context) => ExecuteMatch(pattern, arguments, span, context, static (regex, input) => regex.SearchDetailedData(input)), "pattern.search", ["string", "pos", "endpos"], 1),
                "match" => BoundCallable.Create((arguments, span, context) => ExecuteMatch(pattern, arguments, span, context, static (regex, input) => regex.MatchDetailedData(input)), "pattern.match", ["string", "pos", "endpos"], 1),
                "fullmatch" => BoundCallable.Create((arguments, span, context) => ExecuteMatch(pattern, arguments, span, context, static (regex, input) => regex.FullMatchDetailedData(input)), "pattern.fullmatch", ["string", "pos", "endpos"], 1),
                "findall" => BoundCallable.Create((arguments, span, context) => ExecuteFindAll(pattern, arguments, span, context), "pattern.findall", ["string", "pos", "endpos"], 1),
                "finditer" => BoundCallable.Create((arguments, span, context) => ExecuteFindIter(pattern, arguments, span, context), "pattern.finditer", ["string", "pos", "endpos"], 1),
                "sub" => BoundCallable.Create((arguments, span, context) => ExecuteSub(pattern, arguments, span, context, RegexSubstitutionMode.TextOnly), "pattern.sub", ["repl", "string", "count", "pos", "endpos"], 2),
                "subn" => BoundCallable.Create((arguments, span, context) => ExecuteSub(pattern, arguments, span, context, RegexSubstitutionMode.TextAndCount), "pattern.subn", ["repl", "string", "count", "pos", "endpos"], 2),
                "split" => BoundCallable.Create((arguments, span, context) => ExecuteSplit(pattern, arguments, span, context), "pattern.split", ["string", "maxsplit", "pos", "endpos"], 1),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static PyDict CreateGroupIndex(RePatternObject pattern)
        {
            var dict = new PyDict();
            foreach (var entry in pattern.NamedGroups.OrderBy(entry => entry.Value))
            {
                dict.SetItem(PyString.FromString(entry.Key), new BigInteger(entry.Value));
            }

            return dict;
        }

        private static object ExecuteMatch(
            RePatternObject pattern,
            object[] arguments,
            LythonSourceSpan span,
            ExecutionContext context,
            Func<Utf8PythonRegex, ReadOnlySpan<byte>, Utf8PythonDetailedMatchData> operation)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "Compiled regex method expects string, optional pos, and optional endpos.", span);
            }

            var range = RegexCompiler.CreateSubjectRange(
                text,
                arguments.Length >= 2 ? RegexCompiler.ParseOptionalIntOrDefault(arguments[1], 0, "pos", "compiled regex method", span) : 0,
                arguments.Length >= 3 ? RegexCompiler.ParseOptionalIntOrDefault(arguments[2], text.Length, "endpos", "compiled regex method", span) : text.Length);
            if (!range.IsValid)
            {
                return PyNone.Instance;
            }

            var match = operation(pattern.Regex, range.Segment.Utf8Bytes.Span);
            return match.Success ? RegexMatcher.CreateMatchObject(pattern, range, match, context, span) : PyNone.Instance;
        }

        private static object ExecuteFindAll(RePatternObject pattern, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "pattern.findall(string[, pos[, endpos]]) expects a string argument.", span);
            }

            var range = RegexCompiler.CreateSubjectRange(
                text,
                arguments.Length >= 2 ? RegexCompiler.ParseOptionalIntOrDefault(arguments[1], 0, "pos", "pattern.findall", span) : 0,
                arguments.Length >= 3 ? RegexCompiler.ParseOptionalIntOrDefault(arguments[2], text.Length, "endpos", "pattern.findall", span) : text.Length);
            return RegexMatcher.CreateFindAllResult(pattern, range, span, context);
        }

        internal static PyList ProjectFindAllResult(Utf8PythonFindAllUtf8Result result, LythonSourceSpan span, ExecutionContext context)
        {
            switch (result.Shape)
            {
                case Utf8PythonFindAllShape.FullMatch:
                case Utf8PythonFindAllShape.SingleGroup:
                    var scalars = new object[result.ScalarValues.Length];
                    for (var i = 0; i < scalars.Length; i++)
                    {
                        context.CheckExecutionBudget(span);
                        scalars[i] = CreateUtf8String(result.ScalarValues[i], context, span);
                    }
                    return new PyList(scalars, context.MemoryGovernor, span);

                case Utf8PythonFindAllShape.GroupTuple:
                    var tuples = new object[result.TupleValues.Length];
                    for (var tupleIndex = 0; tupleIndex < tuples.Length; tupleIndex++)
                    {
                        context.CheckExecutionBudget(span);
                        var tuple = result.TupleValues[tupleIndex];
                        var items = new object[tuple.Length];
                        for (var i = 0; i < tuple.Length; i++)
                        {
                            items[i] = CreateUtf8String(tuple[i], context, span);
                        }

                        tuples[tupleIndex] = new PyTuple(items, context.MemoryGovernor, span);
                    }
                    return new PyList(tuples, context.MemoryGovernor, span);

                default:
                    throw new LythonRuntimeException("RuntimeError", "Unsupported regex findall result shape.", span);
            }
        }

        private static object ExecuteFindIter(RePatternObject pattern, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "pattern.finditer(string[, pos[, endpos]]) expects a string argument.", span);
            }

            var range = RegexCompiler.CreateSubjectRange(
                text,
                arguments.Length >= 2 ? RegexCompiler.ParseOptionalIntOrDefault(arguments[1], 0, "pos", "pattern.finditer", span) : 0,
                arguments.Length >= 3 ? RegexCompiler.ParseOptionalIntOrDefault(arguments[2], text.Length, "endpos", "pattern.finditer", span) : text.Length);
            return RegexMatcher.CreateFindIterMatches(pattern, range, context, span);
        }

        private static object ExecuteSub(RePatternObject pattern, object[] arguments, LythonSourceSpan span, ExecutionContext context, RegexSubstitutionMode mode)
        {
            context.CheckExecutionBudget(span);
            var operationName = mode == RegexSubstitutionMode.TextAndCount ? "pattern.subn" : "pattern.sub";
            if (arguments.Length is < 2 or > 5 || !PyStringOps.TryAsString(arguments[1], out var text))
            {
                throw new LythonRuntimeException("TypeError", mode == RegexSubstitutionMode.TextAndCount
                    ? "pattern.subn(replacement, string[, count[, pos[, endpos]]]) expects replacement, text string, and optional count/pos/endpos."
                    : "pattern.sub(replacement, string[, count[, pos[, endpos]]]) expects replacement, text string, and optional count/pos/endpos.", span);
            }

            var replacement = arguments[0];
            if (!PyStringOps.TryAsString(replacement, out _) && replacement is not ICallable)
            {
                throw new LythonRuntimeException("TypeError", mode == RegexSubstitutionMode.TextAndCount
                    ? "pattern.subn(...) replacement must be a string or callable."
                    : "pattern.sub(...) replacement must be a string or callable.", span);
            }

            var count = arguments.Length >= 3 ? RegexCompiler.ParseOptionalIntOrDefault(arguments[2], 0, "count", operationName, span) : 0;
            var range = RegexCompiler.CreateSubjectRange(
                text,
                arguments.Length >= 4 ? RegexCompiler.ParseOptionalIntOrDefault(arguments[3], 0, "pos", operationName, span) : 0,
                arguments.Length >= 5 ? RegexCompiler.ParseOptionalIntOrDefault(arguments[4], text.Length, "endpos", operationName, span) : text.Length);
            return RegexMatcher.ExecuteSubstitute(pattern, replacement, range, count, span, context, mode);
        }

        private static object ExecuteSplit(RePatternObject pattern, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length is < 1 or > 4 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "pattern.split(string[, maxsplit[, pos[, endpos]]]) expects a string and optional maxsplit/pos/endpos.", span);
            }

            var maxSplit = arguments.Length >= 2 ? RegexCompiler.ParseOptionalIntOrDefault(arguments[1], 0, "maxsplit", "pattern.split", span) : 0;
            var range = RegexCompiler.CreateSubjectRange(
                text,
                arguments.Length >= 3 ? RegexCompiler.ParseOptionalIntOrDefault(arguments[2], 0, "pos", "pattern.split", span) : 0,
                arguments.Length >= 4 ? RegexCompiler.ParseOptionalIntOrDefault(arguments[3], text.Length, "endpos", "pattern.split", span) : text.Length);
            return RegexMatcher.ProjectSplitResult(pattern.Regex.SplitDetailed(range.Segment.Utf8Bytes.Span, maxSplit), span, context);
        }
    }

}
