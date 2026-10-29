using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class DifflibSequenceMatcherObject
    {
        private readonly object? _isjunk;
        private readonly object _autojunk;
        // Exact native sequences are borrowed. Only the supported iterable
        // extension needs a separately materialized indexed value.
        private object _aOriginal = null!, _bOriginal = null!;
        private object _a = null!, _b = null!;
        private PyDict _b2j = null!;
        private PySet _bjunk = null!, _bpopular = null!;
        private PyDict? _fullBCount;
        private PyList? _matchingBlocks, _opcodes;

        private DifflibSequenceMatcherObject(object? isjunk, object autojunk)
        { _isjunk = isjunk; _autojunk = autojunk; }

        public DifflibSequenceMatcherObject(object? isjunk, object a, object b, object autojunk,
            LythonSourceSpan span, ExecutionContext context) : this(isjunk, autojunk)
        {
            SetSeq1(a, span, context);
            try { SetSeq2(b, span, context); }
            catch { if (!ReferenceEquals(_a, _aOriginal)) ((PyList)_a).DiscardUnpublishedStorage(); throw; }
        }

        public static async ValueTask<DifflibSequenceMatcherObject> CreateAsync(object? isjunk,
            object a, object b, object autojunk, LythonSourceSpan span, ExecutionContext context)
        {
            var result = new DifflibSequenceMatcherObject(isjunk, autojunk);
            result.SetSeq1(a, span, context);
            try { await result.SetSeq2CoreAsync(b, span, context, true).ConfigureAwait(false); return result; }
            catch { if (!ReferenceEquals(result._a, result._aOriginal)) ((PyList)result._a).DiscardUnpublishedStorage(); throw; }
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "a" => _aOriginal,
                "b" => _bOriginal,
                "b2j" => _b2j,
                "bjunk" => _bjunk,
                "bpopular" => _bpopular,
                "set_seqs" => BoundCallable.Create((args, span, context) =>
                { SetSeqs(args[0], args[1], span, context); return PyNone.Instance; },
                    async (args, span, context) =>
                    { SetSeq1(args[0], span, context); await SetSeq2CoreAsync(args[1], span, context, true).ConfigureAwait(false); return PyNone.Instance; },
                    "SequenceMatcher.set_seqs", ["a", "b"]),
                "set_seq1" => BoundCallable.Create((args, span, context) =>
                { SetSeq1(args[0], span, context); return PyNone.Instance; }, "SequenceMatcher.set_seq1", ["a"]),
                "set_seq2" => BoundCallable.Create((args, span, context) =>
                { SetSeq2(args[0], span, context); return PyNone.Instance; },
                    async (args, span, context) =>
                    { await SetSeq2CoreAsync(args[0], span, context, true).ConfigureAwait(false); return PyNone.Instance; },
                    "SequenceMatcher.set_seq2", ["b"]),
                "ratio" => BoundCallable.Create((args, span, context) => Ratio(span, context),
                    async (args, span, context) => await RatioAsync(span, context, true).ConfigureAwait(false),
                    "SequenceMatcher.ratio", []),
                "quick_ratio" => BoundCallable.Create((args, span, context) => QuickRatio(span, context),
                    "SequenceMatcher.quick_ratio", []),
                "real_quick_ratio" => BoundCallable.Create((args, span, context) => RealQuickRatio(),
                    "SequenceMatcher.real_quick_ratio", []),
                "find_longest_match" => BoundCallable.CreateWithPresence(
                    (args, span, context) => FindMatchAsync(args, span, context, false).GetAwaiter().GetResult(),
                    LythonCallableSignature.Create("SequenceMatcher.find_longest_match", ["alo", "ahi", "blo", "bhi"], requiredCount: 0),
                    (args, span, context) => FindMatchAsync(args, span, context, true)),
                "get_matching_blocks" => BoundCallable.Create(
                    (args, span, context) => GetMatchingBlocksAsync(span, context, false).GetAwaiter().GetResult(),
                    async (args, span, context) => await GetMatchingBlocksAsync(span, context, true).ConfigureAwait(false),
                    "SequenceMatcher.get_matching_blocks", []),
                "get_opcodes" => BoundCallable.Create(
                    (args, span, context) => BuildOpcodes(span, context),
                    async (args, span, context) => await BuildOpcodesAsync(span, context, true).ConfigureAwait(false),
                    "SequenceMatcher.get_opcodes", []),
                "get_grouped_opcodes" => BoundCallable.Create((args, span, context) =>
                    DifflibModule.OutputIterator(GroupedOutput(args.Length == 0 ? new BigInteger(3) : args[0], span, context), span, context),
                    "SequenceMatcher.get_grouped_opcodes", ["n"], requiredCount: 0),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public void SetSeqs(object a, object b, LythonSourceSpan span, ExecutionContext context)
        { SetSeq1(a, span, context); SetSeq2(b, span, context); }

        public void SetSeq1(object source, LythonSourceSpan span, ExecutionContext context)
        {
            if (ReferenceEquals(source, _aOriginal)) return;
            var prepared = PrepareSequence(source, span, context);
            _aOriginal = source; _a = prepared;
            _matchingBlocks = _opcodes = null;
        }

        public void SetSeq2(object source, LythonSourceSpan span, ExecutionContext context)
            => SetSeq2CoreAsync(source, span, context, false).GetAwaiter().GetResult();

        private async ValueTask SetSeq2CoreAsync(object source, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (ReferenceEquals(source, _bOriginal)) return;
            var prepared = PrepareSequence(source, span, context);
            PyDict? index = null;
            PySet? junk = null, popular = null;
            PyList? pendingIndices = null;
            try
            {
                index = new PyDict(context.MemoryGovernor, span);
                junk = new PySet(context.MemoryGovernor, span);
                popular = new PySet(context.MemoryGovernor, span);
                var length = SequenceLength(prepared);
                for (var i = 0; i < length; i++)
                {
                    context.CheckExecution(span);
                    using var ambient = PyStructuralGuard.PushAmbient(context, span);
                    var item = ValidateDictionaryKey(SequenceItem(prepared, i, span), span);
                    pendingIndices ??= new PyList(0, context.MemoryGovernor, span);
                    var indices = (PyList)index.SetDefault(item, pendingIndices, context.MemoryGovernor, span);
                    if (ReferenceEquals(indices, pendingIndices)) pendingIndices = null;
                    indices.Add(new BigInteger(i));
                }

                pendingIndices?.DiscardUnpublishedStorage();
                pendingIndices = null;
                var testJunk = _isjunk is not null && (asynchronous
                    ? await IsTruthyAsync(_isjunk, context, span).ConfigureAwait(false)
                    : IsTruthy(_isjunk, context, span));
                if (testJunk)
                {
                    foreach (var pair in index)
                    {
                        context.CheckExecution(span);
                        if (await DifflibModule.CallJunkPredicateAsync(_isjunk!, pair.Key, span, context, asynchronous).ConfigureAwait(false))
                        {
                            using var ambient = PyStructuralGuard.PushAmbient(context, span);
                            junk.Add(pair.Key);
                        }
                    }
                    foreach (var key in junk)
                    { using var ambient = PyStructuralGuard.PushAmbient(context, span); index.Remove(key); }
                }
                var autojunk = asynchronous ? await IsTruthyAsync(_autojunk, context, span).ConfigureAwait(false)
                    : IsTruthy(_autojunk, context, span);
                if (autojunk && length >= 200)
                {
                    foreach (var pair in index)
                    {
                        context.CheckExecution(span);
                        if (((PyList)pair.Value).Count > length / 100 + 1)
                        { using var ambient = PyStructuralGuard.PushAmbient(context, span); popular.Add(pair.Key); }
                    }
                    foreach (var key in popular)
                    { using var ambient = PyStructuralGuard.PushAmbient(context, span); index.Remove(key); }
                }
                context.CheckExecution(span);
                // Publish once, after successful construction. Old canonical
                // containers stay batch-owned because guest aliases may retain them.
                _bOriginal = source; _b = prepared;
                _b2j = index; _bjunk = junk; _bpopular = popular;
                _fullBCount = null; _matchingBlocks = _opcodes = null;
            }
            catch (Exception error)
            {
                if (index is not null)
                {
                    foreach (var pair in index)
                    {
                        if (ReferenceEquals(pair.Value, pendingIndices)) pendingIndices = null;
                        ((PyList)pair.Value).DiscardUnpublishedStorage();
                    }
                    index.DiscardUnpublishedStorage();
                }
                pendingIndices?.DiscardUnpublishedStorage();
                junk?.ReleaseUnpublishedStorage(); popular?.ReleaseUnpublishedStorage();
                if (!ReferenceEquals(source, prepared)) ((PyList)prepared).DiscardUnpublishedStorage();
                if (error is PyUnhashableException) throw new LythonRuntimeException("TypeError", "difflib.SequenceMatcher sequence elements must be hashable", span);
                throw;
            }
        }

        private static object PrepareSequence(object value, LythonSourceSpan span, ExecutionContext context)
            => value is IPyIndexableValue or IReadOnlyList<object> ? value :
                new PyList(ToSequence(value, span, context), context.MemoryGovernor, span);

        private static int SequenceLength(object value)
            => value is IPyIndexableValue indexed ? indexed.Length : ((IReadOnlyList<object>)value).Count;

        private static object SequenceItem(object value, int index, LythonSourceSpan span)
        {
            var length = SequenceLength(value);
            if (index < 0) index += length;
            if ((uint)index >= (uint)length) throw new LythonRuntimeException("IndexError", "sequence index out of range", span);
            return value is IPyIndexableValue indexed ? indexed.GetIndex(index) : ((IReadOnlyList<object>)value)[index];
        }

        public double Ratio(LythonSourceSpan span, ExecutionContext context) => RatioAsync(span, context, false).GetAwaiter().GetResult();
        private async ValueTask<double> RatioAsync(LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var blocks = await GetMatchingBlocksAsync(span, context, asynchronous).ConfigureAwait(false);
            long matches = 0;
            foreach (var item in blocks) { context.CheckExecution(span); matches += ReadBlock(item, span).Size; }
            return CalculateRatio(matches, (long)SequenceLength(_a) + SequenceLength(_b));
        }

        public double QuickRatio(LythonSourceSpan span, ExecutionContext context)
        {
            using var ambient = PyStructuralGuard.PushAmbient(context, span);
            if (_fullBCount is null)
            {
                var counts = new PyDict(context.MemoryGovernor, span);
                try
                {
                    for (var i = 0; i < SequenceLength(_b); i++)
                    {
                        context.CheckExecution(span);
                        var item = ValidateDictionaryKey(SequenceItem(_b, i, span), span);
                        counts.TryGetValue(item, out var old, context, span);
                        counts.SetItem(item, old is BigInteger n ? n + 1 : BigInteger.One);
                    }
                    _fullBCount = counts;
                }
                catch { counts.DiscardUnpublishedStorage(); throw; }
            }
            var available = new PyDict(context.MemoryGovernor, span);
            try
            {
                var matches = 0;
                for (var i = 0; i < SequenceLength(_a); i++)
                {
                    context.CheckExecution(span);
                    var item = ValidateDictionaryKey(SequenceItem(_a, i, span), span);
                    if (!available.TryGetValue(item, out var count, context, span))
                        count = _fullBCount.TryGetValue(item, out var total, context, span) ? total : BigInteger.Zero;
                    var n = (BigInteger)count;
                    available.SetItem(item, n - 1);
                    if (n > 0) matches++;
                }
                return CalculateRatio(matches, (long)SequenceLength(_a) + SequenceLength(_b));
            }
            finally { available.DiscardUnpublishedStorage(); }
        }

        public double RealQuickRatio() => CalculateRatio(Math.Min(SequenceLength(_a), SequenceLength(_b)),
            (long)SequenceLength(_a) + SequenceLength(_b));

        internal PyList BuildOpcodes(LythonSourceSpan span, ExecutionContext context)
            => BuildOpcodesAsync(span, context, false).GetAwaiter().GetResult();

        private async ValueTask<PyList> BuildOpcodesAsync(LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (_opcodes is not null) return _opcodes;
            var blocks = await GetMatchingBlocksAsync(span, context, asynchronous).ConfigureAwait(false);
            var result = new PyList(0, context.MemoryGovernor, span);
            try
            {
                var i = 0; var j = 0;
                foreach (var item in blocks)
                {
                    context.CheckExecution(span);
                    var block = ReadBlock(item, span);
                    if (i < block.A || j < block.B)
                        result.Add(ToPyTuple(new(i < block.A ? j < block.B ? DiffTag.Replace : DiffTag.Delete : DiffTag.Insert,
                            i, block.A, j, block.B), context, span));
                    i = checked(block.A + block.Size); j = checked(block.B + block.Size);
                    if (block.Size != 0) result.Add(ToPyTuple(new(DiffTag.Equal, block.A, i, block.B, j), context, span));
                }
                return _opcodes = result;
            }
            catch { result.DiscardUnpublishedStorage(); throw; }
        }

        private IEnumerable<object> GroupedOutput(object n, LythonSourceSpan span, ExecutionContext context)
        {
            foreach (var group in BuildGroupedOpcodes(DifflibModule.RequireInteger(n, "context must be an integer", span), span, context))
                yield return group;
        }

        internal IEnumerable<object> BuildGroupedOpcodes(BigInteger n, LythonSourceSpan span, ExecutionContext context)
        {
            var codes = BuildOpcodes(span, context);
            if (codes.Count == 0)
                codes = new PyList([ToPyTuple(new(DiffTag.Equal, 0, 1, 0, 1), context, span)], context.MemoryGovernor, span);
            // CPython adjusts its actual opcode cache's boundaries when grouping.
            // No second opcode inventory or native groups outlive scratch leases.
            var first = ReadOpcode(codes[0], span);
            if (first.Tag == DiffTag.Equal)
                codes[0] = GroupTuple(DiffTag.Equal, BigInteger.Max(first.I1, first.I2 - n), first.I2,
                    BigInteger.Max(first.J1, first.J2 - n), first.J2, context, span);
            var last = ReadOpcode(codes[^1], span);
            if (last.Tag == DiffTag.Equal)
                codes[^1] = GroupTuple(DiffTag.Equal, last.I1, BigInteger.Min(last.I2, last.I1 + n),
                    last.J1, BigInteger.Min(last.J2, last.J1 + n), context, span);
            var group = new PyList(0, context.MemoryGovernor, span);
            foreach (var item in codes)
            {
                context.CheckExecution(span);
                var code = ReadOpcode(item, span);
                object current = item;
                if (code.Tag == DiffTag.Equal && code.I2 - (long)code.I1 > 2 * n)
                {
                    group.Add(GroupTuple(DiffTag.Equal, code.I1, BigInteger.Min(code.I2, code.I1 + n),
                        code.J1, BigInteger.Min(code.J2, code.J1 + n), context, span));
                    yield return group;
                    group = new PyList(0, context.MemoryGovernor, span);
                    current = GroupTuple(DiffTag.Equal, BigInteger.Max(code.I1, code.I2 - n), code.I2,
                        BigInteger.Max(code.J1, code.J2 - n), code.J2, context, span);
                }
                group.Add(current);
            }
            if (group.Count != 0 && !(group.Count == 1 && ReadOpcode(group[0], span).Tag == DiffTag.Equal)) yield return group;
        }

        private async ValueTask<object> FindMatchAsync(BoundCallArguments bound, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var args = bound.Values;
            var alo = bound.Assigned[0] ? DifflibModule.RequireInt32(args[0], "alo must be an integer", span) : 0;
            var ahi = bound.Assigned[1] && args[1] is not PyNone ? DifflibModule.RequireInt32(args[1], "ahi must be an integer", span) : SequenceLength(_a);
            var blo = bound.Assigned[2] ? DifflibModule.RequireInt32(args[2], "blo must be an integer", span) : 0;
            var bhi = bound.Assigned[3] && args[3] is not PyNone ? DifflibModule.RequireInt32(args[3], "bhi must be an integer", span) : SequenceLength(_b);
            var match = await FindLongestMatchAsync(new(alo, ahi, blo, bhi), span, context, asynchronous).ConfigureAwait(false);
            return DifflibModule.CreateMatch(match.A, match.B, match.Size, context, span);
        }

        private async ValueTask<PyList> GetMatchingBlocksAsync(LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (_matchingBlocks is not null) return _matchingBlocks;
            // Grow private work capacities before allocating, retaining overlap.
            // A long contiguous match does not need a worst-case fragmented grid.
            using var storage = context.MemoryGovernor.ReserveTemporary(64, span);
            var blocks = new List<MatchingBlock>();
            var queue = new List<MatchRange>();
            DifflibModule.AddSequenceItem(queue, new MatchRange(0, SequenceLength(_a), 0, SequenceLength(_b)), storage, span);
            while (queue.Count != 0)
            {
                context.CheckExecution(span);
                var range = queue[^1];
                queue.RemoveAt(queue.Count - 1);
                var match = await FindLongestMatchAsync(range, span, context, asynchronous).ConfigureAwait(false);
                if (match.Size == 0) continue;
                DifflibModule.AddSequenceItem(blocks, match, storage, span);
                if (range.ALo < match.A && range.BLo < match.B) DifflibModule.AddSequenceItem(queue, new MatchRange(range.ALo, match.A, range.BLo, match.B), storage, span);
                if (match.A + match.Size < range.AHi && match.B + match.Size < range.BHi)
                    DifflibModule.AddSequenceItem(queue, new MatchRange(match.A + match.Size, range.AHi, match.B + match.Size, range.BHi), storage, span);
            }
            ExecutionSort.Sort(blocks, context, span, static (x, y) => x.A != y.A ? x.A.CompareTo(y.A) : x.B.CompareTo(y.B));
            var result = new PyList(0, context.MemoryGovernor, span);
            try
            {
                var a = 0; var b = 0; var size = 0;
                foreach (var block in blocks)
                {
                    context.CheckExecution(span);
                    if (a + size == block.A && b + size == block.B) { size += block.Size; continue; }
                    if (size != 0) result.Add(DifflibModule.CreateMatch(a, b, size, context, span));
                    a = block.A; b = block.B; size = block.Size;
                }
                if (size != 0) result.Add(DifflibModule.CreateMatch(a, b, size, context, span));
                result.Add(DifflibModule.CreateMatch(SequenceLength(_a), SequenceLength(_b), 0, context, span));
                return _matchingBlocks = result;
            }
            catch { result.DiscardUnpublishedStorage(); throw; }
        }

        private async ValueTask<MatchingBlock> FindLongestMatchAsync(MatchRange range, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            using var storage = context.MemoryGovernor.ReserveTemporary(128, span);
            var bestA = range.ALo; var bestB = range.BLo; var bestSize = 0;
            var previous = new Dictionary<int, int>(); var current = new Dictionary<int, int>();
            var covered = 0;
            for (var i = range.ALo; i < range.AHi; i++)
            {
                context.CheckExecution(span);
                using var ambient = PyStructuralGuard.PushAmbient(context, span);
                current.Clear();
                if (_b2j.TryGetValue(ValidateDictionaryKey(SequenceItem(_a, i, span), span), out var indexes, context, span))
                {
                    foreach (var position in ToSequence(indexes, span, context))
                    {
                        context.CheckExecution(span);
                        var j = DifflibModule.RequireInteger(position, "index must be an integer", span);
                        if (j < range.BLo) continue;
                        if (j >= range.BHi) break;
                        var at = (int)j;
                        var need = previous.Count + current.Count + 1;
                        if (need > covered) { storage.Grow(64L * (need - covered), span); covered = need; }
                        var length = previous.TryGetValue(at - 1, out var prior) ? prior + 1 : 1;
                        current[at] = length;
                        if (length > bestSize) { bestA = i - length + 1; bestB = at - length + 1; bestSize = length; }
                    }
                }
                (previous, current) = (current, previous);
            }
            // Python tests non-junk extension first, then adjacent junk. Equality
            // here is == (not container identity-or-equality), including NaNs.
            foreach (var junk in new[] { false, true })
            {
                while (bestA > range.ALo && bestB > range.BLo &&
                    IsJunk(SequenceItem(_b, bestB - 1, span)) == junk &&
                    await EqualAsync(SequenceItem(_a, bestA - 1, span), SequenceItem(_b, bestB - 1, span)).ConfigureAwait(false))
                { context.CheckExecution(span); bestA--; bestB--; bestSize++; }
                while (bestA + bestSize < range.AHi && bestB + bestSize < range.BHi &&
                    IsJunk(SequenceItem(_b, bestB + bestSize, span)) == junk &&
                    await EqualAsync(SequenceItem(_a, bestA + bestSize, span), SequenceItem(_b, bestB + bestSize, span)).ConfigureAwait(false))
                { context.CheckExecution(span); bestSize++; }
            }
            return new(bestA, bestB, bestSize);

            bool IsJunk(object value)
            {
                using var ambient = PyStructuralGuard.PushAmbient(context, span);
                return _bjunk.Contains(value);
            }

            async ValueTask<bool> EqualAsync(object left, object right)
                => asynchronous ? await IsTruthyAsync(await AreEqualWithProtocolsAsync(left, right, context, span).ConfigureAwait(false), context, span).ConfigureAwait(false)
                    : IsTruthy(AreEqualWithProtocols(left, right, context, span), context, span);
        }

        private static double CalculateRatio(long matches, long length) => length == 0 ? 1.0 : 2.0 * matches / length;
        private static MatchingBlock ReadBlock(object value, LythonSourceSpan span)
        {
            var fields = Fields(value, 3, span);
            return new(DifflibModule.RequireInt32(fields[0], "invalid match coordinate", span),
                DifflibModule.RequireInt32(fields[1], "invalid match coordinate", span),
                DifflibModule.RequireInt32(fields[2], "invalid match size", span));
        }
        internal static DiffOpcode ReadOpcode(object value, LythonSourceSpan span)
        {
            var fields = Fields(value, 5, span);
            var tag = fields[0] is PyString text ? text.AsString() : "";
            return new(tag switch { "equal" => DiffTag.Equal, "replace" => DiffTag.Replace, "insert" => DiffTag.Insert,
                "delete" => DiffTag.Delete, _ => throw new LythonRuntimeException("ValueError", "invalid opcode tag", span) },
                DifflibModule.RequireInt32(fields[1], "invalid opcode coordinate", span), DifflibModule.RequireInt32(fields[2], "invalid opcode coordinate", span),
                DifflibModule.RequireInt32(fields[3], "invalid opcode coordinate", span), DifflibModule.RequireInt32(fields[4], "invalid opcode coordinate", span));
        }
        private static IReadOnlyList<object> Fields(object value, int count, LythonSourceSpan span)
            => value is IReadOnlyList<object> fields && fields.Count == count ? fields
                : throw new LythonRuntimeException("TypeError", "invalid cached match/opcode sequence", span);
        private static PyTuple ToPyTuple(DiffOpcode code, ExecutionContext context, LythonSourceSpan span)
            => GroupTuple(code.Tag, code.I1, code.I2, code.J1, code.J2, context, span);
        private static PyTuple GroupTuple(DiffTag tag, BigInteger i1, BigInteger i2, BigInteger j1, BigInteger j2,
            ExecutionContext context, LythonSourceSpan span)
            => new([PyString.FromString(tag switch { DiffTag.Equal => "equal", DiffTag.Replace => "replace", DiffTag.Delete => "delete", _ => "insert" }),
                i1, i2, j1, j2], context.MemoryGovernor, span);

        private readonly record struct MatchRange(int ALo, int AHi, int BLo, int BHi);
        private readonly record struct MatchingBlock(int A, int B, int Size);
    }
}
