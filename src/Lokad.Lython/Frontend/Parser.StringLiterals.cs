namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
    private const string UnsupportedNamedUnicodeEscapeMessage =
        "Unsupported named Unicode escape '\\N{...}'; use a Unicode character or numeric '\\u'/'\\U' escapes instead.";
    private const string UnsupportedSurrogateStringMessage =
        "Unsupported surrogate code point in string literal; Lython strings contain Unicode scalar values.";

    private static string StringLiteralDiagnosticCode(string message)
        => message is UnsupportedNamedUnicodeEscapeMessage or UnsupportedSurrogateStringMessage ? "LA2000"
            : message == SyntaxNestingLimitMessage ? "LA0003"
            : "LA1007";

    private bool TryDecodeStringLiteral(ReadOnlySpan<char> literal, [MaybeNullWhen(false)] out string value, out string message)
    {
        CheckCancellation();
        var isRaw = !literal.IsEmpty && literal[0] is 'r' or 'R';
        if (isRaw) literal = literal[1..];
        if (!TryGetLiteralBody(literal, out var body))
        {
            value = string.Empty;
            message = "Invalid string literal. Malformed literal body.";
            return false;
        }
        return TryDecodeStringBody(body, isRaw, out value, out message);
    }

    private bool TryDecodeStringBody(ReadOnlySpan<char> literal, bool isRaw,
        [MaybeNullWhen(false)] out string value, out string message)
    {
        CheckCancellation();
        value = string.Empty;
        // Borrow the token body until its one AST value is materialized.
        if (isRaw || literal.IndexOf('\\') < 0)
        {
            if (!ValidateScalarString(literal, out message)) return false;
            value = literal.ToString();
            return true;
        }

        var builder = new System.Text.StringBuilder(literal.Length);
        for (var i = 0; i < literal.Length; i++)
        {
            CheckCancellation(i);
            var character = literal[i];
            if (character != '\\')
            {
                builder.Append(character);
                continue;
            }

            var count = ReadLiteralEscape(literal, ref i, unicode: true, out var first, out var second, out message);
            if (count < 0) return false;
            if (count >= 1) builder.Append(first);
            if (count == 2) builder.Append(second);
        }
        value = builder.ToString();
        return ValidateScalarString(value, out message);
    }

    // Advance from a backslash to the final source character of its escape.
    // Return the number of UTF-16 units, or -1 for an invalid escape.
    private static int ReadLiteralEscape(ReadOnlySpan<char> literal, ref int index, bool unicode,
        out char first, out char second, out string message)
    {
        first = second = default;
        message = string.Empty;
        if (++index == literal.Length)
        {
            message = "Invalid string literal. Unfinished escape sequence.";
            return -1;
        }

        var escape = literal[index];
        switch (escape)
        {
            case '\n': return 0;
            case '\\' or '\'' or '"': first = escape; return 1;
            case 'n': first = '\n'; return 1;
            case 'a': first = '\a'; return 1;
            case 'b': first = '\b'; return 1;
            case 'f': first = '\f'; return 1;
            case 'r': first = '\r'; return 1;
            case 't': first = '\t'; return 1;
            case 'v': first = '\v'; return 1;
            case 'x':
                if (!TryReadHexCodePoint(literal, index + 1, 2, out var hex))
                {
                    message = "Invalid string literal. Malformed \\x escape; expected two hexadecimal digits.";
                    return -1;
                }
                first = (char)hex;
                index += 2;
                return 1;
            case 'N' when unicode:
                message = UnsupportedNamedUnicodeEscapeMessage;
                return -1;
            case 'u' or 'U' when unicode:
                var digits = escape == 'u' ? 4 : 8;
                if (!TryReadHexCodePoint(literal, index + 1, digits, out var codePoint) || (uint)codePoint > 0x10FFFF)
                {
                    message = escape == 'u'
                        ? "Invalid string literal. Malformed \\u escape; expected four hexadecimal digits."
                        : "Invalid string literal. Malformed \\U escape; expected eight hexadecimal digits naming a Unicode code point.";
                    return -1;
                }
                if (codePoint is >= 0xD800 and <= 0xDFFF)
                {
                    message = UnsupportedSurrogateStringMessage;
                    return -1;
                }
                index += digits;
                if (codePoint <= char.MaxValue)
                {
                    first = (char)codePoint;
                    return 1;
                }
                codePoint -= 0x10000;
                first = (char)(0xD800 + (codePoint >> 10));
                second = (char)(0xDC00 + (codePoint & 0x3FF));
                return 2;
            case >= '0' and <= '7':
                var octal = escape - '0';
                for (var i = 1; i < 3 && index + 1 < literal.Length && literal[index + 1] is >= '0' and <= '7'; i++)
                    octal = (octal * 8) + literal[++index] - '0';
                first = (char)octal;
                return 1;
            default:
                // Unknown escapes preserve the backslash, including Unicode
                // escape spelling in bytes.
                first = '\\';
                second = escape;
                return 2;
        }
    }

    private bool ValidateScalarString(ReadOnlySpan<char> text, out string message)
    {
        for (var i = 0; i < text.Length; i++)
        {
            CheckCancellation(i);
            if (!char.IsSurrogate(text[i])) continue;
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
                continue;
            }
            message = UnsupportedSurrogateStringMessage;
            return false;
        }
        message = string.Empty;
        return true;
    }

    private static bool TryReadHexCodePoint(
        ReadOnlySpan<char> literal,
        int start,
        int length,
        out int codePoint)
    {
        codePoint = 0;
        if (length > literal.Length - start)
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

    private bool TryDecodeBytesLiteral(ReadOnlySpan<char> literal, out byte[] value, out string message, bool isRaw)
    {
        CheckCancellation();
        value = [];
        if (!TryGetLiteralBody(literal, out var body))
        {
            message = "Invalid string literal. Malformed bytes literal body.";
            return false;
        }

        // Reject non-ASCII source before interpreting any escape. The decoder
        // can shorten the output and cannot exceed the token body's capacity.
        for (var offset = 0; offset < body.Length;)
        {
            CheckCancellation();
            var count = Math.Min(4096, body.Length - offset);
            if (!System.Text.Ascii.IsValid(body.Slice(offset, count)))
            {
                message = "Invalid string literal. Bytes literals can only contain ASCII source characters.";
                return false;
            }
            offset += count;
        }

        var bytes = body.IsEmpty ? Array.Empty<byte>() : new byte[body.Length];
        var written = 0;
        for (var i = 0; i < body.Length; i++)
        {
            CheckCancellation(i);
            var character = body[i];
            if (isRaw || character != '\\')
            {
                bytes[written++] = (byte)character;
                continue;
            }
            var count = ReadLiteralEscape(body, ref i, unicode: false, out var first, out var second, out message);
            if (count < 0) return false;
            if (count >= 1) bytes[written++] = (byte)first;
            if (count == 2) bytes[written++] = (byte)second;
        }

        if (written == bytes.Length) value = bytes;
        else if (written != 0)
        {
            value = new byte[written];
            CopyLiteralBytes(bytes.AsSpan(0, written), value);
        }
        message = string.Empty;
        return true;
    }

    private void CopyLiteralBytes(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        for (var offset = 0; offset < source.Length;)
        {
            CheckCancellation();
            var count = Math.Min(4096, source.Length - offset);
            source.Slice(offset, count).CopyTo(destination.Slice(offset, count));
            offset += count;
        }
    }

    private static bool TryGetLiteralBody(ReadOnlySpan<char> literal, out ReadOnlySpan<char> body)
    {
        body = default;
        if (literal.Length < 2 || literal[0] is not ('"' or '\'')) return false;

        var quote = literal[0];
        var triple = literal.Length >= 3 && literal[1] == quote && literal[2] == quote;
        var width = triple ? 3 : 1;
        if (literal.Length < 2 * width || literal[^1] != quote ||
            triple && (literal[^2] != quote || literal[^3] != quote)) return false;
        body = literal[width..^width];
        return true;
    }

    private bool TryDecodeEscapedText(string text, bool isRaw, [MaybeNullWhen(false)] out string value, out string message)
    {
        if (isRaw)
        {
            value = text;
            return ValidateScalarString(value, out message);
        }

        // A parsed f-string text segment already is a body, not another token.
        return TryDecodeStringBody(text, isRaw: false, out value, out message);
    }

}
