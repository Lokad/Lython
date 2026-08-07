using System.Text;
using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
    private ExpressionSyntax? ParseExpression()
    {
        var expression = ParseOrExpression();
        if (expression is null)
        {
            return null;
        }

        if (TryGetUnsupportedTrailingExpressionConstruct(CurrentToken, out var construct))
        {
            AddDiagnostic("LA2000", $"Unsupported Python construct '{construct}'.", _position);
            return null;
        }

        if (CurrentToken == Token.ColonEqual)
        {
            var operatorToken = ReadToken();
            if (expression is not IdentifierExpressionSyntax identifier)
            {
                AddDiagnostic("LA2000", "Unsupported Python construct 'assignment expression target'.", operatorToken);
                return null;
            }

            var assignedExpression = ParseExpression();
            if (assignedExpression is null)
            {
                AddDiagnostic("LA1046", "Expected expression after ':='.", operatorToken);
                return null;
            }

            expression = new AssignmentExpressionSyntax(
                identifier.Name,
                assignedExpression,
                Merge(expression.Span, assignedExpression.Span));
        }

        if (CurrentToken == Token.If)
        {
            var ifToken = ReadToken();
            var condition = ParseOrExpression();
            if (condition is null)
            {
                AddDiagnostic("LA1010", "Expected condition after 'if'.", ifToken);
                return null;
            }

            if (!TryRead(Token.Else, out var elseToken))
            {
                AddDiagnostic("LA2000", "Unsupported Python construct 'conditional expression'.", ifToken);
                return null;
            }

            var alternative = ParseExpression();
            if (alternative is null)
            {
                AddDiagnostic("LA1016", "Expected expression after 'else'.", elseToken);
                return null;
            }

            expression = new ConditionalExpressionSyntax(expression, condition, alternative, Merge(expression.Span, alternative.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseExpressionList()
    {
        var firstIsUnpacking = CurrentToken == Token.Star;
        if (firstIsUnpacking)
        {
            ReadToken();
        }

        var first = ParseExpression();
        if (first is null)
        {
            return null;
        }

        if (CurrentToken != Token.Comma)
        {
            if (firstIsUnpacking)
            {
                AddDiagnostic("LA2000", "Unsupported Python construct 'bare starred expression'.", first.Span);
                return null;
            }

            return first;
        }

        var items = new List<ExpressionSyntax> { first };
        var unpackingFlags = new List<bool> { firstIsUnpacking };
        var endSpan = first.Span;
        while (CurrentToken == Token.Comma)
        {
            var commaToken = ReadToken();
            endSpan = SpanOf(commaToken);
            if (CurrentToken is Token.Eol or Token.Semicolon or Token.Dedent or Token.End or Token.Colon)
            {
                break;
            }

            var isUnpacking = CurrentToken == Token.Star;
            if (isUnpacking)
            {
                ReadToken();
            }

            var item = ParseExpression();
            if (item is null)
            {
                AddDiagnostic("LA1004", "Expected expression after ','.", commaToken);
                return null;
            }

            items.Add(item);
            unpackingFlags.Add(isUnpacking);
            endSpan = item.Span;
        }

        return new TupleLiteralExpressionSyntax(items, unpackingFlags, Merge(first.Span, endSpan));
    }

    private ExpressionSyntax? ParseOrExpression()
    {
        var expression = ParseAndExpression();
        if (expression is null)
        {
            return null;
        }

        while (CurrentToken == Token.Or)
        {
            ReadToken();
            var right = ParseAndExpression();
            if (right is null)
            {
                AddDiagnostic("LA1037", "Expected expression after 'or'.", _position);
                return null;
            }

            expression = new BinaryExpressionSyntax(expression, BinaryOperatorSyntax.Or, right, Merge(expression.Span, right.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseAndExpression()
    {
        var expression = ParseComparisonExpression();
        if (expression is null)
        {
            return null;
        }

        while (CurrentToken == Token.And)
        {
            ReadToken();
            var right = ParseComparisonExpression();
            if (right is null)
            {
                AddDiagnostic("LA1038", "Expected expression after 'and'.", _position);
                return null;
            }

            expression = new BinaryExpressionSyntax(expression, BinaryOperatorSyntax.And, right, Merge(expression.Span, right.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseComparisonExpression()
    {
        var expression = ParseBitwiseOrExpression();
        if (expression is null)
        {
            return null;
        }

        if (CurrentToken != Token.Less &&
            CurrentToken != Token.LessEqual &&
            CurrentToken != Token.Greater &&
            CurrentToken != Token.GreaterEqual &&
            CurrentToken != Token.EqualEqual &&
            CurrentToken != Token.BangEqual &&
            CurrentToken != Token.In &&
            CurrentToken != Token.Is &&
            CurrentToken != Token.Not)
        {
            return expression;
        }

        var operands = new List<ExpressionSyntax> { expression };
        var operators = new List<BinaryOperatorSyntax>();

        while (CurrentToken is Token.Less or Token.LessEqual or Token.Greater or Token.GreaterEqual or Token.EqualEqual or Token.BangEqual or Token.In or Token.Is or Token.Not)
        {
            var operatorToken = ReadToken();
            BinaryOperatorSyntax op;
            if (_tokens.Tokens[operatorToken].Token == Token.Not)
            {
                if (!TryRead(Token.In, out var inToken))
                {
                    AddDiagnostic("LA1039", "Expected 'in' after 'not'.", operatorToken);
                    return null;
                }

                op = BinaryOperatorSyntax.NotIn;
                operatorToken = inToken;
            }
            else if (_tokens.Tokens[operatorToken].Token == Token.Is)
            {
                if (CurrentToken == Token.Not)
                {
                    ReadToken();
                    op = BinaryOperatorSyntax.IsNot;
                }
                else
                {
                    op = BinaryOperatorSyntax.Is;
                }
            }
            else
            {
                op = _tokens.Tokens[operatorToken].Token switch
                {
                    Token.EqualEqual => BinaryOperatorSyntax.Equal,
                    Token.BangEqual => BinaryOperatorSyntax.NotEqual,
                    Token.Less => BinaryOperatorSyntax.Less,
                    Token.LessEqual => BinaryOperatorSyntax.LessEqual,
                    Token.Greater => BinaryOperatorSyntax.Greater,
                    Token.GreaterEqual => BinaryOperatorSyntax.GreaterEqual,
                    Token.In => BinaryOperatorSyntax.In,
                    _ => throw new InvalidOperationException()
                };
            }

            var right = ParseBitwiseOrExpression();
            if (right is null)
            {
                AddDiagnostic("LA1040", "Expected expression after comparison operator.", operatorToken);
                return null;
            }

            operators.Add(op);
            operands.Add(right);
            expression = right;
        }

        return operators.Count == 1
            ? new BinaryExpressionSyntax(operands[0], operators[0], operands[1], Merge(operands[0].Span, operands[1].Span))
            : new ChainedComparisonExpressionSyntax(operands, operators, Merge(operands[0].Span, operands[^1].Span));
    }

    private ExpressionSyntax? ParseBitwiseOrExpression()
    {
        var expression = ParseBitwiseXorExpression();
        if (expression is null)
        {
            return null;
        }

        while (CurrentToken == Token.Pipe)
        {
            var operatorKind = ReadToken();
            var right = ParseBitwiseXorExpression();
            if (right is null)
            {
                AddDiagnostic("LA1061", "Expected expression after bitwise '|'.", operatorKind);
                return null;
            }

            expression = new BinaryExpressionSyntax(expression, BinaryOperatorSyntax.BitwiseOr, right, Merge(expression.Span, right.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseBitwiseXorExpression()
    {
        var expression = ParseBitwiseAndExpression();
        if (expression is null)
        {
            return null;
        }

        while (CurrentToken == Token.Caret)
        {
            var operatorKind = ReadToken();
            var right = ParseBitwiseAndExpression();
            if (right is null)
            {
                AddDiagnostic("LA1062", "Expected expression after bitwise '^'.", operatorKind);
                return null;
            }

            expression = new BinaryExpressionSyntax(expression, BinaryOperatorSyntax.BitwiseXor, right, Merge(expression.Span, right.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseBitwiseAndExpression()
    {
        var expression = ParseShiftExpression();
        if (expression is null)
        {
            return null;
        }

        while (CurrentToken == Token.Ampersand)
        {
            var operatorKind = ReadToken();
            var right = ParseShiftExpression();
            if (right is null)
            {
                AddDiagnostic("LA1063", "Expected expression after bitwise '&'.", operatorKind);
                return null;
            }

            expression = new BinaryExpressionSyntax(expression, BinaryOperatorSyntax.BitwiseAnd, right, Merge(expression.Span, right.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseShiftExpression()
    {
        var expression = ParseAdditiveExpression();
        if (expression is null)
        {
            return null;
        }

        while (CurrentToken is Token.LessLess or Token.GreaterGreater)
        {
            var operatorKind = ReadToken();
            var right = ParseAdditiveExpression();
            if (right is null)
            {
                AddDiagnostic("LA1064", "Expected expression after shift operator.", operatorKind);
                return null;
            }

            expression = new BinaryExpressionSyntax(
                expression,
                _tokens.Tokens[operatorKind].Token == Token.LessLess ? BinaryOperatorSyntax.LeftShift : BinaryOperatorSyntax.RightShift,
                right,
                Merge(expression.Span, right.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseAdditiveExpression()
    {
        var expression = ParseMultiplicativeExpression();
        if (expression is null)
        {
            return null;
        }

        while (CurrentToken is Token.Plus or Token.Minus)
        {
            var operatorKind = ReadToken();
            var right = ParseMultiplicativeExpression();
            if (right is null)
            {
                AddDiagnostic("LA1041", "Expected expression after additive operator.", operatorKind);
                return null;
            }

            expression = new BinaryExpressionSyntax(
                expression,
                _tokens.Tokens[operatorKind].Token == Token.Plus ? BinaryOperatorSyntax.Add : BinaryOperatorSyntax.Subtract,
                right,
                Merge(expression.Span, right.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseMultiplicativeExpression()
    {
        var expression = ParseUnaryExpression();
        if (expression is null)
        {
            return null;
        }

        while (CurrentToken is Token.Star or Token.Slash or Token.SlashSlash or Token.Percent)
        {
            var operatorKind = ReadToken();
            var right = ParseUnaryExpression();
            if (right is null)
            {
                AddDiagnostic("LA1042", "Expected expression after multiplicative operator.", operatorKind);
                return null;
            }

            var op = _tokens.Tokens[operatorKind].Token switch
            {
                Token.Star => BinaryOperatorSyntax.Multiply,
                Token.Slash => BinaryOperatorSyntax.Divide,
                Token.SlashSlash => BinaryOperatorSyntax.FloorDivide,
                Token.Percent => BinaryOperatorSyntax.Modulo,
                _ => throw new InvalidOperationException()
            };

            expression = new BinaryExpressionSyntax(expression, op, right, Merge(expression.Span, right.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseUnaryExpression()
    {
        if (CurrentToken == Token.Not)
        {
            var notToken = ReadToken();
            var operand = ParseUnaryExpression();
            if (operand is null)
            {
                AddDiagnostic("LA1043", "Expected expression after 'not'.", notToken);
                return null;
            }

            return new UnaryExpressionSyntax(UnaryOperatorSyntax.Not, operand, Merge(SpanOf(notToken), operand.Span));
        }

        if (CurrentToken is Token.Plus or Token.Minus)
        {
            var operatorToken = ReadToken();
            var operand = ParseUnaryExpression();
            if (operand is null)
            {
                AddDiagnostic("LA1044", "Expected expression after unary operator.", operatorToken);
                return null;
            }

            return new UnaryExpressionSyntax(
                _tokens.Tokens[operatorToken].Token == Token.Plus ? UnaryOperatorSyntax.Plus : UnaryOperatorSyntax.Minus,
                operand,
                Merge(SpanOf(operatorToken), operand.Span));
        }

        if (CurrentToken == Token.Tilde)
        {
            var operatorToken = ReadToken();
            var operand = ParseUnaryExpression();
            if (operand is null)
            {
                AddDiagnostic("LA1065", "Expected expression after '~'.", operatorToken);
                return null;
            }

            return new UnaryExpressionSyntax(UnaryOperatorSyntax.BitwiseNot, operand, Merge(SpanOf(operatorToken), operand.Span));
        }

        return ParsePowerExpression();
    }

    private ExpressionSyntax? ParsePowerExpression()
    {
        var expression = ParsePostfixExpression();
        if (expression is null)
        {
            return null;
        }

        if (CurrentToken != Token.StarStar)
        {
            return expression;
        }

        var operatorToken = ReadToken();
        var right = ParseUnaryExpression();
        if (right is null)
        {
            AddDiagnostic("LA1066", "Expected expression after '**'.", operatorToken);
            return null;
        }

        return new BinaryExpressionSyntax(expression, BinaryOperatorSyntax.Power, right, Merge(expression.Span, right.Span));
    }

    private ExpressionSyntax? ParsePostfixExpression()
    {
        var expression = ParsePrimaryExpression();
        if (expression is null)
        {
            return null;
        }

        while (true)
        {
            if (CurrentToken == Token.Dot)
            {
                var dotToken = ReadToken();
                if (!TryReadMemberName(out var memberToken))
                {
                    AddDiagnostic("LA1005", "Expected attribute name after '.'.", dotToken);
                    return null;
                }

                expression = new MemberExpressionSyntax(
                    expression,
                    IdentifierText(memberToken),
                    Merge(expression.Span, SpanOf(memberToken)));
                continue;
            }

            if (CurrentToken == Token.OpenParen)
            {
                var openParenToken = ReadToken();
                var arguments = new List<CallArgumentSyntax>();
                SkipGroupedExpressionTrivia();

                if (CurrentToken != Token.CloseParen)
                {
                    var sawKeywordArgument = false;
                    while (true)
                    {
                        var form = CallArgumentForm.Positional;
                        if (CurrentToken == Token.StarStar)
                        {
                            ReadToken();
                            form = CallArgumentForm.StarredDictionary;
                            sawKeywordArgument = true;
                        }
                        else if (CurrentToken == Token.Star)
                        {
                            if (sawKeywordArgument)
                            {
                                AddDiagnostic("LA2000", "Unsupported Python construct 'positional argument after keyword argument'.", _position);
                                return null;
                            }

                            ReadToken();
                            form = CallArgumentForm.StarredList;
                        }
                        else if (IsNameToken(CurrentToken) && PeekToken(1) == Token.Assign)
                        {
                            var nameToken = ReadToken();
                            ReadToken();
                            form = CallArgumentForm.Keyword(IdentifierText(nameToken));
                            sawKeywordArgument = true;
                        }
                        else if (sawKeywordArgument)
                        {
                            AddDiagnostic("LA2000", "Unsupported Python construct 'positional argument after keyword argument'.", _position);
                            return null;
                        }

                        var argument = ParseExpression();
                        if (argument is null)
                        {
                            return null;
                        }

                        SkipGroupedExpressionTrivia();
                        if (CurrentToken == Token.For && form.Kind == CallArgumentKind.Positional)
                        {
                            if (!TryParseComprehensionClauses(out var clauses, out _))
                            {
                                return null;
                            }

                            argument = new GeneratorExpressionSyntax(
                                argument,
                                clauses,
                                Merge(argument.Span, clauses[^1].Span));
                        }
                        SkipGroupedExpressionTrivia();

                        arguments.Add(new CallArgumentSyntax(form, argument));

                        if (CurrentToken != Token.Comma)
                        {
                            break;
                        }

                        ReadToken();
                        SkipGroupedExpressionTrivia();
                        if (CurrentToken == Token.CloseParen)
                        {
                            break;
                        }
                    }
                }

                SkipGroupedExpressionTrivia();
                if (!TryRead(Token.CloseParen, out var closeParenToken))
                {
                    AddDiagnostic("LA1006", "Expected ')' after call arguments.", openParenToken);
                    return null;
                }

                expression = new CallExpressionSyntax(
                    expression,
                    arguments,
                    Merge(expression.Span, SpanOf(closeParenToken)));
                continue;
            }

            if (CurrentToken == Token.OpenBracket)
            {
                var openBracketToken = ReadToken();
                SkipGroupedExpressionTrivia();
                ExpressionSyntax? start = null;
                if (CurrentToken != Token.Colon)
                {
                    start = ParseExpression();
                    if (start is null)
                    {
                        AddDiagnostic("LA1022", "Expected index expression after '['.", openBracketToken);
                        return null;
                    }
                    SkipGroupedExpressionTrivia();
                }

                if (CurrentToken == Token.Colon)
                {
                    ReadToken();
                    SkipGroupedExpressionTrivia();

                    ExpressionSyntax? end = null;
                    if (CurrentToken != Token.CloseBracket && CurrentToken != Token.Colon)
                    {
                        end = ParseExpression();
                        if (end is null)
                        {
                            AddDiagnostic("LA1022", "Expected index expression after '['.", openBracketToken);
                            return null;
                        }
                        SkipGroupedExpressionTrivia();
                    }

                    ExpressionSyntax? step = null;
                    if (CurrentToken == Token.Colon)
                    {
                        ReadToken();
                        SkipGroupedExpressionTrivia();
                        if (CurrentToken != Token.CloseBracket)
                        {
                            step = ParseExpression();
                            if (step is null)
                            {
                                AddDiagnostic("LA1022", "Expected index expression after '['.", openBracketToken);
                                return null;
                            }
                            SkipGroupedExpressionTrivia();
                        }
                    }

                    SkipGroupedExpressionTrivia();
                    if (!TryRead(Token.CloseBracket, out var closeSliceToken))
                    {
                        AddDiagnostic("LA1023", "Expected ']' after index expression.", openBracketToken);
                        return null;
                    }

                    expression = new SliceExpressionSyntax(
                        expression,
                        start,
                        end,
                        step,
                        Merge(expression.Span, SpanOf(closeSliceToken)));
                    continue;
                }

                if (start is null)
                {
                    AddDiagnostic("LA1022", "Expected index expression after '['.", openBracketToken);
                    return null;
                }

                if (CurrentToken == Token.Comma)
                {
                    var items = new List<ExpressionSyntax> { start };
                    while (CurrentToken == Token.Comma)
                    {
                        ReadToken();
                        SkipGroupedExpressionTrivia();
                        if (CurrentToken == Token.CloseBracket)
                        {
                            break;
                        }

                        var next = ParseExpression();
                        if (next is null)
                        {
                            AddDiagnostic("LA1022", "Expected index expression after '['.", openBracketToken);
                            return null;
                        }
                        SkipGroupedExpressionTrivia();

                        items.Add(next);
                    }

                    start = new TupleLiteralExpressionSyntax(
                        items,
                        Enumerable.Repeat(false, items.Count).ToArray(),
                        Merge(items[0].Span, items[^1].Span));
                }

                SkipGroupedExpressionTrivia();
                if (!TryRead(Token.CloseBracket, out var closeBracketToken))
                {
                    AddDiagnostic("LA1023", "Expected ']' after index expression.", openBracketToken);
                    return null;
                }

                expression = new SubscriptExpressionSyntax(
                    expression,
                    start,
                    Merge(expression.Span, SpanOf(closeBracketToken)));
                continue;
            }

            break;
        }

        return expression;
    }

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
            return ParseDictLiteral();
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

        var body = ParseExpression();
        if (body is null)
        {
            AddDiagnostic("LA1072", "Expected expression body in lambda.", _position);
            return null;
        }

        return new LambdaExpressionSyntax(parameters, body, Merge(SpanOf(lambdaToken), body.Span));
    }

    private ExpressionSyntax? ParseListLiteral()
    {
        var openBracket = ReadToken();
        var items = new List<ExpressionSyntax>();
        var unpackingFlags = new List<bool>();
        SkipGroupedExpressionTrivia();

        if (CurrentToken == Token.End)
        {
            AddDiagnostic("LA1021", "Unexpected end of file while parsing list literal; expected ']'.", openBracket);
            return null;
        }

        if (CurrentToken != Token.CloseBracket)
        {
            while (true)
            {
                if (CurrentToken == Token.End)
                {
                    AddDiagnostic("LA1021", "Unexpected end of file while parsing list literal; expected ']'.", openBracket);
                    return null;
                }

                var isUnpacking = CurrentToken == Token.Star;
                if (isUnpacking)
                {
                    ReadToken();
                }

                var item = ParseExpression();
                if (item is null)
                {
                    return null;
                }

                items.Add(item);
                unpackingFlags.Add(isUnpacking);
                SkipGroupedExpressionTrivia();

                if (CurrentToken == Token.For)
                {
                    if (items.Count != 1 || isUnpacking)
                    {
                        AddDiagnostic("LA2000", "Unsupported Python construct 'iterable unpacking in comprehension'.", _position);
                        return null;
                    }

                    if (!TryParseComprehensionClauses(out var clauses, out _))
                    {
                        return null;
                    }

                    SkipGroupedExpressionTrivia();
                    if (!TryRead(Token.CloseBracket, out var closeComprehension))
                    {
                        AddDiagnostic("LA1021", "Expected ']' after list literal.", openBracket);
                        return null;
                    }

                    return new ListComprehensionExpressionSyntax(
                        item,
                        clauses,
                        Merge(SpanOf(openBracket), SpanOf(closeComprehension)));
                }

                if (CurrentToken != Token.Comma)
                {
                    break;
                }

                ReadToken();
                SkipGroupedExpressionTrivia();
                if (CurrentToken == Token.CloseBracket)
                {
                    break;
                }
            }
        }

        SkipGroupedExpressionTrivia();
        if (!TryRead(Token.CloseBracket, out var closeBracket))
        {
            AddDiagnostic("LA1021", "Expected ']' after list literal.", openBracket);
            return null;
        }

        return new ListLiteralExpressionSyntax(items, unpackingFlags, Merge(SpanOf(openBracket), SpanOf(closeBracket)));
    }

    private ExpressionSyntax? ParseTupleOrParenthesized()
    {
        var openParen = ReadToken();
        SkipGroupedExpressionTrivia();

        if (CurrentToken == Token.End)
        {
            AddDiagnostic("LA1008", "Unexpected end of file while parsing parenthesized expression; expected ')'.", openParen);
            return null;
        }

        if (CurrentToken == Token.CloseParen)
        {
            var closeEmpty = ReadToken();
            return new TupleLiteralExpressionSyntax(Array.Empty<ExpressionSyntax>(), Array.Empty<bool>(), Merge(SpanOf(openParen), SpanOf(closeEmpty)));
        }

        var firstIsUnpacking = CurrentToken == Token.Star;
        if (firstIsUnpacking)
        {
            ReadToken();
        }

        var first = ParseExpression();
        if (first is null)
        {
            return null;
        }
        SkipGroupedExpressionTrivia();

        if (CurrentToken != Token.Comma)
        {
            if (firstIsUnpacking)
            {
                AddDiagnostic("LA2000", "Unsupported Python construct 'bare starred expression'.", first.Span);
                return null;
            }

            if (CurrentToken == Token.For)
            {
                if (!TryParseComprehensionClauses(out var clauses, out _))
                {
                    return null;
                }

                SkipGroupedExpressionTrivia();
                if (!TryRead(Token.CloseParen, out var closeComprehension))
                {
                    AddDiagnostic("LA1045", "Expected ')' after generator expression.", openParen);
                    return null;
                }

                return new GeneratorExpressionSyntax(
                    first,
                    clauses,
                    Merge(SpanOf(openParen), SpanOf(closeComprehension)));
            }

            SkipGroupedExpressionTrivia();
            if (!TryRead(Token.CloseParen, out var closeParen))
            {
                AddDiagnostic("LA1008", "Expected ')' after expression.", first.Span);
                return null;
            }

            return new ParenthesizedExpressionSyntax(first, Merge(first.Span, SpanOf(closeParen)));
        }

        var items = new List<ExpressionSyntax> { first };
        var unpackingFlags = new List<bool> { firstIsUnpacking };
        while (CurrentToken == Token.Comma)
        {
            ReadToken();
            SkipGroupedExpressionTrivia();
            if (CurrentToken == Token.CloseParen)
            {
                break;
            }

            if (CurrentToken == Token.End)
            {
                AddDiagnostic("LA1045", "Unexpected end of file while parsing tuple literal; expected ')'.", openParen);
                return null;
            }

            var isUnpacking = CurrentToken == Token.Star;
            if (isUnpacking)
            {
                ReadToken();
            }

            var item = ParseExpression();
            if (item is null)
            {
                return null;
            }

            items.Add(item);
            unpackingFlags.Add(isUnpacking);
            SkipGroupedExpressionTrivia();
        }

        SkipGroupedExpressionTrivia();
        if (!TryRead(Token.CloseParen, out var closeTuple))
        {
            AddDiagnostic("LA1045", "Expected ')' after tuple literal.", openParen);
            return null;
        }

        return new TupleLiteralExpressionSyntax(items, unpackingFlags, Merge(SpanOf(openParen), SpanOf(closeTuple)));
    }

    private ExpressionSyntax? ParseDictLiteral()
    {
        var openBrace = ReadToken();
        var dictionaryItems = new List<DictionaryDisplayItemSyntax>();
        var setItems = new List<ExpressionSyntax>();
        var setUnpackingFlags = new List<bool>();
        SkipGroupedExpressionTrivia();

        if (CurrentToken == Token.End)
        {
            AddDiagnostic("LA1026", "Unexpected end of file while parsing dictionary or set literal; expected '}'.", openBrace);
            return null;
        }

        if (CurrentToken != Token.CloseBrace)
        {
            while (true)
            {
                if (CurrentToken == Token.End)
                {
                    AddDiagnostic("LA1026", "Unexpected end of file while parsing dictionary or set literal; expected '}'.", openBrace);
                    return null;
                }

                if (CurrentToken == Token.StarStar)
                {
                    if (setItems.Count != 0)
                    {
                        AddDiagnostic("LA2000", "Unsupported Python construct 'dictionary unpacking in set display'.", _position);
                        return null;
                    }

                    var unpackToken = ReadToken();
                    SkipGroupedExpressionTrivia();
                    var mapping = ParseExpression();
                    if (mapping is null)
                    {
                        AddDiagnostic("LA1025", "Expected mapping after '**' in dictionary literal.", unpackToken);
                        return null;
                    }

                    dictionaryItems.Add(new DictionaryUnpackingItemSyntax(
                        mapping,
                        Merge(SpanOf(unpackToken), mapping.Span)));
                    SkipGroupedExpressionTrivia();
                }
                else
                {
                    var isSetUnpacking = CurrentToken == Token.Star;
                    if (isSetUnpacking)
                    {
                        if (dictionaryItems.Count != 0)
                        {
                            AddDiagnostic("LA2000", "Unsupported Python construct 'set unpacking in dictionary display'.", _position);
                            return null;
                        }

                        ReadToken();
                    }

                    var key = ParseExpression();
                    if (key is null)
                    {
                        return null;
                    }
                    SkipGroupedExpressionTrivia();

                    if (TryRead(Token.Colon, out var colonToken))
                    {
                        if (isSetUnpacking || setItems.Count != 0)
                        {
                            AddDiagnostic("LA1024", "Cannot mix set items with dictionary entries.", key.Span);
                            return null;
                        }

                        SkipGroupedExpressionTrivia();
                        var value = ParseExpression();
                        if (value is null)
                        {
                            AddDiagnostic("LA1025", "Expected value in dictionary literal.", colonToken);
                            return null;
                        }
                        SkipGroupedExpressionTrivia();

                        dictionaryItems.Add(new DictionaryKeyValueItemSyntax(
                            key,
                            value,
                            Merge(key.Span, value.Span)));

                        if (CurrentToken == Token.For)
                        {
                            if (dictionaryItems.Count != 1 || dictionaryItems[0] is not DictionaryKeyValueItemSyntax)
                            {
                                AddDiagnostic("LA2000", "Unsupported Python construct 'comprehension'.", _position);
                                return null;
                            }

                            if (!TryParseComprehensionClauses(out var clauses, out _))
                            {
                                return null;
                            }

                            SkipGroupedExpressionTrivia();
                            if (!TryRead(Token.CloseBrace, out var closeComprehension))
                            {
                                AddDiagnostic("LA1026", "Expected '}' after dictionary literal.", openBrace);
                                return null;
                            }

                            return new DictComprehensionExpressionSyntax(
                                key,
                                value,
                                clauses,
                                Merge(SpanOf(openBrace), SpanOf(closeComprehension)));
                        }
                    }
                    else
                    {
                        if (dictionaryItems.Count != 0)
                        {
                            AddDiagnostic("LA1024", "Expected ':' in dictionary literal.", key.Span);
                            return null;
                        }

                        if (CurrentToken == Token.For)
                        {
                            if (isSetUnpacking || setItems.Count != 0)
                            {
                                AddDiagnostic("LA2000", "Unsupported Python construct 'iterable unpacking in comprehension'.", _position);
                                return null;
                            }

                            if (!TryParseComprehensionClauses(out var clauses, out _))
                            {
                                return null;
                            }

                            SkipGroupedExpressionTrivia();
                            if (!TryRead(Token.CloseBrace, out var closeComprehension))
                            {
                                AddDiagnostic("LA1026", "Expected '}' after set comprehension.", openBrace);
                                return null;
                            }

                            return new SetComprehensionExpressionSyntax(
                                key,
                                clauses,
                                Merge(SpanOf(openBrace), SpanOf(closeComprehension)));
                        }

                        setItems.Add(key);
                        setUnpackingFlags.Add(isSetUnpacking);
                    }
                }

                if (CurrentToken != Token.Comma)
                {
                    break;
                }

                ReadToken();
                SkipGroupedExpressionTrivia();
                if (CurrentToken == Token.CloseBrace)
                {
                    break;
                }
            }
        }

        SkipGroupedExpressionTrivia();
        if (!TryRead(Token.CloseBrace, out var closeBrace))
        {
            AddDiagnostic("LA1026", "Expected '}' after dictionary literal.", openBrace);
            return null;
        }

        return setItems.Count != 0
            ? new SetLiteralExpressionSyntax(setItems, setUnpackingFlags, Merge(SpanOf(openBrace), SpanOf(closeBrace)))
            : new DictLiteralExpressionSyntax(dictionaryItems, Merge(SpanOf(openBrace), SpanOf(closeBrace)));
    }

    private bool TryParseComprehensionClauses(
        out IReadOnlyList<ComprehensionClauseSyntax> clauses,
        out LythonSourceSpan span)
    {
        clauses = Array.Empty<ComprehensionClauseSyntax>();
        span = new LythonSourceSpan(0, 0, 0, 0);

        var parsedClauses = new List<ComprehensionClauseSyntax>();
        while (true)
        {
            SkipGroupedExpressionTrivia();
            var forToken = ReadToken();
            SkipGroupedExpressionTrivia();
            if (!TryParseLoopTarget(out var parsedTarget, out var targetToken))
            {
                AddDiagnostic("LA1015", "Expected loop variable after 'for'.", forToken);
                return false;
            }

            SkipGroupedExpressionTrivia();
            if (!TryRead(Token.In, out _))
            {
                AddDiagnostic("LA1016", "Expected 'in' in comprehension.", targetToken);
                return false;
            }

            SkipGroupedExpressionTrivia();
            var iterable = ParseComprehensionIterableExpression();
            if (iterable is null)
            {
                AddDiagnostic("LA1017", "Expected iterable expression in comprehension.", targetToken);
                return false;
            }
            SkipGroupedExpressionTrivia();

            ExpressionSyntax? condition = null;
            if (CurrentToken == Token.If)
            {
                ReadToken();
                SkipGroupedExpressionTrivia();
                condition = ParseExpression();
                if (condition is null)
                {
                    AddDiagnostic("LA1010", "Expected condition after 'if'.", _position);
                    return false;
                }
                SkipGroupedExpressionTrivia();
            }

            parsedClauses.Add(new ComprehensionClauseSyntax(
                parsedTarget,
                iterable,
                condition,
                Merge(SpanOf(forToken), (condition ?? iterable).Span)));

            SkipGroupedExpressionTrivia();
            if (CurrentToken != Token.For)
            {
                break;
            }
        }

        clauses = parsedClauses;
        span = Merge(parsedClauses[0].Span, parsedClauses[^1].Span);
        return true;
    }

    private bool TryParseLoopTarget(out LoopTargetSyntax target, out int tokenIndex)
    {
        target = new LoopTupleTargetSyntax(Array.Empty<LoopTargetSyntax>());
        tokenIndex = _position;

        if (!TryParseLoopTargetAtom(out var first, out tokenIndex))
        {
            return false;
        }

        if (CurrentToken != Token.Comma)
        {
            target = first;
            return true;
        }

        var items = new List<LoopTargetSyntax> { first };
        while (CurrentToken == Token.Comma)
        {
            ReadToken();
            if (!TryParseLoopTargetAtom(out var item, out _))
            {
                AddDiagnostic("LA1015", "Expected loop variable after ','.", _position);
                return false;
            }

            items.Add(item);
        }

        target = new LoopTupleTargetSyntax(items);
        return true;
    }

    private bool TryParseLoopTargetAtom(out LoopTargetSyntax target, out int tokenIndex)
    {
        if (TryReadNameToken(out tokenIndex))
        {
            target = new LoopNameTargetSyntax(IdentifierText(tokenIndex));
            return true;
        }

        if (TryRead(Token.OpenParen, out var openParen))
        {
            if (!TryParseLoopTarget(out target, out _))
            {
                target = new LoopTupleTargetSyntax(Array.Empty<LoopTargetSyntax>());
                return false;
            }

            if (!TryRead(Token.CloseParen, out _))
            {
                AddDiagnostic("LA1045", "Expected ')' after loop target.", _position);
                target = new LoopTupleTargetSyntax(Array.Empty<LoopTargetSyntax>());
                return false;
            }

            tokenIndex = openParen;
            return true;
        }

        target = new LoopTupleTargetSyntax(Array.Empty<LoopTargetSyntax>());
        tokenIndex = _position;
        return false;
    }

    private ExpressionSyntax? ParseComprehensionIterableExpression()
    {
        var expression = ParseOrExpression();
        if (expression is null)
        {
            return null;
        }

        if (TryGetUnsupportedTrailingExpressionConstruct(CurrentToken, out var construct))
        {
            AddDiagnostic("LA2000", $"Unsupported Python construct '{construct}'.", _position);
            return null;
        }

        return expression;
    }

}
