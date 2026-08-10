using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class StatementSyntaxTraversalTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);
    private static readonly ExpressionSyntax Condition = new BooleanLiteralExpressionSyntax(true, Span);

    [Fact]
    public void EnumerateChildBodies_CoversEveryCompoundStatementShape()
    {
        IReadOnlyList<StatementSyntax> first = [new PassStatementSyntax(Span)];
        IReadOnlyList<StatementSyntax> second = [new BreakStatementSyntax(Span)];
        IReadOnlyList<StatementSyntax> third = [new ContinueStatementSyntax(Span)];
        IReadOnlyList<StatementSyntax> fourth = [new ReturnStatementSyntax(null, Span)];

        AssertBodies(new FunctionDefinitionStatementSyntax("f", [], [], null, first, Span), first);
        AssertBodies(new ClassDefinitionStatementSyntax("C", null, [], [], [], first, Span), first);
        AssertBodies(new IfStatementSyntax(Condition, first, second, Span), first, second);
        AssertBodies(new ForStatementSyntax(new LoopNameTargetSyntax("item"), Condition, second, first, Span), first, second);
        AssertBodies(new WhileStatementSyntax(Condition, second, first, Span), first, second);
        AssertBodies(new WithStatementSyntax(Condition, null, first, Span), first);
        AssertBodies(new TryStatementSyntax(first, ["Exception"], null, second, third, fourth, Span), first, second, third, fourth);
        AssertBodies(
            new MatchStatementSyntax(
                Condition,
                [
                    new MatchCaseSyntax(new MatchWildcardPatternSyntax(Span), null, first, Span),
                    new MatchCaseSyntax(new MatchWildcardPatternSyntax(Span), null, second, Span)
                ],
                Span),
            first,
            second);
    }

    private static void AssertBodies(
        StatementSyntax statement,
        params IReadOnlyList<StatementSyntax>[] expected)
        => Assert.Equal(expected, StatementSyntaxTraversal.EnumerateChildBodies(statement));
}
