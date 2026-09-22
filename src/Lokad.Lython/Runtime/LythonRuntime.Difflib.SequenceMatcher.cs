using System.Net;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class DifflibSequenceMatcherObject
    {
        // Must remain a power of two: chunk checks below use it as a bit mask.
        private const int BudgetCheckInterval = 64;
        private const long SequenceBaseBytes = 32;
        private const long SequenceBytesPerItem = 16;
        private const long ChainBaseBytes = 96;
        private const long ChainKeyBytesPerElement = 96;
        private const long ChainIndexBytesPerElement = 16;
        private const long SetBaseBytes = 80;
        private const long SetBytesPerItem = 24;
        private const long CountBaseBytes = 96;
        private const long CountBytesPerEntry = 64;
        private const long BlocksBaseBytes = 64;
        private const long BlockBytesPerEntry = 32;
        private const long ScratchBytesPerEntry = 64;
        private const long ViewListBaseBytes = 32;

        private readonly ICallable? _isjunk;
        private readonly bool _autojunk;
        private readonly MemoryGovernor _governor;
        private object _aOriginal;
        private object _bOriginal;
        private IReadOnlyList<object> _a;
        private IReadOnlyList<object> _b;
        private ContextualKeyTable<List<int>> _b2j;
        private ContextualKeySet _bjunk;
        private ContextualKeySet _bpopular;
        private ContextualKeyTable<int>? _fullBCount;
        private List<MatchingBlock>? _matchingBlocks;
        private List<DiffOpcode>? _opcodes;
        private long _aCharge;
        private long _bCharge;
        private long _chainCharge;
        private PyDict? _b2jView;
        private PySet? _bjunkView;
        private PySet? _bpopularView;
        // N05: escaped views own their lifetime via the run pool (view as owner),
        // never tied to the matcher coupon. The matcher caches references without owning charges.
        private readonly ChargeReclamationPool _pool;
        private readonly LythonRuntime.ExecutionContext _creationContext;
        private long _fullBCountCharge;
        private long _matchingBlocksCharge;
        private long _opcodesCharge;

        public DifflibSequenceMatcherObject(ICallable? isjunk, object aOriginal, object bOriginal, bool autojunk, LythonSourceSpan span, ExecutionContext context)
        {
            _isjunk = isjunk;
            _autojunk = autojunk;
            _governor = context.MemoryGovernor;
            _pool = context.Services.State.CallTemporaries;
            _creationContext = context;
            _aOriginal = aOriginal;
            _bOriginal = bOriginal;
            _a = DifflibModule.MaterializeGovernedSequence(aOriginal, span, context, out _aCharge);
            _b = [];
            _b2j = new ContextualKeyTable<List<int>>();
            _bjunk = new ContextualKeySet();
            _bpopular = new ContextualKeySet();
            try
            {
                SetSeq2(bOriginal, span, context);
            }
            catch
            {
                _governor.Release(_aCharge);
                throw;
            }
            // Fresh matchers reclaim through the pool once dropped; later caches re-snapshot
            // below while the member-call funnel has no matcher branch.
            context.Services.State.CallTemporaries.TrackFreshMutable(this, CommittedStorageBytes, span);
        }

        // Current committed backing charges across chains and caches (escaped views own themselves), for pooled
        // owners that release them if this matcher is dropped. Mutations re-snapshot
        // through NoteGrowth below; untracked matchers no-op inside the notification.
        internal long CommittedStorageBytes =>
            _aCharge + _bCharge + _chainCharge +
            _fullBCountCharge + _matchingBlocksCharge + _opcodesCharge;

        // Refreshes the pool coupon after charge mutations; change-detected over field
        // reads so steady use costs nothing when charges are unchanged.
        private void NoteGrowth(long beforeCharges)
        {
            var current = CommittedStorageBytes;
            if (current != beforeCharges)
            {
                ChargeReclamationPool.NotifyStorageReplaced(this, current);
            }
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "a" => _aOriginal,
                "b" => _bOriginal,
                "b2j" => GetB2JView(),
                "bjunk" => GetBjunkView(),
                "bpopular" => GetBpopularView(),
                "set_seqs" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 2)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.set_seqs(a, b) expects two arguments.", span);
                    }

                    SetSeqs(arguments[0], arguments[1], span, context);
                    return PyNone.Instance;
                }, "SequenceMatcher.set_seqs", ["a", "b"]),
                "set_seq1" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.set_seq1(a) expects one argument.", span);
                    }

                    SetSeq1(arguments[0], span, context);
                    return PyNone.Instance;
                }, "SequenceMatcher.set_seq1", ["a"]),
                "set_seq2" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.set_seq2(b) expects one argument.", span);
                    }

                    SetSeq2(arguments[0], span, context);
                    return PyNone.Instance;
                }, "SequenceMatcher.set_seq2", ["b"]),
                "ratio" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.ratio() expects no arguments.", span);
                    }

                    return Ratio(span, context);
                }),
                "quick_ratio" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.quick_ratio() expects no arguments.", span);
                    }

                    return QuickRatio(span, context);
                }),
                "real_quick_ratio" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.real_quick_ratio() expects no arguments.", span);
                    }

                    return RealQuickRatio();
                }),
                "find_longest_match" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 4)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.find_longest_match([alo][, ahi][, blo][, bhi]) expects zero to four arguments.", span);
                    }

                    var alo = arguments.Length >= 1 && arguments[0] is not PyNone ? DifflibModule.RequireInt32(arguments[0], "SequenceMatcher.find_longest_match(..., alo=...) expects an integer.", span) : 0;
                    var ahi = arguments.Length >= 2 && arguments[1] is not PyNone ? DifflibModule.RequireInt32(arguments[1], "SequenceMatcher.find_longest_match(..., ahi=...) expects an integer or None.", span) : _a.Count;
                    var blo = arguments.Length >= 3 && arguments[2] is not PyNone ? DifflibModule.RequireInt32(arguments[2], "SequenceMatcher.find_longest_match(..., blo=...) expects an integer.", span) : 0;
                    var bhi = arguments.Length >= 4 && arguments[3] is not PyNone ? DifflibModule.RequireInt32(arguments[3], "SequenceMatcher.find_longest_match(..., bhi=...) expects an integer or None.", span) : _b.Count;
                    var block = FindLongestMatch(new MatchRange(alo, ahi, blo, bhi), span, context);
                    return new DifflibMatchObject(block.A, block.B, block.Size);
                }, LythonCallableSignature.Create("SequenceMatcher.find_longest_match", ["alo", "ahi", "blo", "bhi"], requiredCount: 0)),
                "get_matching_blocks" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.get_matching_blocks() expects no arguments.", span);
                    }

                    return new PyList(GetMatchingBlocks(span, context).Select(block => (object)new DifflibMatchObject(block.A, block.B, block.Size)), context.MemoryGovernor, span);
                }),
                "get_opcodes" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.get_opcodes() expects no arguments.", span);
                    }

                    return new PyList(BuildOpcodes(span, context).Select(opcode => (object)ToPyTuple(opcode)), context.MemoryGovernor, span);
                }),
                "get_grouped_opcodes" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.get_grouped_opcodes([n]) expects zero or one argument.", span);
                    }

                    var n = arguments.Length == 1 && arguments[0] is not PyNone ? DifflibModule.RequireInt32(arguments[0], "SequenceMatcher.get_grouped_opcodes(..., n=...) expects an integer.", span) : 3;
                    return new PyList(BuildGroupedOpcodes(n, span, context).Select(group => (object)new PyList(group.Select(opcode => (object)ToPyTuple(opcode)), context.MemoryGovernor, span)), context.MemoryGovernor, span);
                }, "SequenceMatcher.get_grouped_opcodes", ["n"], requiredCount: 0),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public void SetSeqs(object aOriginal, object bOriginal, LythonSourceSpan span, ExecutionContext context)
        {
            SetSeq1(aOriginal, span, context);
            SetSeq2(bOriginal, span, context);
        }

        public void SetSeq1(object aOriginal, LythonSourceSpan span, ExecutionContext context)
        {
            var beforeCharges = CommittedStorageBytes;
            var array = DifflibModule.MaterializeGovernedSequence(aOriginal, span, context, out var charge);
            _aOriginal = aOriginal;
            _governor.Release(_aCharge);
            _a = array;
            _aCharge = charge;
            ReleaseMatchingBlocks();
            ReleaseOpcodes();
            NoteGrowth(beforeCharges);
        }

        public void SetSeq2(object bOriginal, LythonSourceSpan span, ExecutionContext context)
        {
            // N05: strong guarantee - swap sequence only after the new index builds;
            // failure restores the previous array with exact charges.
            var beforeCharges = CommittedStorageBytes;
            var newArray = DifflibModule.MaterializeGovernedSequence(bOriginal, span, context, out var newCharge);
            var oldB = _b;
            var oldBCharge = _bCharge;
            var oldBOriginal = _bOriginal;
            _bOriginal = bOriginal;
            _b = newArray;
            try
            {
                ChainB(span, context);
                _governor.Release(oldBCharge);
                _bCharge = newCharge;
                ReleaseMatchingBlocks();
                ReleaseOpcodes();
                ReleaseFullBCount();
                ReleaseViews();
                NoteGrowth(beforeCharges);
            }
            catch
            {
                _b = oldB;
                _bOriginal = oldBOriginal;
                _governor.Release(newCharge);
                throw;
            }
        }

        private void ReleaseMatchingBlocks()
        {
            _governor.Release(_matchingBlocksCharge);
            _matchingBlocksCharge = 0;
            _matchingBlocks = null;
        }

        private void ReleaseOpcodes()
        {
            _governor.Release(_opcodesCharge);
            _opcodesCharge = 0;
            _opcodes = null;
        }

        private void ReleaseFullBCount()
        {
            _governor.Release(_fullBCountCharge);
            _fullBCountCharge = 0;
            _fullBCount = null;
        }

        public double Ratio(LythonSourceSpan span, ExecutionContext context)
        {
            var matches = 0;
            var work = 0;
            foreach (var block in GetMatchingBlocks(span, context))
            {
                matches += block.Size;
                if ((++work & (BudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            return CalculateRatio(matches, _a.Count + _b.Count);
        }

        public double QuickRatio(LythonSourceSpan span, ExecutionContext context)
        {
            _fullBCount ??= BuildFullBCount(span, context);
            using var reservation = _governor.ReserveTemporary(0, span);
            var available = new ContextualKeyTable<int>();
            var peakCount = 0;
            var matches = 0;
            var work = 0;
            foreach (var item in _a)
            {
                if (!available.TryGetValue(item, context, span, out var count))
                {
                    _fullBCount.TryGetValue(item, context, span, out count);
                }

                // Single hash-plus-scan publish (GetOrAdd returns the stored entry).
                available.GetOrAdd(item, count - 1, context, span).Value = count - 1;
                if (available.Count > peakCount)
                {
                    reservation.Grow(CountBytesPerEntry * (available.Count - peakCount), span);
                    peakCount = available.Count;
                }

                if (count > 0)
                {
                    matches++;
                }

                if ((++work & (BudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            return CalculateRatio(matches, _a.Count + _b.Count);
        }

        public double RealQuickRatio()
            => CalculateRatio(Math.Min(_a.Count, _b.Count), _a.Count + _b.Count);

        internal List<DiffOpcode> BuildOpcodes(LythonSourceSpan span, ExecutionContext context)
        {
            var beforeCharges = CommittedStorageBytes;
            if (_opcodes is not null)
            {
                return _opcodes;
            }

            using var reservation = _governor.ReserveTemporary(0, span);
            var opcodes = new List<DiffOpcode>();
            var chargedOpcodes = 0;
            var i = 0;
            var j = 0;
            var work = 0;
            foreach (var block in GetMatchingBlocks(span, context))
            {
                var tag = DiffTag.Equal;
                if (i < block.A && j < block.B)
                {
                    tag = DiffTag.Replace;
                }
                else if (i < block.A)
                {
                    tag = DiffTag.Delete;
                }
                else if (j < block.B)
                {
                    tag = DiffTag.Insert;
                }

                if (tag != DiffTag.Equal)
                {
                    opcodes.Add(new DiffOpcode(tag, i, block.A, j, block.B));
                }

                i = block.A + block.Size;
                j = block.B + block.Size;
                if (block.Size != 0)
                {
                    opcodes.Add(new DiffOpcode(DiffTag.Equal, block.A, i, block.B, j));
                }

                if (opcodes.Count > chargedOpcodes)
                {
                    reservation.Grow(BlockBytesPerEntry * (opcodes.Count - chargedOpcodes), span);
                    chargedOpcodes = opcodes.Count;
                }

                if ((++work & (BudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            var opcodesCharge = BlocksBaseBytes + (BlockBytesPerEntry * opcodes.Count);
            _governor.Reserve(opcodesCharge, span);
            _governor.Commit(opcodesCharge);
            _governor.Release(_opcodesCharge);
            _opcodesCharge = opcodesCharge;
            _opcodes = opcodes;
            NoteGrowth(beforeCharges);
            return opcodes;
        }

        internal List<List<DiffOpcode>> BuildGroupedOpcodes(int contextLines, LythonSourceSpan span, ExecutionContext context)
        {
            using var reservation = _governor.ReserveTemporary(0, span);
            var codes = BuildOpcodes(span, context).Select(static opcode => opcode).ToList();
            reservation.Grow(BlockBytesPerEntry * codes.Count, span);
            if (codes.Count == 0)
            {
                codes.Add(new DiffOpcode(DiffTag.Equal, 0, 1, 0, 1));
            }

            if (codes[0].Tag == DiffTag.Equal)
            {
                var first = codes[0];
                codes[0] = first with
                {
                    I1 = Math.Max(first.I1, first.I2 - contextLines),
                    J1 = Math.Max(first.J1, first.J2 - contextLines)
                };
            }

            if (codes[^1].Tag == DiffTag.Equal)
            {
                var last = codes[^1];
                codes[^1] = last with
                {
                    I2 = Math.Min(last.I2, last.I1 + contextLines),
                    J2 = Math.Min(last.J2, last.J1 + contextLines)
                };
            }

            var groups = new List<List<DiffOpcode>>();
            var group = new List<DiffOpcode>();
            var doubleContext = contextLines + contextLines;
            var work = 0;
            foreach (var code in codes)
            {
                reservation.Grow(BlockBytesPerEntry, span);
                if ((++work & (BudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }

                var current = code;
                if (current.Tag == DiffTag.Equal && current.I2 - current.I1 > doubleContext)
                {
                    group.Add(current with
                    {
                        I2 = Math.Min(current.I2, current.I1 + contextLines),
                        J2 = Math.Min(current.J2, current.J1 + contextLines)
                    });
                    groups.Add(group);
                    group = [];
                    current = current with
                    {
                        I1 = Math.Max(current.I1, current.I2 - contextLines),
                        J1 = Math.Max(current.J1, current.J2 - contextLines)
                    };
                }

                group.Add(current);
            }

            if (group.Count != 0 && !(group.Count == 1 && group[0].Tag == DiffTag.Equal))
            {
                groups.Add(group);
            }

            return groups;
        }

        private void ChainB(LythonSourceSpan span, ExecutionContext context)
        {
            // Build into locals so a failure (unhashable elements, budgets,
            // junk-predicate errors) leaves the previous index intact with its
            // charge; only swap and commit on success. Keys compare with guest
            // __hash__/__eq__ (N12) instead of the structural comparer.
            var b2j = new ContextualKeyTable<List<int>>();
            var bjunk = new ContextualKeySet();
            var bpopular = new ContextualKeySet();
            using var reservation = _governor.ReserveTemporary(0, span);
            var work = 0;

            try
            {
                for (var index = 0; index < _b.Count; index++)
                {
                    var indices = b2j.GetOrAdd(_b[index], static () => new List<int>(), context, span).Value;
                    if (indices.Count == 0)
                    {
                        reservation.Grow(ChainKeyBytesPerElement, span);
                    }

                    indices.Add(index);
                    reservation.Grow(ChainIndexBytesPerElement, span);
                    if ((++work & (BudgetCheckInterval - 1)) == 0)
                    {
                        context.CheckExecutionBudget(span);
                    }
                }

                if (_isjunk is not null)
                {
                    foreach (var item in b2j.EntriesInOrder.Select(static entry => entry.Key).ToArray())
                    {
                        if (DifflibModule.CallJunkPredicate(_isjunk, item, span, context))
                        {
                            bjunk.Add(item, context, span);
                        }

                        if ((++work & (BudgetCheckInterval - 1)) == 0)
                        {
                            context.CheckExecutionBudget(span);
                        }
                    }

                    foreach (var item in bjunk.Keys)
                    {
                        b2j.Remove(item, context, span);
                    }
                }

                if (_autojunk && _b.Count >= 200)
                {
                    var threshold = _b.Count / 100 + 1;
                    foreach (var entry in b2j.EntriesInOrder)
                    {
                        if (entry.Value.Count > threshold)
                        {
                            bpopular.Add(entry.Key, context, span);
                        }

                        if ((++work & (BudgetCheckInterval - 1)) == 0)
                        {
                            context.CheckExecutionBudget(span);
                        }
                    }

                    foreach (var item in bpopular.Keys)
                    {
                        b2j.Remove(item, context, span);
                    }
                }
            }
            catch (PyUnhashableException)
            {
                throw new LythonRuntimeException("TypeError", "difflib.SequenceMatcher sequence elements must be hashable.", span);
            }

            var chainCharge =
                ChainBaseBytes +
                (ChainKeyBytesPerElement * b2j.Count) +
                (ChainIndexBytesPerElement * _b.Count) +
                SetBaseBytes + (SetBytesPerItem * bjunk.Count) +
                SetBaseBytes + (SetBytesPerItem * bpopular.Count);
            _governor.Reserve(chainCharge, span);
            _governor.Commit(chainCharge);
            _governor.Release(_chainCharge);
            ReleaseViews();
            _b2j = b2j;
            _bjunk = bjunk;
            _bpopular = bpopular;
            _chainCharge = chainCharge;
        }

        private ContextualKeyTable<int> BuildFullBCount(LythonSourceSpan span, ExecutionContext context)
        {
            var beforeCharges = CommittedStorageBytes;
            using var reservation = _governor.ReserveTemporary(0, span);
            // N12: contextual counts (guest __hash__/__eq__).
            var counts = new ContextualKeyTable<int>();
            var work = 0;
            foreach (var item in _b)
            {
                var entry = counts.GetOrAdd(item, 0, context, span);
                if (entry.Value == 0)
                {
                    reservation.Grow(CountBytesPerEntry, span);
                }
                entry.Value++;
                if ((++work & (BudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            var charge = CountBaseBytes + (CountBytesPerEntry * counts.Count);
            _governor.Reserve(charge, span);
            _governor.Commit(charge);
            _governor.Release(_fullBCountCharge);
            _fullBCountCharge = charge;
            NoteGrowth(beforeCharges);
            return counts;
        }

        private void ReleaseViews()
        {
            // N05: cached views own their charges via the pool (view as owner);
            // dropping the cache must not refund aliases still holding old graphs.
            _b2jView = null;
            _bjunkView = null;
            _bpopularView = null;
        }

        // R03: exposed chain state is a cached snapshot, so repeated access
        // observes identical objects until set_seq2 rebuilds the chain. Charges
        // cover the retained view storage; user mutation of a view does not
        // feed back into matching. Spans are unavailable at member access, so
        // view charges carry no allocation site.
        private PyDict GetB2JView()
        {
            if (_b2jView is not null)
            {
                return _b2jView;
            }

            // N05: preflight before the CLR graph can allocate; the graph (dict +
            // nested index lists/boxes) is covered by one coupon owned by the view itself.
            var charge = ChainBaseBytes;
            foreach (var entry in _b2j.EntriesInOrder)
            {
                charge = checked(charge + ChainKeyBytesPerElement + ViewListBaseBytes + (ChainIndexBytesPerElement * entry.Value.Count));
            }

            _governor.EnsureCanReserve(charge, null);
            var dict = new PyDict();
            var work = 0;
            foreach (var entry in _b2j.EntriesInOrder)
            {
                dict.SetItem(entry.Key, new PyList(entry.Value.Select(index => (object)new BigInteger(index))));
                if ((++work & 63) == 0)
                {
                    _creationContext.CheckExecutionBudget(null);
                }
            }

            try
            {
                _governor.Reserve(charge, null);
                _governor.Commit(charge);
                _pool.TrackFreshMutable(dict, charge, null);
            }
            catch
            {
                _governor.Release(charge);
                throw;
            }

            _b2jView = dict;
            return dict;
        }
        private PySet GetBjunkView()
        {
            if (_bjunkView is not null)
            {
                return _bjunkView;
            }

            var charge = checked(SetBaseBytes + (SetBytesPerItem * _bjunk.Count));
            _governor.EnsureCanReserve(charge, null);
            var view = new PySet(_bjunk.Keys);
            try
            {
                _governor.Reserve(charge, null);
                _governor.Commit(charge);
                _pool.TrackFreshMutable(view, charge, null);
            }
            catch
            {
                _governor.Release(charge);
                throw;
            }

            _bjunkView = view;
            return view;
        }
        private PySet GetBpopularView()
        {
            if (_bpopularView is not null)
            {
                return _bpopularView;
            }

            var charge = checked(SetBaseBytes + (SetBytesPerItem * _bpopular.Count));
            _governor.EnsureCanReserve(charge, null);
            var view = new PySet(_bpopular.Keys);
            try
            {
                _governor.Reserve(charge, null);
                _governor.Commit(charge);
                _pool.TrackFreshMutable(view, charge, null);
            }
            catch
            {
                _governor.Release(charge);
                throw;
            }

            _bpopularView = view;
            return view;
        }
        private List<MatchingBlock> GetMatchingBlocks(LythonSourceSpan span, ExecutionContext context)
        {
            var beforeCharges = CommittedStorageBytes;
            if (_matchingBlocks is not null)
            {
                return _matchingBlocks;
            }

            using var reservation = _governor.ReserveTemporary(0, span);
            var blocks = new List<MatchingBlock>();
            var chargedBlocks = 0;
            var queue = new Stack<MatchRange>();
            queue.Push(new MatchRange(0, _a.Count, 0, _b.Count));
            var work = 0;
            while (queue.Count != 0)
            {
                var range = queue.Pop();
                var match = FindLongestMatch(range, span, context);
                if (match.Size == 0)
                {
                    continue;
                }

                blocks.Add(match);
                if (blocks.Count > chargedBlocks)
                {
                    reservation.Grow(BlockBytesPerEntry * (blocks.Count - chargedBlocks), span);
                    chargedBlocks = blocks.Count;
                }

                if ((++work & (BudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
                if (range.ALo < match.A && range.BLo < match.B)
                {
                    queue.Push(new MatchRange(range.ALo, match.A, range.BLo, match.B));
                }

                if (match.A + match.Size < range.AHi && match.B + match.Size < range.BHi)
                {
                    queue.Push(new MatchRange(match.A + match.Size, range.AHi, match.B + match.Size, range.BHi));
                }
            }

            blocks.Sort(static (left, right) =>
            {
                var byA = left.A.CompareTo(right.A);
                return byA != 0 ? byA : left.B.CompareTo(right.B);
            });

            var collapsed = new List<MatchingBlock>();
            var i1 = 0;
            var j1 = 0;
            var k1 = 0;
            var collapseWork = 0;
            foreach (var block in blocks)
            {
                if ((++collapseWork & (BudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }

                if (i1 + k1 == block.A && j1 + k1 == block.B)
                {
                    k1 += block.Size;
                    continue;
                }

                if (k1 != 0)
                {
                    collapsed.Add(new MatchingBlock(i1, j1, k1));
                }

                i1 = block.A;
                j1 = block.B;
                k1 = block.Size;
            }

            if (k1 != 0)
            {
                collapsed.Add(new MatchingBlock(i1, j1, k1));
            }

            collapsed.Add(new MatchingBlock(_a.Count, _b.Count, 0));
            var blocksCharge = BlocksBaseBytes + (BlockBytesPerEntry * collapsed.Count);
            _governor.Reserve(blocksCharge, span);
            _governor.Commit(blocksCharge);
            _governor.Release(_matchingBlocksCharge);
            _matchingBlocksCharge = blocksCharge;
            _matchingBlocks = collapsed;
            NoteGrowth(beforeCharges);
            return collapsed;
        }

        private MatchingBlock FindLongestMatch(MatchRange range, LythonSourceSpan span, ExecutionContext context)
        {
            // Row scratch reuses two maps instead of allocating one per row.
            // Previous and current coexist, so the reservation always covers
            // both live maps plus the next insertion before either grows.
            using var reservation = _governor.ReserveTemporary(0, span);
            var bestA = range.ALo;
            var bestB = range.BLo;
            var bestSize = 0;
            var previousLengths = new Dictionary<int, int>();
            var currentLengths = new Dictionary<int, int>();
            var coveredEntries = 0;
            var work = 0;

            for (var i = range.ALo; i < range.AHi; i++)
            {
                currentLengths.Clear();
                if (!_b2j.TryGetValue(_a[i], context, span, out var indexes))
                {
                    // No match: the cleared map becomes the empty previous row.
                    (previousLengths, currentLengths) = (currentLengths, previousLengths);
                }
                else
                {
                    foreach (var j in indexes)
                    {
                        if (j < range.BLo)
                        {
                            continue;
                        }

                        if (j >= range.BHi)
                        {
                            break;
                        }

                        var need = previousLengths.Count + currentLengths.Count + 1;
                        if (need > coveredEntries)
                        {
                            reservation.Grow(ScratchBytesPerEntry * (need - coveredEntries), span);
                            coveredEntries = need;
                        }

                        var length = previousLengths.TryGetValue(j - 1, out var previous) ? previous + 1 : 1;
                        currentLengths[j] = length;
                        if (length > bestSize)
                        {
                            bestA = i - length + 1;
                            bestB = j - length + 1;
                            bestSize = length;
                        }

                        if ((++work & (BudgetCheckInterval - 1)) == 0)
                        {
                            context.CheckExecutionBudget(span);
                        }
                    }

                    (previousLengths, currentLengths) = (currentLengths, previousLengths);
                }

                if ((++work & (BudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            while (bestA > range.ALo &&
                bestB > range.BLo &&
                !_bjunk.Contains(_b[bestB - 1], context, span) &&
                AreEqual(_a[bestA - 1], _b[bestB - 1]))
            {
                bestA--;
                bestB--;
                bestSize++;

                if ((++work & (BudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            while (bestA + bestSize < range.AHi &&
                bestB + bestSize < range.BHi &&
                !_bjunk.Contains(_b[bestB + bestSize], context, span) &&
                AreEqual(_a[bestA + bestSize], _b[bestB + bestSize]))
            {
                bestSize++;

                if ((++work & (BudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            while (bestA > range.ALo &&
                bestB > range.BLo &&
                _bjunk.Contains(_b[bestB - 1], context, span) &&
                AreEqual(_a[bestA - 1], _b[bestB - 1]))
            {
                bestA--;
                bestB--;
                bestSize++;

                if ((++work & (BudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            while (bestA + bestSize < range.AHi &&
                bestB + bestSize < range.BHi &&
                _bjunk.Contains(_b[bestB + bestSize], context, span) &&
                AreEqual(_a[bestA + bestSize], _b[bestB + bestSize]))
            {
                bestSize++;

                if ((++work & (BudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            return new MatchingBlock(bestA, bestB, bestSize);
        }

        private static double CalculateRatio(int matches, int length)
            => length == 0 ? 1.0 : 2.0 * matches / length;

        private static PyTuple ToPyTuple(DiffOpcode opcode)
            => new([
                PyString.FromString(opcode.Tag switch
                {
                    DiffTag.Equal => "equal",
                    DiffTag.Delete => "delete",
                    DiffTag.Insert => "insert",
                    DiffTag.Replace => "replace",
                    _ => throw new InvalidOperationException($"Unknown diff opcode: {opcode.Tag}")
                }),
                new BigInteger(opcode.I1),
                new BigInteger(opcode.I2),
                new BigInteger(opcode.J1),
                new BigInteger(opcode.J2)]);


        private readonly record struct MatchRange(int ALo, int AHi, int BLo, int BHi);

        private readonly record struct MatchingBlock(int A, int B, int Size);
    }

}
