using System.Text;

namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
    private ExpressionSyntax? ParseExpression()
    {
        if (CurrentToken == Token.Yield) return ParseYieldExpression();
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

            var diagnosticCount = _diagnostics.Count;
            ExpressionSyntax? alternative;
            if (!EnterNestingDepth(elseToken))
            {
                return null;
            }

            try
            {
                alternative = ParseExpression();
            }
            finally
            {
                LeaveNestingDepth();
            }

            if (alternative is null)
            {
                if (_diagnostics.Count == diagnosticCount)
                {
                    AddDiagnostic("LA1016", "Expected expression after 'else'.", elseToken);
                }

                return null;
            }

            expression = new ConditionalExpressionSyntax(expression, condition, alternative, Merge(expression.Span, alternative.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseExpressionList(bool restrictUnpackingOperand = false)
    {
        var firstIsUnpacking = CurrentToken == Token.Star;
        var firstUnpackingSpan = default(LythonSourceSpan?);
        if (firstIsUnpacking)
        {
            firstUnpackingSpan = SpanOf(ReadToken());
        }

        var first = firstIsUnpacking && restrictUnpackingOperand ? ParseBitwiseOrExpression() : ParseExpression();
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

        var items = new List<CollectionDisplayItemSyntax>
        {
            firstIsUnpacking
                ? new CollectionUnpackingItemSyntax(first, Merge(firstUnpackingSpan ?? first.Span, first.Span))
                : new CollectionValueItemSyntax(first)
        };
        var endSpan = first.Span;
        while (CurrentToken == Token.Comma)
        {
            var commaToken = ReadToken();
            endSpan = SpanOf(commaToken);
            if (CurrentToken is Token.Eol or Token.Semicolon or Token.Dedent or Token.End or Token.Colon or
                Token.CloseParen or Token.CloseBracket or Token.CloseBrace)
            {
                break;
            }

            var isUnpacking = CurrentToken == Token.Star;
            var unpackingSpan = default(LythonSourceSpan?);
            if (isUnpacking)
            {
                unpackingSpan = SpanOf(ReadToken());
            }

            var item = isUnpacking && restrictUnpackingOperand ? ParseBitwiseOrExpression() : ParseExpression();
            if (item is null)
            {
                AddDiagnostic("LA1004", "Expected expression after ','.", commaToken);
                return null;
            }

            items.Add(isUnpacking
                ? new CollectionUnpackingItemSyntax(item, Merge(unpackingSpan ?? item.Span, item.Span))
                : new CollectionValueItemSyntax(item));
            endSpan = item.Span;
        }

        return new TupleLiteralExpressionSyntax(items, Merge(first.Span, endSpan));
    }

    private enum LeftAssociativeLayer
    {
        Or,
        And,
        BitwiseOr,
        BitwiseXor,
        BitwiseAnd,
        Shift,
        Additive,
        Multiplicative,
    }

    private readonly record struct LeftAssociativeDiagnostic(
        LythonDiagnosticCode Code,
        string Message,
        bool AnchorAfterOperator);

    private ExpressionSyntax? ParseOrExpression()
        => ParseLeftAssociativeExpression(LeftAssociativeLayer.Or);

    private ExpressionSyntax? ParseAndExpression()
        => ParseLeftAssociativeExpression(LeftAssociativeLayer.And);

    private ExpressionSyntax? ParseComparisonExpression()
    {
        var left = ParseBitwiseOrExpression();
        if (left is null || ComparisonOperator(CurrentToken) is not { } firstOperator) return left;
        var right = ParseComparisonOperand(ref firstOperator);
        if (right is null) return null;
        if (ComparisonOperator(CurrentToken) is not { } nextOperator)
            return new BinaryExpressionSyntax(left, firstOperator, right, Merge(left.Span, right.Span));

        List<ExpressionSyntax> operands = [left, right];
        List<BinaryOperatorSyntax> operators = [firstOperator];
        while (true)
        {
            var operand = ParseComparisonOperand(ref nextOperator);
            if (operand is null) return null;
            operands.Add(operand);
            operators.Add(nextOperator);
            if (ComparisonOperator(CurrentToken) is not { } following) break;
            nextOperator = following;
        }
        return new ChainedComparisonExpressionSyntax(operands, operators, Merge(left.Span, operands[^1].Span));
    }

    private static BinaryOperatorSyntax? ComparisonOperator(Token token) => token switch
    {
        Token.EqualEqual => BinaryOperatorSyntax.Equal,
        Token.BangEqual => BinaryOperatorSyntax.NotEqual,
        Token.Less => BinaryOperatorSyntax.Less,
        Token.LessEqual => BinaryOperatorSyntax.LessEqual,
        Token.Greater => BinaryOperatorSyntax.Greater,
        Token.GreaterEqual => BinaryOperatorSyntax.GreaterEqual,
        Token.In => BinaryOperatorSyntax.In,
        Token.Is => BinaryOperatorSyntax.Is,
        Token.Not => BinaryOperatorSyntax.NotIn,
        _ => null,
    };

    private ExpressionSyntax? ParseComparisonOperand(ref BinaryOperatorSyntax operation)
    {
        var operatorToken = ReadToken();
        if (operation == BinaryOperatorSyntax.NotIn)
        {
            if (!TryRead(Token.In, out var inToken))
            {
                AddDiagnostic("LA1039", "Expected 'in' after 'not'.", operatorToken);
                return null;
            }
            operatorToken = inToken;
        }
        else if (operation == BinaryOperatorSyntax.Is && TryRead(Token.Not, out _))
            operation = BinaryOperatorSyntax.IsNot;

        var operand = ParseBitwiseOrExpression();
        if (operand is null) AddDiagnostic("LA1040", "Expected expression after comparison operator.", operatorToken);
        return operand;
    }

    private ExpressionSyntax? ParseBitwiseOrExpression()
        => ParseArithmeticExpression(LeftAssociativeLayer.BitwiseOr);

    // Descend only for tighter operators, rather than for every grammar level.
    private ExpressionSyntax? ParseArithmeticExpression(LeftAssociativeLayer minimumLayer)
    {
        var expression = ParseUnaryExpression();
        if (expression is null) return null;
        var precedingLayer = LeftAssociativeLayer.Multiplicative;
        var operandCount = 1;
        while (LeftAssociativeOperator(CurrentToken) is { } operation && operation.Layer >= minimumLayer)
        {
            var operatorToken = ReadToken();
            // Tighter operators occur in the right operand. Descending to a
            // looser level starts that level's independent operand count.
            if (operation.Layer != precedingLayer)
            {
                precedingLayer = operation.Layer;
                operandCount = 1;
            }
            if (++operandCount > MaxNestingDepth)
            {
                AddDiagnostic("LA0003", $"Expression chain exceeds the maximum of {MaxNestingDepth} operands.", operatorToken);
                return null;
            }
            var right = ParseArithmeticExpression(operation.Layer + 1);
            if (right is null)
            {
                var diagnostic = LeftAssociativeError(operation.Layer);
                AddDiagnostic(diagnostic.Code, diagnostic.Message, operatorToken);
                return null;
            }
            expression = new BinaryExpressionSyntax(expression, operation.Operator, right, Merge(expression.Span, right.Span));
        }
        return expression;
    }

    private ExpressionSyntax? ParseLeftAssociativeExpression(LeftAssociativeLayer layer)
    {
        var expression = layer == LeftAssociativeLayer.Or ? ParseAndExpression() : ParseNotExpression();
        if (expression is null)
        {
            return null;
        }

        var operandCount = 1;
        while (LeftAssociativeOperator(CurrentToken) is { } operation && operation.Layer == layer)
        {
            var operatorToken = ReadToken();
            operandCount++;
            if (operandCount > MaxNestingDepth)
            {
                AddDiagnostic(
                    "LA0003",
                    $"Expression chain exceeds the maximum of {MaxNestingDepth} operands.",
                    operatorToken);
                return null;
            }
            var right = layer == LeftAssociativeLayer.Or ? ParseAndExpression() : ParseNotExpression();
            if (right is null)
            {
                var diagnostic = LeftAssociativeError(layer);
                AddDiagnostic(
                    diagnostic.Code,
                    diagnostic.Message,
                    diagnostic.AnchorAfterOperator ? _position : operatorToken);
                return null;
            }

            expression = new BinaryExpressionSyntax(
                expression,
                operation.Operator,
                right,
                Merge(expression.Span, right.Span));
        }


        return expression;
    }

    private static (LeftAssociativeLayer Layer, BinaryOperatorSyntax Operator)? LeftAssociativeOperator(Token token)
        => token switch
        {
            Token.Or => (LeftAssociativeLayer.Or, BinaryOperatorSyntax.Or),
            Token.And => (LeftAssociativeLayer.And, BinaryOperatorSyntax.And),
            Token.Pipe => (LeftAssociativeLayer.BitwiseOr, BinaryOperatorSyntax.BitwiseOr),
            Token.Caret => (LeftAssociativeLayer.BitwiseXor, BinaryOperatorSyntax.BitwiseXor),
            Token.Ampersand => (LeftAssociativeLayer.BitwiseAnd, BinaryOperatorSyntax.BitwiseAnd),
            Token.LessLess => (LeftAssociativeLayer.Shift, BinaryOperatorSyntax.LeftShift),
            Token.GreaterGreater => (LeftAssociativeLayer.Shift, BinaryOperatorSyntax.RightShift),
            Token.Plus => (LeftAssociativeLayer.Additive, BinaryOperatorSyntax.Add),
            Token.Minus => (LeftAssociativeLayer.Additive, BinaryOperatorSyntax.Subtract),
            Token.At => (LeftAssociativeLayer.Multiplicative, BinaryOperatorSyntax.MatrixMultiply),
            Token.Star => (LeftAssociativeLayer.Multiplicative, BinaryOperatorSyntax.Multiply),
            Token.Slash => (LeftAssociativeLayer.Multiplicative, BinaryOperatorSyntax.Divide),
            Token.SlashSlash => (LeftAssociativeLayer.Multiplicative, BinaryOperatorSyntax.FloorDivide),
            Token.Percent => (LeftAssociativeLayer.Multiplicative, BinaryOperatorSyntax.Modulo),
            _ => null,
        };

    private static LeftAssociativeDiagnostic LeftAssociativeError(LeftAssociativeLayer layer)
        => layer switch
        {
            LeftAssociativeLayer.Or => new("LA1037", "Expected expression after 'or'.", AnchorAfterOperator: true),
            LeftAssociativeLayer.And => new("LA1038", "Expected expression after 'and'.", AnchorAfterOperator: true),
            LeftAssociativeLayer.BitwiseOr => new("LA1061", "Expected expression after bitwise '|'.", AnchorAfterOperator: false),
            LeftAssociativeLayer.BitwiseXor => new("LA1062", "Expected expression after bitwise '^'.", AnchorAfterOperator: false),
            LeftAssociativeLayer.BitwiseAnd => new("LA1063", "Expected expression after bitwise '&'.", AnchorAfterOperator: false),
            LeftAssociativeLayer.Shift => new("LA1064", "Expected expression after shift operator.", AnchorAfterOperator: false),
            LeftAssociativeLayer.Additive => new("LA1041", "Expected expression after additive operator.", AnchorAfterOperator: false),
            LeftAssociativeLayer.Multiplicative => new("LA1042", "Expected expression after multiplicative operator.", AnchorAfterOperator: false),
            _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "Unknown binary precedence layer."),
        };

    private ExpressionSyntax? ParseNotExpression()
    {
        if (CurrentToken == Token.Not)
        {
            var notToken = ReadToken();
            var diagnosticCount = _diagnostics.Count;
            if (_unaryOperatorDepth >= LythonEngine.MaxUnaryOperatorNesting)
            {
                AddDiagnostic("LA0004", $"Unary operator nesting exceeds the maximum of {LythonEngine.MaxUnaryOperatorNesting} levels.", notToken);
                return null;
            }

            _unaryOperatorDepth++;
            ExpressionSyntax? operand;
            try
            {
                operand = ParseNotExpression();
            }
            finally
            {
                _unaryOperatorDepth--;
            }
            if (operand is null)
            {
                if (_diagnostics.Count == diagnosticCount)
                {
                    AddDiagnostic("LA1043", "Expected expression after 'not'.", notToken);
                }
                return null;
            }

            return new UnaryExpressionSyntax(UnaryOperatorSyntax.Not, operand, Merge(notToken, operand.Span));
        }

        return ParseComparisonExpression();
    }

    private ExpressionSyntax? ParseUnaryExpression()
    {
        if (CurrentToken is Token.Plus or Token.Minus)
        {
            var operatorToken = ReadToken();
            var diagnosticCount = _diagnostics.Count;
            var operand = ParseNestedUnaryOperand(operatorToken);
            if (operand is null)
            {
                if (_diagnostics.Count == diagnosticCount)
                {
                    AddDiagnostic("LA1044", "Expected expression after unary operator.", operatorToken);
                }
                return null;
            }

            return new UnaryExpressionSyntax(
                _tokens.Tokens[operatorToken].Token == Token.Plus ? UnaryOperatorSyntax.Plus : UnaryOperatorSyntax.Minus,
                operand,
                Merge(operatorToken, operand.Span));
        }

        if (CurrentToken == Token.Tilde)
        {
            var operatorToken = ReadToken();
            var diagnosticCount = _diagnostics.Count;
            var operand = ParseNestedUnaryOperand(operatorToken);
            if (operand is null)
            {
                if (_diagnostics.Count == diagnosticCount)
                {
                    AddDiagnostic("LA1065", "Expected expression after '~'.", operatorToken);
                }
                return null;
            }

            return new UnaryExpressionSyntax(UnaryOperatorSyntax.BitwiseNot, operand, Merge(operatorToken, operand.Span));
        }

        return ParsePowerExpression();

        ExpressionSyntax? ParseNestedUnaryOperand(int operatorToken)
        {
            if (_unaryOperatorDepth >= LythonEngine.MaxUnaryOperatorNesting)
            {
                AddDiagnostic(
                    "LA0004",
                    $"Unary operator nesting exceeds the maximum of {LythonEngine.MaxUnaryOperatorNesting} levels.",
                    operatorToken);
                return null;
            }

            _unaryOperatorDepth++;
            try
            {
                return ParseUnaryExpression();
            }
            finally
            {
                _unaryOperatorDepth--;
            }
        }

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
            var diagnosticCount = _diagnostics.Count;
            ExpressionSyntax? right;
            if (!EnterNestingDepth(operatorToken))
            {
                return null;
            }

            try
            {
                right = ParseUnaryExpression();
            }
            finally
            {
                LeaveNestingDepth();
            }

            if (right is null)
            {
                if (_diagnostics.Count == diagnosticCount)
                {
                    AddDiagnostic("LA1066", "Expected expression after '**'.", operatorToken);
                }

                return null;
            }

            return new BinaryExpressionSyntax(expression, BinaryOperatorSyntax.Power, right, Merge(expression.Span, right.Span));
        }
    }
}
