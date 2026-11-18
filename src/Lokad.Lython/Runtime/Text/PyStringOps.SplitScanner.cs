using System.Buffers;
using System.Text;

namespace Lokad.Lython.Runtime.Text;

internal static partial class PyStringOps
{
    private static readonly SearchValues<byte> OtherLineBreakCandidates = SearchValues.Create(
        (ReadOnlySpan<byte>)[(byte)'\r', (byte)'\v', (byte)'\f', 0x1c, 0x1d, 0x1e, 0xc2, 0xe2]);

    // Positions only: ordinary splits replay this deterministic scanner into
    // their admitted final storage, without retaining a range inventory.
    private struct SplitScanState(int length, int maxSplit, bool reverse,
        bool lines, bool keepEnds, CaseMapping? casing)
    {
        internal int Position = reverse ? length : 0;
        internal int Remaining = maxSplit < 0 ? int.MaxValue : maxSplit;
        internal int LineStart;
        internal bool Done;
        internal readonly bool Reverse = reverse, Lines = lines, KeepEnds = keepEnds;
        internal WhitespaceRangeState Whitespace = new(maxSplit, casing);
    }

    // Common sizing needs a count, not each fragment's coordinates. Conservatively
    // admit minor fragment headroom, then commit the final estimates while writing.
    private static bool TryCountBulkSplit(ReadOnlySpan<byte> source, ReadOnlySpan<byte> separator,
        in SplitScanState state, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span,
        out int count, out int payloadBytes, out bool wholeSource)
    {
        count = 0;
        payloadBytes = source.Length;
        wholeSource = false;
        if (state.Lines)
        {
            var breaks = 0;
            var breakBytes = 0;
            for (var offset = 0; offset < source.Length;)
            {
                context?.CheckExecution(span);
                var end = offset + Math.Min(4096, source.Length - offset);
                var lineFeeds = source[offset..end].Count((byte)'\n');
                breaks = checked(breaks + lineFeeds);
                breakBytes += lineFeeds;
                var position = offset;
                while (position < end)
                {
                    var found = source[position..end].IndexOfAny(OtherLineBreakCandidates);
                    if (found < 0) break;
                    position += found;
                    if (TryGetLineBreakByteLength(source, position, out var width))
                    {
                        // LF was counted in bulk, including a CRLF crossing
                        // this chunk boundary. Count the combined break once.
                        if (source[position] == (byte)'\r' && width == 2) breakBytes++;
                        else { breaks++; breakBytes += width; }
                        position += width;
                    }
                    else position++;
                }
                offset = end;
            }
            var endsWithBreak = false;
            if (!source.IsEmpty)
            {
                Rune.DecodeLastFromUtf8(source, out _, out var width);
                endsWithBreak = TryGetLineBreakByteLength(source, source.Length - width, out _);
            }
            count = checked(breaks + (!source.IsEmpty && !endsWithBreak ? 1 : 0));
            payloadBytes = state.KeepEnds ? source.Length : source.Length - breakBytes;
            wholeSource = count == 1 && (state.KeepEnds || !endsWithBreak);
            return true;
        }
        if (separator.Length != 1) return false;
        var foundSeparators = 0;
        for (var offset = 0; offset < source.Length && foundSeparators < state.Remaining;)
        {
            context?.CheckExecution(span);
            var length = Math.Min(4096, source.Length - offset);
            foundSeparators = Math.Min(state.Remaining,
                checked(foundSeparators + source.Slice(offset, length).Count(separator[0])));
            offset += length;
        }
        count = checked(foundSeparators + 1);
        payloadBytes = source.Length - foundSeparators;
        wholeSource = count == 1;
        return true;
    }

    private static bool TryReadSplitRange(ReadOnlySpan<byte> source, ReadOnlySpan<byte> separator,
        ref SplitScanState state, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span,
        out (int Start, int End, int Capacity) range)
    {
        if (state.Lines) return TryReadLineRange(source, ref state, context, span, out range);
        if (separator.IsEmpty)
            return state.Reverse ? TryReadReverseWhitespaceRange(source, ref state, context, span, out range)
                : TryReadWhitespaceRange(source, ref state.Whitespace, context, span, out range);

        range = default;
        if (state.Done) return false;
        var offset = state.Position;
        if (state.Remaining != 0)
        {
            var remaining = state.Reverse ? source[..offset] : source[offset..];
            var found = LythonRuntime.FindBytes(remaining, separator, context, span, state.Reverse);
            if (found >= 0)
            {
                if (!state.Reverse) found += offset;
                var start = state.Reverse ? found + separator.Length : offset;
                var end = state.Reverse ? offset : found;
                range = (start, end, end - start);
                state.Position = state.Reverse ? found : found + separator.Length;
                state.Remaining--;
                return true;
            }
        }
        state.Done = true;
        range = state.Reverse ? (0, offset, offset) : (offset, source.Length, source.Length - offset);
        return true;
    }

    private static bool TryReadLineRange(ReadOnlySpan<byte> source, ref SplitScanState state,
        LythonRuntime.ExecutionContext? context, LythonSourceSpan? span,
        out (int Start, int End, int Capacity) range)
    {
        var start = state.LineStart;
        while (state.Position < source.Length)
        {
            context?.CheckExecution(span);
            var end = state.Position + Math.Min(4096, source.Length - state.Position);
            while (state.Position < end)
            {
                var candidate = source[state.Position..end].IndexOfAny(LineBreakCandidates);
                if (candidate < 0) { state.Position = end; break; }
                state.Position += candidate;
                if (TryGetLineBreakByteLength(source, state.Position, out var width))
                {
                    var boundary = state.Position;
                    state.LineStart = state.Position = boundary + width;
                    var stop = state.KeepEnds ? state.Position : boundary;
                    range = (start, stop, stop - start);
                    return true;
                }
                state.Position++;
            }
        }
        range = (start, source.Length, source.Length - start);
        state.LineStart = source.Length;
        return start < source.Length;
    }

    private static bool TryReadReverseWhitespaceRange(ReadOnlySpan<byte> source, ref SplitScanState state,
        LythonRuntime.ExecutionContext? context, LythonSourceSpan? span,
        out (int Start, int End, int Capacity) range)
    {
        var end = -1;
        while (state.Position > 0)
        {
            context?.CheckExecution(span);
            var chunkStart = Math.Max(0, state.Position - 64);
            do
            {
                var position = state.Position;
                int width;
                bool whitespace;
                if (source[position - 1] < 0x80)
                {
                    whitespace = IsAsciiWhitespace(source[position - 1]);
                    width = AsciiWhitespaceRunLength(source[chunkStart..position], whitespace, reverse: true);
                }
                else
                {
                    Rune.DecodeLastFromUtf8(source[..position], out var rune, out width);
                    whitespace = IsPythonWhitespace(rune);
                }
                state.Position -= width;
                if (whitespace && end >= 0)
                {
                    state.Remaining--;
                    range = (position, end, end - position);
                    return true;
                }
                if (!whitespace && end < 0)
                {
                    end = position;
                    if (state.Remaining == 0)
                    {
                        state.Position = 0;
                        range = (0, end, end);
                        return true;
                    }
                }
            } while (state.Position > chunkStart);
        }
        range = (0, Math.Max(0, end), Math.Max(0, end));
        return end >= 0;
    }
}
