namespace Lokad.Lython.Frontend;

internal static class FormattedStringLexicalScanner
{
    internal static readonly char[] Whitespace = [' ', '\t', '\r', '\n', '\f'];
    internal static bool IsWhitespace(char c) => c is ' ' or '\t' or '\r' or '\n' or '\f';

    internal readonly record struct FieldBounds(int End, int Conversion, int Format, int LastExpressionCharacter);

    internal static void SkipComment(ReadOnlySpan<char> text, ref int index, CancellationToken cancellationToken = default)
    {
        while (index < text.Length && text[index] is not ('\r' or '\n'))
        { PythonTokenization.CheckCancellation(cancellationToken, index); index++; }
        index--;
    }

    private static bool TrySkipString(ReadOnlySpan<char> text, ref int index, int depth = 0, CancellationToken cancellationToken = default, List<int>? newlines = null)
    {
        var prefixStart = index;
        while (prefixStart > 0 && index - prefixStart < 2 && text[prefixStart - 1] is 'f' or 'F' or 'r' or 'R') prefixStart--;
        var prefix = text.Slice(prefixStart, index - prefixStart);
        var formatted = prefix is "f" or "F" or "fr" or "Fr" or "fR" or "FR" or "rf" or "rF" or "Rf" or "RF";
        if (prefixStart > 0 && (char.IsLetterOrDigit(text[prefixStart - 1]) || text[prefixStart - 1] == '_')) formatted = false;
        if (!TryFindStringEnd(text, index, formatted, out var end, out _, depth, cancellationToken, newlines)) return false;
        index = end - 1;
        return true;
    }

    internal static string DebugText(ReadOnlySpan<char> text, int marker, out int visibleMarker, CancellationToken cancellationToken)
    {
        visibleMarker = marker;
        var result = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            PythonTokenization.CheckCancellation(cancellationToken, i);
            if (i == marker) visibleMarker = result.Length;
            if (text[i] == '#')
            {
                SkipComment(text, ref i, cancellationToken);
                continue;
            }
            if (text[i] is '\'' or '"')
            {
                var start = i;
                if (!TrySkipString(text, ref i, cancellationToken: cancellationToken))
                {
                    visibleMarker = marker;
                    return text.ToString();
                }
                result.Append(text.Slice(start, i - start + 1));
            }
            else if (text[i] == '\r')
            {
                result.Append('\n');
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
            }
            else result.Append(text[i]);
        }
        return result.ToString();
    }

    internal static bool TryFindStringEnd(ReadOnlySpan<char> source, int start, bool formatted, out int end, out int width, int depth = 0,
        CancellationToken cancellationToken = default, List<int>? newlines = null)
    {
        end = start;
        width = 0;
        if (depth >= Parser.MaxNestingDepth) return false;
        var quote = source[start];
        width = start + 2 < source.Length && source[start + 1] == quote && source[start + 2] == quote ? 3 : 1;
        for (var i = start + width; i < source.Length; i++)
        {
            PythonTokenization.CheckCancellation(cancellationToken, i);
            var c = source[i];
            if (c == '\\')
            {
                if (i + 1 < source.Length && (!formatted || source[i + 1] is not '{' and not '}'))
                {
                    AdvanceEscape(source, ref i, newlines);
                }
                continue;
            }
            if (formatted && c == '{')
            {
                if (i + 1 < source.Length && source[i + 1] == '{') i++;
                else if (TryScanField(source, i + 1, out var field, depth + 1, quote, width, cancellationToken, newlines)) i = field.End;
                else break;
                continue;
            }
            if (c == quote && (width == 1 || (i + 2 < source.Length && source[i + 1] == quote && source[i + 2] == quote)))
            {
                end = i + width;
                return true;
            }
            if (width == 1 && c is '\n' or '\r') break;
            if (c == '\n') newlines?.Add(i);
        }
        end = start;
        return false;
    }

    internal static bool TryScanField(ReadOnlySpan<char> source, int start, out FieldBounds field, int depth = 0,
        char? outerQuote = null, int outerQuoteWidth = 0, CancellationToken cancellationToken = default, List<int>? newlines = null)
    {
        field = default;
        if (depth >= Parser.MaxNestingDepth) return false;
        var parens = 0;
        var brackets = 0;
        var braces = 0;
        var conversion = -1;
        var format = -1;
        var last = -1;

        for (var i = start; i < source.Length; i++)
        {
            PythonTokenization.CheckCancellation(cancellationToken, i);
            var c = source[i];
            if (c == '\n') newlines?.Add(i);
            if (format >= 0)
            {
                if (c == '\\' && i + 1 < source.Length && source[i + 1] is not ('{' or '}'))
                    AdvanceEscape(source, ref i, newlines);
                else if (outerQuoteWidth == 1 && c is '\r' or '\n' ||
                    c == outerQuote && (outerQuoteWidth == 1 ||
                        i + 2 < source.Length && source[i + 1] == c && source[i + 2] == c)) return false;
                else if (c == '{')
                {
                    if (!TryScanField(source, i + 1, out var nested, depth + 1, outerQuote, outerQuoteWidth,
                        cancellationToken, newlines)) return false;
                    i = nested.End;
                }
                else if (c == '}')
                {
                    field = new(i, conversion, format, last);
                    return true;
                }
                continue;
            }

            if (c == '#')
            {
                SkipComment(source, ref i, cancellationToken);
                continue;
            }
            if (c is '"' or '\'')
            {
                if (!TrySkipString(source, ref i, depth + 1, cancellationToken, newlines)) return false;
                if (conversion < 0) last = i;
                continue;
            }

            if (c is ':' or '!' or '}' && parens == 0 && brackets == 0 && braces == 0)
            {
                if (c == '}')
                {
                    field = new(i, conversion, format, last);
                    return true;
                }
                if (c == ':')
                {
                    format = i;
                    continue;
                }
                if (conversion < 0 && i + 1 < source.Length && source[i + 1] != '=')
                {
                    conversion = i;
                    continue;
                }
            }

            switch (c)
            {
                case '(': parens++; break;
                case ')': if (parens-- == 0) return false; break;
                case '[': brackets++; break;
                case ']': if (brackets-- == 0) return false; break;
                case '{': braces++; break;
                case '}': if (braces-- == 0) return false; break;
            }
            if (conversion < 0 && !IsWhitespace(c)) last = i;
        }

        return false;
    }

    private static void AdvanceEscape(ReadOnlySpan<char> source, ref int index, List<int>? newlines)
    {
        index++;
        if (source[index] == '\r' && index + 1 < source.Length && source[index + 1] == '\n') index++;
        if (source[index] == '\n') newlines?.Add(index);
    }
}
