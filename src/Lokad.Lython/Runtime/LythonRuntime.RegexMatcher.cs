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
        internal static PreparedCursor OpenCursor(RePatternObject pattern, RegexSubjectRange range,
            ExecutionContext context, LythonSourceSpan span)
            => new(pattern, range.Original, Window(range, context, span), context, span);

        // The cursor, subject borrow and capture buffer have one lifetime.
        // Retained Python matches own their projected captures.
        internal sealed class PreparedCursor : IDisposable
        {
            private readonly RePatternObject _pattern;
            private readonly ExecutionContext _context;
            private readonly LythonSourceSpan _span;
            private readonly MemoryGovernor.TemporaryMemoryReservation _storage;
            private readonly LythonRegexCapture?[] _captures;
            private ILythonRegexSubject? _subject;
            private ILythonRegexCursor? _cursor;
            private long[]? _coordinates;
            private Comparison<long>? _coordinateComparison;
            private readonly LythonRegexWindow _window;
            private RegexScalarPosition _position;
            private int? _lastIndex;
            internal PyString Original { get; }
            internal ReadOnlySpan<LythonRegexCapture?> Captures => _captures;

            internal PreparedCursor(RePatternObject pattern, PyString original, LythonRegexWindow window,
                ExecutionContext context, LythonSourceSpan span)
            {
                _pattern = pattern; _context = context; _span = span;
                Original = original;
                _window = window;
                _position = new(window.StartByte, window.Pos);
                _storage = context.MemoryGovernor.ReserveTemporary(checked(144L + 16L * pattern.CaptureSlotCount), span);
                try
                {
                    context.CheckExecution(span);
                    _captures = new LythonRegexCapture?[pattern.CaptureSlotCount];
                    var subject = OpenSubject(pattern, original, window, context, span);
                    try
                    {
                        var token = context.State.CancellationToken;
                        _cursor = RegexCompiler.ProviderCall((Subject: subject, Token: token),
                            static state => state.Subject.Find(state.Token), context.MemoryGovernor, token, span, pattern.Pattern);
                    }
                    catch { subject.Dispose(); throw; }
                    _subject = subject;
                }
                catch { _storage.Dispose(); throw; }
            }

            internal bool Next()
            {
                ObjectDisposedException.ThrowIf(_cursor is null, this);
                // Capture zero holds the last successful pull and starts null.
                var previous = _captures[0];
                if (!RegexCompiler.TryNext(_cursor, _captures, out _lastIndex, _context.MemoryGovernor,
                    _context.State.CancellationToken, _span, _pattern.Pattern)) return false;

                ValidateMatch(Original, _window, LythonRegexMatchMode.Search, _captures, _lastIndex, _context, _span);
                var whole = _captures[0].GetValueOrDefault();
                if (previous is { } last && (whole.StartByte < last.EndByte || whole == last))
                    throw RuntimeErrors.Runtime("Regex provider cursor did not advance.", _span);
                return true;
            }

            internal PyString MaterializeRange(int start, int end)
                => MaterializeRegexRange(Original, start, end, _context.MemoryGovernor, _context, _span);

            internal ReMatchObject CreateMatch()
                => CreateMatchObject(_pattern, Original, _window, _captures, _lastIndex, _context, _span, this);

            internal void ProjectUnicode(Span<ReCaptureRange> captures)
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
                ProjectScalarBounds(Original, _window, _captures, captures, ref _position, _coordinates, _context, _span,
                    _coordinateComparison);
            }

            private int CompareCoordinates(long left, long right)
            {
                _context.CheckExecution(_span);
                return left.CompareTo(right);
            }

            public void Dispose()
            {
                var cursor = _cursor; _cursor = null;
                var subject = _subject; _subject = null;
                try { cursor?.Dispose(); }
                finally
                {
                    try { subject?.Dispose(); }
                    finally
                    {
                        _coordinates = null;
                        _coordinateComparison = null;
                        _storage.Dispose();
                    }
                }
            }
        }

        private static ILythonRegexSubject OpenSubject(RePatternObject pattern, PyString original,
            LythonRegexWindow window, ExecutionContext context, LythonSourceSpan span)
        {
            var token = context.State.CancellationToken;
            return RegexCompiler.ProviderCall((pattern, original, window, context, token),
                static state => state.pattern.Regex.Prepare(state.original.Utf8Bytes, state.window,
                    state.context.MemoryGovernor.Budget, state.token), context.MemoryGovernor, token, span, pattern.Pattern);
        }

        private static void ValidateMatch(PyString original, LythonRegexWindow window, LythonRegexMatchMode mode,
            ReadOnlySpan<LythonRegexCapture?> captures, int? lastIndex, ExecutionContext context, LythonSourceSpan span)
        {
            var error = RegexCompiler.MatchResultError(original.Utf8Span, window, mode, captures, lastIndex,
                context.MemoryGovernor, context.State.CancellationToken, span);
            if (error is not null) throw RuntimeErrors.Runtime(error, span);
        }

        private static LythonRegexWindow Window(RegexSubjectRange range, ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            var token = context.State.CancellationToken;
            var subject = range.Original;
            var utf8 = subject.Utf8Span;
            var length = subject.GetCharacterCount(token, span);
            // ASCII and windows using only the subject edges need no index.
            // Interior bounds may borrow an already-funded scalar index.
            var offsets = length != utf8.Length &&
                (range.Pos > 0 && range.Pos < length || range.EndPos > 0 && range.EndPos < length)
                    ? subject.PreparedRuneByteOffsets : default;
            return RegexCompiler.CreateWindow(utf8, length, range.Pos, range.EndPos, token, span, offsets);
        }

        private static void ProjectScalarBounds(PyString original, LythonRegexWindow window,
            ReadOnlySpan<LythonRegexCapture?> slots, Span<ReCaptureRange> captures,
            ref RegexScalarPosition previous, Span<long> coordinates, ExecutionContext context, LythonSourceSpan span,
            Comparison<long>? coordinateComparison)
        {
            var count = checked(2 * captures.Length);
            var input = original.Utf8Span;
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
                        nearest = new(input.Length, original.Length);
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
            var window = Window(range, context, span);
            var count = pattern.CaptureSlotCount;
            using var scratch = count > RegexCompiler.InlineCaptureCapacity
                ? context.MemoryGovernor.ReserveTemporary(RegexCompiler.CaptureBufferBytes(count), span) : null;
            context.CheckExecution(span);
            RegexCompiler.InlineCaptureBuffer buffer = default;
            Span<LythonRegexCapture?> slots = buffer;
            slots = scratch is null ? slots[..count] : new LythonRegexCapture?[count];
            var original = range.Original;
            using var subject = OpenSubject(pattern, original, window, context, span);
            if (!RegexCompiler.TryMatch(subject, mode, slots, out var lastIndex,
                context.MemoryGovernor, context.State.CancellationToken, span, pattern.Pattern)) return PyNone.Instance;
            ValidateMatch(original, window, mode, slots, lastIndex, context, span);
            return CreateMatchObject(pattern, original, window, slots, lastIndex, context, span);
        }

        internal static PyList CreateFindAllResult(RePatternObject pattern, RegexSubjectRange range,
            LythonSourceSpan span, ExecutionContext context)
        {
            var result = new PyList([], context.MemoryGovernor, span);
            if (!range.IsValid) return result;
            using var match = OpenCursor(pattern, range, context, span);
            while (match.Next())
            {
                context.CheckExecution(span);
                if (pattern.CaptureSlotCount <= 2)
                {
                    var slot = pattern.CaptureSlotCount == 1 ? 0 : 1;
                    result.Add(match.Captures[slot] is { } capture
                        ? match.MaterializeRange(capture.StartByte, capture.EndByte) : PyString.Empty);
                }
                else result.Add(CopyFindAllGroups(match, context, span));
            }
            return result;
        }

        private static PyTuple CopyFindAllGroups(PreparedCursor match, ExecutionContext context, LythonSourceSpan span)
        {
            var captures = match.Captures[1..];
            var governor = context.MemoryGovernor;
            var bytes = PyTuple.EstimateApproximateBytes(captures.Length);
            governor.Reserve(bytes, span);
            try
            {
                context.CheckExecution(span);
                var items = new PyString[captures.Length];
                for (var i = 0; i < items.Length; i++)
                {
                    if (i != 0 && (i & 1023) == 0) context.CheckExecution(span);
                    items[i] = captures[i] is { } capture
                        ? match.MaterializeRange(capture.StartByte, capture.EndByte) : PyString.Empty;
                }
                var tuple = PyTuple.FromAdmittedOwnedArray(items, governor, span, bytes);
                governor.Commit(bytes);
                return tuple;
            }
            catch { governor.ReleaseReserved(bytes); throw; }
        }

        private static ReMatchObject CreateMatchObject(RePatternObject pattern, PyString original, LythonRegexWindow window,
            ReadOnlySpan<LythonRegexCapture?> slots, int? lastIndex, ExecutionContext context, LythonSourceSpan span,
            PreparedCursor? match = null)
        {
            context.CheckExecution(span);
            var bytes = checked(128L + (slots.Length == 1 ? 0 : 32L * slots.Length));
            using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
            context.CheckExecution(span);
            var result = ReMatchObject.CreateAdmitted(pattern, original, window, lastIndex, context.MemoryGovernor);
            var captures = result.UnpublishedCaptures;
            if (original.Length == original.ByteLength)
            {
                for (var i = 0; i < captures.Length; i++)
                {
                    context.CheckExecution(span);
                    var (start, end) = slots[i].GetValueOrDefault(new(-1, -1));
                    captures[i] = new(start, end, start, end);
                }
            }
            else if (match is not null) match.ProjectUnicode(captures);
            else ProjectSingleMatch(original, window, slots, captures, context, span);
            storage.Commit(bytes);
            return result;
        }

        private static void ProjectSingleMatch(PyString original, LythonRegexWindow window,
            ReadOnlySpan<LythonRegexCapture?> slots, Span<ReCaptureRange> captures, ExecutionContext context, LythonSourceSpan span)
        {
            var position = new RegexScalarPosition(window.StartByte, window.Pos);
            var count = checked(captures.Length * 2);
            if (count <= DirectCoordinateSortCount)
            {
                Span<long> coordinates = stackalloc long[count];
                ProjectScalarBounds(original, window, slots, captures, ref position, coordinates, context, span, null);
                return;
            }

            // A large coordinate sort needs its own funded buffer and a
            // cancellable comparison for the duration of this projection.
            using var scratch = context.MemoryGovernor.ReserveTemporary(checked(160L + 8L * count), span);
            context.CheckExecution(span);
            var wide = new long[count];
            ProjectScalarBounds(original, window, slots, captures, ref position, wide, context, span, CreateCoordinateComparison(context, span));
        }

        private static Comparison<long> CreateCoordinateComparison(ExecutionContext context, LythonSourceSpan span)
            => (left, right) =>
            {
                context.CheckExecution(span);
                return left.CompareTo(right);
            };

        internal static PyList Split(RePatternObject pattern, RegexSubjectRange range, long maxSplit,
            LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            var result = new PyList([], context.MemoryGovernor, span);
            if (!range.IsValid) { result.Add(PyString.Empty); return result; }
            var window = Window(range, context, span);
            var lastByte = window.StartByte;
            if (maxSplit >= 0)
            {
                using var match = new PreparedCursor(pattern, range.Original, window, context, span);
                long splits = 0;
                while ((maxSplit == 0 || splits < maxSplit) && match.Next())
                {
                    context.CheckExecution(span);
                    var whole = match.Captures[0]!.Value;
                    result.Add(match.MaterializeRange(lastByte, whole.StartByte));
                    for (var i = 1; i < match.Captures.Length; i++)
                    {
                        context.CheckExecution(span);
                        result.Add(match.Captures[i] is { } capture
                            ? match.MaterializeRange(capture.StartByte, capture.EndByte) : PyNone.Instance);
                    }
                    lastByte = whole.EndByte;
                    splits++;
                }
            }
            context.CheckExecution(span);
            result.Add(MaterializeRegexRange(range.Original, lastByte, window.EndByte, context.MemoryGovernor, context, span));
            return result;
        }

        internal static async ValueTask<object> Substitute(RePatternObject pattern, object replacement, object subject,
            long count, int pos, int end, LythonSourceSpan span, ExecutionContext context, RegexSubstitutionMode mode,
            string name, bool asynchronous)
        {
            context.CheckExecution(span);
            var hasText = PyStringOps.TryAsString(subject, out var input);
            var range = hasText ? RegexCompiler.CreateSubjectRange(input, pos, end, span, context) : default;
            if (hasText && !range.IsValid)
                throw new LythonRuntimeException("ValueError", "Regex substitution endpos must not precede pos.", span);
            var isTemplate = PyStringOps.TryAsString(replacement, out var template);
            if (!isTemplate && !IsCallable(replacement))
                throw RuntimeErrors.Type("Regex replacement must be a string or callable.", span);
            // Python validates templates even with no matches, negative count
            // or an invalid subject. The private plan lives through cleanup.
            using var plan = isTemplate ? ReMatchMembers.CompileReplacementTemplate(pattern, template!, context, span) : null;
            if (!hasText) throw RuntimeErrors.Type(RePatternMembers.SubjectError(name), span);
            if (count < 0)
                return mode == RegexSubstitutionMode.TextAndCount
                    ? new PyTuple([range.Original, BigInteger.Zero], context.MemoryGovernor, span) : range.Original;
            using var match = OpenCursor(pattern, range, context, span);
            var original = match.Original;
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            PyString? soleReplacement = null;
            var lastByte = 0;
            long replaced = 0;
            PyString result;
            try
            {
                while ((count == 0 || replaced < count) && match.Next())
                {
                    context.CheckExecution(span);
                    var projected = isTemplate ? null : match.CreateMatch();
                    var whole = match.Captures[0]!.Value;
                    if (soleReplacement is not null)
                    {
                        AppendRegexBytes(builder, soleReplacement.Utf8Span, context, span);
                        soleReplacement = null;
                    }
                    AppendRegexBytes(builder, original.Utf8Span[lastByte..whole.StartByte], context, span);
                    if (plan is not null) plan.AppendTo(builder, template!, original.Utf8Span, match.Captures, context, span);
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
                        if (replaced == 0 && whole.StartByte == 0 && whole.EndByte == original.ByteLength)
                            soleReplacement = text;
                        else AppendRegexBytes(builder, text!.Utf8Span, context, span);
                    }
                    lastByte = whole.EndByte;
                    replaced++;
                }
                context.CheckExecution(span);
                if (replaced == 0) result = original;
                else if (soleReplacement is not null) result = soleReplacement;
                else
                {
                    AppendRegexBytes(builder, original.Utf8Span[lastByte..], context, span);
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
