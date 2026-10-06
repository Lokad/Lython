namespace Lokad.Lython.Frontend;

internal static class FormattedStringLexicalScanner
{
    internal static readonly char[] Whitespace = [' ', '\t', '\r', '\n', '\f'];
    internal static bool IsWhitespace(char c) => c is ' ' or '\t' or '\r' or '\n' or '\f';

    internal static void SkipComment(string text, ref int index)
    {
        while (index < text.Length && text[index] is not ('\r' or '\n')) index++;
        index--;
    }

    internal static bool TrySkipString(string text, ref int index, int depth = 0)
    {
        var prefixStart = index;
        while (prefixStart > 0 && index - prefixStart < 2 && text[prefixStart - 1] is 'f' or 'F' or 'r' or 'R') prefixStart--;
        var prefix = text[prefixStart..index];
        var formatted = prefix is "f" or "F" or "fr" or "Fr" or "fR" or "FR" or "rf" or "rF" or "Rf" or "RF";
        if (prefixStart > 0 && (char.IsLetterOrDigit(text[prefixStart - 1]) || text[prefixStart - 1] == '_')) formatted = false;
        if (!FormattedStringTokenization.TryFindEnd(text, index, formatted, out var end, out _, depth)) return false;
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
}
