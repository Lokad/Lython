using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
    private const string MalformedFormattedStringMessage = "Invalid string literal. Malformed f-string replacement field or unmatched brace.";

    private bool TryParseFormattedStringLiteral(string prefix, string literal, int literalStart, out IReadOnlyList<FormattedStringPartSyntax> parts, out string message)
    {
        parts = Array.Empty<FormattedStringPartSyntax>();
        message = MalformedFormattedStringMessage;
        if (!TryGetLiteralBody(literal, out var content))
        {
            return false;
        }

        var quoteWidth = literal.Length >= 3 && literal[0] == literal[1] && literal[0] == literal[2] ? 3 : 1;
        return TryParseFormattedStringContent(prefix, content.ToString(), literalStart + quoteWidth, inFormatSpecifier: false, allowNestedFormatFields: true, out parts, out message);
    }

    private bool TryParseFormattedStringContent(
        string prefix,
        string content,
        int contentStart,
        bool inFormatSpecifier,
        bool allowNestedFormatFields,
        out IReadOnlyList<FormattedStringPartSyntax> parts,
        out string message)
    {
        parts = Array.Empty<FormattedStringPartSyntax>();
        message = MalformedFormattedStringMessage;
        var isRaw = prefix.Contains('r', StringComparison.OrdinalIgnoreCase);

        var parsedParts = new List<FormattedStringPartSyntax>();
        var text = new System.Text.StringBuilder();

        for (var i = 0; i < content.Length; i++)
        {
            CheckCancellation(i);
            var c = content[i];
            if (!isRaw && c == '\\' && i + 1 < content.Length)
            {
                if (content[i + 1] == '\\')
                {
                    text.Append("\\\\");
                    i++;
                    continue;
                }
                if (content[i + 1] == 'N')
                {
                    message = UnsupportedNamedUnicodeEscapeMessage;
                    return false;
                }
            }
            if (c == '{')
            {
                if (!inFormatSpecifier && i + 1 < content.Length && content[i + 1] == '{')
                {
                    text.Append('{');
                    i++;
                    continue;
                }

                if (text.Length > 0)
                {
                    if (!TryDecodeEscapedText(text.ToString(), isRaw, out var decodedText, out message))
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
                        contentStart,
                        allowNestedFormatFields,
                        out var end,
                        out var expressionPart,
                        out var debugText,
                        out message))
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
                if (!inFormatSpecifier && i + 1 < content.Length && content[i + 1] == '}')
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
            if (!TryDecodeEscapedText(text.ToString(), isRaw, out var decodedText, out message))
            {
                return false;
            }

            parsedParts.Add(new FormattedStringTextPartSyntax(decodedText));
        }

        parts = parsedParts;
        return true;
    }

    private bool TryParseFormattedStringField(
        string prefix,
        string content,
        int start,
        int contentStart,
        bool allowNestedFormatFields,
        out int end,
        [MaybeNullWhen(false)] out FormattedStringExpressionPartSyntax part,
        out string? debugText,
        out string message)
    {
        end = -1;
        part = null;
        debugText = null;
        message = MalformedFormattedStringMessage;

        if (!FormattedStringLexicalScanner.TryScanField(content, start, out var bounds, cancellationToken: _cancellationToken) ||
            bounds.LastExpressionCharacter < 0 || !TryReadFieldConversion(content, bounds, out var conversion))
        {
            return false;
        }

        end = bounds.End;
        var expressionEnd = bounds.Conversion >= 0 ? bounds.Conversion : bounds.Format >= 0 ? bounds.Format : end;
        var source = content.AsSpan(start, expressionEnd - start);
        var whitespace = FormattedStringLexicalScanner.Whitespace.AsSpan();
        if (content[bounds.LastExpressionCharacter] == '=')
        {
            // Only a trailing, unquoted '=' can introduce debug text. Render
            // comments and original whitespace only for that candidate.
            var marker = bounds.LastExpressionCharacter - start;
            var visible = FormattedStringLexicalScanner.DebugText(source, marker, out var visibleMarker, _cancellationToken);
            var beforeMarker = visible.AsSpan(0, visibleMarker);
            if (beforeMarker.IsEmpty || beforeMarker[^1] is not ('=' or '!' or '<' or '>' or ':'))
            {
                if (beforeMarker.Trim(whitespace).IsEmpty) return false;
                debugText = visible;
                source = source[..marker];
            }
        }
        // Keep the physical newline of a trailing explicit continuation.
        var expressionText = source.TrimStart(whitespace);
        var expressionStart = contentStart + start + source.Length - expressionText.Length;
        if (expressionText.IsEmpty || !TryParseEmbeddedExpression(expressionText.ToString(), expressionStart, out var expression, out message))
        {
            return false;
        }

        var formatSpecifier = bounds.Format >= 0 ? content[(bounds.Format + 1)..end] : null;
        IReadOnlyList<FormattedStringPartSyntax>? formatSpecifierParts = null;
        if (formatSpecifier is not null &&
            (formatSpecifier.Contains('{', StringComparison.Ordinal) || formatSpecifier.Contains('}', StringComparison.Ordinal)))
        {
            if (!allowNestedFormatFields ||
                !TryParseFormattedStringContent(
                    prefix,
                    formatSpecifier,
                    contentStart + bounds.Format + 1,
                    inFormatSpecifier: true,
                    allowNestedFormatFields: false,
                    out formatSpecifierParts,
                    out message))
            {
                return false;
            }

            formatSpecifier = null;
        }
        else if (formatSpecifier is not null)
        {
            if (!TryDecodeEscapedText(formatSpecifier, prefix.Contains('r', StringComparison.OrdinalIgnoreCase), out var decodedSpecifier, out message))
                return false;
            formatSpecifier = decodedSpecifier;
        }

        if (debugText is not null && conversion is null && formatSpecifier is null && formatSpecifierParts is null)
        {
            conversion = 'r';
        }

        part = new FormattedStringExpressionPartSyntax(
            expression,
            conversion,
            formatSpecifier is null ? null : PyString.FromString(formatSpecifier),
            formatSpecifierParts);
        return true;
    }

    private bool TryReadFieldConversion(ReadOnlySpan<char> content, FormattedStringLexicalScanner.FieldBounds bounds, out char? conversion)
    {
        conversion = null;
        if (bounds.Conversion < 0) return true;
        if (bounds.Conversion + 1 >= bounds.End) return false;
        conversion = content[bounds.Conversion + 1];
        if (conversion is not ('s' or 'r' or 'a')) return false;

        var limit = bounds.Format >= 0 ? bounds.Format : bounds.End;
        for (var i = bounds.Conversion + 2; i < limit; i++)
        {
            CheckCancellation(i);
            if (content[i] == '#')
                FormattedStringLexicalScanner.SkipComment(content[..limit], ref i, _cancellationToken);
            else if (!FormattedStringLexicalScanner.IsWhitespace(content[i])) return false;
        }
        return true;
    }

    private bool TryParseEmbeddedExpression(string expressionText, int expressionStart, [MaybeNullWhen(false)] out ExpressionSyntax expression, out string message)
    {
        expression = null;
        message = MalformedFormattedStringMessage;
        // The enclosing syntax pass supplies the real scope, including yield legality.
        var tokens = PythonTokenization.ReadFormattedField(expressionText, _cancellationToken);
        if (tokens.HasInvalidTokens || tokens.GluedNumbers.Count != 0) return false;
        var parser = new Parser(tokens, _memoryGovernor, _cancellationToken)
        {
            _privateNames = _privateNames,
            _nestingDepth = _nestingDepth + 1,
            _spanSourceTokens = _spanSourceTokens ?? _tokens,
            _spanOffset = expressionStart
        };
        var field = parser.ParseTupleOrParenthesized();
        parser.CheckCancellation();
        if (field is null || parser.CurrentToken is not (Token.Eol or Token.End) || parser._diagnostics.Count != 0)
        {
            if (parser._diagnostics.Any(diagnostic => diagnostic.Message == UnsupportedNamedUnicodeEscapeMessage))
                message = UnsupportedNamedUnicodeEscapeMessage;
            else if (parser._diagnostics.Any(diagnostic => diagnostic.Code == "LA0003"))
                message = SyntaxNestingLimitMessage;
            return false;
        }

        expression = field is ParenthesizedExpressionSyntax parenthesized ? parenthesized.Inner : field;
        if (field is TupleLiteralExpressionSyntax tuple)
        {
            (_spanSourceTokens ?? _tokens).LineOfPosition(expressionStart, out var line, out var column);
            expression = tuple with { Span = new LythonSourceSpan(expressionStart,
                expressionText.AsSpan().TrimEnd(FormattedStringLexicalScanner.Whitespace).Length, line, column) };
        }
        return true;
    }
}
