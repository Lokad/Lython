using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class AbstractStateResolutionLifetimeTests
{
    private static readonly LythonSourceSpan Span = new(1, 1, 1, 2);
    private static readonly IdentifierExpressionSyntax Read = new("value", Span);

    [Fact]
    public void ResolutionObservesNewBindingsChangesAndWithdrawal()
    {
        var state = new AbstractState();
        Assert.False(StaticAbstractValueResolver.TryResolve(Read, state, out _));
        state.Set("value", AbstractValue.Integer("1", Span));
        AssertInteger(state, "1");
        state.Set("value", AbstractValue.Integer("2", Span));
        AssertInteger(state, "2");
        state.Remove("value");
        Assert.False(StaticAbstractValueResolver.TryResolve(Read, state, out _));
    }

    [Fact]
    public void ClonedBindingsAndReplacementResolveIndependently()
    {
        var original = new AbstractState();
        original.Set("value", AbstractValue.Integer("1", Span));
        AssertInteger(original, "1");
        var clone = original.Clone();
        clone.Set("value", AbstractValue.Integer("2", Span));
        AssertInteger(clone, "2");
        AssertInteger(original, "1");
        original.ReplaceWith(clone);
        AssertInteger(original, "2");
    }

    [Fact]
    public void BranchJoinDiscardsEarlierLiteralResolution()
    {
        var state = new AbstractState();
        state.Set("value", AbstractValue.Integer("1", Span));
        AssertInteger(state, "1");
        var left = state.Clone();
        var right = state.Clone();
        right.Set("value", AbstractValue.Integer("2", Span));
        state.MergeFrom(left, right);
        Assert.True(StaticAbstractValueResolver.TryResolve(Read, state, out var joined));
        Assert.Equal(AbstractValueKind.IntegerType, joined.Kind);
    }

    private static void AssertInteger(AbstractState state, string expected)
    {
        Assert.True(StaticAbstractValueResolver.TryResolve(Read, state, out var value));
        Assert.Equal(AbstractValueKind.Integer, value.Kind);
        Assert.Equal(expected, value.RequireText());
    }
}
