using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static PyString TranslateWildcardPattern(PyString pattern, ExecutionContext context, LythonSourceSpan span)
    {
        context.State.CancellationToken.ThrowIfCancellationRequested();
        using var builder = new GovernedByteBuilder(context.MemoryGovernor, span,
            (int)Math.Min(int.MaxValue, pattern.Utf8Bytes.Length + 16L), context.State.CancellationToken);
        builder.Append("(?s:"u8);
        AppendWildcardSegment(builder, pattern.Utf8Bytes.Span, [], context.State.CancellationToken);
        builder.Append(")\\Z"u8);
        return builder.ToPyStringAndRelease();
    }

    private static PyString TranslateGlobPattern(PyString pattern, bool recursive, bool includeHidden,
        PyString? separators, ExecutionContext context, LythonSourceSpan span)
    {
        var bytes = pattern.Utf8Bytes.Span;
        var seps = separators is null ? "/"u8 : separators.Utf8Bytes.Span;
        var token = context.State.CancellationToken;
        // Admit a useful initial capacity once. Unusual separator/class expansion
        // still uses the builder's existing funded growth and replacement overlap.
        using var builder = new GovernedByteBuilder(context.MemoryGovernor, span,
            (int)Math.Min(int.MaxValue, bytes.Length + 64L), token);
        builder.Append("(?s:"u8);
        var start = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var end = start;
            var next = start;
            while (end < bytes.Length)
            {
                token.ThrowIfCancellationRequested();
                next = end;
                var scalar = ReadWildcardScalar(bytes, ref next);
                if (IsWildcardSeparator(scalar, seps, token)) break;
                end = next;
            }
            var last = end == bytes.Length;
            var segment = bytes[start..end];
            if (segment.SequenceEqual("*"u8)) AppendOneGlobSegment(builder, seps, includeHidden, !last, token);
            else if (recursive && segment.SequenceEqual("**"u8))
            {
                if (last)
                {
                    if (includeHidden) builder.Append(".*"u8);
                    else
                    {
                        AppendAnyGlobSegments(builder, seps, includeHidden, token);
                        builder.Append("(?:"u8);
                        AppendOneGlobSegment(builder, seps, includeHidden, false, token);
                        builder.Append(")?"u8);
                    }
                }
                else if (!NextGlobSegmentIsRecursive(bytes, next, seps, token))
                    AppendAnyGlobSegments(builder, seps, includeHidden, token);
            }
            else
            {
                if (!segment.IsEmpty)
                {
                    if (!includeHidden && segment[0] is (byte)'*' or (byte)'?') builder.Append("(?!\\.)"u8);
                    AppendWildcardSegment(builder, segment, seps, token);
                }
                if (!last) AppendWildcardSeparator(builder, seps, token);
            }
            if (last) break;
            start = next;
        }
        builder.Append(")\\Z"u8);
        return builder.ToPyStringAndRelease();
    }

    private static bool NextGlobSegmentIsRecursive(ReadOnlySpan<byte> pattern, int start,
        ReadOnlySpan<byte> separators, CancellationToken token)
    {
        if (!pattern[start..].StartsWith("**"u8)) return false;
        var next = start + 2;
        return next == pattern.Length || IsWildcardSeparator(ReadWildcardScalar(pattern, ref next), separators, token);
    }

    private static bool IsWildcardSeparator(int scalar, ReadOnlySpan<byte> separators, CancellationToken token)
    {
        for (var index = 0; index < separators.Length;)
        {
            token.ThrowIfCancellationRequested();
            if (ReadWildcardScalar(separators, ref index) == scalar) return true;
        }
        return false;
    }

    private static void AppendOneGlobSegment(GovernedByteBuilder builder, ReadOnlySpan<byte> separators,
        bool includeHidden, bool trailingSeparator, CancellationToken token)
    {
        if (includeHidden)
        {
            AppendWildcardNonSeparator(builder, separators, token);
            builder.Append((byte)'+');
        }
        else
        {
            builder.Append("[^"u8);
            AppendWildcardClassScalars(builder, separators, token);
            builder.Append(".]"u8);
            AppendWildcardNonSeparator(builder, separators, token);
            builder.Append((byte)'*');
        }
        if (trailingSeparator) AppendWildcardSeparator(builder, separators, token);
    }

    private static void AppendAnyGlobSegments(GovernedByteBuilder builder, ReadOnlySpan<byte> separators,
        bool includeHidden, CancellationToken token)
    {
        builder.Append("(?:"u8);
        if (includeHidden)
        {
            builder.Append(".+"u8);
            AppendWildcardSeparator(builder, separators, token);
            builder.Append(")?"u8);
        }
        else
        {
            AppendOneGlobSegment(builder, separators, false, true, token);
            builder.Append(")*"u8);
        }
    }

    // Empty separators select fnmatch's unrestricted star/question semantics.
    // Both callers own valid immutable UTF-8; only syntax bytes need decoding
    // outside classes, so ordinary literals copy directly into the final storage.
    private static void AppendWildcardSegment(GovernedByteBuilder builder, ReadOnlySpan<byte> pattern,
        ReadOnlySpan<byte> separators, CancellationToken token)
    {
        var noClosingBracketRemaining = false;
        for (var index = 0; index < pattern.Length;)
        {
            token.ThrowIfCancellationRequested();
            var ch = pattern[index++];
            switch (ch)
            {
                case (byte)'*':
                    if (separators.IsEmpty) builder.Append((byte)'.');
                    else AppendWildcardNonSeparator(builder, separators, token);
                    builder.Append((byte)'*');
                    break;
                case (byte)'?':
                    if (separators.IsEmpty) builder.Append((byte)'.');
                    else AppendWildcardNonSeparator(builder, separators, token);
                    break;
                case (byte)'[':
                    if (!noClosingBracketRemaining && TryAppendWildcardClass(builder, pattern, index, token, out var next))
                        index = next;
                    else
                    {
                        noClosingBracketRemaining = true;
                        builder.Append("\\["u8);
                    }
                    break;
                default:
                    if (".^$*+?{}[]\\|()"u8.Contains(ch)) builder.Append((byte)'\\');
                    builder.Append(ch);
                    break;
            }
        }
    }

    private static bool TryAppendWildcardClass(GovernedByteBuilder builder, ReadOnlySpan<byte> pattern,
        int contentStart, CancellationToken token, out int next)
    {
        next = contentStart;
        if (contentStart == pattern.Length) return false;
        var negated = pattern[contentStart] == (byte)'!';
        if (negated) contentStart++;
        if (contentStart == pattern.Length) return false;
        var close = contentStart;
        if (pattern[close] == (byte)']') close++;
        for (; close < pattern.Length && pattern[close] != (byte)']'; close++) token.ThrowIfCancellationRequested();
        if (close == pattern.Length) return false;

        var outputStart = builder.Length;
        builder.Append((byte)'[');
        if (negated) builder.Append((byte)'^');
        var outputContentStart = builder.Length;
        for (var index = contentStart; index < close;)
        {
            token.ThrowIfCancellationRequested();
            var first = ReadWildcardScalar(pattern, ref index);
            if (index + 1 < close && pattern[index] == (byte)'-')
            {
                index++;
                var last = ReadWildcardScalar(pattern, ref index);
                if (first <= last)
                {
                    AppendWildcardClassScalar(builder, first);
                    builder.Append((byte)'-');
                    AppendWildcardClassScalar(builder, last);
                }
            }
            else AppendWildcardClassScalar(builder, first);
        }
        if (builder.Length == outputContentStart)
        {
            builder.Truncate(outputStart);
            builder.Append(negated ? "."u8 : "(?!)"u8);
        }
        else builder.Append((byte)']');
        next = close + 1;
        return true;
    }

    private static void AppendWildcardClassScalar(GovernedByteBuilder builder, int scalar)
    {
        if (scalar is '\\' or '[' or ']' or '^' or '-' or '&' or '~' or '|') builder.Append((byte)'\\');
        builder.Append(new Rune(scalar));
    }

    private static void AppendWildcardClassScalars(GovernedByteBuilder builder, ReadOnlySpan<byte> value, CancellationToken token)
    {
        for (var index = 0; index < value.Length;)
        {
            token.ThrowIfCancellationRequested();
            AppendWildcardClassScalar(builder, ReadWildcardScalar(value, ref index));
        }
    }

    private static void AppendWildcardSeparator(GovernedByteBuilder builder, ReadOnlySpan<byte> separators, CancellationToken token)
    {
        var index = 0;
        var first = ReadWildcardScalar(separators, ref index);
        // Preserve the previous spelling for single BMP versus supplementary
        // separators, although either spelling has the same match semantics.
        if (index == separators.Length && first <= char.MaxValue)
        {
            if (first < 128 && ".^$*+?{}[]\\|()"u8.Contains((byte)first)) builder.Append((byte)'\\');
            builder.Append(new Rune(first));
        }
        else
        {
            builder.Append((byte)'[');
            AppendWildcardClassScalars(builder, separators, token);
            builder.Append((byte)']');
        }
    }

    private static void AppendWildcardNonSeparator(GovernedByteBuilder builder, ReadOnlySpan<byte> separators, CancellationToken token)
    {
        builder.Append("[^"u8);
        AppendWildcardClassScalars(builder, separators, token);
        builder.Append((byte)']');
    }

    private static int ReadWildcardScalar(ReadOnlySpan<byte> text, ref int index)
    {
        if (text[index] < 128) return text[index++];
        if (Rune.DecodeFromUtf8(text[index..], out var rune, out var width) != System.Buffers.OperationStatus.Done)
            throw new InvalidOperationException("Malformed internal UTF-8 string.");
        index += width;
        return rune.Value;
    }
}
