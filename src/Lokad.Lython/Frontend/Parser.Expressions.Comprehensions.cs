using System.Text;
using Lokad.Parsing.Lexer;

namespace Lokad.Lython.Frontend;

internal sealed partial class Parser
{
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
        if (!EnterNestingDepth(_position))
        {
            return null;
        }

        ExpressionSyntax? expression;
        try
        {
            expression = ParseOrExpression();
        }
        finally
        {
            LeaveNestingDepth();
        }
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
