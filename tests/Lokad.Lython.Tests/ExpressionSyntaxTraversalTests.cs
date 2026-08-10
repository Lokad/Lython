using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class ExpressionSyntaxTraversalTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    [Fact]
    public void EnumerateChildren_CoversEveryCompositeExpressionShape()
    {
        var first = new IdentifierExpressionSyntax("first", Span);
        var second = new IdentifierExpressionSyntax("second", Span);
        var third = new IdentifierExpressionSyntax("third", Span);
        var clause = new ComprehensionClauseSyntax(new LoopNameTargetSyntax("item"), second, third, Span);

        AssertChildren(
            new FormattedStringExpressionSyntax([new FormattedStringExpressionPartSyntax(first)], Span),
            first);
        AssertChildren(new ListLiteralExpressionSyntax([first], [false], Span), first);
        AssertChildren(new ListComprehensionExpressionSyntax(first, [clause], Span), first, second, third);
        AssertChildren(new GeneratorExpressionSyntax(first, [clause], Span), first, second, third);
        AssertChildren(
            new DictLiteralExpressionSyntax(
                [new DictionaryKeyValueItemSyntax(first, second, Span), new DictionaryUnpackingItemSyntax(third, Span)],
                Span),
            first,
            second,
            third);
        AssertChildren(new SetLiteralExpressionSyntax([first], [false], Span), first);
        AssertChildren(new SetComprehensionExpressionSyntax(first, [clause], Span), first, second, third);
        AssertChildren(new DictComprehensionExpressionSyntax(first, second, [clause], Span), first, second, second, third);
        AssertChildren(new TupleLiteralExpressionSyntax([first, second], [false, false], Span), first, second);
        AssertChildren(new ParenthesizedExpressionSyntax(first, Span), first);
        AssertChildren(new MemberExpressionSyntax(first, "value", Span), first);
        AssertChildren(
            new CallExpressionSyntax(first, [new CallArgumentSyntax(CallArgumentForm.Positional, second)], Span),
            first,
            second);
        AssertChildren(new SubscriptExpressionSyntax(first, second, Span), first, second);
        AssertChildren(new SliceExpressionSyntax(first, second, third, null, Span), first, second, third);
        AssertChildren(new BinaryExpressionSyntax(first, BinaryOperatorSyntax.Add, second, Span), first, second);
        AssertChildren(
            new ChainedComparisonExpressionSyntax([first, second, third], [BinaryOperatorSyntax.Less, BinaryOperatorSyntax.Less], Span),
            first,
            second,
            third);
        AssertChildren(new UnaryExpressionSyntax(UnaryOperatorSyntax.Not, first, Span), first);
        AssertChildren(new ConditionalExpressionSyntax(first, second, third, Span), second, first, third);
        AssertChildren(new AssignmentExpressionSyntax("value", first, Span), first);
        AssertChildren(new LambdaExpressionSyntax([], first, Span), first);
    }

    private static void AssertChildren(ExpressionSyntax expression, params ExpressionSyntax[] expected)
        => Assert.Equal(expected, ExpressionSyntaxTraversal.EnumerateChildren(expression));
}
