using System.Text;
using Lokad.Lython.Runtime;

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
            return ParseIntegerLiteral(ReadToken());
        }

        if (CurrentToken == Token.Imaginary)
        {
            var imaginaryToken = ReadToken();
            var spelling = _tokens.GetString(imaginaryToken);
            return new ImaginaryLiteralExpressionSyntax(ParseFloatingLiteral(spelling, imaginary: true), SpanOf(imaginaryToken));
        }

        if (CurrentToken == Token.Float)
        {
            var tokenIndex = ReadToken();
            var spelling = _tokens.GetString(tokenIndex);
            return new FloatLiteralExpressionSyntax(ParseFloatingLiteral(spelling, imaginary: false), spelling, SpanOf(tokenIndex));
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
            return new EllipsisLiteralExpressionSyntax(Merge(firstDot, lastDot));
        }

        if (IsNameToken(CurrentToken))
        {
            if (PeekToken(1) == Token.String)
            {
                var prefix = _tokens.GetString(_position);
                if (IsFormattedStringPrefix(prefix) || prefix is "u" or "U")
                {
                    return ParseStringLiteralExpression();
                }

                if (IsBytesStringPrefix(prefix))
                {
                    return ParseBytesLiteralExpression();
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
            AddDiagnostic("LA1000", $"Unexpected closing delimiter {TokenNamer.TokenName(CurrentToken)}.", _position);
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

    private ExpressionSyntax? ParseBytesLiteralExpression()
    {
        var firstToken = _position;
        var lastToken = firstToken;
        byte[]? first = null;
        List<byte[]>? rest = null;
        var length = 0;
        while (IsNameToken(CurrentToken) && PeekToken(1) == Token.String &&
            IsBytesStringPrefix(_tokens.GetString(_position)))
        {
            var prefix = _tokens.GetString(_position);
            var prefixToken = ReadToken();
            var stringToken = ReadToken();
            if (_tokens.Tokens[prefixToken].Start + _tokens.Tokens[prefixToken].Length != _tokens.Tokens[stringToken].Start)
            {
                AddDiagnostic("LA1007", "String prefix must be adjacent to its literal.", prefixToken);
                return null;
            }
            if (!TryDecodeBytesLiteral(_tokens.GetSpan(stringToken), out var bytes, out var message,
                isRaw: prefix.Contains('r') || prefix.Contains('R')))
            {
                AddDiagnostic("LA1007", message, prefixToken);
                return null;
            }
            if (first is null) first = bytes;
            else (rest ??= []).Add(bytes);
            length = checked(length + bytes.Length);
            lastToken = stringToken;
        }
        if (CurrentToken == Token.String ||
            IsNameToken(CurrentToken) && PeekToken(1) == Token.String &&
            (_tokens.GetString(_position) is "u" or "U" || IsFormattedStringPrefix(_tokens.GetString(_position))))
        {
            AddDiagnostic("LA1007", "Invalid string literal. Cannot mix bytes and nonbytes literals.", _position);
            return null;
        }

        // Copy once, keeping implicit concatenation linear in literal size.
        var firstPiece = first.RequireNotNull();
        var combined = firstPiece;
        if (rest is not null)
        {
            combined = new byte[length];
            CopyLiteralBytes(firstPiece, combined);
            var offset = firstPiece.Length;
            foreach (var piece in rest)
            {
                CheckCancellation();
                CopyLiteralBytes(piece, combined.AsSpan(offset));
                offset += piece.Length;
            }
        }
        return new BytesLiteralExpressionSyntax(new PyBytes(combined), Merge(firstToken, lastToken));
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
        var text = string.Empty;
        System.Text.StringBuilder? adjacent = null;
        List<FormattedStringPartSyntax>? parts = null;

        while (true)
        {
            var prefix = IsNameToken(CurrentToken) && PeekToken(1) == Token.String
                ? _tokens.GetString(_position) : null;
            if (CurrentToken == Token.String || prefix is "u" or "U")
            {
                var pieceStart = _position;
                if (prefix is not null)
                {
                    var unicodePrefixToken = ReadToken();
                    if (_tokens.Tokens[unicodePrefixToken].Start + _tokens.Tokens[unicodePrefixToken].Length != _tokens.Tokens[_position].Start)
                    {
                        AddDiagnostic("LA1007", "String prefix must be adjacent to its literal.", unicodePrefixToken);
                        return null;
                    }
                }
                var tokenIndex = ReadToken();
                if (firstToken < 0) firstToken = pieceStart;
                if (!TryDecodeStringLiteral(_tokens.GetSpan(tokenIndex), out var value, out var message))
                {
                    AddDiagnostic(StringLiteralDiagnosticCode(message), message, tokenIndex);
                    return null;
                }
                if (adjacent is not null) adjacent.Append(value);
                else if (text.Length == 0) text = value;
                else adjacent = new System.Text.StringBuilder(text).Append(value);
                lastToken = tokenIndex;
                continue;
            }

            if (prefix is not null)
            {
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
                if (_tokens.Tokens[prefixToken].Start + _tokens.Tokens[prefixToken].Length != _tokens.Tokens[stringToken].Start)
                {
                    AddDiagnostic("LA1007", "String prefix must be adjacent to its literal.", prefixToken);
                    return null;
                }
                if (firstToken < 0)
                {
                    firstToken = prefixToken;
                }

                var start = _tokens.Tokens[stringToken].Start + _spanOffset;
                if (!TryParseFormattedStringLiteral(prefix, _tokens.GetString(stringToken), start, out var segmentParts, out var formattedMessage))
                {
                    AddDiagnostic(StringLiteralDiagnosticCode(formattedMessage), formattedMessage, prefixToken);
                    return null;
                }

                parts ??= [];
                if (adjacent is not null) { text = adjacent.ToString(); adjacent = null; }
                if (text.Length > 0)
                {
                    parts.Add(new FormattedStringTextPartSyntax(text));
                    text = string.Empty;
                }

                parts.AddRange(segmentParts);
                lastToken = stringToken;
                continue;
            }

            break;
        }

        if (adjacent is not null) text = adjacent.ToString();
        if (parts is null)
        {
            return new StringLiteralExpressionSyntax(
                text,
                Merge(firstToken, lastToken));
        }

        if (text.Length > 0)
        {
            parts.Add(new FormattedStringTextPartSyntax(text));
        }

        return new FormattedStringExpressionSyntax(
            parts,
            Merge(firstToken, lastToken));
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

        return new LambdaExpressionSyntax(parameters, body, Merge(lambdaToken, body.Span));
    }
}
