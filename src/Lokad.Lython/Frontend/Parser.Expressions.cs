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

        ExpressionSyntax? ParsePowerExpression()
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
    }
}
