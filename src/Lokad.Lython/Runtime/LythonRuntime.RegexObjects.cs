using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class PyRegexFindIterator : PyIteratorBase, IDisposable
    {
        private readonly RePatternObject _pattern;
        private readonly RegexSubjectRange _range;
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        private ILythonRegexSubject? _subject;
        private ILythonRegexCursor? _cursor;
        private bool _finished;
        internal PyRegexFindIterator? NextResource;

        public PyRegexFindIterator(RePatternObject pattern, RegexSubjectRange range, ExecutionContext context, LythonSourceSpan span)
        {
            ChargeIteratorValue(context.MemoryGovernor, span);
            _pattern = pattern; _range = range; _context = context; _span = span;
        }

        public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            _context.CheckExecution(_span);
            value = PyNone.Instance;
            if (_finished || !_range.IsValid) return false;
            try
            {
                if (_subject is null)
                {
                    _subject = RegexMatcher.Prepare(_pattern, _range, _context, _span);
                    _cursor = RegexCompiler.ProviderCall(() => _subject.Find(_context.Limits.CancellationToken),
                        _context.MemoryGovernor, _context.Limits.CancellationToken, _span);
                    _context.State.TrackRegexIterator(this);
                }
                var match = RegexCompiler.ProviderCall(() => _cursor!.Next(_context.Limits.CancellationToken),
                    _context.MemoryGovernor, _context.Limits.CancellationToken, _span);
                if (match is null) { Dispose(); return false; }
                value = RegexMatcher.CreateMatchObject(_pattern, _range, match, _context, _span);
                return true;
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (_finished) return;
            _finished = true;
            _context.State.UntrackRegexIterator(this);
            var cursor = _cursor; _cursor = null;
            var subject = _subject; _subject = null;
            try { cursor?.Dispose(); }
            finally { subject?.Dispose(); }
        }

        public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<callable_iterator object>");
    }

    internal static partial class ReMatchMembers
    {
        // N17: hot fixed signatures hoisted per family (see ListMembers).
        private static readonly LythonCallableSignature MatchGroupsSignature = LythonCallableSignature.Create("match.groups", ["default"], 0);
        private static readonly LythonCallableSignature MatchGroupDictSignature = LythonCallableSignature.Create("match.groupdict", ["default"], 0);
        private static readonly LythonCallableSignature MatchExpandSignature = LythonCallableSignature.Create("match.expand", ["template"]);
        private static readonly LythonCallableSignature MatchStartSignature = LythonCallableSignature.Create("match.start", ["group"], 0, ArgumentCountLimit.AtMost(1), LythonVariadicParameters.None, 1);
        private static readonly LythonCallableSignature MatchEndSignature = LythonCallableSignature.Create("match.end", ["group"], 0, ArgumentCountLimit.AtMost(1), LythonVariadicParameters.None, 1);
        private static readonly LythonCallableSignature MatchSpanSignature = LythonCallableSignature.Create("match.span", ["group"], 0, ArgumentCountLimit.AtMost(1), LythonVariadicParameters.None, 1);
        private static readonly LythonCallableSignature MatchGetItemSignature = LythonCallableSignature.Create("match.__getitem__", ["key"], 1, ArgumentCountLimit.AtMost(1), LythonVariadicParameters.None, 1);
        private readonly record struct RegexGroupBounds(BigInteger Start, BigInteger End);

        public static bool TryGetMember(ReMatchObject match, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__module__" => LythonRuntime.ExceptionTypeValue.SharedModuleLabel("re"),
                "re" => match.Pattern,
                "string" => match.String,
                "pos" => match.Pos,
                "endpos" => match.EndPos,
                "lastindex" => LastIndex(match),
                "lastgroup" => LastGroup(match),
                "group" => BoundCallable.Create(
                    (arguments, span, context) => GroupCoreAsync(match, arguments, context, span, false).GetAwaiter().GetResult(),
                    (arguments, span, context) => GroupCoreAsync(match, arguments, context, span, true)),
                "__getitem__" => BoundCallable.Create(
                    (arguments, span, context) => GetSubscript(match, arguments[0], span, context),
                    MatchGetItemSignature,
                    (arguments, span, context) => GetSubscriptAsync(match, arguments[0], span, context)),
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
                }, MatchGroupsSignature),
                "groupdict" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "match.groupdict(default=None) expects zero or one argument.", span);
                    }

                    var defaultValue = arguments.Length == 1 ? arguments[0] : PyNone.Instance;
                    var dict = new PyDict(context.MemoryGovernor, span);
                    foreach (var entry in match.NamedGroups)
                    {
                        dict.SetItem(entry.Key, ResolveIndexedGroup(match, entry.Value, span, defaultValue));
                    }

                    return dict;
                }, MatchGroupDictSignature),
                "expand" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var template))
                    {
                        throw new LythonRuntimeException("TypeError", "match.expand(template) expects one string argument.", span);
                    }

                    return ExpandReplacementTemplate(match, template, context, span);
                }, MatchExpandSignature),
                "start" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "match.start(group=0) expects zero or one group identifier.", span);
                    }

                    return GetBoundsCoreAsync(match, arguments, context, span, false).GetAwaiter().GetResult().Start;
                }, MatchStartSignature, async (arguments, span, context) =>
                    (await GetBoundsCoreAsync(match, arguments, context, span, true).ConfigureAwait(false)).Start),
                "end" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "match.end(group=0) expects zero or one group identifier.", span);
                    }

                    return GetBoundsCoreAsync(match, arguments, context, span, false).GetAwaiter().GetResult().End;
                }, MatchEndSignature, async (arguments, span, context) =>
                    (await GetBoundsCoreAsync(match, arguments, context, span, true).ConfigureAwait(false)).End),
                "span" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "match.span(group=0) expects zero or one group identifier.", span);
                    }

                    var bounds = GetBoundsCoreAsync(match, arguments, context, span, false).GetAwaiter().GetResult();
                    return CreateTuple(2, i => i == 0 ? bounds.Start : bounds.End, context, span);
                }, MatchSpanSignature, async (arguments, span, context) =>
                {
                    var bounds = await GetBoundsCoreAsync(match, arguments, context, span, true).ConfigureAwait(false);
                    return CreateTuple(2, i => i == 0 ? bounds.Start : bounds.End, context, span);
                }),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        internal static object GetSubscript(ReMatchObject match, object key, LythonSourceSpan span, ExecutionContext? context)
            => GetSubscriptCoreAsync(match, key, span, context, false).GetAwaiter().GetResult();

        internal static ValueTask<object> GetSubscriptAsync(ReMatchObject match, object key, LythonSourceSpan span, ExecutionContext context)
            => GetSubscriptCoreAsync(match, key, span, context, true);

        private static async ValueTask<object> GetSubscriptCoreAsync(ReMatchObject match, object key, LythonSourceSpan span, ExecutionContext? context, bool asynchronous)
        {
            key = await CoerceGroupKeyAsync(key, context, span, asynchronous).ConfigureAwait(false);
            return TryResolveGroupIndex(match, key, span, out var index)
                ? ResolveIndexedGroup(match, index, span)
                : throw new LythonRuntimeException("IndexError", "no such group", span);
        }

        private static async ValueTask<object> GroupCoreAsync(ReMatchObject match, object[] arguments, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            if (arguments.Length == 0)
            {
                return match.Value;
            }
            if (arguments.Length == 1)
            {
                return await GetSubscriptCoreAsync(match, arguments[0], span, context, asynchronous).ConfigureAwait(false);
            }

            var groups = new object[arguments.Length];
            for (var i = 0; i < arguments.Length; i++)
            {
                groups[i] = await GetSubscriptCoreAsync(match, arguments[i], span, context, asynchronous).ConfigureAwait(false);
            }
            return new PyTuple(groups, context.MemoryGovernor, span);
        }

        private static async ValueTask<RegexGroupBounds> GetBoundsCoreAsync(ReMatchObject match, object[] arguments, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            var key = await CoerceGroupKeyAsync(arguments.Length == 0 ? BigInteger.Zero : arguments[0], context, span, asynchronous).ConfigureAwait(false);
            return ResolveGroupBounds(match, key, span);
        }

        private static async ValueTask<object> CoerceGroupKeyAsync(object key, ExecutionContext? context, LythonSourceSpan span, bool asynchronous)
        {
            if (context is null || key is not PyInstance instance ||
                !instance.Type.TryLookupInMro("__index__", 0, out var rawValue, out _))
            {
                return key;
            }

            // Numeric protocol slots come from the type, bypassing instance
            // attributes and __getattr__/__getattribute__ as Python does.
            var member = asynchronous
                ? await PyAttributeLookup.BindForInstanceAsync(instance, rawValue, context, span).ConfigureAwait(false)
                : PyAttributeLookup.BindForInstance(instance, rawValue, context, span);
            if (member is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "'" + UnboundTypeMethod.PythonTypeName(member, context) + "' object is not callable", span);
            }
            var converted = asynchronous
                ? await callable.InvokeAsync([], span, context).ConfigureAwait(false)
                : callable.Invoke([], span, context);
            if (!PyNumberOps.TryAsInteger(converted, out var integer))
            {
                throw new LythonRuntimeException("TypeError", "__index__ returned non-int (type " + UnboundTypeMethod.PythonTypeName(converted, context) + ")", span);
            }
            return integer;
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

            throw new LythonRuntimeException("IndexError", "no such group", span);
        }

        private static bool TryResolveGroupIndex(ReMatchObject match, object group, LythonSourceSpan span, out int index)
        {
            index = group switch
            {
                BigInteger integer => ResolveIntegerGroupIndex(match, integer, span),
                int integer => ResolveIntegerGroupIndex(match, integer, span),
                bool boolean => ResolveIntegerGroupIndex(match, boolean ? BigInteger.One : BigInteger.Zero, span),
                _ when PyStringOps.TryAsString(group, out var nameText) => match.NamedGroups.TryGetValue(nameText, out var namedIndex)
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

        private static int ResolveIntegerGroupIndex(ReMatchObject match, BigInteger index, LythonSourceSpan span)
        {
            // Validate the Python integer before narrowing: enormous guest
            // indices must stay catchable IndexErrors, never CLR overflows.
            if (index < 0 || index >= match.CaptureSlotCount)
            {
                throw new LythonRuntimeException("IndexError", "Regex group index is out of range.", span);
            }

            return (int)index;
        }

        private static object LastIndex(ReMatchObject match)
            => match.LastCapturedIndex is { } index ? new BigInteger(index) : PyNone.Instance;

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
                    return entry.Key;
                }
            }

            return PyNone.Instance;
        }

    }

    internal static class RePatternMembers
    {
        // N17: hot fixed signatures hoisted per family (see ListMembers).
        private static readonly LythonCallableSignature PatternSearchSignature = LythonCallableSignature.Create("pattern.search", ["string", "pos", "endpos"], 1);
        private static readonly LythonCallableSignature PatternMatchSignature = LythonCallableSignature.Create("pattern.match", ["string", "pos", "endpos"], 1);
        private static readonly LythonCallableSignature PatternFullMatchSignature = LythonCallableSignature.Create("pattern.fullmatch", ["string", "pos", "endpos"], 1);
        private static readonly LythonCallableSignature PatternFindAllSignature = LythonCallableSignature.Create("pattern.findall", ["string", "pos", "endpos"], 1);
        private static readonly LythonCallableSignature PatternFindIterSignature = LythonCallableSignature.Create("pattern.finditer", ["string", "pos", "endpos"], 1);
        private static readonly LythonCallableSignature PatternSubSignature = LythonCallableSignature.Create("pattern.sub", ["repl", "string", "count", "pos", "endpos"], 2);
        private static readonly LythonCallableSignature PatternSubnSignature = LythonCallableSignature.Create("pattern.subn", ["repl", "string", "count", "pos", "endpos"], 2);
        private static readonly LythonCallableSignature PatternSplitSignature = LythonCallableSignature.Create("pattern.split", ["string", "maxsplit", "pos", "endpos"], 1);
        public static bool TryGetMember(RePatternObject pattern, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "pattern" => pattern.Pattern,
                "flags" => new BigInteger(pattern.Flags),
                "groups" => new BigInteger(Math.Max(0, pattern.CaptureSlotCount - 1)),
                "groupindex" => CreateGroupIndex(pattern),
                "search" => BoundCallable.Create((arguments, span, context) => ExecuteMatch(pattern, arguments, span, context, LythonRegexMatchMode.Search), PatternSearchSignature),
                "match" => BoundCallable.Create((arguments, span, context) => ExecuteMatch(pattern, arguments, span, context, LythonRegexMatchMode.Match), PatternMatchSignature),
                "fullmatch" => BoundCallable.Create((arguments, span, context) => ExecuteMatch(pattern, arguments, span, context, LythonRegexMatchMode.FullMatch), PatternFullMatchSignature),
                "findall" => BoundCallable.Create((arguments, span, context) => ExecuteFindAll(pattern, arguments, span, context), PatternFindAllSignature),
                "finditer" => BoundCallable.Create((arguments, span, context) => ExecuteFindIter(pattern, arguments, span, context), PatternFindIterSignature),
                "sub" => BoundCallable.Create((arguments, span, context) => ExecuteSub(pattern, arguments, span, context, RegexSubstitutionMode.TextOnly), PatternSubSignature, (arguments, span, context) => ExecuteSubCoreAsync(pattern, arguments, span, context, RegexSubstitutionMode.TextOnly, true)),
                "subn" => BoundCallable.Create((arguments, span, context) => ExecuteSub(pattern, arguments, span, context, RegexSubstitutionMode.TextAndCount), PatternSubnSignature, (arguments, span, context) => ExecuteSubCoreAsync(pattern, arguments, span, context, RegexSubstitutionMode.TextAndCount, true)),
                "split" => BoundCallable.Create((arguments, span, context) => ExecuteSplit(pattern, arguments, span, context), PatternSplitSignature),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static PyDict CreateGroupIndex(RePatternObject pattern)
        {
            var dict = new PyDict();
            foreach (var entry in pattern.NamedGroups)
            {
                dict.SetItem(entry.Key, new BigInteger(entry.Value));
            }

            return dict;
        }

        private static object ExecuteMatch(
            RePatternObject pattern,
            object[] arguments,
            LythonSourceSpan span,
            ExecutionContext context,
            LythonRegexMatchMode operation)
        {
            context.CheckExecution(span);
            if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "Compiled regex method expects string, optional pos, and optional endpos.", span);
            }

            var range = RegexCompiler.CreateSubjectRange(
                text,
                arguments.Length >= 2 ? RegexCompiler.ParseExplicitInt(arguments[1], "pos", "compiled regex method", span, context) : 0,
                arguments.Length >= 3 ? RegexCompiler.ParseExplicitInt(arguments[2], "endpos", "compiled regex method", span, context) : text.Length);
            if (!range.IsValid)
            {
                return PyNone.Instance;
            }

            return RegexMatcher.Match(pattern, range, operation, span, context);
        }

        private static object ExecuteFindAll(RePatternObject pattern, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "pattern.findall(string[, pos[, endpos]]) expects a string argument.", span);
            }

            var range = RegexCompiler.CreateSubjectRange(
                text,
                arguments.Length >= 2 ? RegexCompiler.ParseExplicitInt(arguments[1], "pos", "pattern.findall", span, context) : 0,
                arguments.Length >= 3 ? RegexCompiler.ParseExplicitInt(arguments[2], "endpos", "pattern.findall", span, context) : text.Length);
            return RegexMatcher.CreateFindAllResult(pattern, range, span, context);
        }

        private static object ExecuteFindIter(RePatternObject pattern, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "pattern.finditer(string[, pos[, endpos]]) expects a string argument.", span);
            }

            var range = RegexCompiler.CreateSubjectRange(
                text,
                arguments.Length >= 2 ? RegexCompiler.ParseExplicitInt(arguments[1], "pos", "pattern.finditer", span, context) : 0,
                arguments.Length >= 3 ? RegexCompiler.ParseExplicitInt(arguments[2], "endpos", "pattern.finditer", span, context) : text.Length);
            // Finditer shells charge per live instance like other iterator factories.
            PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
            var findIter = RegexMatcher.CreateFindIterMatches(pattern, range, context, span);
            return findIter;
        }

        private static object ExecuteSub(RePatternObject pattern, object[] arguments, LythonSourceSpan span, ExecutionContext context, RegexSubstitutionMode mode)
            => ExecuteSubCoreAsync(pattern, arguments, span, context, mode, false).GetAwaiter().GetResult();

        private static ValueTask<object> ExecuteSubCoreAsync(RePatternObject pattern, object[] arguments, LythonSourceSpan span,
            ExecutionContext context, RegexSubstitutionMode mode, bool asynchronous)
        {
            context.CheckExecution(span);
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

            var count = arguments.Length >= 3 ? RegexCompiler.ParseExplicitInt(arguments[2], "count", operationName, span, context) : 0;
            var range = RegexCompiler.CreateSubjectRange(
                text,
                arguments.Length >= 4 ? RegexCompiler.ParseExplicitInt(arguments[3], "pos", operationName, span, context) : 0,
                arguments.Length >= 5 ? RegexCompiler.ParseExplicitInt(arguments[4], "endpos", operationName, span, context) : text.Length);
            return asynchronous ? RegexMatcher.ExecuteSubstituteAsync(pattern, replacement, range, count, span, context, mode)
                : new ValueTask<object>(RegexMatcher.ExecuteSubstitute(pattern, replacement, range, count, span, context, mode));
        }

        private static object ExecuteSplit(RePatternObject pattern, object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length is < 1 or > 4 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "pattern.split(string[, maxsplit[, pos[, endpos]]]) expects a string and optional maxsplit/pos/endpos.", span);
            }

            var maxSplit = arguments.Length >= 2 ? RegexCompiler.ParseExplicitInt(arguments[1], "maxsplit", "pattern.split", span, context) : 0;
            var range = RegexCompiler.CreateSubjectRange(
                text,
                arguments.Length >= 3 ? RegexCompiler.ParseExplicitInt(arguments[2], "pos", "pattern.split", span, context) : 0,
                arguments.Length >= 4 ? RegexCompiler.ParseExplicitInt(arguments[3], "endpos", "pattern.split", span, context) : text.Length);
            return RegexMatcher.Split(pattern, range, maxSplit, span, context);
        }
    }

}
