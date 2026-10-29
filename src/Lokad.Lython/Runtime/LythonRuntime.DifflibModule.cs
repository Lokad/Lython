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
        private readonly PyNamedTupleType _matchType;

        internal DifflibModule(ExecutionContext context) : base("difflib")
        { _matchType = MatchType(context, null); }

        private static PyNamedTupleType MatchType(ExecutionContext context, LythonSourceSpan? span)
            => context.State.DifflibMatchType ??= new PyNamedTupleType("Match", ["a", "b", "size"], null, context.MemoryGovernor, span)
                { ModuleName = ExceptionTypeValue.SharedModuleLabel("difflib") };

        internal static PyNamedTupleObject CreateMatch(int a, int b, int size, ExecutionContext context, LythonSourceSpan span)
            => MatchType(context, span).CreateFromValues([new BigInteger(a), new BigInteger(b), new BigInteger(size)], context.MemoryGovernor, span);

        internal static PyEnumerableIterator OutputIterator(IEnumerable<object> source, LythonSourceSpan span, ExecutionContext context)
        {
            PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
            return new PyEnumerableIterator(source, span, context);
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "IS_LINE_JUNK" => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibIsLineJunk, IsLineJunk),
                "IS_CHARACTER_JUNK" => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibIsCharacterJunk, IsCharacterJunk),
                "unified_diff" => BuiltinCallable.CreateWithPresence(LythonKnownCallableSignatures.DifflibUnifiedDiff, UnifiedDiff, static (args, span, context) => new(UnifiedDiff(args, span, context))),
                "context_diff" => BuiltinCallable.CreateWithPresence(LythonKnownCallableSignatures.DifflibContextDiff, ContextDiff, static (args, span, context) => new(ContextDiff(args, span, context))),
                "ndiff" => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibNdiff, Ndiff),
                "restore" => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibRestore, Restore),
                "get_close_matches" => BuiltinCallable.CreateWithPresence(LythonKnownCallableSignatures.DifflibGetCloseMatches, GetCloseMatches, static (args, span, context) => new(GetCloseMatches(args, span, context))),
                "diff_bytes" => BuiltinCallable.CreateWithPresence(LythonKnownCallableSignatures.DifflibDiffBytes, DiffBytes, static (args, span, context) => new(DiffBytes(args, span, context))),
                "Differ" => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibDiffer, Differ),
                "HtmlDiff" => BuiltinCallable.Create(LythonKnownCallableSignatures.DifflibHtmlDiff, HtmlDiff),
                "Match" => _matchType,
                "SequenceMatcher" => BuiltinCallable.CreateWithPresence(LythonKnownCallableSignatures.DifflibSequenceMatcher, SequenceMatcher, SequenceMatcherAsync),
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

        private static object UnifiedDiff(BoundCallArguments arguments, LythonSourceSpan span, ExecutionContext context)
            => OutputIterator(DiffLines(arguments, span, GetGlobalContext(context), true), span, context);

        private static object ContextDiff(BoundCallArguments arguments, LythonSourceSpan span, ExecutionContext context)
            => OutputIterator(DiffLines(arguments, span, GetGlobalContext(context), false), span, context);

        private static IEnumerable<object> DiffLines(BoundCallArguments arguments, LythonSourceSpan span, ExecutionContext context, bool unified)
        {
            var owner = unified ? "difflib.unified_diff" : "difflib.context_diff";
            var options = ParseDiffArguments(arguments, owner, span);
            var a = RequireStringSequence(arguments.Values[0], owner, span, context);
            var b = RequireStringSequence(arguments.Values[1], owner, span, context);
            foreach (var line in unified ? BuildUnifiedDiff(a, b, options, context, span) : BuildContextDiff(a, b, options, context, span))
                yield return line;
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
            var compared = new PyList(differ.CompareLines(a, b, span, context), context.MemoryGovernor, span);
            return compared;
        }

        private static object Restore(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => OutputIterator(RestoreLines(arguments[0], arguments[1], span, GetGlobalContext(context)), span, context);

        private static IEnumerable<object> RestoreLines(object source, object whichValue, LythonSourceSpan span, ExecutionContext context)
        {
            var which = RequireInteger(whichValue, "restore which must be 1 or 2", span);
            if (which != 1 && which != 2) throw new LythonRuntimeException("ValueError", "restore which must be 1 or 2", span);
            var accepted = which == 1 ? "- " : "+ ";
            foreach (var item in ToSequence(source, span, context))
            {
                context.CheckExecution(span);
                if (item is not PyString line) throw new LythonRuntimeException("TypeError", "restore expects string lines", span);
                var text = line.AsString();
                if (text.StartsWith("  ", StringComparison.Ordinal) || text.StartsWith(accepted, StringComparison.Ordinal))
                    yield return PyString.FromString(text[2..], context.MemoryGovernor, span);
            }
        }

        private static object GetCloseMatches(BoundCallArguments bound, LythonSourceSpan span, ExecutionContext context)
        {
            var arguments = bound.Values;
            if (arguments.Length is < 2 or > 4 || !PyStringOps.TryAsString(arguments[0], out var word))
            {
                throw new LythonRuntimeException("TypeError", "difflib.get_close_matches(word, possibilities[, n][, cutoff]) expects a string word and an iterable of strings.", span);
            }

            var limit = bound.Assigned[2] ? RequireInteger(arguments[2], "difflib.get_close_matches(..., n=...) expects n to be an integer.", span) : new BigInteger(3);
            var cutoff = bound.Assigned[3] ? RequireDouble(arguments[3], "difflib.get_close_matches(..., cutoff=...) expects cutoff to be a number.", span) : 0.6;
            if (limit <= 0)
            {
                throw new LythonRuntimeException("ValueError", "difflib.get_close_matches(..., n=...) expects n to be positive.", span);
            }

            if (!(cutoff >= 0.0 && cutoff <= 1.0))
            {
                throw new LythonRuntimeException("ValueError", "difflib.get_close_matches(..., cutoff=...) expects cutoff between 0 and 1.", span);
            }

            var possibilities = RequireStringSequence(arguments[1], "difflib.get_close_matches(word, possibilities)", span, context);
            using var scoreStorage = context.MemoryGovernor.ReserveTemporary(128L + 64L * possibilities.Count, span);
            var scored = new List<(double Score, PyString Value)>();
            var matcher = new DifflibSequenceMatcherObject(null, PyString.Empty, word, autojunk: true, span, context);
            var work = 0;
            foreach (var candidate in possibilities)
            {
                matcher.SetSeq1(candidate, span, context);
                if ((++work & 63) == 0)
                {
                    context.CheckExecution(span);
                }

                if (matcher.RealQuickRatio() >= cutoff &&
                    matcher.QuickRatio(span, context) >= cutoff &&
                    matcher.Ratio(span, context) >= cutoff)
                {
                    scored.Add((matcher.Ratio(span, context), candidate));
                }
            }

            ExecutionSort.Sort(scored, context, span, (left, right) =>
            {
                var byScore = right.Score.CompareTo(left.Score);
                return byScore != 0 ? byScore : PyString.CompareOrdinal(right.Value, left.Value);
            });

            return new PyList(scored.Take((int)BigInteger.Min(limit, scored.Count)).Select(item => (object)item.Value), context.MemoryGovernor, span);
        }

        private static object DiffBytes(BoundCallArguments bound, LythonSourceSpan span, ExecutionContext context)
        {
            var arguments = bound.Values;
            if (arguments.Length is < 3 or > 9 || arguments[0] is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "difflib.diff_bytes(dfunc, a, b, ...) expects a callable diff function and byte line iterables.", span);
            }

            var a = RequireBytesSequence(arguments[1], "difflib.diff_bytes(..., a=...)", span, context);
            var b = RequireBytesSequence(arguments[2], "difflib.diff_bytes(..., b=...)", span, context);
            var fromfile = !bound.Assigned[3] ? Array.Empty<byte>() : ParseOptionalBytes(arguments, 3, [], "difflib.diff_bytes(..., fromfile=...)", span);
            var tofile = !bound.Assigned[4] ? Array.Empty<byte>() : ParseOptionalBytes(arguments, 4, [], "difflib.diff_bytes(..., tofile=...)", span);
            var fromfiledate = !bound.Assigned[5] ? Array.Empty<byte>() : ParseOptionalBytes(arguments, 5, [], "difflib.diff_bytes(..., fromfiledate=...)", span);
            var tofiledate = !bound.Assigned[6] ? Array.Empty<byte>() : ParseOptionalBytes(arguments, 6, [], "difflib.diff_bytes(..., tofiledate=...)", span);
            var n = bound.Assigned[7] ? RequireInteger(arguments[7], "difflib.diff_bytes(..., n=...) expects n to be an integer.", span) : new BigInteger(3);
            var lineterm = !bound.Assigned[8] ? new byte[] { (byte)'\n' } : ParseOptionalBytes(arguments, 8, [(byte)'\n'], "difflib.diff_bytes(..., lineterm=...)", span);

            var result = callable.Invoke(
                [
                    CallArgumentValue.Positional(new PyList(a.Select(line => (object)BytesToDiffText(line)), context.MemoryGovernor, span)),
                    CallArgumentValue.Positional(new PyList(b.Select(line => (object)BytesToDiffText(line)), context.MemoryGovernor, span)),
                    CallArgumentValue.Positional(BytesToDiffText(fromfile)),
                    CallArgumentValue.Positional(BytesToDiffText(tofile)),
                    CallArgumentValue.Positional(BytesToDiffText(fromfiledate)),
                    CallArgumentValue.Positional(BytesToDiffText(tofiledate)),
                    CallArgumentValue.Positional(n),
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
            if (arguments.Length > 2)
            {
                throw new LythonRuntimeException("TypeError", "difflib.Differ([linejunk][, charjunk]) expects zero to two arguments.", span);
            }

            var linejunk = ParseOptionalPredicate(arguments, 0, "difflib.Differ(..., linejunk=...)", span);
            var charjunk = ParseOptionalPredicate(arguments, 1, "difflib.Differ(..., charjunk=...)", span);
            return TrackFreshDiffShell(new DifflibDifferObject(linejunk, charjunk), span, context);
        }

        private static object HtmlDiff(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length > 4)
            {
                throw new LythonRuntimeException("TypeError", "difflib.HtmlDiff([tabsize][, wrapcolumn][, linejunk][, charjunk]) expects zero to four arguments.", span);
            }

            var tabsize = arguments.Length >= 1 && arguments[0] is not PyNone ? RequireInt32(arguments[0], "difflib.HtmlDiff(..., tabsize=...) expects an integer.", span) : 8;
            var wrapcolumn = arguments.Length >= 2 && arguments[1] is not PyNone ? RequireInt32(arguments[1], "difflib.HtmlDiff(..., wrapcolumn=...) expects an integer or None.", span) : (int?)null;
            var linejunk = ParseOptionalPredicate(arguments, 2, "difflib.HtmlDiff(..., linejunk=...)", span);
            var charjunk = ParseOptionalPredicate(arguments, 3, "difflib.HtmlDiff(..., charjunk=...)", span);
            if (wrapcolumn is not null)
                throw new LythonRuntimeException("NotImplementedError", "HtmlDiff wrapcolumn must be None; line wrapping is unsupported.", span);
            if (charjunk is not null)
                throw new LythonRuntimeException("NotImplementedError", "HtmlDiff charjunk must be None; character predicates are unsupported by line-level HTML rendering.", span);
            return TrackFreshDiffShell(new DifflibHtmlDiffObject(tabsize, linejunk), span, context);
        }

        private static object TrackFreshDiffShell(object shell, LythonSourceSpan span, ExecutionContext context)
        {
            context.MemoryGovernor.Reserve(64L, span);
            context.MemoryGovernor.Commit(64L);
            return shell;
        }

        private static object SequenceMatcher(BoundCallArguments arguments, LythonSourceSpan span, ExecutionContext context)
            => SequenceMatcherCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

        private static ValueTask<object> SequenceMatcherAsync(BoundCallArguments arguments, LythonSourceSpan span, ExecutionContext context)
            => SequenceMatcherCoreAsync(arguments, span, context, true);

        private static async ValueTask<object> SequenceMatcherCoreAsync(BoundCallArguments bound, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var arguments = bound.Values;
            if (arguments.Length > 4)
            {
                throw new LythonRuntimeException("TypeError", "difflib.SequenceMatcher([isjunk][, a][, b][, autojunk]) expects zero to four arguments.", span);
            }

            var isjunk = !bound.Assigned[0] || arguments[0] is PyNone ? null : arguments[0];
            var aOriginal = bound.Assigned[1] ? arguments[1] : PyString.Empty;
            var bOriginal = bound.Assigned[2] ? arguments[2] : PyString.Empty;
            var autojunk = !bound.Assigned[3] ? (object)true : arguments[3];
            return asynchronous
                ? await DifflibSequenceMatcherObject.CreateAsync(isjunk, aOriginal, bOriginal, autojunk, span, context).ConfigureAwait(false)
                : new DifflibSequenceMatcherObject(isjunk, aOriginal, bOriginal, autojunk, span, context);
        }

        private static DiffOptions ParseDiffArguments(BoundCallArguments bound, string owner, LythonSourceSpan span)
        {
            var arguments = bound.Values;
            if (arguments.Length is < 2 or > 8)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(a, b[, fromfile][, tofile][, fromfiledate][, tofiledate][, n][, lineterm]) expects two to eight arguments.", span);
            }

            return new DiffOptions(
                !bound.Assigned[2] ? string.Empty : ParseOptionalString(arguments, 2, string.Empty, $"{owner}(..., fromfile=...)", span),
                !bound.Assigned[3] ? string.Empty : ParseOptionalString(arguments, 3, string.Empty, $"{owner}(..., tofile=...)", span),
                !bound.Assigned[4] ? string.Empty : ParseOptionalString(arguments, 4, string.Empty, $"{owner}(..., fromfiledate=...)", span),
                !bound.Assigned[5] ? string.Empty : ParseOptionalString(arguments, 5, string.Empty, $"{owner}(..., tofiledate=...)", span),
                bound.Assigned[6] ? RequireInteger(arguments[6], $"{owner}(..., n=...) expects n to be an integer.", span) : new BigInteger(3),
                !bound.Assigned[7] ? "\n" : ParseOptionalString(arguments, 7, "\n", $"{owner}(..., lineterm=...)", span));
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
            if (arguments.Length <= index)
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
            if (arguments.Length <= index)
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
            // Keep growth and the final copy funded throughout acquisition.
            try
            {
                using var reservation = context.MemoryGovernor.ReserveTemporary(32, span);
                var items = new List<PyString>();
                foreach (var item in ToSequence(value, span, context))
                {
                    var text = PyStringOps.TryAsString(item, out var parsed)
                        ? parsed
                        : throw new LythonRuntimeException("TypeError", $"{owner} expects an iterable of strings.", span);

                    AddSequenceItem(items, text, reservation, span);
                    context.CheckExecution(span);
                }

                var charge = checked(32L + (16L * items.Count));
                reservation.Grow(charge, span);
                var result = items.ToArray();
                reservation.Commit(charge);
                return result;
            }
            catch (PyNotIterableException)
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects an iterable of strings.", span);
            }
        }

        private static IReadOnlyList<byte[]> RequireBytesSequence(object value, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            // Payloads remain funded alongside both reference arrays.
            try
            {
                using var reservation = context.MemoryGovernor.ReserveTemporary(32, span);
                var items = new List<byte[]>();
                long payloadBytes = 0;
                foreach (var item in ToSequence(value, span, context))
                {
                    if (item is not PyBytes bytes)
                    {
                        throw new LythonRuntimeException("TypeError", $"{owner} expects an iterable of bytes.", span);
                    }

                    var payloadCharge = checked(32L + bytes.Length);
                    reservation.Grow(payloadCharge, span);
                    var payload = bytes.ToArray();
                    payloadBytes = checked(payloadBytes + payloadCharge);
                    AddSequenceItem(items, payload, reservation, span);
                    context.CheckExecution(span);
                }

                var backingCharge = checked(32L + (16L * items.Count));
                reservation.Grow(backingCharge, span);
                var result = items.ToArray();
                reservation.Commit(checked(backingCharge + payloadBytes));
                return result;
            }
            catch (PyNotIterableException)
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects an iterable of bytes.", span);
            }
        }

        private static void AddSequenceItem<T>(List<T> items, T item,
            MemoryGovernor.TemporaryMemoryReservation storage, LythonSourceSpan span)
        {
            if (items.Count == items.Capacity)
            {
                var oldCapacity = items.Capacity;
                var nextCapacity = oldCapacity == 0 ? 4 : checked(oldCapacity * 2);
                storage.Grow(16L * nextCapacity, span);
                items.Capacity = nextCapacity;
                storage.Shrink(16L * oldCapacity);
            }
            items.Add(item);
        }

        internal static BigInteger RequireInteger(object value, string message, LythonSourceSpan span)
            => PyNumberOps.TryAsInteger(value, out var integer) ? integer : throw new LythonRuntimeException("TypeError", message, span);

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

        internal static async ValueTask<bool> CallJunkPredicateAsync(object predicate, object argument,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var result = asynchronous
                ? await InvokeCallableTargetAsync(predicate, span, span, context, () => ValueTask.FromResult(new[] { CallArgumentValue.Positional(argument) })).ConfigureAwait(false)
                : InvokeCallableTarget(predicate, span, span, context, () => [CallArgumentValue.Positional(argument)]);
            return asynchronous ? await IsTruthyAsync(result, context, span).ConfigureAwait(false) : IsTruthy(result, context, span);
        }

        private static IEnumerable<object> BuildUnifiedDiff(IReadOnlyList<PyString> a, IReadOnlyList<PyString> b, DiffOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            var matcher = new DifflibSequenceMatcherObject(null, a, b, autojunk: true, span, context);
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

                var first = DifflibSequenceMatcherObject.ReadOpcode(((PyList)group)[0], span);
                var last = DifflibSequenceMatcherObject.ReadOpcode(((PyList)group)[^1], span);
                yield return LythonRuntime.CreateString("@@ -" + FormatUnifiedRange(first.I1, last.I2) + " +" + FormatUnifiedRange(first.J1, last.J2) + " @@" + options.LineTerminator, context, span);
                foreach (var item in (PyList)group)
                {
                    var opcode = DifflibSequenceMatcherObject.ReadOpcode(item, span);
                    foreach (var line in FormatUnifiedOpcode(opcode, a, b, context.MemoryGovernor, span))
                    {
                        yield return line;
                    }
                }
            }
        }

        private static IEnumerable<object> BuildContextDiff(IReadOnlyList<PyString> a, IReadOnlyList<PyString> b, DiffOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            var matcher = new DifflibSequenceMatcherObject(null, a, b, autojunk: true, span, context);
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

                var first = DifflibSequenceMatcherObject.ReadOpcode(((PyList)group)[0], span);
                var last = DifflibSequenceMatcherObject.ReadOpcode(((PyList)group)[^1], span);
                yield return LythonRuntime.CreateString("***************" + options.LineTerminator, context, span);
                yield return LythonRuntime.CreateString("*** " + FormatContextRange(first.I1, last.I2) + " ****" + options.LineTerminator, context, span);
                if (((PyList)group).Any(item => DifflibSequenceMatcherObject.ReadOpcode(item, span).Tag is DiffTag.Replace or DiffTag.Delete))
                {
                    foreach (var line in FormatContextOldLines((PyList)group, a, context.MemoryGovernor, span))
                    {
                        yield return line;
                    }
                }

                yield return LythonRuntime.CreateString("--- " + FormatContextRange(first.J1, last.J2) + " ----" + options.LineTerminator, context, span);
                if (((PyList)group).Any(item => DifflibSequenceMatcherObject.ReadOpcode(item, span).Tag is DiffTag.Replace or DiffTag.Insert))
                {
                    foreach (var line in FormatContextNewLines((PyList)group, b, context.MemoryGovernor, span))
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

        private static IEnumerable<PyString> FormatContextOldLines(PyList group, IReadOnlyList<PyString> a, MemoryGovernor governor, LythonSourceSpan span)
        {
            foreach (var item in group)
            {
                var opcode = DifflibSequenceMatcherObject.ReadOpcode(item, span);
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

        private static IEnumerable<PyString> FormatContextNewLines(PyList group, IReadOnlyList<PyString> b, MemoryGovernor governor, LythonSourceSpan span)
        {
            foreach (var item in group)
            {
                var opcode = DifflibSequenceMatcherObject.ReadOpcode(item, span);
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
            BigInteger ContextLines,
            string LineTerminator);
    }

}
