namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
    private static bool TryDecodeStringLiteral(string literal, [MaybeNullWhen(false)] out string value, out string message)
        => TryDecodeStringLiteral(literal, out value, out message, true);

    private static bool TryDecodeStringLiteral(
        string literal,
        [MaybeNullWhen(false)] out string value,
        out string message,
        bool decodeUnicodeEscapes)
    {
        value = string.Empty;
        message = "Invalid string literal. Malformed literal body.";

        if (literal.Length < 2)
        {
            return false;
        }

        var index = 0;
        var isRaw = false;
        if (literal[0] is 'r' or 'R')
        {
            isRaw = true;
            index = 1;
        }

        if (index >= literal.Length)
        {
            return false;
        }

        var quote = literal[index];
        var isTriple = index + 2 < literal.Length &&
            literal[index + 1] == quote &&
            literal[index + 2] == quote;
        var prefixLength = isTriple ? 3 : 1;
        var start = index + prefixLength;
        var end = literal.Length - prefixLength;

        if ((quote != '"' && quote != '\'') || end < start)
        {
            return false;
        }

        if (!isTriple && literal[^1] != quote)
        {
            return false;
        }

        if (isTriple &&
            (literal[^1] != quote || literal[^2] != quote || literal[^3] != quote))
        {
            return false;
        }

        if (isRaw)
        {
            // Tokenization has already established the closing delimiter. Raw strings
            // preserve every body code unit here, including backslash sequences.
            value = literal[start..end];
            return true;
        }

        var builder = new System.Text.StringBuilder(end - start);
        for (var i = start; i < end; i++)
        {
            var c = literal[i];
            if (c != '\\')
            {
                builder.Append(c);
                continue;
            }

            if (i + 1 >= end)
            {
                message = "Invalid string literal. Unfinished escape sequence.";
                return false;
            }

            i++;
            switch (literal[i])
            {
                case '\\':
                    builder.Append('\\');
                    break;
                case '\'':
                    builder.Append('\'');
                    break;
                case '"':
                    builder.Append('"');
                    break;
                case 'n':
                    builder.Append('\n');
                    break;
                case 'a':
                    builder.Append('\a');
                    break;
                case 'b':
                    builder.Append('\b');
                    break;
                case 'f':
                    builder.Append('\f');
                    break;
                case 'r':
                    builder.Append('\r');
                    break;
                case 't':
                    builder.Append('\t');
                    break;
                case 'v':
                    builder.Append('\v');
                    break;
                case 'x':
                    if (i + 2 >= end ||
                        !byte.TryParse(literal.AsSpan(i + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var hex))
                    {
                        message = "Invalid string literal. Malformed \\x escape; expected two hexadecimal digits.";
                        return false;
                    }

                    builder.Append((char)hex);
                    i += 2;
                    break;
                case 'u' when decodeUnicodeEscapes:
                    if (!TryReadHexCodePoint(literal, i + 1, 4, end, out var shortCodePoint))
                    {
                        message = "Invalid string literal. Malformed \\u escape; expected four hexadecimal digits.";
                        return false;
                    }

                    builder.Append((char)shortCodePoint);
                    i += 4;
                    break;
                case 'U' when decodeUnicodeEscapes:
                    if (!TryReadHexCodePoint(literal, i + 1, 8, end, out var longCodePoint) ||
                        longCodePoint > 0x10FFFF)
                    {
                        message = "Invalid string literal. Malformed \\U escape; expected eight hexadecimal digits naming a Unicode code point.";
                        return false;
                    }

                    if (longCodePoint <= char.MaxValue)
                    {
                        builder.Append((char)longCodePoint);
                    }
                    else
                    {
                        builder.Append(char.ConvertFromUtf32(longCodePoint));
                    }

                    i += 8;
                    break;
                default:
                    if (literal[i] is >= '0' and <= '7')
                    {
                        var octal = literal[i] - '0';
                        var digits = 1;
                        while (digits < 3 && i + 1 < end && literal[i + 1] is >= '0' and <= '7')
                        {
                            i++;
                            digits++;
                            octal = (octal * 8) + literal[i] - '0';
                        }

                        builder.Append((char)octal);
                    }
                    else
                    {
                        // Python preserves unknown escape sequences rather than silently
                        // dropping the backslash; diagnostics for those escapes are not fatal.
                        builder.Append('\\');
                        builder.Append(literal[i]);
                    }
                    break;
            }
        }

        value = builder.ToString();
        return true;
    }

    private static bool TryReadHexCodePoint(
        string literal,
        int start,
        int length,
        int end,
        out int codePoint)
    {
        codePoint = 0;
        if (start + length > end)
        {
            return false;
        }

        for (var i = start; i < start + length; i++)
        {
            var digit = literal[i] switch
            {
                >= '0' and <= '9' => literal[i] - '0',
                >= 'a' and <= 'f' => literal[i] - 'a' + 10,
                >= 'A' and <= 'F' => literal[i] - 'A' + 10,
                _ => -1
            };
            if (digit < 0)
            {
                return false;
            }

            codePoint = (codePoint * 16) + digit;
        }

        return true;
    }

    private static bool TryGetUnsupportedStringPrefix(string text, out string construct)
    {
        if (text.Length is > 0 and <= 3)
        {
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] is not ('r' or 'R' or 'b' or 'B' or 'f' or 'F' or 'u' or 'U'))
                {
                    construct = string.Empty;
                    return false;
                }
            }

            construct = text;
            return true;
        }

        construct = string.Empty;
        return false;
    }


    private static bool IsBytesStringPrefix(string text)
    {
        return text is "b" or "B" or "br" or "Br" or "bR" or "BR" or "rb" or "rB" or "Rb" or "RB";
    }

    private static bool IsFormattedStringPrefix(string text)
    {
        return text is "f" or "F" or "fr" or "Fr" or "fR" or "FR" or "rf" or "rF" or "Rf" or "RF";
    }

    private static bool TryDecodeBytesLiteral(string literal, out byte[] value, out string message)
    {
        value = Array.Empty<byte>();
        if (!TryExtractStringContent(literal, out var content))
        {
            message = "Invalid string literal. Malformed bytes literal body.";
            return false;
        }

        if (content.Any(static c => c > 0x7F))
        {
            message = "Invalid string literal. Bytes literals can only contain ASCII source characters.";
            return false;
        }

        if (!TryDecodeStringLiteral(literal, out var decoded, out message, decodeUnicodeEscapes: false))
        {
            return false;
        }

        value = new byte[decoded.Length];
        for (var i = 0; i < decoded.Length; i++)
        {
            value[i] = (byte)decoded[i];
        }

        return true;
    }

    private static bool TryExtractStringContent(string literal, out string content)
    {
        content = string.Empty;
        if (literal.Length < 2)
        {
            return false;
        }

        if ((literal.StartsWith("\"\"\"", StringComparison.Ordinal) && literal.EndsWith("\"\"\"", StringComparison.Ordinal)) ||
            (literal.StartsWith("'''", StringComparison.Ordinal) && literal.EndsWith("'''", StringComparison.Ordinal)))
        {
            content = literal[3..^3];
            return true;
        }

        if ((literal[0] == '"' && literal[^1] == '"') || (literal[0] == '\'' && literal[^1] == '\''))
        {
            content = literal[1..^1];
            return true;
        }

        return false;
    }

    private static bool TryDecodeEscapedText(string text, bool isRaw, [MaybeNullWhen(false)] out string value)
    {
        if (isRaw)
        {
            value = text;
            return true;
        }

        var builder = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\\')
            {
                builder.Append(text[i]);
                continue;
            }

            if (i + 1 >= text.Length)
            {
                value = string.Empty;
                return false;
            }

            i++;
            builder.Append(text[i] switch
            {
                '\\' => '\\',
                '\'' => '\'',
                '"' => '"',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => text[i],
            });
        }

        value = builder.ToString();
        return true;
    }

}
