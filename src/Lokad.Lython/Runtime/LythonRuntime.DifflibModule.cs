using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class DifflibModule : PyModule
    {
        public static readonly DifflibModule Instance = new();

        private DifflibModule() : base("difflib")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "unified_diff" => new BuiltinCallable(LythonKnownCallableSignatures.DifflibUnifiedDiff, UnifiedDiff),
                "context_diff" => new BuiltinCallable(LythonKnownCallableSignatures.DifflibContextDiff, ContextDiff),
                "ndiff" => new BuiltinCallable(LythonKnownCallableSignatures.DifflibNdiff, Ndiff),
                "restore" => new BuiltinCallable(LythonKnownCallableSignatures.DifflibRestore, Restore),
                "get_close_matches" => new BuiltinCallable(LythonKnownCallableSignatures.DifflibGetCloseMatches, GetCloseMatches),
                "SequenceMatcher" => new BuiltinCallable(LythonKnownCallableSignatures.DifflibSequenceMatcher, SequenceMatcher),
                _ => null!,
            };

            return value is not null;
        }

        private static object UnifiedDiff(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var options = ParseDiffArguments(arguments, "difflib.unified_diff", span);
            var a = RequireStringSequence(arguments[0], "difflib.unified_diff(a, b)", span);
            var b = RequireStringSequence(arguments[1], "difflib.unified_diff(a, b)", span);
            return new PyList(BuildUnifiedDiff(a, b, options), context.MemoryGovernor, span);
        }

        private static object ContextDiff(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var options = ParseDiffArguments(arguments, "difflib.context_diff", span);
            var a = RequireStringSequence(arguments[0], "difflib.context_diff(a, b)", span);
            var b = RequireStringSequence(arguments[1], "difflib.context_diff(a, b)", span);
            return new PyList(BuildContextDiff(a, b, options), context.MemoryGovernor, span);
        }

        private static object Ndiff(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", "difflib.ndiff(a, b) expects two arguments.", span);
            }

            var a = RequireStringSequence(arguments[0], "difflib.ndiff(a, b)", span);
            var b = RequireStringSequence(arguments[1], "difflib.ndiff(a, b)", span);
            return new PyList(BuildNdiff(a, b), context.MemoryGovernor, span);
        }

        private static object Restore(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", "difflib.restore(delta, which) expects two arguments.", span);
            }

            var which = RequireInt32(arguments[1], "difflib.restore(delta, which) expects which to be 1 or 2.", span);
            if (which is not 1 and not 2)
            {
                throw new LythonRuntimeException("ValueError", "difflib.restore(delta, which) expects which to be 1 or 2.", span);
            }

            var lines = RequireStringSequence(arguments[0], "difflib.restore(delta, which)", span);
            var restored = new List<object>();
            foreach (var line in lines)
            {
                var text = line.AsString();
                if (text.Length < 2)
                {
                    continue;
                }

                if ((which == 1 && text[0] is ' ' or '-') ||
                    (which == 2 && text[0] is ' ' or '+'))
                {
                    restored.Add(PyString.FromString(text[2..]));
                }
            }

            return new PyList(restored, context.MemoryGovernor, span);
        }

        private static object GetCloseMatches(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length is < 2 or > 4 || !PyStringOps.TryAsString(arguments[0], out var word))
            {
                throw new LythonRuntimeException("TypeError", "difflib.get_close_matches(word, possibilities[, n][, cutoff]) expects a string word and an iterable of strings.", span);
            }

            var possibilities = RequireStringSequence(arguments[1], "difflib.get_close_matches(word, possibilities)", span);
            var limit = arguments.Length >= 3 && arguments[2] is not PyNone ? RequireInt32(arguments[2], "difflib.get_close_matches(..., n=...) expects n to be an integer.", span) : 3;
            var cutoff = arguments.Length >= 4 && arguments[3] is not PyNone ? RequireDouble(arguments[3], "difflib.get_close_matches(..., cutoff=...) expects cutoff to be a number.", span) : 0.6;
            if (limit < 0)
            {
                throw new LythonRuntimeException("ValueError", "difflib.get_close_matches(..., n=...) expects n to be non-negative.", span);
            }

            if (cutoff < 0.0 || cutoff > 1.0)
            {
                throw new LythonRuntimeException("ValueError", "difflib.get_close_matches(..., cutoff=...) expects cutoff between 0 and 1.", span);
            }

            var scored = new List<(double Score, PyString Value)>();
            var wordItems = MaterializeSequence(word, span);
            foreach (var candidate in possibilities)
            {
                var matcher = new DifflibSequenceMatcherObject(wordItems, MaterializeSequence(candidate, span));
                var score = matcher.Ratio();
                if (score >= cutoff)
                {
                    scored.Add((score, candidate));
                }
            }

            scored.Sort((left, right) =>
            {
                var byScore = right.Score.CompareTo(left.Score);
                return byScore != 0 ? byScore : PyString.CompareOrdinal(left.Value, right.Value);
            });

            return new PyList(scored.Take(limit).Select(item => (object)item.Value), context.MemoryGovernor, span);
        }

        private static object SequenceMatcher(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length > 4)
            {
                throw new LythonRuntimeException("TypeError", "difflib.SequenceMatcher([isjunk][, a][, b][, autojunk]) expects zero to four arguments.", span);
            }

            var a = arguments.Length >= 2 ? MaterializeSequence(arguments[1], span) : [];
            var b = arguments.Length >= 3 ? MaterializeSequence(arguments[2], span) : [];
            _ = context;
            return new DifflibSequenceMatcherObject(a, b);
        }

        private static DiffOptions ParseDiffArguments(object[] arguments, string owner, LythonSourceSpan span)
        {
            if (arguments.Length is < 2 or > 8)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(a, b[, fromfile][, tofile][, fromfiledate][, tofiledate][, n][, lineterm]) expects two to eight arguments.", span);
            }

            return new DiffOptions(
                ParseOptionalString(arguments, 2, string.Empty, $"{owner}(..., fromfile=...)", span),
                ParseOptionalString(arguments, 3, string.Empty, $"{owner}(..., tofile=...)", span),
                ParseOptionalString(arguments, 4, string.Empty, $"{owner}(..., fromfiledate=...)", span),
                ParseOptionalString(arguments, 5, string.Empty, $"{owner}(..., tofiledate=...)", span),
                arguments.Length >= 7 ? RequireInt32(arguments[6], $"{owner}(..., n=...) expects n to be an integer.", span) : 3,
                ParseOptionalString(arguments, 7, "\n", $"{owner}(..., lineterm=...)", span));
        }

        private static string ParseOptionalString(object[] arguments, int index, string defaultValue, string owner, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return defaultValue;
            }

            if (!PyStringOps.TryAsString(arguments[index], out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects a string or None.", span);
            }

            return text.AsString();
        }

        private static IReadOnlyList<PyString> RequireStringSequence(object value, string owner, LythonSourceSpan span)
        {
            try
            {
                return ToSequence(value, span)
                    .Select(item => PyStringOps.TryAsString(item, out var text)
                        ? text
                        : throw new LythonRuntimeException("TypeError", $"{owner} expects an iterable of strings.", span))
                    .ToArray();
            }
            catch (LythonRuntimeException ex) when (ex.ExceptionType == "TypeError" && ex.Message == "Object is not iterable.")
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects an iterable of strings.", span);
            }
        }

        public static IReadOnlyList<object> MaterializeSequence(object value, LythonSourceSpan span)
        {
            try
            {
                return ToSequence(value, span).ToArray();
            }
            catch (LythonRuntimeException ex) when (ex.ExceptionType == "TypeError" && ex.Message == "Object is not iterable.")
            {
                throw new LythonRuntimeException("TypeError", "difflib.SequenceMatcher sequence arguments must be iterable.", span);
            }
        }

        private static int RequireInt32(object value, string message, LythonSourceSpan span)
        {
            if (!PyNumberOps.TryAsInteger(value, out var integer))
            {
                throw new LythonRuntimeException("TypeError", message, span);
            }

            if (integer < int.MinValue || integer > int.MaxValue)
            {
                throw new LythonRuntimeException("OverflowError", message, span);
            }

            return (int)integer;
        }

        private static double RequireDouble(object value, string message, LythonSourceSpan span)
        {
            if (!PyNumberOps.TryAsNumber(value, out var number))
            {
                throw new LythonRuntimeException("TypeError", message, span);
            }

            return number.ToDouble();
        }

        private static IEnumerable<object> BuildUnifiedDiff(IReadOnlyList<PyString> a, IReadOnlyList<PyString> b, DiffOptions options)
        {
            var groups = BuildGroupedOpcodes(a, b, options.ContextLines);
            if (groups.Count == 0)
            {
                yield break;
            }

            yield return PyString.FromString("--- " + FileHeader(options.FromFile, options.FromFileDate) + options.LineTerminator);
            yield return PyString.FromString("+++ " + FileHeader(options.ToFile, options.ToFileDate) + options.LineTerminator);
            foreach (var group in groups)
            {
                var first = group[0];
                var last = group[^1];
                yield return PyString.FromString("@@ -" + FormatUnifiedRange(first.I1, last.I2 - first.I1) + " +" + FormatUnifiedRange(first.J1, last.J2 - first.J1) + " @@" + options.LineTerminator);
                foreach (var opcode in group)
                {
                    foreach (var line in FormatUnifiedOpcode(opcode, a, b))
                    {
                        yield return line;
                    }
                }
            }
        }

        private static IEnumerable<object> BuildContextDiff(IReadOnlyList<PyString> a, IReadOnlyList<PyString> b, DiffOptions options)
        {
            var groups = BuildGroupedOpcodes(a, b, options.ContextLines);
            if (groups.Count == 0)
            {
                yield break;
            }

            yield return PyString.FromString("*** " + FileHeader(options.FromFile, options.FromFileDate) + options.LineTerminator);
            yield return PyString.FromString("--- " + FileHeader(options.ToFile, options.ToFileDate) + options.LineTerminator);
            foreach (var group in groups)
            {
                var first = group[0];
                var last = group[^1];
                yield return PyString.FromString("***************" + options.LineTerminator);
                yield return PyString.FromString("*** " + FormatContextRange(first.I1, last.I2 - first.I1) + " ****" + options.LineTerminator);
                foreach (var line in FormatContextOldLines(group, a))
                {
                    yield return line;
                }

                yield return PyString.FromString("--- " + FormatContextRange(first.J1, last.J2 - first.J1) + " ----" + options.LineTerminator);
                foreach (var line in FormatContextNewLines(group, b))
                {
                    yield return line;
                }
            }
        }

        private static IEnumerable<object> BuildNdiff(IReadOnlyList<PyString> a, IReadOnlyList<PyString> b)
        {
            foreach (var opcode in BuildOpcodes(a, b))
            {
                switch (opcode.Tag)
                {
                    case DiffTag.Equal:
                        for (var i = opcode.I1; i < opcode.I2; i++)
                        {
                            yield return Prefix("  ", a[i]);
                        }

                        break;
                    case DiffTag.Delete:
                        for (var i = opcode.I1; i < opcode.I2; i++)
                        {
                            yield return Prefix("- ", a[i]);
                        }

                        break;
                    case DiffTag.Insert:
                        for (var i = opcode.J1; i < opcode.J2; i++)
                        {
                            yield return Prefix("+ ", b[i]);
                        }

                        break;
                    case DiffTag.Replace:
                        for (var i = opcode.I1; i < opcode.I2; i++)
                        {
                            yield return Prefix("- ", a[i]);
                        }

                        for (var j = opcode.J1; j < opcode.J2; j++)
                        {
                            yield return Prefix("+ ", b[j]);
                        }

                        break;
                }
            }
        }

        private static List<DiffOpcode> BuildOpcodes(IReadOnlyList<PyString> a, IReadOnlyList<PyString> b)
            => new DifflibSequenceMatcherObject(a.Cast<object>().ToArray(), b.Cast<object>().ToArray()).BuildOpcodes();

        private static List<List<DiffOpcode>> BuildGroupedOpcodes(IReadOnlyList<PyString> a, IReadOnlyList<PyString> b, int contextLines)
        {
            var opcodes = BuildOpcodes(a, b);
            var changes = opcodes.Where(opcode => opcode.Tag != DiffTag.Equal).ToArray();
            if (changes.Length == 0)
            {
                return [];
            }

            var context = Math.Max(0, contextLines);
            var ranges = new List<DiffRange>();
            foreach (var change in changes)
            {
                var oldStart = Math.Max(0, change.I1 - context);
                var oldEnd = Math.Min(a.Count, change.I2 + context);
                var newStart = Math.Max(0, change.J1 - context);
                var newEnd = Math.Min(b.Count, change.J2 + context);

                if (ranges.Count != 0 && oldStart <= ranges[^1].OldEnd && newStart <= ranges[^1].NewEnd)
                {
                    ranges[^1] = ranges[^1] with
                    {
                        OldEnd = Math.Max(ranges[^1].OldEnd, oldEnd),
                        NewEnd = Math.Max(ranges[^1].NewEnd, newEnd)
                    };
                }
                else
                {
                    ranges.Add(new DiffRange(oldStart, oldEnd, newStart, newEnd));
                }
            }

            var groups = new List<List<DiffOpcode>>();
            foreach (var range in ranges)
            {
                var group = new List<DiffOpcode>();
                foreach (var opcode in opcodes)
                {
                    if (opcode.I2 < range.OldStart || opcode.I1 > range.OldEnd ||
                        opcode.J2 < range.NewStart || opcode.J1 > range.NewEnd)
                    {
                        continue;
                    }

                    var clipped = ClipOpcode(opcode, range);
                    if (clipped.I1 != clipped.I2 || clipped.J1 != clipped.J2)
                    {
                        group.Add(clipped);
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        private static DiffOpcode ClipOpcode(DiffOpcode opcode, DiffRange range)
        {
            if (opcode.Tag == DiffTag.Equal)
            {
                var trimLeft = Math.Max(range.OldStart - opcode.I1, range.NewStart - opcode.J1);
                var trimRight = Math.Max(opcode.I2 - range.OldEnd, opcode.J2 - range.NewEnd);
                trimLeft = Math.Max(0, trimLeft);
                trimRight = Math.Max(0, trimRight);
                return opcode with
                {
                    I1 = opcode.I1 + trimLeft,
                    J1 = opcode.J1 + trimLeft,
                    I2 = opcode.I2 - trimRight,
                    J2 = opcode.J2 - trimRight
                };
            }

            return opcode with
            {
                I1 = Math.Max(opcode.I1, range.OldStart),
                I2 = Math.Min(opcode.I2, range.OldEnd),
                J1 = Math.Max(opcode.J1, range.NewStart),
                J2 = Math.Min(opcode.J2, range.NewEnd)
            };
        }

        private static IEnumerable<PyString> FormatUnifiedOpcode(DiffOpcode opcode, IReadOnlyList<PyString> a, IReadOnlyList<PyString> b)
        {
            switch (opcode.Tag)
            {
                case DiffTag.Equal:
                    for (var i = opcode.I1; i < opcode.I2; i++)
                    {
                        yield return Prefix(" ", a[i]);
                    }

                    break;
                case DiffTag.Delete:
                    for (var i = opcode.I1; i < opcode.I2; i++)
                    {
                        yield return Prefix("-", a[i]);
                    }

                    break;
                case DiffTag.Insert:
                    for (var j = opcode.J1; j < opcode.J2; j++)
                    {
                        yield return Prefix("+", b[j]);
                    }

                    break;
                case DiffTag.Replace:
                    for (var i = opcode.I1; i < opcode.I2; i++)
                    {
                        yield return Prefix("-", a[i]);
                    }

                    for (var j = opcode.J1; j < opcode.J2; j++)
                    {
                        yield return Prefix("+", b[j]);
                    }

                    break;
            }
        }

        private static IEnumerable<PyString> FormatContextOldLines(IReadOnlyList<DiffOpcode> group, IReadOnlyList<PyString> a)
        {
            foreach (var opcode in group)
            {
                var prefix = opcode.Tag switch
                {
                    DiffTag.Equal => "  ",
                    DiffTag.Delete => "- ",
                    DiffTag.Replace => "! ",
                    _ => null,
                };

                if (prefix is null)
                {
                    continue;
                }

                for (var i = opcode.I1; i < opcode.I2; i++)
                {
                    yield return Prefix(prefix, a[i]);
                }
            }
        }

        private static IEnumerable<PyString> FormatContextNewLines(IReadOnlyList<DiffOpcode> group, IReadOnlyList<PyString> b)
        {
            foreach (var opcode in group)
            {
                var prefix = opcode.Tag switch
                {
                    DiffTag.Equal => "  ",
                    DiffTag.Insert => "+ ",
                    DiffTag.Replace => "! ",
                    _ => null,
                };

                if (prefix is null)
                {
                    continue;
                }

                for (var j = opcode.J1; j < opcode.J2; j++)
                {
                    yield return Prefix(prefix, b[j]);
                }
            }
        }

        private static PyString Prefix(string prefix, PyString line)
            => PyString.FromString(prefix + line.AsString());

        private static string FileHeader(string file, string date)
            => string.IsNullOrEmpty(date) ? file : file + "\t" + date;

        private static string FormatUnifiedRange(int start, int length)
        {
            var beginning = start + 1;
            if (length == 0)
            {
                beginning--;
            }

            return length == 1 ? beginning.ToString() : beginning + "," + length;
        }

        private static string FormatContextRange(int start, int length)
        {
            if (length == 0)
            {
                return (start + 1) + ",0";
            }

            var beginning = start + 1;
            var ending = start + length;
            return beginning == ending ? beginning.ToString() : beginning + "," + ending;
        }

        private sealed record DiffOptions(
            string FromFile,
            string ToFile,
            string FromFileDate,
            string ToFileDate,
            int ContextLines,
            string LineTerminator);

        private readonly record struct DiffRange(int OldStart, int OldEnd, int NewStart, int NewEnd);
    }

    internal sealed class DifflibSequenceMatcherObject
    {
        private IReadOnlyList<object> _a;
        private IReadOnlyList<object> _b;
        private List<MatchingBlock>? _matchingBlocks;
        private List<DiffOpcode>? _opcodes;

        public DifflibSequenceMatcherObject(IReadOnlyList<object> a, IReadOnlyList<object> b)
        {
            _a = a;
            _b = b;
        }

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "set_seqs" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 2)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.set_seqs(a, b) expects two arguments.", span);
                    }

                    SetSequences(DifflibModule.MaterializeSequence(arguments[0], span), DifflibModule.MaterializeSequence(arguments[1], span));
                    return PyNone.Instance;
                }, "SequenceMatcher.set_seqs", ["a", "b"]),
                "set_seq1" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.set_seq1(a) expects one argument.", span);
                    }

                    SetSequences(DifflibModule.MaterializeSequence(arguments[0], span), _b);
                    return PyNone.Instance;
                }, "SequenceMatcher.set_seq1", ["a"]),
                "set_seq2" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.set_seq2(b) expects one argument.", span);
                    }

                    SetSequences(_a, DifflibModule.MaterializeSequence(arguments[0], span));
                    return PyNone.Instance;
                }, "SequenceMatcher.set_seq2", ["b"]),
                "ratio" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.ratio() expects no arguments.", span);
                    }

                    return Ratio();
                }),
                "quick_ratio" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.quick_ratio() expects no arguments.", span);
                    }

                    return Ratio();
                }),
                "real_quick_ratio" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.real_quick_ratio() expects no arguments.", span);
                    }

                    var total = _a.Count + _b.Count;
                    return total == 0 ? 1.0 : 2.0 * Math.Min(_a.Count, _b.Count) / total;
                }),
                "get_matching_blocks" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.get_matching_blocks() expects no arguments.", span);
                    }

                    return new PyList(GetMatchingBlocks().Select(block => (object)new PyTuple([new BigInteger(block.A), new BigInteger(block.B), new BigInteger(block.Size)])), context.MemoryGovernor, span);
                }),
                "get_opcodes" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.get_opcodes() expects no arguments.", span);
                    }

                    return new PyList(BuildOpcodes().Select(opcode => (object)new PyTuple([
                        PyString.FromString(TagName(opcode.Tag)),
                        new BigInteger(opcode.I1),
                        new BigInteger(opcode.I2),
                        new BigInteger(opcode.J1),
                        new BigInteger(opcode.J2)])), context.MemoryGovernor, span);
                }),
                _ => null!,
            };

            return value is not null;
        }

        public double Ratio()
        {
            var matches = GetMatchingBlocks().Sum(block => block.Size);
            var total = _a.Count + _b.Count;
            return total == 0 ? 1.0 : 2.0 * matches / total;
        }

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

                if (block.Size != 0)
                {
                    opcodes.Add(new DiffOpcode(DiffTag.Equal, block.A, block.A + block.Size, block.B, block.B + block.Size));
                }

                i = block.A + block.Size;
                j = block.B + block.Size;
            }

            _opcodes = opcodes;
            return opcodes;
        }

        private void SetSequences(IReadOnlyList<object> a, IReadOnlyList<object> b)
        {
            _a = a;
            _b = b;
            _matchingBlocks = null;
            _opcodes = null;
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

            blocks.Sort((left, right) =>
            {
                var byA = left.A.CompareTo(right.A);
                return byA != 0 ? byA : left.B.CompareTo(right.B);
            });

            var collapsed = new List<MatchingBlock>();
            foreach (var block in blocks)
            {
                if (collapsed.Count != 0 &&
                    collapsed[^1].A + collapsed[^1].Size == block.A &&
                    collapsed[^1].B + collapsed[^1].Size == block.B)
                {
                    collapsed[^1] = collapsed[^1] with { Size = collapsed[^1].Size + block.Size };
                }
                else
                {
                    collapsed.Add(block);
                }
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
            var previous = new int[range.BHi - range.BLo];
            var current = new int[previous.Length];

            for (var i = range.ALo; i < range.AHi; i++)
            {
                Array.Clear(current);
                for (var j = range.BLo; j < range.BHi; j++)
                {
                    if (!AreEqual(_a[i], _b[j]))
                    {
                        continue;
                    }

                    var length = j == range.BLo ? 1 : previous[j - range.BLo - 1] + 1;
                    current[j - range.BLo] = length;
                    var candidateA = i - length + 1;
                    var candidateB = j - length + 1;
                    if (length > bestSize ||
                        (length == bestSize && (candidateA < bestA || (candidateA == bestA && candidateB < bestB))))
                    {
                        bestA = candidateA;
                        bestB = candidateB;
                        bestSize = length;
                    }
                }

                (previous, current) = (current, previous);
            }

            return new MatchingBlock(bestA, bestB, bestSize);
        }

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

    internal readonly record struct DiffOpcode(DiffTag Tag, int I1, int I2, int J1, int J2);

    internal enum DiffTag
    {
        Equal,
        Delete,
        Insert,
        Replace,
    }
}
