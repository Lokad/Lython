using System.Text;
using Lokad.Parsing.Lexer;

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

    private static bool IsSupportedImport(string moduleName)
    {
        return !string.IsNullOrWhiteSpace(moduleName);
    }

    private static bool IsAugmentedAssignmentToken(Token token)
    {
        return token is
            Token.PlusEqual or
            Token.MinusEqual or
            Token.StarEqual or
            Token.SlashEqual or
            Token.PercentEqual or
            Token.SlashSlashEqual or
            Token.StarStarEqual or
            Token.AmpersandEqual or
            Token.PipeEqual or
            Token.CaretEqual or
            Token.LessLessEqual or
            Token.GreaterGreaterEqual;
    }

    private static bool TryMapAugmentedAssignmentOperator(Token token, out AugmentedAssignmentOperatorSyntax op)
    {
        switch (token)
        {
            case Token.PlusEqual:
                op = AugmentedAssignmentOperatorSyntax.Add;
                return true;
            case Token.MinusEqual:
                op = AugmentedAssignmentOperatorSyntax.Subtract;
                return true;
            case Token.StarEqual:
                op = AugmentedAssignmentOperatorSyntax.Multiply;
                return true;
            case Token.SlashEqual:
                op = AugmentedAssignmentOperatorSyntax.Divide;
                return true;
            case Token.SlashSlashEqual:
                op = AugmentedAssignmentOperatorSyntax.FloorDivide;
                return true;
            case Token.PercentEqual:
                op = AugmentedAssignmentOperatorSyntax.Modulo;
                return true;
            case Token.StarStarEqual:
                op = AugmentedAssignmentOperatorSyntax.Power;
                return true;
            case Token.PipeEqual:
                op = AugmentedAssignmentOperatorSyntax.BitwiseOr;
                return true;
            case Token.CaretEqual:
                op = AugmentedAssignmentOperatorSyntax.BitwiseXor;
                return true;
            case Token.AmpersandEqual:
                op = AugmentedAssignmentOperatorSyntax.BitwiseAnd;
                return true;
            case Token.LessLessEqual:
                op = AugmentedAssignmentOperatorSyntax.LeftShift;
                return true;
            case Token.GreaterGreaterEqual:
                op = AugmentedAssignmentOperatorSyntax.RightShift;
                return true;
            default:
                op = default;
                return false;
        }
    }

    private static bool TryGetUnsupportedStringPrefix(string text, out string construct)
    {
        if (text.Length is > 0 and <= 3)
        {
            for (var i = 0; i < text.Length; i++)
            {
                if (!IsStringPrefixLetter(text[i]))
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

    private static bool IsStringPrefixLetter(char c)
        => c is 'r' or 'R' or 'b' or 'B' or 'f' or 'F' or 'u' or 'U';

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

    private static bool TryParseFormattedStringLiteral(string prefix, string literal, out IReadOnlyList<FormattedStringPartSyntax> parts)
    {
        parts = Array.Empty<FormattedStringPartSyntax>();
        if (!TryExtractStringContent(literal, out var content))
        {
            return false;
        }

        return TryParseFormattedStringContent(prefix, content, allowNestedFormatFields: true, out parts);
    }

    private static bool TryParseFormattedStringContent(
        string prefix,
        string content,
        bool allowNestedFormatFields,
        out IReadOnlyList<FormattedStringPartSyntax> parts)
    {
        parts = Array.Empty<FormattedStringPartSyntax>();
        var isRaw = prefix.Contains('r', StringComparison.OrdinalIgnoreCase);

        var parsedParts = new List<FormattedStringPartSyntax>();
        var text = new System.Text.StringBuilder();

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (c == '{')
            {
                if (i + 1 < content.Length && content[i + 1] == '{')
                {
                    text.Append('{');
                    i++;
                    continue;
                }

                if (text.Length > 0)
                {
                    if (!TryDecodeEscapedText(text.ToString(), isRaw, out var decodedText))
                    {
                        return false;
                    }

                    parsedParts.Add(new FormattedStringTextPartSyntax(decodedText));
                    text.Clear();
                }

                if (!TryParseFormattedStringField(
                        prefix,
                        content,
                        i + 1,
                        allowNestedFormatFields,
                        out var end,
                        out var expressionPart,
                        out var debugText))
                {
                    return false;
                }

                if (debugText is not null)
                {
                    parsedParts.Add(new FormattedStringTextPartSyntax(debugText));
                }

                parsedParts.Add(expressionPart);
                i = end;
                continue;
            }

            if (c == '}')
            {
                if (i + 1 < content.Length && content[i + 1] == '}')
                {
                    text.Append('}');
                    i++;
                    continue;
                }

                return false;
            }

            text.Append(c);
        }

        if (text.Length > 0)
        {
            if (!TryDecodeEscapedText(text.ToString(), isRaw, out var decodedText))
            {
                return false;
            }

            parsedParts.Add(new FormattedStringTextPartSyntax(decodedText));
        }

        parts = parsedParts;
        return true;
    }

    private static bool TryParseFormattedStringField(
        string prefix,
        string content,
        int start,
        bool allowNestedFormatFields,
        out int end,
        [MaybeNullWhen(false)] out FormattedStringExpressionPartSyntax part,
        out string? debugText)
    {
        end = -1;
        part = null;
        debugText = null;

        if (!TryFindFormattedStringFieldEnd(content, start, out end))
        {
            return false;
        }

        var field = content[start..end];
        if (!TrySplitFormattedStringField(
                field,
                out var expressionText,
                out var conversion,
                out var formatSpecifier,
                out debugText) ||
            expressionText.Length == 0 ||
            !TryParseEmbeddedExpression(expressionText, out var expression))
        {
            return false;
        }

        IReadOnlyList<FormattedStringPartSyntax>? formatSpecifierParts = null;
        if (formatSpecifier is not null &&
            (formatSpecifier.Contains('{', StringComparison.Ordinal) || formatSpecifier.Contains('}', StringComparison.Ordinal)))
        {
            if (!allowNestedFormatFields ||
                !TryParseFormattedStringContent(
                    prefix,
                    formatSpecifier,
                    allowNestedFormatFields: false,
                    out formatSpecifierParts))
            {
                return false;
            }

            formatSpecifier = null;
        }

        if (debugText is not null && conversion is null && formatSpecifier is null && formatSpecifierParts is null)
        {
            conversion = 'r';
        }

        part = new FormattedStringExpressionPartSyntax(
            expression,
            conversion,
            formatSpecifier,
            formatSpecifierParts);
        return true;
    }

    private static bool TryFindFormattedStringFieldEnd(string content, int start, out int end)
    {
        end = -1;
        var parenDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;

        for (var i = start; i < content.Length; i++)
        {
            var c = content[i];
            if (c is '"' or '\'')
            {
                if (!TrySkipStringLiteral(content, ref i))
                {
                    return false;
                }

                continue;
            }

            switch (c)
            {
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    if (parenDepth == 0)
                    {
                        return false;
                    }

                    parenDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    if (bracketDepth == 0)
                    {
                        return false;
                    }

                    bracketDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    if (parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
                    {
                        end = i;
                        return true;
                    }

                    if (braceDepth == 0)
                    {
                        return false;
                    }

                    braceDepth--;
                    break;
            }
        }

        return false;
    }

    private static bool TrySplitFormattedStringField(
        string field,
        out string expressionText,
        out char? conversion,
        out string? formatSpecifier,
        out string? debugText)
    {
        expressionText = string.Empty;
        conversion = null;
        formatSpecifier = null;
        debugText = null;

        if (!TryFindFormattedStringSeparators(field, out var conversionIndex, out var formatIndex))
        {
            return false;
        }

        var expressionEnd = conversionIndex >= 0
            ? conversionIndex
            : formatIndex >= 0
                ? formatIndex
                : field.Length;
        var expressionSource = field[..expressionEnd];
        var debugEnd = expressionSource.Length - 1;
        while (debugEnd >= 0 && char.IsWhiteSpace(expressionSource[debugEnd]))
        {
            debugEnd--;
        }

        if (debugEnd >= 0 &&
            expressionSource[debugEnd] == '=' &&
            (debugEnd == 0 || expressionSource[debugEnd - 1] is not ('=' or '!' or '<' or '>' or ':')))
        {
            debugText = expressionSource;
            expressionSource = expressionSource[..debugEnd];
        }

        expressionText = expressionSource.Trim();

        if (conversionIndex >= 0)
        {
            if (conversionIndex + 1 >= field.Length)
            {
                return false;
            }

            conversion = field[conversionIndex + 1];
            if (conversion is not ('s' or 'r' or 'a'))
            {
                return false;
            }

            var afterConversion = conversionIndex + 2;
            if (afterConversion < field.Length)
            {
                if (field[afterConversion] != ':')
                {
                    return false;
                }

                formatIndex = afterConversion;
            }
        }

        if (formatIndex >= 0)
        {
            formatSpecifier = field[(formatIndex + 1)..];
        }

        return true;
    }

    private static bool TryFindFormattedStringSeparators(string field, out int conversionIndex, out int formatIndex)
    {
        conversionIndex = -1;
        formatIndex = -1;
        var parenDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;

        for (var i = 0; i < field.Length; i++)
        {
            var c = field[i];
            if (c is '"' or '\'')
            {
                if (!TrySkipStringLiteral(field, ref i))
                {
                    return false;
                }

                continue;
            }

            if (parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
            {
                if (c == ':' && formatIndex < 0)
                {
                    formatIndex = i;
                    return true;
                }

                if (c == '!' && i + 1 < field.Length && field[i + 1] != '=' && conversionIndex < 0)
                {
                    conversionIndex = i;
                    continue;
                }
            }

            switch (c)
            {
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    if (parenDepth == 0)
                    {
                        return false;
                    }

                    parenDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    if (bracketDepth == 0)
                    {
                        return false;
                    }

                    bracketDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    if (braceDepth == 0)
                    {
                        return false;
                    }

                    braceDepth--;
                    break;
            }
        }

        return parenDepth == 0 && bracketDepth == 0 && braceDepth == 0;
    }

    private static bool TrySkipStringLiteral(string text, ref int index)
    {
        var quote = text[index];
        var triple = index + 2 < text.Length && text[index + 1] == quote && text[index + 2] == quote;
        index += triple ? 3 : 1;

        while (index < text.Length)
        {
            if (text[index] == '\\')
            {
                index += 2;
                continue;
            }

            if (triple)
            {
                if (index + 2 < text.Length &&
                    text[index] == quote &&
                    text[index + 1] == quote &&
                    text[index + 2] == quote)
                {
                    index += 2;
                    return true;
                }

                index++;
                continue;
            }

            if (text[index] == quote)
            {
                return true;
            }

            index++;
        }

        return false;
    }

    private static bool TryParseEmbeddedExpression(string expressionText, [MaybeNullWhen(false)] out ExpressionSyntax expression)
    {
        expression = null;
        var frontend = LythonFrontend.Compile("value = " + expressionText + "\n");
        if (frontend.Script?.Statements is not [AssignmentStatementSyntax assignment] || frontend.Diagnostics.Count != 0)
        {
            return false;
        }

        expression = assignment.Expression;
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
