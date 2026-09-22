using System.Text;
using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
    private ExpressionSyntax? ParsePrimaryExpression()
    {
        if (TryParseUnsupportedExpression(out var unsupported))
        {
            return unsupported;
        }

        if (CurrentToken == Token.Lambda)
        {
            return ParseLambdaExpression();
        }

        if (CurrentToken == Token.String)
        {
            return ParseStringLiteralExpression();
        }

        if (CurrentToken == Token.Integer)
        {
            var tokenIndex = ReadToken();
            return new IntegerLiteralExpressionSyntax(
                _tokens.GetString(tokenIndex),
                SpanOf(tokenIndex));
        }

        if (CurrentToken == Token.Float)
        {
            var tokenIndex = ReadToken();
            return new FloatLiteralExpressionSyntax(
                _tokens.GetString(tokenIndex),
                SpanOf(tokenIndex));
        }

        if (CurrentToken == Token.True)
        {
            var tokenIndex = ReadToken();
            return new BooleanLiteralExpressionSyntax(true, SpanOf(tokenIndex));
        }

        if (CurrentToken == Token.False)
        {
            var tokenIndex = ReadToken();
            return new BooleanLiteralExpressionSyntax(false, SpanOf(tokenIndex));
        }

        if (CurrentToken == Token.None)
        {
            var tokenIndex = ReadToken();
            return new NoneLiteralExpressionSyntax(SpanOf(tokenIndex));
        }

        // An ellipsis is three dots with no lexer support of its own;
        // member access still starts from a parsed target expression.
        if (CurrentToken == Token.Dot && PeekToken(1) == Token.Dot && PeekToken(2) == Token.Dot)
        {
            var firstDot = ReadToken();
            ReadToken();
            var lastDot = ReadToken();
            return new EllipsisLiteralExpressionSyntax(Merge(SpanOf(firstDot), SpanOf(lastDot)));
        }

        if (IsNameToken(CurrentToken))
        {
            if (PeekToken(1) == Token.String)
            {
                var prefix = _tokens.GetString(_position);
                if (IsFormattedStringPrefix(prefix))
                {
                    return ParseStringLiteralExpression();
                }

                if (IsBytesStringPrefix(prefix))
                {
                    var prefixToken = ReadToken();
                    var stringToken = ReadToken();
                    if (!TryDecodeBytesLiteral(_tokens.GetString(stringToken), out var bytes, out var message))
                    {
                        AddDiagnostic("LA1007", message, prefixToken);
                        return null;
                    }

                    return new BytesLiteralExpressionSyntax(bytes, Merge(SpanOf(prefixToken), SpanOf(stringToken)));
                }

                if (TryGetUnsupportedStringPrefix(prefix, out var unsupportedPrefix))
                {
                    AddDiagnostic("LA1007", $"Invalid string literal. Unsupported string prefix '{unsupportedPrefix}'.", _position);
                    return null;
                }
            }

            var tokenIndex = ReadToken();
            return new IdentifierExpressionSyntax(
                IdentifierText(tokenIndex),
                SpanOf(tokenIndex));
        }

        if (CurrentToken == Token.OpenBracket)
        {
            return ParseListLiteral();
        }

        if (CurrentToken == Token.OpenBrace)
        {
            return new BraceDisplayParser(this).Parse();
        }

        if (CurrentToken == Token.OpenParen)
        {
            return ParseTupleOrParenthesized();
        }

        if (CurrentToken is Token.CloseParen or Token.CloseBracket or Token.CloseBrace)
        {
            AddDiagnostic("LA1000", $"Unexpected closing delimiter {TokenNamer.Instance.TokenName(CurrentToken, Array.Empty<Token>())}.", _position);
            return null;
        }

        if (CurrentToken == Token.Indent)
        {
            AddDiagnostic("LA1000", "Unexpected indentation.", _position);
            return null;
        }

        if (CurrentToken == Token.Dedent)
        {
            AddDiagnostic("LA1000", "Unexpected dedentation.", _position);
            return null;
        }

        AddDiagnostic("LA1000", "Expected expression.", _position);
        return null;
    }

    // Adjacent literal pieces fold left-to-right exactly like CPython implicit
    // concatenation. Plain text accumulates into one constant, while any
    // formatted piece lifts the whole run into a single formatted node whose
    // parts preserve source order (and therefore exactly-once evaluation).
    // Bytes-prefixed pieces never mix with text. Callers enter with either a
    // plain string token or a formatted-prefix pair, so at least one piece is
    // always consumed.
    private ExpressionSyntax? ParseStringLiteralExpression()
    {
        var firstToken = -1;
        var lastToken = -1;
        var text = new System.Text.StringBuilder();
        var parts = new List<FormattedStringPartSyntax>();
        var hasFormatted = false;

        while (true)
        {
            if (CurrentToken == Token.String)
            {
                var tokenIndex = ReadToken();
                if (firstToken < 0)
                {
                    firstToken = tokenIndex;
                }

                var literal = _tokens.GetString(tokenIndex);
                if (!TryDecodeStringLiteral(literal, out var value, out var message))
                {
                    AddDiagnostic("LA1007", message, tokenIndex);
                    return null;
                }

                text.Append(value);
                lastToken = tokenIndex;
                continue;
            }

            if (IsNameToken(CurrentToken) && PeekToken(1) == Token.String)
            {
                var prefix = _tokens.GetString(_position);
                if (!IsFormattedStringPrefix(prefix))
                {
                    if (IsBytesStringPrefix(prefix))
                    {
                        AddDiagnostic("LA1007", "Invalid string literal. Cannot mix bytes and nonbytes literals.", _position);
                        return null;
                    }

                    break;
                }

                var prefixToken = ReadToken();
                var stringToken = ReadToken();
                if (firstToken < 0)
                {
                    firstToken = prefixToken;
                }

                if (!TryParseFormattedStringLiteral(prefix, _tokens.GetString(stringToken), out var segmentParts))
                {
                    AddDiagnostic("LA1007", "Invalid string literal. Malformed f-string replacement field or unmatched brace.", prefixToken);
                    return null;
                }

                if (text.Length > 0)
                {
                    parts.Add(new FormattedStringTextPartSyntax(text.ToString()));
                    text.Clear();
                }

                parts.AddRange(segmentParts);
                hasFormatted = true;
                lastToken = stringToken;
                continue;
            }

            break;
        }

        if (!hasFormatted)
        {
            return new StringLiteralExpressionSyntax(
                text.ToString(),
                Merge(SpanOf(firstToken), SpanOf(lastToken)));
        }

        if (text.Length > 0)
        {
            parts.Add(new FormattedStringTextPartSyntax(text.ToString()));
            text.Clear();
        }

        return new FormattedStringExpressionSyntax(
            parts,
            Merge(SpanOf(firstToken), SpanOf(lastToken)));
    }

    private ExpressionSyntax? ParseLambdaExpression()
    {
        var lambdaToken = ReadToken();
        if (!TryParseFunctionParameters(Token.Colon, "lambda", allowAnnotations: false, out var parameters, out _))
        {
            return null;
        }

        var diagnosticCount = _diagnostics.Count;
        ExpressionSyntax? body;
        if (!EnterNestingDepth(_position))
        {
            return null;
        }

        try
        {
            body = ParseExpression();
        }
        finally
        {
            LeaveNestingDepth();
        }

        if (body is null)
        {
            if (_diagnostics.Count == diagnosticCount)
            {
                AddDiagnostic("LA1072", "Expected expression body in lambda.", _position);
            }

            return null;
        }

        return new LambdaExpressionSyntax(parameters, body, Merge(SpanOf(lambdaToken), body.Span));
    }
}
