namespace Lokad.Lython.Runtime.Text;

/// <summary>
/// Pure UTF-8 line-boundary scan shared by text and gzip readers (and future
/// ZIP text consumers) so newline semantics cannot drift between handle kinds.
/// Positions are byte offsets into UTF-8 data; rune-aware slicing stays with
/// the callers. Returns the offset just past the line terminator, or
/// <c>source.Length</c> when no terminator follows <c>startByte</c>.
/// </summary>
internal static class TextLineScanning
{
    public static int FindLineEndByte(ReadOnlySpan<byte> source, int startByte, LythonRuntime.TextNewlineMode newline)
    {
        for (var i = startByte; i < source.Length; i++)
        {
            if (source[i] == (byte)'\n' &&
                newline is LythonRuntime.TextNewlineMode.TranslateUniversal or LythonRuntime.TextNewlineMode.PreserveUniversal or LythonRuntime.TextNewlineMode.PreserveLineFeed)
            {
                return i + 1;
            }

            if (source[i] != (byte)'\r')
            {
                continue;
            }

            if (newline == LythonRuntime.TextNewlineMode.PreserveCarriageReturn)
            {
                return i + 1;
            }

            if (newline == LythonRuntime.TextNewlineMode.PreserveCarriageReturnLineFeed)
            {
                if (i + 1 < source.Length && source[i + 1] == (byte)'\n')
                {
                    return i + 2;
                }

                continue;
            }

            if (newline == LythonRuntime.TextNewlineMode.PreserveUniversal)
            {
                return i + 1 < source.Length && source[i + 1] == (byte)'\n' ? i + 2 : i + 1;
            }
        }

        return source.Length;
    }
}
