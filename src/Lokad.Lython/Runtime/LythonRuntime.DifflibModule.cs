using System.Net;
using System.Numerics;
using System.Text;
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

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "IS_LINE_JUNK" => new BuiltinCallable(LythonKnownCallableSignatures.DifflibIsLineJunk, IsLineJunk),
                "IS_CHARACTER_JUNK" => new BuiltinCallable(LythonKnownCallableSignatures.DifflibIsCharacterJunk, IsCharacterJunk),
                "unified_diff" => new BuiltinCallable(LythonKnownCallableSignatures.DifflibUnifiedDiff, UnifiedDiff),
                "context_diff" => new BuiltinCallable(LythonKnownCallableSignatures.DifflibContextDiff, ContextDiff),
                "ndiff" => new BuiltinCallable(LythonKnownCallableSignatures.DifflibNdiff, Ndiff),
                "restore" => new BuiltinCallable(LythonKnownCallableSignatures.DifflibRestore, Restore),
                "get_close_matches" => new BuiltinCallable(LythonKnownCallableSignatures.DifflibGetCloseMatches, GetCloseMatches),
                "diff_bytes" => new BuiltinCallable(LythonKnownCallableSignatures.DifflibDiffBytes, DiffBytes),
                "Differ" => new BuiltinCallable(LythonKnownCallableSignatures.DifflibDiffer, Differ),
                "HtmlDiff" => new BuiltinCallable(LythonKnownCallableSignatures.DifflibHtmlDiff, HtmlDiff),
                "SequenceMatcher" => new BuiltinCallable(LythonKnownCallableSignatures.DifflibSequenceMatcher, SequenceMatcher),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object IsLineJunk(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var line))
            {
                throw new LythonRuntimeException("TypeError", "difflib.IS_LINE_JUNK(line) expects one string argument.", span);
            }

            var text = line.AsString();
            var index = 0;
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            if (index == text.Length)
            {
                return true;
            }

            if (text[index] != '#')
            {
                return false;
            }

            index++;
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            return index == text.Length;
        }

        private static object IsCharacterJunk(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var ch))
            {
                throw new LythonRuntimeException("TypeError", "difflib.IS_CHARACTER_JUNK(ch) expects one string argument.", span);
            }

            var text = ch.AsString();
            return text is " " or "\t";
        }

        private static object UnifiedDiff(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var options = ParseDiffArguments(arguments, "difflib.unified_diff", span);
            var a = RequireStringSequence(arguments[0], "difflib.unified_diff(a, b)", span);
            var b = RequireStringSequence(arguments[1], "difflib.unified_diff(a, b)", span);
            return new PyList(BuildUnifiedDiff(a, b, options, context, span), context.MemoryGovernor, span);
        }

        private static object ContextDiff(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var options = ParseDiffArguments(arguments, "difflib.context_diff", span);
            var a = RequireStringSequence(arguments[0], "difflib.context_diff(a, b)", span);
            var b = RequireStringSequence(arguments[1], "difflib.context_diff(a, b)", span);
            return new PyList(BuildContextDiff(a, b, options, context, span), context.MemoryGovernor, span);
        }

        private static object Ndiff(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length is < 2 or > 4)
            {
                throw new LythonRuntimeException("TypeError", "difflib.ndiff(a, b[, linejunk][, charjunk]) expects two to four arguments.", span);
            }

            var a = RequireStringSequence(arguments[0], "difflib.ndiff(a, b)", span);
            var b = RequireStringSequence(arguments[1], "difflib.ndiff(a, b)", span);
            var linejunk = ParseOptionalPredicate(arguments, 2, "difflib.ndiff(..., linejunk=...)", span);
            var charjunk = arguments.Length >= 4
                ? ParseOptionalPredicate(arguments, 3, "difflib.ndiff(..., charjunk=...)", span)
                : DefaultCharacterJunkCallable();
            var differ = new DifflibDifferObject(linejunk, charjunk);
            return new PyList(differ.CompareLines(a, b, span, context), context.MemoryGovernor, span);
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
            var accepted = which == 1 ? "- " : "+ ";
            foreach (var line in lines)
            {
                var text = line.AsString();
                if (text.Length >= 2 &&
                    (text.StartsWith("  ", StringComparison.Ordinal) || text.StartsWith(accepted, StringComparison.Ordinal)))
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
            if (limit <= 0)
            {
                throw new LythonRuntimeException("ValueError", "difflib.get_close_matches(..., n=...) expects n to be positive.", span);
            }

            if (cutoff < 0.0 || cutoff > 1.0)
            {
                throw new LythonRuntimeException("ValueError", "difflib.get_close_matches(..., cutoff=...) expects cutoff between 0 and 1.", span);
            }

            var scored = new List<(double Score, PyString Value)>();
            var matcher = new DifflibSequenceMatcherObject(null, PyString.Empty, word, autojunk: true, span, context);
            foreach (var candidate in possibilities)
            {
                matcher.SetSeq1(candidate, span);
                if (matcher.RealQuickRatio() >= cutoff &&
                    matcher.QuickRatio() >= cutoff &&
                    matcher.Ratio() >= cutoff)
                {
                    scored.Add((matcher.Ratio(), candidate));
                }
            }

            scored.Sort((left, right) =>
            {
                var byScore = right.Score.CompareTo(left.Score);
                return byScore != 0 ? byScore : PyString.CompareOrdinal(right.Value, left.Value);
            });

            return new PyList(scored.Take(limit).Select(item => (object)item.Value), context.MemoryGovernor, span);
        }

        private static object DiffBytes(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length is < 3 or > 9 || arguments[0] is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "difflib.diff_bytes(dfunc, a, b, ...) expects a callable diff function and byte line iterables.", span);
            }

            var a = RequireBytesSequence(arguments[1], "difflib.diff_bytes(..., a=...)", span);
            var b = RequireBytesSequence(arguments[2], "difflib.diff_bytes(..., b=...)", span);
            var fromfile = ParseOptionalBytes(arguments, 3, [], "difflib.diff_bytes(..., fromfile=...)", span);
            var tofile = ParseOptionalBytes(arguments, 4, [], "difflib.diff_bytes(..., tofile=...)", span);
            var fromfiledate = ParseOptionalBytes(arguments, 5, [], "difflib.diff_bytes(..., fromfiledate=...)", span);
            var tofiledate = ParseOptionalBytes(arguments, 6, [], "difflib.diff_bytes(..., tofiledate=...)", span);
            var n = arguments.Length >= 8 && arguments[7] is not PyNone ? RequireInt32(arguments[7], "difflib.diff_bytes(..., n=...) expects n to be an integer.", span) : 3;
            var lineterm = ParseOptionalBytes(arguments, 8, [(byte)'\n'], "difflib.diff_bytes(..., lineterm=...)", span);

            var result = callable.Invoke(
                [
                    CallArgumentValue.Positional(new PyList(a.Select(line => (object)BytesToDiffText(line)), context.MemoryGovernor, span)),
                    CallArgumentValue.Positional(new PyList(b.Select(line => (object)BytesToDiffText(line)), context.MemoryGovernor, span)),
                    CallArgumentValue.Positional(BytesToDiffText(fromfile)),
                    CallArgumentValue.Positional(BytesToDiffText(tofile)),
                    CallArgumentValue.Positional(BytesToDiffText(fromfiledate)),
                    CallArgumentValue.Positional(BytesToDiffText(tofiledate)),
                    CallArgumentValue.Positional(new BigInteger(n)),
                    CallArgumentValue.Positional(BytesToDiffText(lineterm)),
                ],
                span,
                context);

            var bytes = new List<object>();
            foreach (var line in ToSequence(result, span))
            {
                if (!PyStringOps.TryAsString(line, out var text))
                {
                    throw new LythonRuntimeException("TypeError", "difflib.diff_bytes(...) diff function must return strings.", span);
                }

                bytes.Add(new PyBytes(DiffTextToBytes(text.AsString()), context.MemoryGovernor, span));
            }

            return new PyList(bytes, context.MemoryGovernor, span);
        }

        private static object Differ(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length > 2)
            {
                throw new LythonRuntimeException("TypeError", "difflib.Differ([linejunk][, charjunk]) expects zero to two arguments.", span);
            }

            var linejunk = ParseOptionalPredicate(arguments, 0, "difflib.Differ(..., linejunk=...)", span);
            var charjunk = ParseOptionalPredicate(arguments, 1, "difflib.Differ(..., charjunk=...)", span);
            return new DifflibDifferObject(linejunk, charjunk);
        }

        private static object HtmlDiff(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length > 4)
            {
                throw new LythonRuntimeException("TypeError", "difflib.HtmlDiff([tabsize][, wrapcolumn][, linejunk][, charjunk]) expects zero to four arguments.", span);
            }

            var tabsize = arguments.Length >= 1 && arguments[0] is not PyNone ? RequireInt32(arguments[0], "difflib.HtmlDiff(..., tabsize=...) expects an integer.", span) : 8;
            var wrapcolumn = arguments.Length >= 2 && arguments[1] is not PyNone ? RequireInt32(arguments[1], "difflib.HtmlDiff(..., wrapcolumn=...) expects an integer or None.", span) : (int?)null;
            var linejunk = ParseOptionalPredicate(arguments, 2, "difflib.HtmlDiff(..., linejunk=...)", span);
            var charjunk = arguments.Length >= 4
                ? ParseOptionalPredicate(arguments, 3, "difflib.HtmlDiff(..., charjunk=...)", span)
                : DefaultCharacterJunkCallable();
            return new DifflibHtmlDiffObject(tabsize, wrapcolumn, linejunk, charjunk);
        }

        private static object SequenceMatcher(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length > 4)
            {
                throw new LythonRuntimeException("TypeError", "difflib.SequenceMatcher([isjunk][, a][, b][, autojunk]) expects zero to four arguments.", span);
            }

            var isjunk = ParseOptionalPredicate(arguments, 0, "difflib.SequenceMatcher(..., isjunk=...)", span);
            var aOriginal = arguments.Length >= 2 ? arguments[1] : PyString.Empty;
            var bOriginal = arguments.Length >= 3 ? arguments[2] : PyString.Empty;
            var autojunk = arguments.Length < 4 || arguments[3] is PyNone || IsTruthy(arguments[3]);
            return new DifflibSequenceMatcherObject(isjunk, aOriginal, bOriginal, autojunk, span, context);
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

        private static object? ParseOptionalPredicate(object[] arguments, int index, string owner, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return null;
            }

            if (arguments[index] is not ICallable)
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects a callable or None.", span);
            }

            return arguments[index];
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

        private static byte[] ParseOptionalBytes(object[] arguments, int index, byte[] defaultValue, string owner, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return defaultValue;
            }

            if (arguments[index] is not PyBytes bytes)
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects bytes or None.", span);
            }

            return bytes.ToArray();
        }

        internal static IReadOnlyList<PyString> RequireStringSequence(object value, string owner, LythonSourceSpan span)
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

        private static IReadOnlyList<byte[]> RequireBytesSequence(object value, string owner, LythonSourceSpan span)
        {
            try
            {
                return ToSequence(value, span)
                    .Select(item => item is PyBytes bytes
                        ? bytes.ToArray()
                        : throw new LythonRuntimeException("TypeError", $"{owner} expects an iterable of bytes.", span))
                    .ToArray();
            }
            catch (LythonRuntimeException ex) when (ex.ExceptionType == "TypeError" && ex.Message == "Object is not iterable.")
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects an iterable of bytes.", span);
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

        internal static int RequireInt32(object value, string message, LythonSourceSpan span)
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

        private static BuiltinCallable DefaultCharacterJunkCallable()
            => new(LythonKnownCallableSignatures.DifflibIsCharacterJunk, IsCharacterJunk);

        internal static bool CallJunkPredicate(object? predicate, object argument, LythonSourceSpan span, ExecutionContext context)
        {
            if (predicate is null)
            {
                return false;
            }

            if (predicate is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "difflib junk predicate must be callable or None.", span);
            }

            return IsTruthy(callable.Invoke([CallArgumentValue.Positional(argument)], span, context));
        }

        private static IEnumerable<object> BuildUnifiedDiff(IReadOnlyList<PyString> a, IReadOnlyList<PyString> b, DiffOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            var matcher = new DifflibSequenceMatcherObject(null, new PyList(a.Cast<object>()), new PyList(b.Cast<object>()), autojunk: true, span, context);
            var groups = matcher.BuildGroupedOpcodes(options.ContextLines);
            var started = false;
            foreach (var group in groups)
            {
                if (!started)
                {
                    started = true;
                    yield return PyString.FromString("--- " + FileHeader(options.FromFile, options.FromFileDate) + options.LineTerminator);
                    yield return PyString.FromString("+++ " + FileHeader(options.ToFile, options.ToFileDate) + options.LineTerminator);
                }

                var first = group[0];
                var last = group[^1];
                yield return PyString.FromString("@@ -" + FormatUnifiedRange(first.I1, last.I2) + " +" + FormatUnifiedRange(first.J1, last.J2) + " @@" + options.LineTerminator);
                foreach (var opcode in group)
                {
                    foreach (var line in FormatUnifiedOpcode(opcode, a, b))
                    {
                        yield return line;
                    }
                }
            }
        }

        private static IEnumerable<object> BuildContextDiff(IReadOnlyList<PyString> a, IReadOnlyList<PyString> b, DiffOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            var matcher = new DifflibSequenceMatcherObject(null, new PyList(a.Cast<object>()), new PyList(b.Cast<object>()), autojunk: true, span, context);
            var groups = matcher.BuildGroupedOpcodes(options.ContextLines);
            var started = false;
            foreach (var group in groups)
            {
                if (!started)
                {
                    started = true;
                    yield return PyString.FromString("*** " + FileHeader(options.FromFile, options.FromFileDate) + options.LineTerminator);
                    yield return PyString.FromString("--- " + FileHeader(options.ToFile, options.ToFileDate) + options.LineTerminator);
                }

                var first = group[0];
                var last = group[^1];
                yield return PyString.FromString("***************" + options.LineTerminator);
                yield return PyString.FromString("*** " + FormatContextRange(first.I1, last.I2) + " ****" + options.LineTerminator);
                if (group.Any(static opcode => opcode.Tag is DiffTag.Replace or DiffTag.Delete))
                {
                    foreach (var line in FormatContextOldLines(group, a))
                    {
                        yield return line;
                    }
                }

                yield return PyString.FromString("--- " + FormatContextRange(first.J1, last.J2) + " ----" + options.LineTerminator);
                if (group.Any(static opcode => opcode.Tag is DiffTag.Replace or DiffTag.Insert))
                {
                    foreach (var line in FormatContextNewLines(group, b))
                    {
                        yield return line;
                    }
                }
            }
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

        internal static PyString Prefix(string prefix, PyString line)
            => PyString.FromString(prefix + line.AsString());

        private static string FileHeader(string file, string date)
            => string.IsNullOrEmpty(date) ? file : file + "\t" + date;

        private static string FormatUnifiedRange(int start, int stop)
        {
            var beginning = start + 1;
            var length = stop - start;
            if (length == 1)
            {
                return beginning.ToString();
            }

            if (length == 0)
            {
                beginning--;
            }

            return beginning + "," + length;
        }

        private static string FormatContextRange(int start, int stop)
        {
            var beginning = start + 1;
            var length = stop - start;
            if (length == 0)
            {
                beginning--;
            }

            return length <= 1 ? beginning.ToString() : beginning + "," + (beginning + length - 1);
        }

        private static PyString BytesToDiffText(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length);
            foreach (var value in bytes)
            {
                builder.Append((char)value);
            }

            return PyString.FromString(builder.ToString());
        }

        private static byte[] DiffTextToBytes(string text)
        {
            var bytes = new byte[text.Length];
            for (var i = 0; i < text.Length; i++)
            {
                bytes[i] = (byte)(text[i] & 0xff);
            }

            return bytes;
        }

        private sealed record DiffOptions(
            string FromFile,
            string ToFile,
            string FromFileDate,
            string ToFileDate,
            int ContextLines,
            string LineTerminator);
    }

    internal sealed class DifflibDifferObject
    {
        private readonly object? _linejunk;
        private readonly object? _charjunk;

        public DifflibDifferObject(object? linejunk, object? charjunk)
        {
            _linejunk = linejunk;
            _charjunk = charjunk;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "linejunk" => _linejunk ?? PyNone.Instance,
                "charjunk" => _charjunk ?? PyNone.Instance,
                "compare" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 2)
                    {
                        throw new LythonRuntimeException("TypeError", "Differ.compare(a, b) expects two arguments.", span);
                    }

                    var a = DifflibModule.RequireStringSequence(arguments[0], "Differ.compare(a, b)", span);
                    var b = DifflibModule.RequireStringSequence(arguments[1], "Differ.compare(a, b)", span);
                    return new PyList(CompareLines(a, b, span, context), context.MemoryGovernor, span);
                }, "Differ.compare", ["a", "b"]),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        internal IEnumerable<object> CompareLines(IReadOnlyList<PyString> a, IReadOnlyList<PyString> b, LythonSourceSpan span, ExecutionContext context)
        {
            var matcher = new DifflibSequenceMatcherObject(_linejunk, new PyList(a.Cast<object>()), new PyList(b.Cast<object>()), autojunk: true, span, context);
            foreach (var opcode in matcher.BuildOpcodes())
            {
                foreach (var line in opcode.Tag switch
                {
                    DiffTag.Replace => FancyReplace(a, opcode.I1, opcode.I2, b, opcode.J1, opcode.J2, span, context),
                    DiffTag.Delete => Dump("-", a, opcode.I1, opcode.I2),
                    DiffTag.Insert => Dump("+", b, opcode.J1, opcode.J2),
                    DiffTag.Equal => Dump(" ", a, opcode.I1, opcode.I2),
                    _ => throw new InvalidOperationException($"Unknown diff opcode: {opcode.Tag}")
                })
                {
                    yield return line;
                }
            }
        }

        private static IEnumerable<object> Dump(string tag, IReadOnlyList<PyString> lines, int lo, int hi)
        {
            for (var i = lo; i < hi; i++)
            {
                yield return DifflibModule.Prefix(tag + " ", lines[i]);
            }
        }

        private IEnumerable<object> PlainReplace(IReadOnlyList<PyString> a, int alo, int ahi, IReadOnlyList<PyString> b, int blo, int bhi)
        {
            if (bhi - blo < ahi - alo)
            {
                foreach (var line in Dump("+", b, blo, bhi))
                {
                    yield return line;
                }

                foreach (var line in Dump("-", a, alo, ahi))
                {
                    yield return line;
                }
            }
            else
            {
                foreach (var line in Dump("-", a, alo, ahi))
                {
                    yield return line;
                }

                foreach (var line in Dump("+", b, blo, bhi))
                {
                    yield return line;
                }
            }
        }

        private IEnumerable<object> FancyReplace(IReadOnlyList<PyString> a, int alo, int ahi, IReadOnlyList<PyString> b, int blo, int bhi, LythonSourceSpan span, ExecutionContext context)
        {
            var bestRatio = 0.74;
            const double cutoff = 0.75;
            var cruncher = new DifflibSequenceMatcherObject(_charjunk, PyString.Empty, PyString.Empty, autojunk: true, span, context);
            int? equalI = null;
            int? equalJ = null;
            var bestI = alo;
            var bestJ = blo;

            for (var j = blo; j < bhi; j++)
            {
                var bj = b[j];
                cruncher.SetSeq2(bj, span, context);
                for (var i = alo; i < ahi; i++)
                {
                    var ai = a[i];
                    if (AreEqual(ai, bj))
                    {
                        equalI ??= i;
                        equalJ ??= j;
                        continue;
                    }

                    cruncher.SetSeq1(ai, span);
                    if (cruncher.RealQuickRatio() > bestRatio &&
                        cruncher.QuickRatio() > bestRatio &&
                        cruncher.Ratio() > bestRatio)
                    {
                        bestRatio = cruncher.Ratio();
                        bestI = i;
                        bestJ = j;
                    }
                }
            }

            if (bestRatio < cutoff)
            {
                if (equalI is null || equalJ is null)
                {
                    foreach (var line in PlainReplace(a, alo, ahi, b, blo, bhi))
                    {
                        yield return line;
                    }

                    yield break;
                }

                bestI = equalI.Value;
                bestJ = equalJ.Value;
            }
            else
            {
                equalI = null;
            }

            foreach (var line in FancyHelper(a, alo, bestI, b, blo, bestJ, span, context))
            {
                yield return line;
            }

            var aLine = a[bestI];
            var bLine = b[bestJ];
            if (equalI is null)
            {
                var aTags = new StringBuilder();
                var bTags = new StringBuilder();
                cruncher.SetSeqs(aLine, bLine, span, context);
                foreach (var opcode in cruncher.BuildOpcodes())
                {
                    var leftLength = opcode.I2 - opcode.I1;
                    var rightLength = opcode.J2 - opcode.J1;
                    switch (opcode.Tag)
                    {
                        case DiffTag.Replace:
                            aTags.Append('^', leftLength);
                            bTags.Append('^', rightLength);
                            break;
                        case DiffTag.Delete:
                            aTags.Append('-', leftLength);
                            break;
                        case DiffTag.Insert:
                            bTags.Append('+', rightLength);
                            break;
                        case DiffTag.Equal:
                            aTags.Append(' ', leftLength);
                            bTags.Append(' ', rightLength);
                            break;
                    }
                }

                foreach (var line in QFormat(aLine.AsString(), bLine.AsString(), aTags.ToString(), bTags.ToString()))
                {
                    yield return line;
                }
            }
            else
            {
                yield return DifflibModule.Prefix("  ", aLine);
            }

            foreach (var line in FancyHelper(a, bestI + 1, ahi, b, bestJ + 1, bhi, span, context))
            {
                yield return line;
            }
        }

        private IEnumerable<object> FancyHelper(IReadOnlyList<PyString> a, int alo, int ahi, IReadOnlyList<PyString> b, int blo, int bhi, LythonSourceSpan span, ExecutionContext context)
        {
            if (alo < ahi)
            {
                if (blo < bhi)
                {
                    return FancyReplace(a, alo, ahi, b, blo, bhi, span, context);
                }

                return Dump("-", a, alo, ahi);
            }

            return blo < bhi ? Dump("+", b, blo, bhi) : [];
        }

        private static IEnumerable<object> QFormat(string aLine, string bLine, string aTags, string bTags)
        {
            aTags = KeepOriginalWhitespace(aLine, aTags).TrimEnd();
            bTags = KeepOriginalWhitespace(bLine, bTags).TrimEnd();

            yield return PyString.FromString("- " + aLine);
            if (aTags.Length != 0)
            {
                yield return PyString.FromString("? " + aTags + "\n");
            }

            yield return PyString.FromString("+ " + bLine);
            if (bTags.Length != 0)
            {
                yield return PyString.FromString("? " + bTags + "\n");
            }
        }

        private static string KeepOriginalWhitespace(string source, string tags)
        {
            var builder = new StringBuilder(Math.Min(source.Length, tags.Length));
            var count = Math.Min(source.Length, tags.Length);
            for (var i = 0; i < count; i++)
            {
                builder.Append(tags[i] == ' ' && char.IsWhiteSpace(source[i]) ? source[i] : tags[i]);
            }

            return builder.ToString();
        }
    }

    internal sealed class DifflibHtmlDiffObject
    {
        private readonly int _tabsize;
        private readonly int? _wrapcolumn;
        private readonly object? _linejunk;
        private readonly object? _charjunk;

        public DifflibHtmlDiffObject(int tabsize, int? wrapcolumn, object? linejunk, object? charjunk)
        {
            _tabsize = tabsize;
            _wrapcolumn = wrapcolumn;
            _linejunk = linejunk;
            _charjunk = charjunk;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "make_table" => new BoundCallable(MakeTable, new LythonCallableSignature("HtmlDiff.make_table", ["fromlines", "tolines", "fromdesc", "todesc", "context", "numlines"], RequiredCount: 2)),
                "make_file" => new BoundCallable(MakeFile, new LythonCallableSignature("HtmlDiff.make_file", ["fromlines", "tolines", "fromdesc", "todesc", "context", "numlines", "charset"], RequiredCount: 2)),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private object MakeTable(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var options = ParseHtmlArguments(arguments, includeCharset: false, span);
            var fromLines = DifflibModule.RequireStringSequence(arguments[0], "HtmlDiff.make_table(fromlines, tolines)", span);
            var toLines = DifflibModule.RequireStringSequence(arguments[1], "HtmlDiff.make_table(fromlines, tolines)", span);
            return PyString.FromString(BuildTable(fromLines, toLines, options, context, span));
        }

        private object MakeFile(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var options = ParseHtmlArguments(arguments, includeCharset: true, span);
            var fromLines = DifflibModule.RequireStringSequence(arguments[0], "HtmlDiff.make_file(fromlines, tolines)", span);
            var toLines = DifflibModule.RequireStringSequence(arguments[1], "HtmlDiff.make_file(fromlines, tolines)", span);
            var table = BuildTable(fromLines, toLines, options, context, span);
            var html =
                "<!DOCTYPE html>\n" +
                "<html><head><meta charset=\"" + Html(options.Charset) + "\">\n" +
                "<style>.diff{font-family:Consolas,monospace;border-collapse:collapse}.diff td,.diff th{padding:2px 6px;border:1px solid #ddd}.diff_add{background:#e6ffed}.diff_sub{background:#ffeef0}.diff_chg{background:#fff5b1}.diff_header{background:#f6f8fa}</style>\n" +
                "</head><body>\n" +
                table +
                "\n</body></html>\n";
            return PyString.FromString(html);
        }

        private HtmlOptions ParseHtmlArguments(object[] arguments, bool includeCharset, LythonSourceSpan span)
        {
            var maximum = includeCharset ? 7 : 6;
            if (arguments.Length is < 2 || arguments.Length > maximum)
            {
                var owner = includeCharset ? "HtmlDiff.make_file" : "HtmlDiff.make_table";
                throw new LythonRuntimeException("TypeError", $"{owner}(fromlines, tolines[, fromdesc][, todesc][, context][, numlines]) received an unsupported argument count.", span);
            }

            return new HtmlOptions(
                ParseHtmlString(arguments, 2, string.Empty, "fromdesc", span),
                ParseHtmlString(arguments, 3, string.Empty, "todesc", span),
                arguments.Length >= 5 && arguments[4] is not PyNone && IsTruthy(arguments[4]),
                arguments.Length >= 6 && arguments[5] is not PyNone ? DifflibModule.RequireInt32(arguments[5], "HtmlDiff numlines expects an integer.", span) : 5,
                includeCharset ? ParseHtmlString(arguments, 6, "utf-8", "charset", span) : "utf-8");
        }

        private static string ParseHtmlString(object[] arguments, int index, string defaultValue, string name, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return defaultValue;
            }

            if (!PyStringOps.TryAsString(arguments[index], out var text))
            {
                throw new LythonRuntimeException("TypeError", $"HtmlDiff {name} expects a string.", span);
            }

            return text.AsString();
        }

        private string BuildTable(IReadOnlyList<PyString> fromLines, IReadOnlyList<PyString> toLines, HtmlOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            var a = fromLines.Select(line => PyString.FromString(PrepareHtmlLine(line.AsString()))).ToArray();
            var b = toLines.Select(line => PyString.FromString(PrepareHtmlLine(line.AsString()))).ToArray();
            var matcher = new DifflibSequenceMatcherObject(_linejunk, new PyList(a.Cast<object>()), new PyList(b.Cast<object>()), autojunk: true, span, context);
            var groups = options.Context
                ? matcher.BuildGroupedOpcodes(options.NumLines).ToArray()
                : [matcher.BuildOpcodes()];

            var builder = new StringBuilder();
            builder.Append("<table class=\"diff\" summary=\"Differences\">\n");
            if (options.FromDescription.Length != 0 || options.ToDescription.Length != 0)
            {
                builder.Append("<thead><tr><th class=\"diff_next\"></th><th colspan=\"2\" class=\"diff_header\">");
                builder.Append(Html(options.FromDescription));
                builder.Append("</th><th class=\"diff_next\"></th><th colspan=\"2\" class=\"diff_header\">");
                builder.Append(Html(options.ToDescription));
                builder.Append("</th></tr></thead>\n");
            }

            builder.Append("<tbody>\n");
            var firstGroup = true;
            foreach (var group in groups)
            {
                if (!firstGroup)
                {
                    builder.Append("<tr><td colspan=\"6\" class=\"diff_next\"></td></tr>\n");
                }

                firstGroup = false;
                foreach (var opcode in group)
                {
                    AppendHtmlOpcode(builder, opcode, a, b);
                }
            }

            builder.Append("</tbody>\n</table>");
            return builder.ToString();
        }

        private string PrepareHtmlLine(string line)
        {
            var withoutNewline = line.TrimEnd('\r', '\n');
            return ExpandTabs(withoutNewline, _tabsize);
        }

        private static void AppendHtmlOpcode(StringBuilder builder, DiffOpcode opcode, IReadOnlyList<PyString> a, IReadOnlyList<PyString> b)
        {
            switch (opcode.Tag)
            {
                case DiffTag.Equal:
                    for (var offset = 0; offset < opcode.I2 - opcode.I1; offset++)
                    {
                        AppendHtmlRow(builder, opcode.I1 + offset + 1, a[opcode.I1 + offset].AsString(), string.Empty, opcode.J1 + offset + 1, b[opcode.J1 + offset].AsString(), string.Empty);
                    }

                    break;
                case DiffTag.Delete:
                    for (var i = opcode.I1; i < opcode.I2; i++)
                    {
                        AppendHtmlRow(builder, i + 1, a[i].AsString(), "diff_sub", null, string.Empty, string.Empty);
                    }

                    break;
                case DiffTag.Insert:
                    for (var j = opcode.J1; j < opcode.J2; j++)
                    {
                        AppendHtmlRow(builder, null, string.Empty, string.Empty, j + 1, b[j].AsString(), "diff_add");
                    }

                    break;
                case DiffTag.Replace:
                    var leftCount = opcode.I2 - opcode.I1;
                    var rightCount = opcode.J2 - opcode.J1;
                    var paired = Math.Max(leftCount, rightCount);
                    for (var offset = 0; offset < paired; offset++)
                    {
                        var hasLeft = offset < leftCount;
                        var hasRight = offset < rightCount;
                        AppendHtmlRow(
                            builder,
                            hasLeft ? opcode.I1 + offset + 1 : null,
                            hasLeft ? a[opcode.I1 + offset].AsString() : string.Empty,
                            hasLeft ? "diff_chg" : string.Empty,
                            hasRight ? opcode.J1 + offset + 1 : null,
                            hasRight ? b[opcode.J1 + offset].AsString() : string.Empty,
                            hasRight ? "diff_chg" : string.Empty);
                    }

                    break;
            }
        }

        private static void AppendHtmlRow(StringBuilder builder, int? leftNumber, string leftText, string leftClass, int? rightNumber, string rightText, string rightClass)
        {
            builder.Append("<tr><td class=\"diff_next\"></td><td class=\"diff_header\">");
            builder.Append(leftNumber?.ToString() ?? string.Empty);
            builder.Append("</td><td nowrap=\"nowrap\"");
            AppendClass(builder, leftClass);
            builder.Append(">");
            builder.Append(HtmlText(leftText));
            builder.Append("</td><td class=\"diff_next\"></td><td class=\"diff_header\">");
            builder.Append(rightNumber?.ToString() ?? string.Empty);
            builder.Append("</td><td nowrap=\"nowrap\"");
            AppendClass(builder, rightClass);
            builder.Append(">");
            builder.Append(HtmlText(rightText));
            builder.Append("</td></tr>\n");
        }

        private static void AppendClass(StringBuilder builder, string className)
        {
            if (className.Length != 0)
            {
                builder.Append(" class=\"");
                builder.Append(className);
                builder.Append("\"");
            }
        }

        private static string HtmlText(string text)
            => Html(text).Replace(" ", "&nbsp;", StringComparison.Ordinal).Replace("\t", "&nbsp;", StringComparison.Ordinal);

        private static string Html(string text)
            => WebUtility.HtmlEncode(text);

        private static string ExpandTabs(string text, int tabsize)
        {
            if (tabsize <= 0 || text.IndexOf('\t') < 0)
            {
                return text;
            }

            var builder = new StringBuilder();
            var column = 0;
            foreach (var ch in text)
            {
                if (ch == '\t')
                {
                    var spaces = tabsize - column % tabsize;
                    builder.Append(' ', spaces);
                    column += spaces;
                }
                else
                {
                    builder.Append(ch);
                    column++;
                }
            }

            return builder.ToString();
        }

        private readonly record struct HtmlOptions(
            string FromDescription,
            string ToDescription,
            bool Context,
            int NumLines,
            string Charset);
    }

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
                "set_seqs" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 2)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.set_seqs(a, b) expects two arguments.", span);
                    }

                    SetSeqs(arguments[0], arguments[1], span, context);
                    return PyNone.Instance;
                }, "SequenceMatcher.set_seqs", ["a", "b"]),
                "set_seq1" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.set_seq1(a) expects one argument.", span);
                    }

                    SetSeq1(arguments[0], span);
                    return PyNone.Instance;
                }, "SequenceMatcher.set_seq1", ["a"]),
                "set_seq2" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.set_seq2(b) expects one argument.", span);
                    }

                    SetSeq2(arguments[0], span, context);
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

                    return QuickRatio();
                }),
                "real_quick_ratio" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.real_quick_ratio() expects no arguments.", span);
                    }

                    return RealQuickRatio();
                }),
                "find_longest_match" => new BoundCallable((arguments, span, context) =>
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
                }, new LythonCallableSignature("SequenceMatcher.find_longest_match", ["alo", "ahi", "blo", "bhi"], RequiredCount: 0)),
                "get_matching_blocks" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.get_matching_blocks() expects no arguments.", span);
                    }

                    return new PyList(GetMatchingBlocks().Select(block => (object)new DifflibMatchObject(block.A, block.B, block.Size)), context.MemoryGovernor, span);
                }),
                "get_opcodes" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "SequenceMatcher.get_opcodes() expects no arguments.", span);
                    }

                    return new PyList(BuildOpcodes().Select(opcode => (object)ToPyTuple(opcode)), context.MemoryGovernor, span);
                }),
                "get_grouped_opcodes" => new BoundCallable((arguments, span, context) =>
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

    internal sealed class DifflibMatchObject : IPySequenceValue, IPyIndexableValue, IPyIterableValue, IPyRenderableValue, IPyHashableValue
    {
        public DifflibMatchObject(int a, int b, int size)
        {
            A = a;
            B = b;
            Size = size;
        }

        public int A { get; }

        public int B { get; }

        public int Size { get; }

        public int Count => 3;

        public int Length => 3;

        public object this[int index] => GetItem(index);

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "a" => new BigInteger(A),
                "b" => new BigInteger(B),
                "size" => new BigInteger(Size),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public object GetItem(int index)
            => index switch
            {
                0 => new BigInteger(A),
                1 => new BigInteger(B),
                2 => new BigInteger(Size),
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };

        public object CreateSlice(IEnumerable<object> items) => new PyTuple(items);

        public object GetIndex(int index) => GetItem(index);

        public object GetSlice(IEnumerable<int> indices)
            => new PyTuple(indices.Select(GetItem));

        public IEnumerator<object> GetEnumerator()
        {
            yield return new BigInteger(A);
            yield return new BigInteger(B);
            yield return new BigInteger(Size);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerable<object> Iterate() => this;

        public int GetPyHashCode()
        {
            var hash = new HashCode();
            hash.Add(A);
            hash.Add(B);
            hash.Add(Size);
            return hash.ToHashCode();
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"Match(a={A}, b={B}, size={Size})");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
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
