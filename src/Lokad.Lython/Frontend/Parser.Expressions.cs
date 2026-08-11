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
        var firstUnpackingSpan = default(LythonSourceSpan?);
        if (firstIsUnpacking)
        {
            firstUnpackingSpan = SpanOf(ReadToken());
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
            if (CurrentToken is Token.Eol or Token.Semicolon or Token.Dedent or Token.End or Token.Colon)
            {
                break;
            }

            var isUnpacking = CurrentToken == Token.Star;
            var unpackingSpan = default(LythonSourceSpan?);
            if (isUnpacking)
            {
                unpackingSpan = SpanOf(ReadToken());
            }

            var item = ParseExpression();
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
        => ParseLeftAssociativeExpression(LeftAssociativeLayer.BitwiseOr);

    private ExpressionSyntax? ParseBitwiseXorExpression()
        => ParseLeftAssociativeExpression(LeftAssociativeLayer.BitwiseXor);

    private ExpressionSyntax? ParseBitwiseAndExpression()
        => ParseLeftAssociativeExpression(LeftAssociativeLayer.BitwiseAnd);

    private ExpressionSyntax? ParseShiftExpression()
        => ParseLeftAssociativeExpression(LeftAssociativeLayer.Shift);

    private ExpressionSyntax? ParseAdditiveExpression()
        => ParseLeftAssociativeExpression(LeftAssociativeLayer.Additive);

    private ExpressionSyntax? ParseMultiplicativeExpression()
        => ParseLeftAssociativeExpression(LeftAssociativeLayer.Multiplicative);

    private ExpressionSyntax? ParseLeftAssociativeExpression(LeftAssociativeLayer layer)
    {
        var expression = ParseNextLayer(layer);
        if (expression is null)
        {
            return null;
        }

        while (TryGetLeftAssociativeOperator(layer, CurrentToken, out var binaryOperator))
        {
            var operatorToken = ReadToken();
            var right = ParseNextLayer(layer);
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
                binaryOperator,
                right,
                Merge(expression.Span, right.Span));
        }

        return expression;
    }

    private ExpressionSyntax? ParseNextLayer(LeftAssociativeLayer layer)
        => layer switch
        {
            LeftAssociativeLayer.Or => ParseAndExpression(),
            LeftAssociativeLayer.And => ParseComparisonExpression(),
            LeftAssociativeLayer.BitwiseOr => ParseBitwiseXorExpression(),
            LeftAssociativeLayer.BitwiseXor => ParseBitwiseAndExpression(),
            LeftAssociativeLayer.BitwiseAnd => ParseShiftExpression(),
            LeftAssociativeLayer.Shift => ParseAdditiveExpression(),
            LeftAssociativeLayer.Additive => ParseMultiplicativeExpression(),
            LeftAssociativeLayer.Multiplicative => ParseUnaryExpression(),
            _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "Unknown binary precedence layer."),
        };

    private static bool TryGetLeftAssociativeOperator(
        LeftAssociativeLayer layer,
        Token token,
        out BinaryOperatorSyntax binaryOperator)
    {
        switch (layer, token)
        {
            case (LeftAssociativeLayer.Or, Token.Or):
                binaryOperator = BinaryOperatorSyntax.Or;
                return true;
            case (LeftAssociativeLayer.And, Token.And):
                binaryOperator = BinaryOperatorSyntax.And;
                return true;
            case (LeftAssociativeLayer.BitwiseOr, Token.Pipe):
                binaryOperator = BinaryOperatorSyntax.BitwiseOr;
                return true;
            case (LeftAssociativeLayer.BitwiseXor, Token.Caret):
                binaryOperator = BinaryOperatorSyntax.BitwiseXor;
                return true;
            case (LeftAssociativeLayer.BitwiseAnd, Token.Ampersand):
                binaryOperator = BinaryOperatorSyntax.BitwiseAnd;
                return true;
            case (LeftAssociativeLayer.Shift, Token.LessLess):
                binaryOperator = BinaryOperatorSyntax.LeftShift;
                return true;
            case (LeftAssociativeLayer.Shift, Token.GreaterGreater):
                binaryOperator = BinaryOperatorSyntax.RightShift;
                return true;
            case (LeftAssociativeLayer.Additive, Token.Plus):
                binaryOperator = BinaryOperatorSyntax.Add;
                return true;
            case (LeftAssociativeLayer.Additive, Token.Minus):
                binaryOperator = BinaryOperatorSyntax.Subtract;
                return true;
            case (LeftAssociativeLayer.Multiplicative, Token.Star):
                binaryOperator = BinaryOperatorSyntax.Multiply;
                return true;
            case (LeftAssociativeLayer.Multiplicative, Token.Slash):
                binaryOperator = BinaryOperatorSyntax.Divide;
                return true;
            case (LeftAssociativeLayer.Multiplicative, Token.SlashSlash):
                binaryOperator = BinaryOperatorSyntax.FloorDivide;
                return true;
            case (LeftAssociativeLayer.Multiplicative, Token.Percent):
                binaryOperator = BinaryOperatorSyntax.Modulo;
                return true;
            default:
                binaryOperator = default;
                return false;
        }
    }

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

    private ExpressionSyntax? ParseUnaryExpression()
    {
        if (CurrentToken == Token.Not)
        {
            var notToken = ReadToken();
            var diagnosticCount = _diagnostics.Count;
            var operand = ParseNestedUnaryOperand(notToken);
            if (operand is null)
            {
                if (_diagnostics.Count == diagnosticCount)
                {
                    AddDiagnostic("LA1043", "Expected expression after 'not'.", notToken);
                }
                return null;
            }

            return new UnaryExpressionSyntax(UnaryOperatorSyntax.Not, operand, Merge(SpanOf(notToken), operand.Span));
        }

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
                Merge(SpanOf(operatorToken), operand.Span));
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

            return new UnaryExpressionSyntax(UnaryOperatorSyntax.BitwiseNot, operand, Merge(SpanOf(operatorToken), operand.Span));
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
