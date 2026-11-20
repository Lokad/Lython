using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private enum RegexSubstitutionMode { TextOnly, TextAndCount }
    internal readonly record struct RegexScalarPosition(int ByteOffset, int ScalarOffset);

    private static class RegexMatcher
    {
        private const int DirectCoordinateSortCount = 64;

        internal static PreparedSubject Prepare(RePatternObject pattern, RegexSubjectRange range,
            ExecutionContext context, LythonSourceSpan span)
        {
            var window = Window(range, context, span);
            return Prepare(pattern, range, window, context, span);
        }

        private static PreparedSubject Prepare(RePatternObject pattern, RegexSubjectRange range,
            LythonRegexWindow window, ExecutionContext context, LythonSourceSpan span)
            => new(pattern, range, window, context, span);

        // One admitted capture buffer belongs to the prepared operation, never
        // to a cached pattern or a retained Python match. Native users close first.
        internal sealed class PreparedSubject : IDisposable
        {
            private readonly RePatternObject _pattern;
            private readonly ExecutionContext _context;
            private readonly LythonSourceSpan _span;
            private readonly MemoryGovernor.TemporaryMemoryReservation _storage;
            private ILythonRegexSubject? _subject;
            private long[]? _coordinates;
            private Comparison<long>? _coordinateComparison;
            private readonly LythonRegexWindow _window;
            private RegexScalarPosition _position;
            internal readonly LythonRegexCapture?[] Captures;
            internal int? LastIndex;

            internal PreparedSubject(RePatternObject pattern, RegexSubjectRange range, LythonRegexWindow window,
                ExecutionContext context, LythonSourceSpan span)
            {
                _pattern = pattern; _context = context; _span = span;
                _window = window;
                _position = new(window.StartByte, window.Pos);
                _storage = context.MemoryGovernor.ReserveTemporary(checked(128L + 16L * pattern.CaptureSlotCount), span);
                try
                {
                    context.CheckExecution(span);
                    Captures = new LythonRegexCapture?[pattern.CaptureSlotCount];
                    _subject = RegexCompiler.ProviderCall(() => pattern.Regex.Prepare(range.Original.Utf8Bytes, window,
                        context.MemoryGovernor.Budget, context.State.CancellationToken), context.MemoryGovernor,
                        context.State.CancellationToken, span, pattern.Pattern);
                }
                catch { _storage.Dispose(); throw; }
            }

            internal bool Match(LythonRegexMatchMode mode, RegexSubjectRange range)
            {
                ObjectDisposedException.ThrowIf(_subject is null, this);
                if (!RegexCompiler.TryMatch(_subject, mode, Captures, out LastIndex,
                    _context.MemoryGovernor, _context.State.CancellationToken, _span, _pattern.Pattern)) return false;

                ValidateMatch(range, mode);
                return true;
            }

            internal ILythonRegexCursor Find()
            {
                ObjectDisposedException.ThrowIf(_subject is null, this);
                return RegexCompiler.ProviderCall(() => _subject.Find(_context.State.CancellationToken),
                    _context.MemoryGovernor, _context.State.CancellationToken, _span, _pattern.Pattern);
            }

            internal bool Next(ILythonRegexCursor cursor, RegexSubjectRange range)
            {
                // Each prepared subject has one cursor. Capture zero still holds the
                // last successful pull and is null before the first pull.
                var previous = Captures[0];
                if (!RegexCompiler.TryNext(cursor, Captures, out LastIndex, _context.MemoryGovernor,
                    _context.State.CancellationToken, _span, _pattern.Pattern)) return false;

                ValidateMatch(range, LythonRegexMatchMode.Search);
                var whole = Captures[0].GetValueOrDefault();
                if (previous is { } last && (whole.StartByte < last.EndByte || whole == last))
                    throw RuntimeErrors.Runtime("Regex provider cursor did not advance.", _span);
                return true;
            }

            private void ValidateMatch(RegexSubjectRange range, LythonRegexMatchMode mode)
            {
                var error = RegexCompiler.MatchResultError(range.Original.Utf8Span, _window, mode, Captures, LastIndex,
                    _context.MemoryGovernor, _context.State.CancellationToken, _span);
                if (error is not null) throw RuntimeErrors.Runtime(error, _span);
            }

            internal void Project(RegexSubjectRange range, ReCaptureRange[] captures)
            {
                ObjectDisposedException.ThrowIf(_subject is null, this);
                if (range.Original.Length != range.Original.ByteLength && _coordinates is null)
                {
                    var count = checked(2 * captures.Length);
                    var needsComparison = count > DirectCoordinateSortCount;
                    _storage.Grow(checked(32L + 8L * count + (needsComparison ? 96L : 0)), _span);
                    _context.CheckExecution(_span);
                    _coordinates = new long[count];
                    if (needsComparison) _coordinateComparison = CompareCoordinates;
                }
                ProjectScalarBounds(range, _window, captures, ref _position, _coordinates, _context, _span,
                    _coordinateComparison);
            }

            private int CompareCoordinates(long left, long right)
            {
                _context.CheckExecution(_span);
                return left.CompareTo(right);
            }

            public void Dispose()
            {
                var subject = _subject; _subject = null;
                try { subject?.Dispose(); }
                finally
                {
                    _coordinates = null;
                    _coordinateComparison = null;
                    _storage.Dispose();
                }
            }
        }

        private static LythonRegexWindow Window(RegexSubjectRange range, ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            var token = context.State.CancellationToken;
            return RegexCompiler.CreateWindow(range.Original.Utf8Span, range.Original.GetCharacterCount(token, span),
                range.Pos, range.EndPos, token, span);
        }

        private static ReadOnlyMemory<byte> CaptureBytes(RegexSubjectRange range, LythonRegexCapture capture)
            => range.Original.Utf8Bytes.Slice(capture.StartByte, capture.EndByte - capture.StartByte);

        private static void ProjectScalarBounds(RegexSubjectRange range, LythonRegexWindow window, ReCaptureRange[] captures,
            ref RegexScalarPosition previous, Span<long> coordinates, ExecutionContext context, LythonSourceSpan span,
            Comparison<long>? coordinateComparison)
        {
            var count = checked(2 * captures.Length);
            var input = range.Original.Utf8Span;
            if (range.Original.Length == input.Length)
            {
                for (var i = 0; i < captures.Length; i++)
                {
                    context.CheckExecution(span);
                    var capture = captures[i];
                    captures[i] = capture with { Start = capture.StartByte, End = capture.EndByte };
                }
                return;
            }
            // One operation-owned array orders byte boundaries and remembers
            // their capture endpoints. Signed high bits put unmatched -1 first;
            // the low bits identify the original endpoint, including ties.
            coordinates = coordinates[..count];
            for (var i = 0; i < captures.Length; i++)
            {
                context.CheckExecution(span);
                coordinates[2 * i] = ((long)captures[i].StartByte << 32) | (uint)(2 * i);
                coordinates[2 * i + 1] = ((long)captures[i].EndByte << 32) | (uint)(2 * i + 1);
            }
            if (coordinateComparison is null) coordinates.Sort();
            else ExecutionSort.Sort(coordinates, coordinateComparison);
            var offset = previous.ByteOffset;
            var scalar = previous.ScalarOffset;
            for (var i = 0; i < count; i++)
            {
                context.CheckExecution(span);
                var boundary = (int)(coordinates[i] >> 32);
                var endpoint = (int)(coordinates[i] & uint.MaxValue);
                // All these scalar boundaries are already known. Long lookahead
                // captures can repeatedly reach an end far beyond the whole match;
                // lookbehind can precede the window. Neither needs a dense index
                // or a repeated walk from the previous match through that prefix.
                if (boundary >= 0 && boundary != offset)
                {
                    var nearest = new RegexScalarPosition(offset, scalar);
                    var distance = Math.Abs(boundary - offset);
                    if (boundary < distance) nearest = default;
                    else if (input.Length - boundary < distance)
                        nearest = new(input.Length, range.Original.Length);
                    // Full-window endpoints are the same anchors above. Only
                    // distinct bounds can supply additional position knowledge.
                    if (window.StartByte != 0)
                        SelectCloserBoundary(boundary, new(window.StartByte, window.Pos), ref nearest);
                    if (window.EndByte != input.Length)
                        SelectCloserBoundary(boundary, new(window.EndByte, window.EndPos), ref nearest);
                    // Both ends are known UTF-8 boundaries on the original
                    // subject. Count leading bytes without decoding each rune.
                    var token = context.State.CancellationToken;
                    scalar = boundary >= nearest.ByteOffset
                        ? nearest.ScalarOffset + PyString.CountUtf8Units(input[nearest.ByteOffset..boundary], false, token, span)
                        : nearest.ScalarOffset - PyString.CountUtf8Units(input[boundary..nearest.ByteOffset], false, token, span);
                    offset = boundary;
                }
                var value = boundary < 0 ? -1 : scalar;
                var slot = endpoint / 2;
                captures[slot] = (endpoint & 1) == 0
                    ? captures[slot] with { Start = value } : captures[slot] with { End = value };
            }
            previous = new(captures[0].EndByte, captures[0].End);
        }

        private static void SelectCloserBoundary(int boundary, RegexScalarPosition candidate, ref RegexScalarPosition current)
        {
            if (Math.Abs(candidate.ByteOffset - boundary) < Math.Abs(current.ByteOffset - boundary))
                current = candidate;
        }

        internal static object Match(RePatternObject pattern, RegexSubjectRange range, LythonRegexMatchMode mode,
            LythonSourceSpan span, ExecutionContext context)
        {
            if (RegexCompiler.IsImpossibleWindow(mode, range.Pos, range.EndPos))
            {
                context.CheckExecution(span);
                return PyNone.Instance;
            }
            using var subject = Prepare(pattern, range, context, span);
            return subject.Match(mode, range) ? CreateMatchObject(pattern, range, subject, context, span) : PyNone.Instance;
        }

        internal static PyList CreateFindAllResult(RePatternObject pattern, RegexSubjectRange range,
            LythonSourceSpan span, ExecutionContext context)
        {
            var result = new PyList([], context.MemoryGovernor, span);
            if (!range.IsValid) return result;
            using var subject = Prepare(pattern, range, context, span);
            using var cursor = subject.Find();
            while (subject.Next(cursor, range))
            {
                var match = subject;
                context.CheckExecution(span);
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
            PreparedSubject match, ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            var bytes = checked(128L + 32L * match.Captures.Length);
            using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
            context.CheckExecution(span);
            var captures = new ReCaptureRange[match.Captures.Length];
            for (var i = 0; i < captures.Length; i++)
            {
                context.CheckExecution(span);
                captures[i] = match.Captures[i] is { } capture
                    ? new(capture.StartByte, capture.EndByte, -1, -1) : new(-1, -1, -1, -1);
            }
            match.Project(range, captures);
            var result = new ReMatchObject(pattern, range.Original, range.Pos, range.EndPos,
                captures, match.LastIndex, context.MemoryGovernor);
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
                using var cursor = subject.Find();
                var splits = 0;
                while ((maxSplit == 0 || splits < maxSplit) &&
                    subject.Next(cursor, range))
                {
                    var match = subject;
                    context.CheckExecution(span);
                    var whole = match.Captures[0]!.Value;
                    result.Add(CreateUtf8String(range.Original.Utf8Bytes.Slice(lastByte, whole.StartByte - lastByte), context, span));
                    for (var i = 1; i < match.Captures.Length; i++)
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
            result.Add(window.StartByte == 0 && window.EndByte == range.Original.ByteLength && lastByte == 0
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
                    ? new PyTuple([range.Original, BigInteger.Zero], context.MemoryGovernor, span) : range.Original;
            var window = Window(range, context, span);
            using var subject = Prepare(pattern, range, window, context, span);
            using var cursor = subject.Find();
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            var lastByte = 0;
            long replaced = 0;
            PyString result;
            try
            {
                while ((count == 0 || replaced < count) &&
                    subject.Next(cursor, range))
                {
                    var match = subject;
                    context.CheckExecution(span);
                    ReMatchObject? projected = null;
                    if (plan is null) projected = CreateMatchObject(pattern, range, match, context, span);
                    var whole = match.Captures[0]!.Value;
                    AppendRegexBytes(builder, range.Original.Utf8Span[lastByte..whole.StartByte], context, span);
                    if (plan is not null) plan.AppendTo(builder, range, match.Captures, context, span);
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
                        AppendRegexBytes(builder, text.Utf8Span, context, span);
                    }
                    lastByte = whole.EndByte;
                    replaced++;
                }
                context.CheckExecution(span);
                if (replaced == 0) result = range.Original;
                else
                {
                    AppendRegexBytes(builder, range.Original.Utf8Span[lastByte..], context, span);
                    result = builder.ToPyStringAndRelease();
                    context.CheckExecution(span);
                }
            }
            finally { builder.Release(); }
            return mode == RegexSubstitutionMode.TextAndCount
                ? new PyTuple([result, new BigInteger(replaced)], context.MemoryGovernor, span) : result;
        }
    }
}
