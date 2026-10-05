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
            while (CurrentToken == Token.If)
            {
                ReadToken();
                SkipGroupedExpressionTrivia();
                var filter = ParseOrExpression();
                if (filter is null)
                {
                    AddDiagnostic("LA1010", "Expected condition after 'if'.", _position);
                    return false;
                }
                condition = condition is null
                    ? filter
                    : new BinaryExpressionSyntax(condition, BinaryOperatorSyntax.And, filter, Merge(condition.Span, filter.Span));
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
            if (AssignmentTargetFacts.IsStarred(first))
            {
                AddDiagnostic("LA1015", "Starred assignment target must be in a list or tuple.", tokenIndex);
                return false;
            }

            target = first;
            return true;
        }

        var items = new List<LoopTargetSyntax> { first };
        var hasStarred = AssignmentTargetFacts.IsStarred(first);
        while (CurrentToken == Token.Comma)
        {
            ReadToken();
            SkipGroupedExpressionTrivia();
            if (CurrentToken is Token.In or Token.CloseParen or Token.CloseBracket) break;
            if (!TryParseLoopTargetAtom(out var item, out var itemToken))
            {
                AddDiagnostic("LA1015", "Expected loop variable after ','.", _position);
                return false;
            }

            if (AssignmentTargetFacts.IsStarred(item))
            {
                if (hasStarred)
                {
                    AddDiagnostic("LA1015", "Multiple starred expressions in assignment.", itemToken);
                    return false;
                }

                hasStarred = true;
            }

            items.Add(item);
        }

        target = new LoopTupleTargetSyntax(items);
        return true;
    }

    private bool TryParseLoopTargetAtom(out LoopTargetSyntax target, out int tokenIndex)
    {
        tokenIndex = _position;
        var starred = TryRead(Token.Star, out _);
        var expression = ParsePostfixExpression();
        if (expression is not null && TryConvertExpressionToAssignmentTarget(expression, out var assignment))
        {
            target = starred ? AssignmentTargetFacts.Starred(assignment!) : AssignmentTargetFacts.ToLoop(assignment!);
            return true;
        }
        target = new LoopTupleTargetSyntax(Array.Empty<LoopTargetSyntax>());
        AddDiagnostic("LA1015", "Invalid assignment target in loop.", tokenIndex);
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
