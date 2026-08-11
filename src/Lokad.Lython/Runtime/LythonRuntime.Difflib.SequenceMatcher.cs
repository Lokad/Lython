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
        private readonly object? _isjunk;
        private readonly bool _autojunk;
        private object _aOriginal;
        private object _bOriginal;
        private IReadOnlyList<object> _a;
        private IReadOnlyList<object> _b;
        private Dictionary<object, List<int>> _b2j;
        private HashSet<object> _bjunk;
        private HashSet<object> _bpopular;
        private Dictionary<object, int>? _fullBCount;
        private List<MatchingBlock>? _matchingBlocks;
        private List<DiffOpcode>? _opcodes;

        public DifflibSequenceMatcherObject(object? isjunk, object aOriginal, object bOriginal, bool autojunk, LythonSourceSpan span, ExecutionContext context)
        {
            _isjunk = isjunk;
            _autojunk = autojunk;
            _aOriginal = aOriginal;
            _bOriginal = bOriginal;
            _a = DifflibModule.MaterializeSequence(aOriginal, span);
            _b = [];
            _b2j = new Dictionary<object, List<int>>(PyValueComparer.Instance);
            _bjunk = new HashSet<object>(PyValueComparer.Instance);
            _bpopular = new HashSet<object>(PyValueComparer.Instance);
            SetSeq2(bOriginal, span, context);
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "a" => _aOriginal,
                "b" => _bOriginal,
                "b2j" => CreateB2JDictionary(),
                "bjunk" => new PySet(_bjunk),
                "bpopular" => new PySet(_bpopular),
                "set_seqs" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 2)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.set_seqs(a, b) expects two arguments.", span);
                    }

                    SetSeqs(arguments[0], arguments[1], span, context);
                    return PyNone.Instance;
                }, "SequenceMatcher.set_seqs", ["a", "b"]),
                "set_seq1" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.set_seq1(a) expects one argument.", span);
                    }

                    SetSeq1(arguments[0], span);
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
                "ratio" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.ratio() expects no arguments.", span);
                    }

                    return Ratio();
                }),
                "quick_ratio" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.quick_ratio() expects no arguments.", span);
                    }

                    return QuickRatio();
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
                    var block = FindLongestMatch(new MatchRange(alo, ahi, blo, bhi));
                    return new DifflibMatchObject(block.A, block.B, block.Size);
                }, LythonCallableSignature.Create("SequenceMatcher.find_longest_match", ["alo", "ahi", "blo", "bhi"], requiredCount: 0)),
                "get_matching_blocks" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.get_matching_blocks() expects no arguments.", span);
                    }

                    return new PyList(GetMatchingBlocks().Select(block => (object)new DifflibMatchObject(block.A, block.B, block.Size)), context.MemoryGovernor, span);
                }),
                "get_opcodes" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.get_opcodes() expects no arguments.", span);
                    }

                    return new PyList(BuildOpcodes().Select(opcode => (object)ToPyTuple(opcode)), context.MemoryGovernor, span);
                }),
                "get_grouped_opcodes" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.get_grouped_opcodes([n]) expects zero or one argument.", span);
                    }

                    var n = arguments.Length == 1 && arguments[0] is not PyNone ? DifflibModule.RequireInt32(arguments[0], "SequenceMatcher.get_grouped_opcodes(..., n=...) expects an integer.", span) : 3;
                    return new PyList(BuildGroupedOpcodes(n).Select(group => (object)new PyList(group.Select(opcode => (object)ToPyTuple(opcode)), context.MemoryGovernor, span)), context.MemoryGovernor, span);
                }, "SequenceMatcher.get_grouped_opcodes", ["n"], requiredCount: 0),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public void SetSeqs(object aOriginal, object bOriginal, LythonSourceSpan span, ExecutionContext context)
        {
            SetSeq1(aOriginal, span);
            SetSeq2(bOriginal, span, context);
        }

        public void SetSeq1(object aOriginal, LythonSourceSpan span)
        {
            _aOriginal = aOriginal;
            _a = DifflibModule.MaterializeSequence(aOriginal, span);
            _matchingBlocks = null;
            _opcodes = null;
        }

        public void SetSeq2(object bOriginal, LythonSourceSpan span, ExecutionContext context)
        {
            _bOriginal = bOriginal;
            _b = DifflibModule.MaterializeSequence(bOriginal, span);
            _matchingBlocks = null;
            _opcodes = null;
            _fullBCount = null;
            ChainB(span, context);
        }

        public double Ratio()
        {
            var matches = GetMatchingBlocks().Sum(block => block.Size);
            return CalculateRatio(matches, _a.Count + _b.Count);
        }

        public double QuickRatio()
        {
            _fullBCount ??= BuildFullBCount();
            var available = new Dictionary<object, int>(PyValueComparer.Instance);
            var matches = 0;
            foreach (var item in _a)
            {
                if (!available.TryGetValue(item, out var count))
                {
                    _fullBCount.TryGetValue(item, out count);
                }

                available[item] = count - 1;
                if (count > 0)
                {
                    matches++;
                }
            }

            return CalculateRatio(matches, _a.Count + _b.Count);
        }

        public double RealQuickRatio()
            => CalculateRatio(Math.Min(_a.Count, _b.Count), _a.Count + _b.Count);

        internal List<DiffOpcode> BuildOpcodes()
        {
            if (_opcodes is not null)
            {
                return _opcodes;
            }

            var opcodes = new List<DiffOpcode>();
            var i = 0;
            var j = 0;
            foreach (var block in GetMatchingBlocks())
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
            }

            _opcodes = opcodes;
            return opcodes;
        }

        internal List<List<DiffOpcode>> BuildGroupedOpcodes(int contextLines)
        {
            var codes = BuildOpcodes().Select(static opcode => opcode).ToList();
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
            foreach (var code in codes)
            {
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
            _b2j = new Dictionary<object, List<int>>(PyValueComparer.Instance);
            _bjunk = new HashSet<object>(PyValueComparer.Instance);
            _bpopular = new HashSet<object>(PyValueComparer.Instance);

            try
            {
                for (var index = 0; index < _b.Count; index++)
                {
                    if (!_b2j.TryGetValue(_b[index], out var indices))
                    {
                        indices = [];
                        _b2j[_b[index]] = indices;
                    }

                    indices.Add(index);
                }

                if (_isjunk is not null)
                {
                    foreach (var item in _b2j.Keys.ToArray())
                    {
                        if (DifflibModule.CallJunkPredicate(_isjunk, item, span, context))
                        {
                            _bjunk.Add(item);
                        }
                    }

                    foreach (var item in _bjunk)
                    {
                        _b2j.Remove(item);
                    }
                }

                if (_autojunk && _b.Count >= 200)
                {
                    var threshold = _b.Count / 100 + 1;
                    foreach (var pair in _b2j)
                    {
                        if (pair.Value.Count > threshold)
                        {
                            _bpopular.Add(pair.Key);
                        }
                    }

                    foreach (var item in _bpopular)
                    {
                        _b2j.Remove(item);
                    }
                }
            }
            catch (InvalidOperationException ex) when (string.Equals(ex.Message, "unhashable value", StringComparison.Ordinal))
            {
                throw new LythonRuntimeException("TypeError", "difflib.SequenceMatcher sequence elements must be hashable.", span);
            }
        }

        private Dictionary<object, int> BuildFullBCount()
        {
            var counts = new Dictionary<object, int>(PyValueComparer.Instance);
            foreach (var item in _b)
            {
                counts.TryGetValue(item, out var count);
                counts[item] = count + 1;
            }

            return counts;
        }

        private PyDict CreateB2JDictionary()
        {
            var dict = new PyDict();
            foreach (var pair in _b2j)
            {
                dict.SetItem(pair.Key, new PyList(pair.Value.Select(index => (object)new BigInteger(index))));
            }

            return dict;
        }

        private List<MatchingBlock> GetMatchingBlocks()
        {
            if (_matchingBlocks is not null)
            {
                return _matchingBlocks;
            }

            var blocks = new List<MatchingBlock>();
            var queue = new Stack<MatchRange>();
            queue.Push(new MatchRange(0, _a.Count, 0, _b.Count));
            while (queue.Count != 0)
            {
                var range = queue.Pop();
                var match = FindLongestMatch(range);
                if (match.Size == 0)
                {
                    continue;
                }

                blocks.Add(match);
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
            foreach (var block in blocks)
            {
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
            _matchingBlocks = collapsed;
            return collapsed;
        }

        private MatchingBlock FindLongestMatch(MatchRange range)
        {
            var bestA = range.ALo;
            var bestB = range.BLo;
            var bestSize = 0;
            var previousLengths = new Dictionary<int, int>();

            for (var i = range.ALo; i < range.AHi; i++)
            {
                var newLengths = new Dictionary<int, int>();
                if (!_b2j.TryGetValue(_a[i], out var indexes))
                {
                    previousLengths = newLengths;
                    continue;
                }

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

                    var length = previousLengths.TryGetValue(j - 1, out var previous) ? previous + 1 : 1;
                    newLengths[j] = length;
                    if (length > bestSize)
                    {
                        bestA = i - length + 1;
                        bestB = j - length + 1;
                        bestSize = length;
                    }
                }

                previousLengths = newLengths;
            }

            while (bestA > range.ALo &&
                bestB > range.BLo &&
                !_bjunk.Contains(_b[bestB - 1]) &&
                AreEqual(_a[bestA - 1], _b[bestB - 1]))
            {
                bestA--;
                bestB--;
                bestSize++;
            }

            while (bestA + bestSize < range.AHi &&
                bestB + bestSize < range.BHi &&
                !_bjunk.Contains(_b[bestB + bestSize]) &&
                AreEqual(_a[bestA + bestSize], _b[bestB + bestSize]))
            {
                bestSize++;
            }

            while (bestA > range.ALo &&
                bestB > range.BLo &&
                _bjunk.Contains(_b[bestB - 1]) &&
                AreEqual(_a[bestA - 1], _b[bestB - 1]))
            {
                bestA--;
                bestB--;
                bestSize++;
            }

            while (bestA + bestSize < range.AHi &&
                bestB + bestSize < range.BHi &&
                _bjunk.Contains(_b[bestB + bestSize]) &&
                AreEqual(_a[bestA + bestSize], _b[bestB + bestSize]))
            {
                bestSize++;
            }

            return new MatchingBlock(bestA, bestB, bestSize);
        }

        private static double CalculateRatio(int matches, int length)
            => length == 0 ? 1.0 : 2.0 * matches / length;

        private static PyTuple ToPyTuple(DiffOpcode opcode)
            => new([
                PyString.FromString(TagName(opcode.Tag)),
                new BigInteger(opcode.I1),
                new BigInteger(opcode.I2),
                new BigInteger(opcode.J1),
                new BigInteger(opcode.J2)]);

        private static string TagName(DiffTag tag)
            => tag switch
            {
                DiffTag.Equal => "equal",
                DiffTag.Delete => "delete",
                DiffTag.Insert => "insert",
                DiffTag.Replace => "replace",
                _ => throw new InvalidOperationException($"Unknown diff opcode: {tag}")
            };

        private readonly record struct MatchRange(int ALo, int AHi, int BLo, int BHi);

        private readonly record struct MatchingBlock(int A, int B, int Size);
    }

}
