using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal static class FormattedStringTokenization
{
    public static LexerResult<Token> Read(string source)
    {
        char[]? masked = null;
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] == '#')
            {
                while (i < source.Length && source[i] != '\n') i++;
                continue;
            }
            var prefixLength = 0;
            if ((i == 0 || !IsIdentifierPart(source[i - 1])) && source[i] is 'f' or 'F' or 'r' or 'R')
            {
                if (source[i] is 'f' or 'F') prefixLength = 1;
                if (i + 1 < source.Length && ((source[i] is 'f' or 'F' && source[i + 1] is 'r' or 'R') ||
                    (source[i] is 'r' or 'R' && source[i + 1] is 'f' or 'F'))) prefixLength = 2;
            }
            var quoteStart = i + prefixLength;
            if (prefixLength > 0 && quoteStart < source.Length && source[quoteStart] is '\'' or '"')
            {
                if (TryFindEnd(source, quoteStart, formatted: true, out var end, out var width))
                {
                    masked ??= source.ToCharArray();
                    // Preserve offsets and the delimiter/prefix. The generic
                    // string lexer sees an opaque body, while Parser reads the
                    // original source including nested expression quotes.
                    Array.Fill(masked, ' ', quoteStart + width, end - quoteStart - 2 * width);
                    i = end - 1;
                    continue;
                }
            }
            if (source[i] is '\'' or '"' && TryFindEnd(source, i, formatted: false, out var plainEnd, out _))
                i = plainEnd - 1;
        }
        var lexed = new ReflectionTokenReader<Token>().ReadAllTokens(masked is null ? source : new string(masked));
        if (masked is null) return lexed;
        var newlines = new List<int>();
        for (var i = 0; i < source.Length; i++) if (source[i] == '\n') newlines.Add(i);
        return new LexerResult<Token>(source, lexed.Tokens, newlines, lexed.HasInvalidTokens);
    }

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static bool TryFindEnd(string source, int start, bool formatted, out int end, out int width)
    {
        var quote = source[start];
        width = start + 2 < source.Length && source[start + 1] == quote && source[start + 2] == quote ? 3 : 1;
        for (var i = start + width; i < source.Length; i++)
        {
            if (source[i] == '\\')
            {
                if (i + 1 < source.Length && (!formatted || source[i + 1] is not '{' and not '}')) i++;
                continue;
            }
            if (formatted && source[i] == '{')
            {
                if (i + 1 < source.Length && source[i + 1] == '{') i++;
                else if (!Parser.TryFindFormattedStringFieldEnd(source, i + 1, out i)) break;
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
