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
        state.Set("value", AbstractValue.Integer(1, Span));
        AssertInteger(state, 1);
        state.Set("value", AbstractValue.Integer(2, Span));
        AssertInteger(state, 2);
        state.Remove("value");
        Assert.False(StaticAbstractValueResolver.TryResolve(Read, state, out _));
    }

    [Fact]
    public void SelectedBranchUsesItsBindingsAndClearsPriorResolution()
    {
        var original = new AbstractState();
        original.Set("value", AbstractValue.Integer(1, Span));
        AssertInteger(original, 1);
        var clone = original.Clone();
        clone.Set("value", AbstractValue.Integer(2, Span));
        AssertInteger(clone, 2);
        AssertInteger(original, 1);
        original.TakeBranchResult(clone);
        AssertInteger(original, 2);
        clone.Remove("value");
        Assert.True(original.TryGet("value", out _));
    }

    [Fact]
    public void BranchJoinDiscardsEarlierLiteralResolution()
    {
        var state = new AbstractState();
        state.Set("value", AbstractValue.Integer(1, Span));
        AssertInteger(state, 1);
        var left = state.Clone();
        var right = state.Clone();
        right.Set("value", AbstractValue.Integer(2, Span));
        state.MergeFrom(left, right);
        Assert.True(StaticAbstractValueResolver.TryResolve(Read, state, out var joined));
        Assert.Equal(AbstractValueKind.IntegerType, joined.Kind);
    }

    [Fact]
    public void JoinedPathsKeepOnlyFactsPresentOnBothPaths()
    {
        var left = new AbstractState();
        left.Set("value", AbstractValue.Integer(1, Span));
        left.Set("left", AbstractValue.Integer(2, Span));
        left.SetSequenceLength("left", AbstractSequenceLengthBounds.Exact(2));
        AssertInteger(left, 1);
        var right = new AbstractState();
        right.Set("value", AbstractValue.Integer(2, Span));
        right.Set("right", AbstractValue.Integer(3, Span));
        right.SetSequenceLength("right", AbstractSequenceLengthBounds.Exact(3));

        left.JoinBranch(right);

        Assert.True(StaticAbstractValueResolver.TryResolve(Read, left, out var joined));
        Assert.Equal(AbstractValueKind.IntegerType, joined.Kind);
        Assert.False(left.TryGet("left", out _));
        Assert.False(left.TryGet("right", out _));
        Assert.False(left.TryGetSequenceLength("left", out _));
        Assert.True(right.TryGetSequenceLength("right", out _));
        AssertInteger(right, 2);
    }

    private static void AssertInteger(AbstractState state, int expected)
    {
        Assert.True(StaticAbstractValueResolver.TryResolve(Read, state, out var value));
        Assert.Equal(AbstractValueKind.Integer, value.Kind);
        Assert.Equal(new System.Numerics.BigInteger(expected), value.RequireInteger());
    }
}
