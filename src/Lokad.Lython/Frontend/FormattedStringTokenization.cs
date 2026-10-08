using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal static class FormattedStringTokenization
{
    public static LexerResult<Token> Read(string source)
    {
        char[]? masked = null;
        var afterBackslash = false;
        var delimiterDepth = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (i == 0 || source[i - 1] == '\n')
            {
                var lastReset = -1;
                for (var end = i; end < source.Length && source[end] is ' ' or '\t' or '\f'; end++)
                    if (source[end] == '\f') lastReset = end;
                if (lastReset >= 0)
                {
                    masked ??= source.ToCharArray();
                    // The generic lexer ignores CR when counting indentation.
                    // A form feed resets the leading indentation accumulated so far.
                    Array.Fill(masked, '\r', i, lastReset - i + 1);
                }
            }
            if (source[i] == '\f')
            {
                // Whitespace after a line-continuation backslash remains invalid.
                if (!afterBackslash)
                {
                    masked ??= source.ToCharArray();
                    masked[i] = '\r';
                }
                continue;
            }
            if (source[i] == '\\') afterBackslash = true;
            else if (source[i] is not ' ' and not '\t') afterBackslash = false;
            if (source[i] == '#')
            {
                var commentStart = i;
                while (i < source.Length && source[i] != '\n') i++;
                if (delimiterDepth > 0)
                {
                    masked ??= source.ToCharArray();
                    // A joined newline must not let the generic comment rule
                    // consume the following physical line.
                    Array.Fill(masked, ' ', commentStart, i - commentStart);
                    if (i < source.Length) masked[i] = '\r';
                }
                continue;
            }
            if (source[i] == '\n' && delimiterDepth > 0 &&
                (i == 0 || source[i - 1] != '\\'))
            {
                masked ??= source.ToCharArray();
                // Python measures indentation at the start of the logical line.
                // Preserve offsets while hiding joined physical newlines from
                // the generic indentation lexer.
                masked[i] = '\r';
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
                // Leave malformed literal newlines visible to the string lexer.
                // They are not joined newlines in the surrounding expression.
                break;
            }
            if (source[i] is '\'' or '"')
            {
                if (!TryFindEnd(source, i, formatted: false, out var plainEnd, out _)) break;
                i = plainEnd - 1;
                continue;
            }
            if (source[i] is '(' or '[' or '{') delimiterDepth++;
            else if (source[i] is ')' or ']' or '}' && delimiterDepth > 0) delimiterDepth--;
        }
        var lexed = new ReflectionTokenReader<Token>().ReadAllTokens(masked is null ? source : new string(masked));
        if (masked is null) return lexed;
        var newlines = new List<int>();
        for (var i = 0; i < source.Length; i++) if (source[i] == '\n') newlines.Add(i);
        return new LexerResult<Token>(source, lexed.Tokens, newlines, lexed.HasInvalidTokens);
    }

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    internal static bool TryFindEnd(string source, int start, bool formatted, out int end, out int width, int depth = 0)
    {
        end = start;
        width = 0;
        if (depth >= Parser.MaxNestingDepth) return false;
        var quote = source[start];
        width = start + 2 < source.Length && source[start + 1] == quote && source[start + 2] == quote ? 3 : 1;
        for (var i = start + width; i < source.Length; i++)
        {
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
                else if (!Parser.TryFindFormattedStringFieldEnd(source, i + 1, out i, depth + 1, quote, width)) break;
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
