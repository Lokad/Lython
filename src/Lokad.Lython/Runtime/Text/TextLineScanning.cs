namespace Lokad.Lython.Runtime.Text;

/// <summary>
/// Pure UTF-8 line-boundary scan shared by text, gzip and ZIP text readers
/// so newline semantics cannot drift between handle kinds.
/// Positions are byte offsets into UTF-8 data; rune-aware slicing stays with
/// the callers. Returns the offset just past the line terminator, or
/// <c>source.Length</c> when no terminator follows <c>startByte</c>.
/// </summary>
internal static class TextLineScanning
{
    public static int FindLineEndByte(ReadOnlySpan<byte> source, int startByte, LythonRuntime.TextNewlineMode newline)
    {
        if (startByte >= source.Length) return source.Length;
        var remaining = source[startByte..];
        var offset = newline switch
        {
            LythonRuntime.TextNewlineMode.TranslateUniversal or LythonRuntime.TextNewlineMode.PreserveLineFeed
                => remaining.IndexOf((byte)'\n'),
            LythonRuntime.TextNewlineMode.PreserveCarriageReturn => remaining.IndexOf((byte)'\r'),
            LythonRuntime.TextNewlineMode.PreserveCarriageReturnLineFeed => remaining.IndexOf("\r\n"u8),
            LythonRuntime.TextNewlineMode.PreserveUniversal => remaining.IndexOfAny((byte)'\r', (byte)'\n'),
            _ => -1,
        };
        if (offset < 0) return source.Length;
        if (newline == LythonRuntime.TextNewlineMode.PreserveCarriageReturnLineFeed)
            return startByte + offset + 2;

        var end = startByte + offset + 1;
        return newline == LythonRuntime.TextNewlineMode.PreserveUniversal && remaining[offset] == (byte)'\r'
            && end < source.Length && source[end] == (byte)'\n' ? end + 1 : end;
    }
}
