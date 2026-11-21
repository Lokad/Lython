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
        state.TakeJoinedBranches(left, right);
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

    [Fact]
    public void DeferredBodyDropsMutableBindingsWithoutChangingItsParentOrSibling()
    {
        var state = new AbstractState();
        state.Set("value", AbstractValue.Integer(1, Span));
        state.Set("items", AbstractValue.List([AbstractValue.String("text", Span)], Span));
        var sibling = state.Clone();
        var deferred = state.CloneForDeferredBody();
        Assert.False(deferred.TryGet("items", out _));
        Assert.False(deferred.TryGetSequenceLength("items", out _));

        state.InvalidateMutableSequenceFacts();
        state.Set("value", AbstractValue.Integer(2, Span));
        Assert.True(sibling.TryGet("items", out var previous));
        Assert.Equal(AbstractValueKind.List, previous.Kind);
        Assert.True(sibling.TryGetSequenceLength("items", out var bounds));
        Assert.Equal(AbstractSequenceLengthBounds.Exact(1), bounds);
        AssertInteger(sibling, 1);
        AssertInteger(deferred, 1);
        AssertInteger(state, 2);
    }

    [Fact]
    public void BranchTransferKeepsSurvivingSnapshotIndependentAcrossAJoin()
    {
        var state = new AbstractState();
        state.Set("value", AbstractValue.Integer(1, Span));
        state.Set("text", AbstractValue.String("abc", Span));
        var branch = state.Clone();
        var later = branch.Clone();
        branch.Remove("text");
        branch.Set("value", AbstractValue.Integer(2, Span));

        state.TakeBranchResult(branch);
        AssertInteger(state, 2);
        AssertInteger(branch, 1);
        AssertInteger(later, 1);
        branch.JoinBranch(state);
        Assert.False(branch.TryGetSequenceLength("text", out _));
        Assert.True(later.TryGetSequenceLength("text", out var bounds));
        Assert.Equal(AbstractSequenceLengthBounds.Exact(3), bounds);
        AssertInteger(later, 1);
    }

    [Fact]
    public void ContainerInvalidationWidensAliasesAndPreservesIndependentLengthFacts()
    {
        var state = new AbstractState();
        var item = AbstractValue.Integer(1, Span);
        var list = AbstractValue.List([item], Span);
        var nestedRead = new IdentifierExpressionSyntax("nested", Span);
        state.Set("nested", AbstractValue.Tuple([list], Span));
        state.Set("mapping", AbstractValue.Dict([new(item, list)], Span));
        state.Set("items", list);
        state.SetSequenceLength("lengthOnly", AbstractSequenceLengthBounds.Exact(2));
        Assert.True(StaticAbstractValueResolver.TryResolve(nestedRead, state, out var before));
        Assert.Equal(AbstractValueKind.List, before.RequireSequenceItems()[0].Kind);

        state.InvalidateCollectionFacts();

        Assert.True(StaticAbstractValueResolver.TryResolve(nestedRead, state, out var after));
        Assert.Equal(AbstractValueKind.ListType, after.RequireSequenceItems()[0].Kind);
        Assert.True(state.TryGet("items", out var widened));
        Assert.Equal(AbstractValueKind.ListType, widened.Kind);
        Assert.False(state.TryGetSequenceLength("items", out _));
        Assert.False(state.TryGet("mapping", out _));
        Assert.True(state.TryGetSequenceLength("lengthOnly", out var bounds));
        Assert.Equal(AbstractSequenceLengthBounds.Exact(2), bounds);
    }

    private static void AssertInteger(AbstractState state, int expected)
    {
        Assert.True(StaticAbstractValueResolver.TryResolve(Read, state, out var value));
        Assert.Equal(AbstractValueKind.Integer, value.Kind);
        Assert.Equal(new System.Numerics.BigInteger(expected), value.RequireInteger());
    }
}
