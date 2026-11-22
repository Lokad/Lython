using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private enum RegexSubstitutionMode { TextOnly, TextAndCount }
    internal readonly record struct RegexScalarPosition(int ByteOffset, int ScalarOffset);

    private static class RegexMatcher
    {
        private const int DirectCoordinateSortCount = 64;
        private const int InlineMatchCaptureCount = 8;

        [InlineArray(InlineMatchCaptureCount)]
        private struct InlineMatchCaptures { private LythonRegexCapture? _first; }

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
                    _subject = OpenSubject(pattern, range, window, context, span);
                }
                catch { _storage.Dispose(); throw; }
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

                ValidateMatch(range, _window, LythonRegexMatchMode.Search, Captures, LastIndex, _context, _span);
                var whole = Captures[0].GetValueOrDefault();
                if (previous is { } last && (whole.StartByte < last.EndByte || whole == last))
                    throw RuntimeErrors.Runtime("Regex provider cursor did not advance.", _span);
                return true;
            }

            internal void ProjectUnicode(RegexSubjectRange range, ReCaptureRange[] captures)
            {
                ObjectDisposedException.ThrowIf(_subject is null, this);
                if (_coordinates is null)
                {
                    var count = checked(2 * captures.Length);
                    var needsComparison = count > DirectCoordinateSortCount;
                    _storage.Grow(checked(32L + 8L * count + (needsComparison ? 96L : 0)), _span);
                    _context.CheckExecution(_span);
                    _coordinates = new long[count];
                    if (needsComparison) _coordinateComparison = CompareCoordinates;
                }
                ProjectScalarBounds(range, _window, Captures, captures, ref _position, _coordinates, _context, _span,
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

        private static ILythonRegexSubject OpenSubject(RePatternObject pattern, RegexSubjectRange range,
            LythonRegexWindow window, ExecutionContext context, LythonSourceSpan span)
            => RegexCompiler.ProviderCall(() => pattern.Regex.Prepare(range.Original.Utf8Bytes, window,
                context.MemoryGovernor.Budget, context.State.CancellationToken), context.MemoryGovernor,
                context.State.CancellationToken, span, pattern.Pattern);

        private static bool TryMatch(ILythonRegexSubject subject, RePatternObject pattern, RegexSubjectRange range,
            LythonRegexWindow window, LythonRegexMatchMode mode, Span<LythonRegexCapture?> captures,
            out int? lastIndex, ExecutionContext context, LythonSourceSpan span)
        {
            if (!RegexCompiler.TryMatch(subject, mode, captures, out lastIndex,
                context.MemoryGovernor, context.State.CancellationToken, span, pattern.Pattern)) return false;

            ValidateMatch(range, window, mode, captures, lastIndex, context, span);
            return true;
        }

        private static void ValidateMatch(RegexSubjectRange range, LythonRegexWindow window, LythonRegexMatchMode mode,
            ReadOnlySpan<LythonRegexCapture?> captures, int? lastIndex, ExecutionContext context, LythonSourceSpan span)
        {
            var error = RegexCompiler.MatchResultError(range.Original.Utf8Span, window, mode, captures, lastIndex,
                context.MemoryGovernor, context.State.CancellationToken, span);
            if (error is not null) throw RuntimeErrors.Runtime(error, span);
        }

        private static LythonRegexWindow Window(RegexSubjectRange range, ExecutionContext context,
            LythonSourceSpan span, bool borrowIndex = false)
        {
            context.CheckExecution(span);
            var token = context.State.CancellationToken;
            return RegexCompiler.CreateWindow(range.Original.Utf8Span, range.Original.GetCharacterCount(token, span),
                range.Pos, range.EndPos, token, span, borrowIndex ? range.Original.PreparedRuneByteOffsets : default);
        }

        private static PyString ReadSubjectRange(RegexSubjectRange range, int start, int end,
            ExecutionContext context, LythonSourceSpan span)
            => MaterializeRegexRange(range.Original, start, end, context.MemoryGovernor, context, span);

        private static void ProjectScalarBounds(RegexSubjectRange range, LythonRegexWindow window,
            ReadOnlySpan<LythonRegexCapture?> slots, ReCaptureRange[] captures,
            ref RegexScalarPosition previous, Span<long> coordinates, ExecutionContext context, LythonSourceSpan span,
            Comparison<long>? coordinateComparison)
        {
            var count = checked(2 * captures.Length);
            var input = range.Original.Utf8Span;
            // One operation-owned array orders byte boundaries and remembers
            // their capture endpoints. Signed high bits put unmatched -1 first;
            // the low bits identify the original endpoint, including ties.
            coordinates = coordinates[..count];
            for (var i = 0; i < captures.Length; i++)
            {
                context.CheckExecution(span);
                var (start, end) = slots[i].GetValueOrDefault(new(-1, -1));
                captures[i] = new(start, end, -1, -1);
                coordinates[2 * i] = ((long)start << 32) | (uint)(2 * i);
                coordinates[2 * i + 1] = ((long)end << 32) | (uint)(2 * i + 1);
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
            // Single matches need no iterator state. Keep working storage
            // local until the provider and the final projection have finished.
            var window = Window(range, context, span, borrowIndex: true);
            var count = pattern.CaptureSlotCount;
            using var scratch = count > InlineMatchCaptureCount
                ? context.MemoryGovernor.ReserveTemporary(checked(32L + 16L * count), span) : null;
            context.CheckExecution(span);
            InlineMatchCaptures buffer = default;
            Span<LythonRegexCapture?> slots = buffer;
            slots = scratch is null ? slots[..count] : new LythonRegexCapture?[count];
            using var subject = OpenSubject(pattern, range, window, context, span);
            return TryMatch(subject, pattern, range, window, mode, slots, out var lastIndex, context, span)
                ? CreateMatchObject(pattern, range, slots, lastIndex, context, span, window) : PyNone.Instance;
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
                    result.Add(match.Captures[slot] is { } capture
                        ? ReadSubjectRange(range, capture.StartByte, capture.EndByte, context, span) : PyString.Empty);
                }
                else result.Add(CreateTuple(pattern.CaptureSlotCount - 1,
                    i => match.Captures[i + 1] is { } capture
                        ? ReadSubjectRange(range, capture.StartByte, capture.EndByte, context, span) : PyString.Empty,
                    context, span));
            }
            return result;
        }

        internal static ReMatchObject CreateMatchObject(RePatternObject pattern, RegexSubjectRange range,
            PreparedSubject match, ExecutionContext context, LythonSourceSpan span)
            => CreateMatchObject(pattern, range, match.Captures, match.LastIndex, context, span, default, match);

        private static ReMatchObject CreateMatchObject(RePatternObject pattern, RegexSubjectRange range,
            ReadOnlySpan<LythonRegexCapture?> slots, int? lastIndex, ExecutionContext context, LythonSourceSpan span,
            LythonRegexWindow window, PreparedSubject? match = null)
        {
            context.CheckExecution(span);
            var bytes = checked(128L + 32L * slots.Length);
            using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
            context.CheckExecution(span);
            var captures = new ReCaptureRange[slots.Length];
            if (range.Original.Length == range.Original.ByteLength)
            {
                for (var i = 0; i < captures.Length; i++)
                {
                    context.CheckExecution(span);
                    var (start, end) = slots[i].GetValueOrDefault(new(-1, -1));
                    captures[i] = new(start, end, start, end);
                }
            }
            else if (match is not null) match.ProjectUnicode(range, captures);
            else ProjectSingleMatch(range, window, slots, captures, context, span);
            var result = new ReMatchObject(pattern, range.Original, range.Pos, range.EndPos,
                captures, lastIndex, context.MemoryGovernor);
            storage.Commit(bytes);
            return result;
        }

        private static void ProjectSingleMatch(RegexSubjectRange range, LythonRegexWindow window,
            ReadOnlySpan<LythonRegexCapture?> slots, ReCaptureRange[] captures, ExecutionContext context, LythonSourceSpan span)
        {
            var position = new RegexScalarPosition(window.StartByte, window.Pos);
            var count = checked(captures.Length * 2);
            if (count <= DirectCoordinateSortCount)
            {
                Span<long> coordinates = stackalloc long[count];
                ProjectScalarBounds(range, window, slots, captures, ref position, coordinates, context, span, null);
                return;
            }

            // A large coordinate sort needs its own funded buffer and a
            // cancellable comparison for the duration of this projection.
            using var scratch = context.MemoryGovernor.ReserveTemporary(checked(160L + 8L * count), span);
            context.CheckExecution(span);
            var wide = new long[count];
            ProjectScalarBounds(range, window, slots, captures, ref position, wide, context, span, CreateCoordinateComparison(context, span));
        }

        private static Comparison<long> CreateCoordinateComparison(ExecutionContext context, LythonSourceSpan span)
            => (left, right) =>
            {
                context.CheckExecution(span);
                return left.CompareTo(right);
            };

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
                    result.Add(ReadSubjectRange(range, lastByte, whole.StartByte, context, span));
                    for (var i = 1; i < match.Captures.Length; i++)
                    {
                        context.CheckExecution(span);
                        result.Add(match.Captures[i] is { } capture
                            ? ReadSubjectRange(range, capture.StartByte, capture.EndByte, context, span) : PyNone.Instance);
                    }
                    lastByte = whole.EndByte;
                    splits++;
                }
            }
            context.CheckExecution(span);
            result.Add(ReadSubjectRange(range, lastByte, window.EndByte, context, span));
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
            PyString? soleReplacement = null;
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
                    var projected = isTemplate ? null : CreateMatchObject(pattern, range, match, context, span);
                    var whole = match.Captures[0]!.Value;
                    if (soleReplacement is not null)
                    {
                        AppendRegexBytes(builder, soleReplacement.Utf8Span, context, span);
                        soleReplacement = null;
                    }
                    AppendRegexBytes(builder, range.Original.Utf8Span[lastByte..whole.StartByte], context, span);
                    if (plan is not null) plan.AppendTo(builder, template!, range, match.Captures, context, span);
                    else
                    {
                        var text = template;
                        if (!isTemplate)
                        {
                            var value = asynchronous
                                ? await InvokeCallableTargetAsync(replacement, span, span, context,
                                    [CallArgumentValue.Positional(projected!)]).ConfigureAwait(false)
                                : InvokeCallableTarget(replacement, span, span, context,
                                    () => [CallArgumentValue.Positional(projected!)]);
                            context.CheckExecution(span);
                            if (!PyStringOps.TryAsString(value, out text))
                                throw RuntimeErrors.Type("Regex replacement callable must return a string.", span);
                        }
                        // Python returns the single replacement object when
                        // it is the only piece, even if it is empty.
                        if (replaced == 0 && whole.StartByte == 0 && whole.EndByte == range.Original.ByteLength)
                            soleReplacement = text;
                        else AppendRegexBytes(builder, text!.Utf8Span, context, span);
                    }
                    lastByte = whole.EndByte;
                    replaced++;
                }
                context.CheckExecution(span);
                if (replaced == 0) result = range.Original;
                else if (soleReplacement is not null) result = soleReplacement;
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
