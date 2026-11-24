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
        private RegexMatcher.PreparedCursor? _cursor;
        private bool _finished;
        internal PyRegexFindIterator? NextResource;

        internal static PyRegexFindIterator Create(RePatternObject pattern, RegexSubjectRange range,
            ExecutionContext context, LythonSourceSpan span)
        {
            ChargeIteratorValue(context.MemoryGovernor, span);
            return new(pattern, range, context, span);
        }

        private PyRegexFindIterator(RePatternObject pattern, RegexSubjectRange range, ExecutionContext context, LythonSourceSpan span)
        {
            _pattern = pattern; _range = range; _context = context; _span = span;
        }

        public override PyIterationResult TryMoveNext()
        {
            _context.CheckExecution(_span);

            if (_finished || !_range.IsValid) return PyIterationResult.End;
            try
            {
                if (_cursor is null)
                {
                    _cursor = RegexMatcher.OpenCursor(_pattern, _range, _context, _span);
                    _context.State.TrackRegexIterator(this);
                }
                if (!_cursor.Next(_range)) { Dispose(); return PyIterationResult.End; }
                return PyIterationResult.Yield(RegexMatcher.CreateMatchObject(_pattern, _range, _cursor, _context, _span));
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (_finished) return;
            _finished = true;
            _context.State.UntrackRegexIterator(this);
            var cursor = _cursor; _cursor = null;
            cursor?.Dispose();
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

        private static readonly ReceiverMethod GroupMethod = new(LythonCallableSignature.Create("bound method"),
            static (receiver, arguments, span, context) => GroupCoreAsync((ReMatchObject)receiver, arguments, context, span, false).GetAwaiter().GetResult(),
            static (receiver, arguments, span, context) => GroupCoreAsync((ReMatchObject)receiver, arguments, context, span, true));
        private static readonly ReceiverMethod GetItemMethod = new(MatchGetItemSignature,
            static (receiver, arguments, span, context) => GetSubscript((ReMatchObject)receiver, arguments[0]!, span, context),
            static (receiver, arguments, span, context) => GetSubscriptAsync((ReMatchObject)receiver, arguments[0]!, span, context));
        private static readonly ReceiverMethod GroupsMethod = new(MatchGroupsSignature,
            static (receiver, arguments, span, context) => Groups((ReMatchObject)receiver, arguments.Length == 1 ? arguments[0]! : PyNone.Instance, context, span));
        private static readonly ReceiverMethod GroupDictMethod = new(MatchGroupDictSignature,
            static (receiver, arguments, span, context) => GroupDictionary((ReMatchObject)receiver, arguments.Length == 1 ? arguments[0]! : PyNone.Instance, context, span));
        private static readonly ReceiverMethod ExpandMethod = new(MatchExpandSignature,
            static (receiver, arguments, span, context) =>
            {
                if (!PyStringOps.TryAsString(arguments[0]!, out var template))
                    throw new LythonRuntimeException("TypeError", "match.expand(template) expects one string argument.", span);
                return ExpandReplacementTemplate((ReMatchObject)receiver, template, context, span);
            });
        private static readonly ReceiverMethod StartMethod = new(MatchStartSignature,
            static (receiver, arguments, span, context) => GetBoundsCoreAsync((ReMatchObject)receiver, arguments, context, span, false).GetAwaiter().GetResult().Start,
            static async (receiver, arguments, span, context) => (await GetBoundsCoreAsync((ReMatchObject)receiver, arguments, context, span, true).ConfigureAwait(false)).Start);
        private static readonly ReceiverMethod EndMethod = new(MatchEndSignature,
            static (receiver, arguments, span, context) => GetBoundsCoreAsync((ReMatchObject)receiver, arguments, context, span, false).GetAwaiter().GetResult().End,
            static async (receiver, arguments, span, context) => (await GetBoundsCoreAsync((ReMatchObject)receiver, arguments, context, span, true).ConfigureAwait(false)).End);
        private static readonly ReceiverMethod SpanMethod = new(MatchSpanSignature,
            static (receiver, arguments, span, context) => BoundsTuple(GetBoundsCoreAsync((ReMatchObject)receiver, arguments, context, span, false).GetAwaiter().GetResult(), context, span),
            static async (receiver, arguments, span, context) => BoundsTuple(await GetBoundsCoreAsync((ReMatchObject)receiver, arguments, context, span, true).ConfigureAwait(false), context, span));

        internal static ReceiverMethod? GetMethod(string name) => name switch
        {
            "group" => GroupMethod,
            "__getitem__" => GetItemMethod,
            "groups" => GroupsMethod,
            "groupdict" => GroupDictMethod,
            "expand" => ExpandMethod,
            "start" => StartMethod,
            "end" => EndMethod,
            "span" => SpanMethod,
            _ => null,
        };

        public static bool TryGetMember(ReMatchObject match, string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            if (GetMethod(name) is { } method) { value = method.Bind(match); return true; }
            value = name switch
            {
                "__module__" => LythonRuntime.ExceptionTypeValue.SharedModuleLabel("re"),
                "re" => match.Pattern,
                "string" => match.String,
                "pos" => new BigInteger(match.Pos),
                "endpos" => new BigInteger(match.EndPos),
                "lastindex" => LastIndex(match),
                "lastgroup" => LastGroup(match, context, span),
                "regs" => match.ReadRegisters(context, span),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object Groups(ReMatchObject match, object defaultValue, ExecutionContext context, LythonSourceSpan span)
            => CreateTuple(match.CaptureSlotCount - 1,
                i => ResolveIndexedGroup(match, i + 1, span, context, defaultValue), context, span);

        private static object GroupDictionary(ReMatchObject match, object defaultValue, ExecutionContext context, LythonSourceSpan span)
        {
            var dict = new PyDict(match.NamedGroups.Count, context.MemoryGovernor, span);
            try
            {
                for (var i = 0; i < match.NamedGroups.Count; i++)
                {
                    context.CheckExecution(span);
                    var (name, index) = match.NamedGroups.ReadNativeStringIntegerEntry(i);
                    var value = ResolveIndexedGroup(match, index, span, context, defaultValue);
                    dict.SetRecordItem(name, value, context, span);
                }
                return dict;
            }
            catch
            {
                dict.DiscardUnpublishedStorage();
                throw;
            }
        }

        private static object BoundsTuple(RegexGroupBounds bounds, ExecutionContext context, LythonSourceSpan span)
        {
            var governor = context.MemoryGovernor;
            governor.Reserve(RegexBoundsStorageBytes, span);
            try
            {
                context.CheckExecution(span);
                var pair = CreateAdmittedRegexBounds(bounds.Start, bounds.End, governor, span);
                governor.Commit(RegexBoundsStorageBytes);
                return pair;
            }
            catch { governor.ReleaseReserved(RegexBoundsStorageBytes); throw; }
        }

        internal static object GetSubscript(ReMatchObject match, object key, LythonSourceSpan span, ExecutionContext? context)
            => GetSubscriptCoreAsync(match, key, span, context, false).GetAwaiter().GetResult();

        internal static ValueTask<object> GetSubscriptAsync(ReMatchObject match, object key, LythonSourceSpan span, ExecutionContext context)
            => GetSubscriptCoreAsync(match, key, span, context, true);

        private static async ValueTask<object> GetSubscriptCoreAsync(ReMatchObject match, object key, LythonSourceSpan span, ExecutionContext? context, bool asynchronous)
        {
            var index = await ResolveGroupIndexAsync(match, key, context, span, asynchronous).ConfigureAwait(false);
            return ResolveIndexedGroup(match, index, span, context);
        }

        private static async ValueTask<object> GroupCoreAsync(ReMatchObject match, ReceiverArguments arguments, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            if (arguments.Length == 0)
            {
                return match.MaterializeGroup(0, context, span);
            }
            if (arguments.Length == 1)
            {
                return await GetSubscriptCoreAsync(match, arguments[0]!, span, context, asynchronous).ConfigureAwait(false);
            }

            if (!asynchronous)
                return CreateTuple(arguments.Length, i => GetSubscript(match, arguments[i]!, span, context), context, span);
            return await CreateTupleAsync(arguments.Length,
                i => GetSubscriptAsync(match, arguments[i]!, span, context), context, span).ConfigureAwait(false);
        }

        private static async ValueTask<RegexGroupBounds> GetBoundsCoreAsync(ReMatchObject match, ReceiverArguments arguments, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            var index = arguments.Length == 0 ? 0 :
                await ResolveGroupIndexAsync(match, arguments[0]!, context, span, asynchronous).ConfigureAwait(false);
            var capture = match.Captures[index];
            return new(capture.Start, capture.End);
        }

        private static object ResolveIndexedGroup(ReMatchObject match, int index, LythonSourceSpan span, ExecutionContext? context)
            => ResolveIndexedGroup(match, index, span, context, PyNone.Instance);

        private static object ResolveIndexedGroup(ReMatchObject match, int index, LythonSourceSpan span,
            ExecutionContext? context, object defaultValue)
        {
            if (index < 0 || index >= match.CaptureSlotCount)
                throw new LythonRuntimeException("IndexError", "no such group", span);
            return match.Captures[index].StartByte < 0 ? defaultValue : match.MaterializeGroup(index, context, span);
        }

        private static async ValueTask<int> ResolveGroupIndexAsync(ReMatchObject match, object key, ExecutionContext? context,
            LythonSourceSpan span, bool asynchronous)
        {
            if (key is BigInteger integer) return ResolveIntegerGroupIndex(match, integer, span);
            if (key is int small) return ResolveIntegerGroupIndex(match, small, span);
            if (key is bool boolean) return ResolveIntegerGroupIndex(match, boolean ? BigInteger.One : BigInteger.Zero, span);
            if (context is not null && key is PyInstance instance &&
                instance.Type.TryLookupInMro("__index__", 0, out var rawValue, out _))
            {
                // The numeric protocol uses the type's slot before name lookup.
                var member = asynchronous
                    ? await PyAttributeLookup.BindForInstanceAsync(instance, rawValue, context, span).ConfigureAwait(false)
                    : PyAttributeLookup.BindForInstance(instance, rawValue, context, span);
                if (member is not ICallable callable)
                    throw new LythonRuntimeException("TypeError", "'" + UnboundTypeMethod.PythonTypeName(member, context) + "' object is not callable", span);
                var converted = asynchronous
                    ? await callable.InvokeAsync([], span, context).ConfigureAwait(false)
                    : callable.Invoke([], span, context);
                if (!PyNumberOps.TryAsInteger(converted, out var index))
                    throw new LythonRuntimeException("TypeError", "__index__ returned non-int (type " + UnboundTypeMethod.PythonTypeName(converted, context) + ")", span);
                return ResolveIntegerGroupIndex(match, index, span);
            }

            // Python uses its group dictionary for non-index keys when the
            // pattern has names. Keep the existing dictionary protocol across
            // suspension. A pattern without names never hashes the key.
            var namedGroups = match.NamedGroups;
            if (namedGroups.Count != 0)
            {
                if (PyStringOps.TryAsString(key, out var name))
                {
                    if (match.Pattern.TryGetNamedGroup(name, context, span, out var namedIndex)) return namedIndex;
                }
                else if (context is not null && asynchronous)
                {
                    var (found, value) = await namedGroups.TryGetKeyAsync(key, context, span).ConfigureAwait(false);
                    if (found) return (int)(BigInteger)value;
                }
                else if (context is null ? namedGroups.TryGetValue(key, out var value)
                    : namedGroups.TryGetValue(key, out value, context, span))
                {
                    return (int)(BigInteger)value!;
                }
            }
            throw new LythonRuntimeException("IndexError", "no such group", span);
        }

        private static int ResolveIntegerGroupIndex(ReMatchObject match, BigInteger index, LythonSourceSpan span)
        {
            // Validate the Python integer before narrowing: enormous guest
            // indices must stay catchable IndexErrors, never CLR overflows.
            if (index < 0 || index >= match.CaptureSlotCount)
            {
                throw new LythonRuntimeException("IndexError", "no such group", span);
            }

            return (int)index;
        }

        private static object LastIndex(ReMatchObject match)
            => match.LastCapturedIndex is { } index ? new BigInteger(index) : PyNone.Instance;

        private static object LastGroup(ReMatchObject match, ExecutionContext context, LythonSourceSpan span)
        {
            if (match.LastCapturedIndex is not int index)
            {
                return PyNone.Instance;
            }

            // Metadata stores capture numbers in ascending order. The name
            // returned here is the same key the groupindex proxy exposes.
            var first = 0;
            var end = match.NamedGroups.Count;
            while (first < end)
            {
                context.CheckExecution(span);
                var middle = first + (end - first) / 2;
                var (name, group) = match.NamedGroups.ReadNativeStringIntegerEntry(middle);
                if (group == index) return name;
                if (group < index) first = middle + 1;
                else end = middle;
            }

            return PyNone.Instance;
        }

    }

    internal enum RegexPatternOperation { Search, Match, FullMatch, FindAll, FindIter, Split, Sub, SubCount }

    internal static class RePatternMembers
    {
        private static readonly ReceiverMethod SearchMethod = CreateWindowMethod("pattern.search", RegexPatternOperation.Search);
        private static readonly ReceiverMethod MatchMethod = CreateWindowMethod("pattern.match", RegexPatternOperation.Match);
        private static readonly ReceiverMethod FullMatchMethod = CreateWindowMethod("pattern.fullmatch", RegexPatternOperation.FullMatch);
        private static readonly ReceiverMethod FindAllMethod = CreateWindowMethod("pattern.findall", RegexPatternOperation.FindAll);
        private static readonly ReceiverMethod FindIterMethod = CreateWindowMethod("pattern.finditer", RegexPatternOperation.FindIter);
        private static readonly ReceiverMethod SubMethod = CreateMethod(
            "pattern.sub", ["repl", "string", "count", "pos", "endpos"], 2, RegexPatternOperation.Sub);
        private static readonly ReceiverMethod SubCountMethod = CreateMethod(
            "pattern.subn", ["repl", "string", "count", "pos", "endpos"], 2, RegexPatternOperation.SubCount);
        private static readonly ReceiverMethod SplitMethod = CreateMethod(
            "pattern.split", ["string", "maxsplit", "pos", "endpos"], 1, RegexPatternOperation.Split);

        private static ReceiverMethod CreateWindowMethod(string name, RegexPatternOperation operation)
            => CreateMethod(name, ["string", "pos", "endpos"], 1, operation);

        private static ReceiverMethod CreateMethod(string name, string[] parameters, int required, RegexPatternOperation operation)
            => new(LythonCallableSignature.Create(name, parameters, required),
                (receiver, arguments, span, context) => Execute((RePatternObject)receiver, arguments, span, context, name, operation, false)
                    .GetAwaiter().GetResult(),
                (receiver, arguments, span, context) => Execute((RePatternObject)receiver, arguments, span, context, name, operation, true));

        internal static ReceiverMethod? GetMethod(string name) => name switch
        {
            "search" => SearchMethod,
            "match" => MatchMethod,
            "fullmatch" => FullMatchMethod,
            "findall" => FindAllMethod,
            "finditer" => FindIterMethod,
            "sub" => SubMethod,
            "subn" => SubCountMethod,
            "split" => SplitMethod,
            _ => null,
        };

        public static bool TryGetMember(RePatternObject pattern, string name, ExecutionContext context, LythonSourceSpan span,
            [MaybeNullWhen(false)] out object value)
        {
            if (GetMethod(name) is { } method) { value = method.Bind(pattern); return true; }
            value = name switch
            {
                "pattern" => pattern.Pattern,
                "flags" => new BigInteger(pattern.Flags),
                "groups" => new BigInteger(Math.Max(0, pattern.CaptureSlotCount - 1)),
                "groupindex" => CreateGroupIndex(pattern, context, span),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object CreateGroupIndex(RePatternObject pattern, ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            return pattern.NamedGroups.Count == 0
                ? new PyDict(context.MemoryGovernor, span)
                : new PyMappingProxy(pattern.NamedGroups, context.MemoryGovernor, span);
        }

        internal static ValueTask<object> ExecuteModule(BoundBuiltinArguments arguments, LythonSourceSpan span,
            ExecutionContext context, string name, RegexPatternOperation operation, bool asynchronous)
        {
            var flagIndex = operation switch
            {
                RegexPatternOperation.Sub or RegexPatternOperation.SubCount => 4,
                RegexPatternOperation.Split => 3,
                _ => 2,
            };
            context.CheckExecution(span);
            // Module-level re functions compile or look up the pattern before
            // the compiled method performs any index or subject operations.
            var pending = RegexCompiler.ResolvePattern(arguments.Values[0], Optional(arguments, flagIndex), span, context, asynchronous);
            return pending.IsCompletedSuccessfully
                ? ExecuteResolvedModule(pending.Result, arguments, flagIndex, context, span, name, operation, asynchronous)
                : AwaitModulePattern(pending, arguments, flagIndex, context, span, name, operation);

            static async ValueTask<object> AwaitModulePattern(ValueTask<RePatternObject> pending,
                BoundBuiltinArguments arguments, int flagIndex, ExecutionContext context, LythonSourceSpan span,
                string name, RegexPatternOperation operation)
                => await ExecuteResolvedModule(await pending.ConfigureAwait(false), arguments, flagIndex, context, span, name,
                    operation, true).ConfigureAwait(false);
        }

        private static object? Optional(BoundBuiltinArguments arguments, int index)
            => arguments.Assigned[index] ? arguments.Values[index] : null;

        private static ValueTask<object> ExecuteResolvedModule(RePatternObject pattern, BoundBuiltinArguments arguments,
            int flagIndex, ExecutionContext context, LythonSourceSpan span, string name, RegexPatternOperation operation, bool asynchronous)
        {
            var substitution = flagIndex == 4;
            // The module's window extension also accepts None for an omitted bound.
            var pos = Optional(arguments, flagIndex + 1);
            var end = Optional(arguments, flagIndex + 2);
            return ExecuteArguments(pattern, arguments.Values[substitution ? 2 : 1], substitution ? arguments.Values[1] : null,
                flagIndex > 2 ? Optional(arguments, flagIndex - 1) : null, pos is PyNone ? null : pos, end is PyNone ? null : end,
                operation, context, span, name, asynchronous);
        }

        private static ValueTask<object> Execute(RePatternObject pattern, ReceiverArguments arguments, LythonSourceSpan span,
            ExecutionContext context, string name, RegexPatternOperation operation, bool asynchronous)
        {
            var substitution = operation is RegexPatternOperation.Sub or RegexPatternOperation.SubCount;
            var subjectIndex = substitution ? 1 : 0;
            var counted = substitution || operation == RegexPatternOperation.Split;
            var positionIndex = subjectIndex + (counted ? 2 : 1);
            return ExecuteArguments(pattern, arguments[subjectIndex]!, substitution ? arguments[0] : null,
                counted ? arguments.Optional(subjectIndex + 1) : null, arguments.Optional(positionIndex),
                arguments.Optional(positionIndex + 1), operation, context, span, name, asynchronous);
        }

        private static ValueTask<object> ExecuteArguments(RePatternObject pattern, object subject, object? replacement,
            object? count, object? pos, object? end, RegexPatternOperation operation, ExecutionContext context,
            LythonSourceSpan span, string name, bool asynchronous)
        {
            context.CheckExecution(span);
            // Integer conversions belong to argument binding. Run them in order
            // before the matcher checks the subject or prepares a replacement.
            if (asynchronous && (count is PyInstance || pos is PyInstance || end is PyInstance))
                return ExecuteWithIndexProtocols(pattern, subject, replacement, count, pos, end, operation, context, span, name);

            var limit = count is null ? 0 : RegexCompiler.ParseIndex(count, span, context);
            var start = pos is null ? 0 : RegexCompiler.ParsePosition(pos, span, context);
            var stop = end is null ? int.MaxValue : RegexCompiler.ParsePosition(end, span, context);
            return ExecutePrepared(pattern, subject, replacement, limit, start, stop, operation, context, span, name, asynchronous);
        }

        private static async ValueTask<object> ExecuteWithIndexProtocols(RePatternObject pattern, object subject, object? replacement,
            object? count, object? pos, object? end, RegexPatternOperation operation,
            ExecutionContext context, LythonSourceSpan span, string name)
        {
            var limit = count is null ? 0
                : await RegexCompiler.ParseIndexAsync(count, span, context).ConfigureAwait(false);
            var start = pos is null ? 0 : await RegexCompiler.ParsePositionAsync(pos, span, context).ConfigureAwait(false);
            var stop = end is null ? int.MaxValue : await RegexCompiler.ParsePositionAsync(end, span, context).ConfigureAwait(false);
            return await ExecutePrepared(pattern, subject, replacement, limit, start, stop, operation, context, span, name, true)
                .ConfigureAwait(false);
        }

        private static ValueTask<object> ExecutePrepared(RePatternObject pattern, object subject, object? replacement,
            long count, int pos, int end, RegexPatternOperation operation, ExecutionContext context, LythonSourceSpan span,
            string name, bool asynchronous)
        {
            if (operation is RegexPatternOperation.Sub or RegexPatternOperation.SubCount)
            {
                var mode = operation == RegexPatternOperation.Sub ? RegexSubstitutionMode.TextOnly : RegexSubstitutionMode.TextAndCount;
                return RegexMatcher.Substitute(pattern, replacement!, subject, count, pos, end, span, context, mode, name, asynchronous);
            }
            if (!PyStringOps.TryAsString(subject, out var text)) throw RuntimeErrors.Type(SubjectError(name), span);
            var range = RegexCompiler.CreateSubjectRange(text, pos, end, span, context);
            switch (operation)
            {
                case RegexPatternOperation.Search: return new(RegexMatcher.Match(pattern, range, LythonRegexMatchMode.Search, span, context));
                case RegexPatternOperation.Match: return new(RegexMatcher.Match(pattern, range, LythonRegexMatchMode.Match, span, context));
                case RegexPatternOperation.FullMatch: return new(RegexMatcher.Match(pattern, range, LythonRegexMatchMode.FullMatch, span, context));
                case RegexPatternOperation.FindAll: return new(RegexMatcher.CreateFindAllResult(pattern, range, span, context));
                case RegexPatternOperation.FindIter: return new(PyRegexFindIterator.Create(pattern, range, context, span));
                case RegexPatternOperation.Split: return new(RegexMatcher.Split(pattern, range, count, span, context));
                default: throw new InvalidOperationException("Unknown compiled regex operation.");
            }
        }

        internal static string SubjectError(string name) => $"{name} expects a string subject.";
    }
}
