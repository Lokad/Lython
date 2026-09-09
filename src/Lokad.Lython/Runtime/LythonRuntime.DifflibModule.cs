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
                "IS_LINE_JUNK" => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibIsLineJunk, IsLineJunk),
                "IS_CHARACTER_JUNK" => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibIsCharacterJunk, IsCharacterJunk),
                "unified_diff" => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibUnifiedDiff, UnifiedDiff),
                "context_diff" => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibContextDiff, ContextDiff),
                "ndiff" => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibNdiff, Ndiff),
                "restore" => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibRestore, Restore),
                "get_close_matches" => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibGetCloseMatches, GetCloseMatches),
                "diff_bytes" => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibDiffBytes, DiffBytes),
                "Differ" => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibDiffer, Differ),
                "HtmlDiff" => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibHtmlDiff, HtmlDiff),
                "SequenceMatcher" => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibSequenceMatcher, SequenceMatcher),
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
            var a = RequireStringSequence(arguments[0], "difflib.unified_diff(a, b)", span, context);
            var b = RequireStringSequence(arguments[1], "difflib.unified_diff(a, b)", span, context);
            return new PyList(BuildUnifiedDiff(a, b, options, context, span), context.MemoryGovernor, span);
        }

        private static object ContextDiff(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var options = ParseDiffArguments(arguments, "difflib.context_diff", span);
            var a = RequireStringSequence(arguments[0], "difflib.context_diff(a, b)", span, context);
            var b = RequireStringSequence(arguments[1], "difflib.context_diff(a, b)", span, context);
            return new PyList(BuildContextDiff(a, b, options, context, span), context.MemoryGovernor, span);
        }

        private static object Ndiff(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length is < 2 or > 4)
            {
                throw new LythonRuntimeException("TypeError", "difflib.ndiff(a, b[, linejunk][, charjunk]) expects two to four arguments.", span);
            }

            var a = RequireStringSequence(arguments[0], "difflib.ndiff(a, b)", span, context);
            var b = RequireStringSequence(arguments[1], "difflib.ndiff(a, b)", span, context);
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

            var lines = RequireStringSequence(arguments[0], "difflib.restore(delta, which)", span, context);
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

            var possibilities = RequireStringSequence(arguments[1], "difflib.get_close_matches(word, possibilities)", span, context);
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
            var work = 0;
            foreach (var candidate in possibilities)
            {
                matcher.SetSeq1(candidate, span, context);
                if ((++work & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }

                if (matcher.RealQuickRatio() >= cutoff &&
                    matcher.QuickRatio(span, context) >= cutoff &&
                    matcher.Ratio(span, context) >= cutoff)
                {
                    scored.Add((matcher.Ratio(span, context), candidate));
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

            var a = RequireBytesSequence(arguments[1], "difflib.diff_bytes(..., a=...)", span, context);
            var b = RequireBytesSequence(arguments[2], "difflib.diff_bytes(..., b=...)", span, context);
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
            foreach (var line in ToSequence(result, span, context))
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

        private static ICallable? ParseOptionalPredicate(object[] arguments, int index, string owner, LythonSourceSpan span)
        {
            if (arguments.Length <= index || arguments[index] is PyNone)
            {
                return null;
            }

            if (arguments[index] is ICallable callable)
            {
                return callable;
            }

            throw new LythonRuntimeException("TypeError", $"{owner} expects a callable or None.", span);
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

        internal static IReadOnlyList<PyString> RequireStringSequence(object value, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            try
            {
                return ToSequence(value, span, context)
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

        private static IReadOnlyList<byte[]> RequireBytesSequence(object value, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            try
            {
                return ToSequence(value, span, context)
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

        /// <summary>Materializes a matcher sequence with growth, count, and work budgets enforced during iteration.</summary>
        /// <remarks>Ownership of the exact array transfers to the matcher, which commits
        /// <paramref name="charge"/> and releases it when sequences are replaced. The temporary
        /// reservation covers growth and the final copy, and is released before ownership
        /// transfers without any allocation in between.</remarks>
        public static object[] MaterializeGovernedSequence(object value, LythonSourceSpan span, ExecutionContext context, out long charge)
        {
            using var reservation = context.MemoryGovernor.ReserveTemporary(0, span);
            var items = new List<object>();
            var chargedCapacity = 0;
            try
            {
                foreach (var item in ToSequence(value, span, context))
                {
                    items.Add(item);
                    if (items.Capacity > chargedCapacity)
                    {
                        reservation.Grow(16L * (items.Capacity - chargedCapacity), span);
                        chargedCapacity = items.Capacity;
                    }

                    context.ObserveCollectionCount(items.Count, span);
                    if ((items.Count & 63) == 0)
                    {
                        context.CheckExecutionBudget(span);
                    }
                }
            }
            catch (LythonRuntimeException ex) when (ex.ExceptionType == "TypeError" && ex.Message == "Object is not iterable.")
            {
                throw new LythonRuntimeException("TypeError", "difflib.SequenceMatcher sequence arguments must be iterable.", span);
            }

            var result = items.ToArray();
            reservation.Grow(16L * result.Length, span);
            charge = 32L + (16L * result.Length);
            context.MemoryGovernor.Reserve(charge, span);
            context.MemoryGovernor.Commit(charge);
            return result;
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
            => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibIsCharacterJunk, IsCharacterJunk);

        internal static bool CallJunkPredicate(ICallable? predicate, object argument, LythonSourceSpan span, ExecutionContext context)
        {
            if (predicate is null)
            {
                return false;
            }

            return IsTruthy(CallableInvocation.InvokeUnary(predicate, argument, span, context));
        }

        private static IEnumerable<object> BuildUnifiedDiff(IReadOnlyList<PyString> a, IReadOnlyList<PyString> b, DiffOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            var matcher = new DifflibSequenceMatcherObject(null, new PyList(a.Cast<object>(), context.MemoryGovernor, span), new PyList(b.Cast<object>(), context.MemoryGovernor, span), autojunk: true, span, context);
            var groups = matcher.BuildGroupedOpcodes(options.ContextLines, span, context);
            var started = false;
            foreach (var group in groups)
            {
                if (!started)
                {
                    started = true;
                    yield return LythonRuntime.CreateString("--- " + FileHeader(options.FromFile, options.FromFileDate) + options.LineTerminator, context, span);
                    yield return LythonRuntime.CreateString("+++ " + FileHeader(options.ToFile, options.ToFileDate) + options.LineTerminator, context, span);
                }

                var first = group[0];
                var last = group[^1];
                yield return LythonRuntime.CreateString("@@ -" + FormatUnifiedRange(first.I1, last.I2) + " +" + FormatUnifiedRange(first.J1, last.J2) + " @@" + options.LineTerminator, context, span);
                foreach (var opcode in group)
                {
                    foreach (var line in FormatUnifiedOpcode(opcode, a, b, context.MemoryGovernor, span))
                    {
                        yield return line;
                    }
                }
            }
        }

        private static IEnumerable<object> BuildContextDiff(IReadOnlyList<PyString> a, IReadOnlyList<PyString> b, DiffOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            var matcher = new DifflibSequenceMatcherObject(null, new PyList(a.Cast<object>(), context.MemoryGovernor, span), new PyList(b.Cast<object>(), context.MemoryGovernor, span), autojunk: true, span, context);
            var groups = matcher.BuildGroupedOpcodes(options.ContextLines, span, context);
            var started = false;
            foreach (var group in groups)
            {
                if (!started)
                {
                    started = true;
                    yield return LythonRuntime.CreateString("*** " + FileHeader(options.FromFile, options.FromFileDate) + options.LineTerminator, context, span);
                    yield return LythonRuntime.CreateString("--- " + FileHeader(options.ToFile, options.ToFileDate) + options.LineTerminator, context, span);
                }

                var first = group[0];
                var last = group[^1];
                yield return LythonRuntime.CreateString("***************" + options.LineTerminator, context, span);
                yield return LythonRuntime.CreateString("*** " + FormatContextRange(first.I1, last.I2) + " ****" + options.LineTerminator, context, span);
                if (group.Any(static opcode => opcode.Tag is DiffTag.Replace or DiffTag.Delete))
                {
                    foreach (var line in FormatContextOldLines(group, a, context.MemoryGovernor, span))
                    {
                        yield return line;
                    }
                }

                yield return LythonRuntime.CreateString("--- " + FormatContextRange(first.J1, last.J2) + " ----" + options.LineTerminator, context, span);
                if (group.Any(static opcode => opcode.Tag is DiffTag.Replace or DiffTag.Insert))
                {
                    foreach (var line in FormatContextNewLines(group, b, context.MemoryGovernor, span))
                    {
                        yield return line;
                    }
                }
            }
        }

        private static IEnumerable<PyString> FormatUnifiedOpcode(DiffOpcode opcode, IReadOnlyList<PyString> a, IReadOnlyList<PyString> b, MemoryGovernor governor, LythonSourceSpan span)
        {
            switch (opcode.Tag)
            {
                case DiffTag.Equal:
                    for (var i = opcode.I1; i < opcode.I2; i++)
                    {
                        yield return Prefix(" ", a[i], governor, span);
                    }

                    break;
                case DiffTag.Delete:
                    for (var i = opcode.I1; i < opcode.I2; i++)
                    {
                        yield return Prefix("-", a[i], governor, span);
                    }

                    break;
                case DiffTag.Insert:
                    for (var j = opcode.J1; j < opcode.J2; j++)
                    {
                        yield return Prefix("+", b[j], governor, span);
                    }

                    break;
                case DiffTag.Replace:
                    for (var i = opcode.I1; i < opcode.I2; i++)
                    {
                        yield return Prefix("-", a[i], governor, span);
                    }

                    for (var j = opcode.J1; j < opcode.J2; j++)
                    {
                        yield return Prefix("+", b[j], governor, span);
                    }

                    break;
            }
        }

        private static IEnumerable<PyString> FormatContextOldLines(IReadOnlyList<DiffOpcode> group, IReadOnlyList<PyString> a, MemoryGovernor governor, LythonSourceSpan span)
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
                    yield return Prefix(prefix, a[i], governor, span);
                }
            }
        }

        private static IEnumerable<PyString> FormatContextNewLines(IReadOnlyList<DiffOpcode> group, IReadOnlyList<PyString> b, MemoryGovernor governor, LythonSourceSpan span)
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
                    yield return Prefix(prefix, b[j], governor, span);
                }
            }
        }

        internal static PyString Prefix(string prefix, PyString line, MemoryGovernor governor, LythonSourceSpan? span)
            => PyString.FromString(prefix + line.AsString(), governor, span);

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

}
