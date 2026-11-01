namespace Lokad.Lython.Frontend;

internal static class FormattedStringLexicalScanner
{
    internal static readonly char[] Whitespace = [' ', '\t', '\r', '\n', '\f'];
    internal static bool IsWhitespace(char c) => c is ' ' or '\t' or '\r' or '\n' or '\f';

    internal static void SkipComment(string text, ref int index, CancellationToken cancellationToken = default)
    {
        while (index < text.Length && text[index] is not ('\r' or '\n'))
        { PythonTokenization.CheckCancellation(cancellationToken, index); index++; }
        index--;
    }

    internal static bool TrySkipString(string text, ref int index, int depth = 0, CancellationToken cancellationToken = default)
    {
        var prefixStart = index;
        while (prefixStart > 0 && index - prefixStart < 2 && text[prefixStart - 1] is 'f' or 'F' or 'r' or 'R') prefixStart--;
        var prefix = text[prefixStart..index];
        var formatted = prefix is "f" or "F" or "fr" or "Fr" or "fR" or "FR" or "rf" or "rF" or "Rf" or "RF";
        if (prefixStart > 0 && (char.IsLetterOrDigit(text[prefixStart - 1]) || text[prefixStart - 1] == '_')) formatted = false;
        if (!TryFindStringEnd(text, index, formatted, out var end, out _, depth, cancellationToken)) return false;
        index = end - 1;
        return true;
    }

    internal static string DebugText(string text, out int lastEqualsIndex)
    {
        lastEqualsIndex = -1;
        var result = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '#')
            {
                SkipComment(text, ref i);
                continue;
            }
            if (text[i] is '\'' or '"')
            {
                var start = i;
                if (!TrySkipString(text, ref i)) return text;
                result.Append(text.AsSpan(start, i - start + 1));
            }
            else if (text[i] == '\r')
            {
                result.Append('\n');
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
            }
            else
            {
                if (text[i] == '=') lastEqualsIndex = i;
                result.Append(text[i]);
            }
        }
        return result.ToString();
    }

    internal static bool TryFindStringEnd(string source, int start, bool formatted, out int end, out int width, int depth = 0, CancellationToken cancellationToken = default)
    {
        end = start;
        width = 0;
        if (depth >= Parser.MaxNestingDepth) return false;
        var quote = source[start];
        width = start + 2 < source.Length && source[start + 1] == quote && source[start + 2] == quote ? 3 : 1;
        for (var i = start + width; i < source.Length; i++)
        {
            PythonTokenization.CheckCancellation(cancellationToken, i);
            if (source[i] == '\\')
            {
                if (i + 1 < source.Length && (!formatted || source[i + 1] is not '{' and not '}'))
                {
                    i++;
                    if (source[i] == '\r' && i + 1 < source.Length && source[i + 1] == '\n') i++;
                }
                continue;
            }
            if (formatted && source[i] == '{')
            {
                if (i + 1 < source.Length && source[i + 1] == '{') i++;
                else if (!Parser.TryFindFormattedStringFieldEnd(source, i + 1, out i, depth + 1, quote, width, cancellationToken)) break;
                continue;
            }
            if (source[i] == quote && (width == 1 || (i + 2 < source.Length && source[i + 1] == quote && source[i + 2] == quote)))
            {
                end = i + width;
                return true;
            }
            if (width == 1 && source[i] is '\n' or '\r') break;
        }
        end = start;
        return false;
    }
}
