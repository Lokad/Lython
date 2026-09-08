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

        if (IsNameToken(CurrentToken))
        {
            if (PeekToken(1) == Token.String)
            {
                var prefix = _tokens.GetString(_position);
                if (IsFormattedStringPrefix(prefix))
                {
                    var prefixToken = ReadToken();
                    var stringToken = ReadToken();
                    if (!TryParseFormattedStringLiteral(prefix, _tokens.GetString(stringToken), out var parts))
                    {
                        AddDiagnostic("LA1007", "Invalid string literal. Malformed f-string replacement field or unmatched brace.", prefixToken);
                        return null;
                    }

                    return new FormattedStringExpressionSyntax(parts, Merge(SpanOf(prefixToken), SpanOf(stringToken)));
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

    private ExpressionSyntax? ParseStringLiteralExpression()
    {
        var firstToken = -1;
        var lastToken = -1;
        var builder = new System.Text.StringBuilder();

        while (CurrentToken == Token.String)
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

            builder.Append(value);
            lastToken = tokenIndex;
        }

        return new StringLiteralExpressionSyntax(
            builder.ToString(),
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
