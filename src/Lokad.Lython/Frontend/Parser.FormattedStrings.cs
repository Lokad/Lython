namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
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
}
