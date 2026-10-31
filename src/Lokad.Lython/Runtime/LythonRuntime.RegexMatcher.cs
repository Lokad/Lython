using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private enum RegexSubstitutionMode { TextOnly, TextAndCount }
    internal readonly record struct RegexScalarPosition(int ByteOffset, int ScalarOffset);

    private static class RegexMatcher
    {
        internal static ILythonRegexSubject Prepare(RePatternObject pattern, RegexSubjectRange range,
            ExecutionContext context, LythonSourceSpan span)
        {
            var window = Window(range, context, span);
            return Prepare(pattern, range, window, context, span);
        }

        private static ILythonRegexSubject Prepare(RePatternObject pattern, RegexSubjectRange range,
            LythonRegexWindow window, ExecutionContext context, LythonSourceSpan span)
            => RegexCompiler.ProviderCall(() => pattern.Regex.Prepare(range.Original.Utf8Bytes, window,
                context.MemoryGovernor.Budget, context.Limits.CancellationToken), context.MemoryGovernor,
                context.Limits.CancellationToken, span);

        private static LythonRegexWindow Window(RegexSubjectRange range, ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            var input = range.Original.Utf8Bytes.Span;
            var length = range.Original.Length;
            if (length == input.Length) return new(range.Pos, range.EndPos, range.Pos, range.EndPos);
            var start = range.Pos == 0 ? 0 : range.Pos == length ? input.Length : -1;
            var end = range.EndPos == 0 ? 0 : range.EndPos == length ? input.Length : -1;
            // One cancellable scan for the requested boundaries; never require
            // a dense whole-subject coordinate map from a native provider.
            for (int scalar = 0, offset = 0; start < 0 || end < 0; scalar++)
            {
                if ((scalar & 1023) == 0) context.CheckExecution(span);
                if (scalar == range.Pos) start = offset;
                if (scalar == range.EndPos) end = offset;
                if (start >= 0 && end >= 0) break;
                Rune.DecodeFromUtf8(input[offset..], out _, out var width);
                offset += width;
            }
            return new(range.Pos, range.EndPos, start, end);
        }

        private static ReadOnlyMemory<byte> CaptureBytes(RegexSubjectRange range, LythonRegexCapture capture)
            => range.Original.Utf8Bytes.Slice(capture.StartByte, capture.EndByte - capture.StartByte);

        private static void ValidateMatch(RePatternObject pattern, RegexSubjectRange range, LythonRegexMatch match,
            ExecutionContext context, LythonSourceSpan span)
        {
            if (match.Captures.Count != pattern.CaptureSlotCount || match.Captures.Count == 0 || match.Captures[0] is null ||
                match.LastIndex is { } last && (last <= 0 || last >= match.Captures.Count || match.Captures[last] is null))
                throw RuntimeErrors.Runtime("Regex provider returned inconsistent capture metadata.", span);
            var input = range.Original.Utf8Bytes.Span;
            for (var i = 0; i < match.Captures.Count; i++)
            {
                context.CheckExecution(span);
                if (match.Captures[i] is not { } value) continue;
                if (value.StartByte < 0 || value.EndByte < value.StartByte || value.EndByte > input.Length ||
                    value.StartByte < input.Length && (input[value.StartByte] & 0xc0) == 0x80 ||
                    value.EndByte < input.Length && (input[value.EndByte] & 0xc0) == 0x80)
                    throw RuntimeErrors.Runtime("Regex provider returned inconsistent capture ranges.", span);
            }
        }

        private static void ProjectScalarBounds(RegexSubjectRange range, LythonRegexMatch match,
            int[] positions, ref RegexScalarPosition previous, ExecutionContext context, LythonSourceSpan span)
        {
            var count = checked(2 * match.Captures.Count);
            var input = range.Original.Utf8Bytes.Span;
            if (range.Original.Length == input.Length)
            {
                for (var i = 0; i < match.Captures.Count; i++)
                {
                    context.CheckExecution(span);
                    positions[2 * i] = match.Captures[i]?.StartByte ?? -1;
                    positions[2 * i + 1] = match.Captures[i]?.EndByte ?? -1;
                }
                return;
            }
            var bytes = new int[count];
            var slots = new int[count];
            for (var i = 0; i < match.Captures.Count; i++)
            {
                context.CheckExecution(span);
                var capture = match.Captures[i];
                bytes[2 * i] = capture?.StartByte ?? -1;
                bytes[2 * i + 1] = capture?.EndByte ?? -1;
                slots[2 * i] = 2 * i;
                slots[2 * i + 1] = 2 * i + 1;
            }
            Array.Sort(bytes, slots);
            var offset = 0;
            var scalar = 0;
            for (var i = 0; i < count; i++)
            {
                context.CheckExecution(span);
                // Iteration normally advances from the previous whole-match end.
                // A lookbehind capture may precede it; scan that prefix separately
                // and rejoin the checkpoint without retaining a dense subject map.
                if (offset < previous.ByteOffset && bytes[i] >= previous.ByteOffset)
                {
                    offset = previous.ByteOffset;
                    scalar = previous.ScalarOffset;
                }
                while (offset < bytes[i])
                {
                    if ((scalar & 1023) == 0) context.CheckExecution(span);
                    Rune.DecodeFromUtf8(input[offset..], out _, out var width);
                    offset += width;
                    scalar++;
                }
                positions[slots[i]] = bytes[i] < 0 ? -1 : scalar;
            }
            previous = new(match.Captures[0]!.Value.EndByte, positions[1]);
        }

        internal static object Match(RePatternObject pattern, RegexSubjectRange range, LythonRegexMatchMode mode,
            LythonSourceSpan span, ExecutionContext context)
        {
            using var subject = Prepare(pattern, range, context, span);
            var match = RegexCompiler.ProviderCall(() => subject.Match(mode, context.Limits.CancellationToken),
                context.MemoryGovernor, context.Limits.CancellationToken, span);
            return match is null ? PyNone.Instance : CreateMatchObject(pattern, range, match, context, span);
        }

        internal static PyRegexFindIterator CreateFindIterMatches(RePatternObject pattern, RegexSubjectRange range,
            ExecutionContext context, LythonSourceSpan span) => new(pattern, range, context, span);

        internal static PyList CreateFindAllResult(RePatternObject pattern, RegexSubjectRange range,
            LythonSourceSpan span, ExecutionContext context)
        {
            var result = new PyList([], context.MemoryGovernor, span);
            if (!range.IsValid) return result;
            using var subject = Prepare(pattern, range, context, span);
            using var cursor = RegexCompiler.ProviderCall(() => subject.Find(context.Limits.CancellationToken),
                context.MemoryGovernor, context.Limits.CancellationToken, span);
            while (RegexCompiler.ProviderCall(() => cursor.Next(context.Limits.CancellationToken),
                context.MemoryGovernor, context.Limits.CancellationToken, span) is { } match)
            {
                context.CheckExecution(span);
                ValidateMatch(pattern, range, match, context, span);
                if (pattern.CaptureSlotCount <= 2)
                {
                    var slot = pattern.CaptureSlotCount == 1 ? 0 : 1;
                    result.Add(match.Captures[slot] is { } capture ? CreateUtf8String(CaptureBytes(range, capture), context, span) : PyString.Empty);
                }
                else result.Add(CreateTuple(pattern.CaptureSlotCount - 1,
                    i => match.Captures[i + 1] is { } capture ? CreateUtf8String(CaptureBytes(range, capture), context, span) : PyString.Empty,
                    context, span));
            }
            return result;
        }

        internal static ReMatchObject CreateMatchObject(RePatternObject pattern, RegexSubjectRange range,
            LythonRegexMatch match, ExecutionContext context, LythonSourceSpan span)
        {
            var position = default(RegexScalarPosition);
            return CreateMatchObject(pattern, range, match, ref position, context, span);
        }

        internal static ReMatchObject CreateMatchObject(RePatternObject pattern, RegexSubjectRange range,
            LythonRegexMatch match, ref RegexScalarPosition position, ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            ValidateMatch(pattern, range, match, context, span);
            var whole = match.Captures[0]!.Value;
            var bytes = checked(128L + 96L * match.Captures.Count);
            using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
            using var projection = context.MemoryGovernor.ReserveTemporary(checked(96L + 32L * match.Captures.Count), span);
            var positions = new int[checked(2 * match.Captures.Count)];
            ProjectScalarBounds(range, match, positions, ref position, context, span);
            var wholeValue = CreateUtf8String(CaptureBytes(range, whole), context, span);
            var captures = new ReCapture?[match.Captures.Count - 1];
            for (var i = 1; i < match.Captures.Count; i++)
            {
                context.CheckExecution(span);
                if (match.Captures[i] is { } capture)
                    captures[i - 1] = new ReCapture(CreateUtf8String(CaptureBytes(range, capture), context, span),
                        new BigInteger(positions[2 * i]), new BigInteger(positions[2 * i + 1]));
            }
            var result = new ReMatchObject(wholeValue, new BigInteger(positions[0]),
                new BigInteger(positions[1]), pattern, range.Original,
                new BigInteger(range.Pos), new BigInteger(range.EndPos), pattern.CaptureSlotCount,
                captures, pattern.NamedGroups, match.LastIndex);
            storage.Commit(bytes);
            return result;
        }

        internal static PyList Split(RePatternObject pattern, RegexSubjectRange range, int maxSplit,
            LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            var result = new PyList([], context.MemoryGovernor, span);
            if (!range.IsValid) { result.Add(PyString.Empty); return result; }
            var window = Window(range, context, span);
            var lastByte = window.StartByte;
            if (maxSplit >= 0)
            {
                using var subject = Prepare(pattern, range, window, context, span);
                using var cursor = RegexCompiler.ProviderCall(() => subject.Find(context.Limits.CancellationToken),
                    context.MemoryGovernor, context.Limits.CancellationToken, span);
                var splits = 0;
                while ((maxSplit == 0 || splits < maxSplit) &&
                    RegexCompiler.ProviderCall(() => cursor.Next(context.Limits.CancellationToken),
                        context.MemoryGovernor, context.Limits.CancellationToken, span) is { } match)
                {
                    context.CheckExecution(span);
                    ValidateMatch(pattern, range, match, context, span);
                    var whole = match.Captures[0]!.Value;
                    result.Add(CreateUtf8String(range.Original.Utf8Bytes.Slice(lastByte, whole.StartByte - lastByte), context, span));
                    for (var i = 1; i < match.Captures.Count; i++)
                    {
                        context.CheckExecution(span);
                        result.Add(match.Captures[i] is { } capture
                            ? CreateUtf8String(CaptureBytes(range, capture), context, span) : PyNone.Instance);
                    }
                    lastByte = whole.EndByte;
                    splits++;
                }
            }
            context.CheckExecution(span);
            result.Add(window.StartByte == 0 && window.EndByte == range.Original.Utf8Bytes.Length && lastByte == 0
                ? range.Original : CreateUtf8String(range.Original.Utf8Bytes.Slice(lastByte, window.EndByte - lastByte), context, span));
            return result;
        }

        internal static object ExecuteSubstitute(RePatternObject pattern, object replacement, RegexSubjectRange range,
            int count, LythonSourceSpan span, ExecutionContext context, RegexSubstitutionMode mode)
            => SubstituteCoreAsync(pattern, replacement, range, count, span, context, mode, false).GetAwaiter().GetResult();

        internal static ValueTask<object> ExecuteSubstituteAsync(RePatternObject pattern, object replacement, RegexSubjectRange range,
            int count, LythonSourceSpan span, ExecutionContext context, RegexSubstitutionMode mode)
            => SubstituteCoreAsync(pattern, replacement, range, count, span, context, mode, true);

        private static async ValueTask<object> SubstituteCoreAsync(RePatternObject pattern, object replacement, RegexSubjectRange range,
            int count, LythonSourceSpan span, ExecutionContext context, RegexSubstitutionMode mode, bool asynchronous)
        {
            context.CheckExecution(span);
            if (!range.IsValid)
                throw new LythonRuntimeException("ValueError", "Regex substitution endpos must not precede pos.", span);
            var isTemplate = PyStringOps.TryAsString(replacement, out var template);
            if (!isTemplate && replacement is not ICallable)
                throw RuntimeErrors.Type("Regex replacement must be a string or callable.", span);
            // Python validates templates even with no matches or negative count.
            // The private plan lives through matching and all actual cleanup; no
            // provider template dialect or retained replacement cache is involved.
            using var plan = isTemplate ? ReMatchMembers.CompileReplacementTemplate(pattern, template!, context, span) : null;
            if (count < 0)
                return mode == RegexSubstitutionMode.TextAndCount
                    ? PyTuple.FromOwnedArray([range.Original, BigInteger.Zero], context.MemoryGovernor, span) : range.Original;
            var window = Window(range, context, span);
            using var subject = Prepare(pattern, range, window, context, span);
            using var cursor = RegexCompiler.ProviderCall(() => subject.Find(context.Limits.CancellationToken),
                context.MemoryGovernor, context.Limits.CancellationToken, span);
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            var lastByte = 0;
            long replaced = 0;
            var position = default(RegexScalarPosition);
            PyString result;
            try
            {
                while ((count == 0 || replaced < count) &&
                    RegexCompiler.ProviderCall(() => cursor.Next(context.Limits.CancellationToken), context.MemoryGovernor,
                        context.Limits.CancellationToken, span) is { } match)
                {
                    context.CheckExecution(span);
                    ReMatchObject? projected = null;
                    if (plan is null) projected = CreateMatchObject(pattern, range, match, ref position, context, span);
                    else ValidateMatch(pattern, range, match, context, span);
                    var whole = match.Captures[0]!.Value;
                    AppendRegexBytes(builder, range.Original.Utf8Bytes.Span[lastByte..whole.StartByte], context, span);
                    if (plan is not null) plan.AppendTo(builder, range, match, context, span);
                    else
                    {
                        var value = asynchronous
                            ? await InvokeCallableTargetAsync(replacement, span, span, context,
                                [CallArgumentValue.Positional(projected!)]).ConfigureAwait(false)
                            : InvokeCallableTarget(replacement, span, span, context,
                                () => [CallArgumentValue.Positional(projected!)]);
                        context.CheckExecution(span);
                        if (!PyStringOps.TryAsString(value, out var text))
                            throw RuntimeErrors.Type("Regex replacement callable must return a string.", span);
                        AppendRegexBytes(builder, text.Utf8Bytes.Span, context, span);
                    }
                    lastByte = whole.EndByte;
                    replaced++;
                }
                context.CheckExecution(span);
                if (replaced == 0) result = range.Original;
                else
                {
                    AppendRegexBytes(builder, range.Original.Utf8Bytes.Span[lastByte..], context, span);
                    result = builder.ToPyStringAndRelease();
                    context.CheckExecution(span);
                }
            }
            finally { builder.Release(); }
            return mode == RegexSubstitutionMode.TextAndCount
                ? PyTuple.FromOwnedArray([result, new BigInteger(replaced)], context.MemoryGovernor, span) : result;
        }
    }
}
