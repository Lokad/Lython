using System.Numerics;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class DequeStorageSimplificationTests
{
    [Theory]
    [InlineData("integer")]
    [InlineData("double")]
    [InlineData("boolean")]
    [InlineData("none")]
    [InlineData("reference")]
    public void RepeatedValuesFundEveryRetainedNode(string kind)
    {
        object value = kind switch
        {
            "integer" => new BigInteger(42),
            "double" => 1.5,
            "boolean" => true,
            "none" => PyNone.Instance,
            _ => new object(),
        };
        var governor = new MemoryGovernor(null);
        var deque = new PyDeque(null, governor, null);
        deque.Append(value);
        deque.AppendLeft(value);
        Assert.Same(value, deque.GetItem(0));
        Assert.Same(value, deque.GetItem(1));
        Assert.Equal(128 + 2 * 128, deque.CommittedStorageBytes);
        Assert.Equal(deque.CommittedStorageBytes, governor.CurrentCommittedBytes);
        deque.Pop();
        Assert.Equal(256, governor.CurrentCommittedBytes);
        deque.Clear();
        Assert.Equal(128, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundedEvictionTransfersTheActualFundedNode(bool left)
    {
        var governor = new MemoryGovernor(256);
        var deque = new PyDeque(1, governor, null);
        deque.Append(new BigInteger(1));
        var storage = Assert.IsType<LinkedList<object>>(deque.Iterate());
        var original = storage.First;
        object replacement = new BigInteger(2);
        if (left) deque.AppendLeft(replacement);
        else deque.Append(replacement);
        Assert.Same(original, storage.First);
        Assert.Same(replacement, deque.GetItem(0));
        Assert.Equal(256, governor.CurrentCommittedBytes);
        Assert.Equal(256, governor.PeakAccountedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void ReplacementTransfersFundedValueHeadroom()
    {
        var governor = new MemoryGovernor(256);
        var deque = new PyDeque(null, governor, null);
        deque.Append(new BigInteger(1));
        object replacement = new BigInteger(2);
        deque.SetIndex(0, replacement);
        Assert.Same(replacement, deque.GetItem(0));
        deque.SetIndex(0, PyNone.Instance);
        deque.SetIndex(0, replacement);
        Assert.Equal(256, governor.CurrentCommittedBytes);
        Assert.Equal(256, governor.PeakAccountedBytes);
    }

    [Fact]
    public void RotationKeepsEveryActualNode()
    {
        var deque = new PyDeque(new object[] { new BigInteger(1), new BigInteger(2), new BigInteger(3) });
        var storage = Assert.IsType<LinkedList<object>>(deque.Iterate());
        var nodes = new[] { storage.First!, storage.First!.Next!, storage.Last! };
        deque.Rotate(1);
        Assert.Same(nodes[2], storage.First);
        Assert.Same(nodes[0], storage.First!.Next);
        deque.Rotate(-1);
        Assert.Same(nodes[0], storage.First);
        Assert.Same(nodes[2], storage.Last);
    }

    [Fact]
    public void AttachmentPreflightsBeforePublishingTheGovernor()
    {
        var deque = new PyDeque(Enumerable.Repeat<object>(new BigInteger(1), 4));
        var denied = new MemoryGovernor(256);
        var error = Assert.Throws<LythonRuntimeException>(() => deque.AttachMemoryGovernor(denied));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Null(deque.OwnerMemoryGovernor);
        Assert.Equal(0, deque.CommittedStorageBytes);
        Assert.Equal(0, denied.CurrentAccountedBytes);
        var funded = new MemoryGovernor(640);
        deque.AttachMemoryGovernor(funded);
        Assert.Same(funded, deque.OwnerMemoryGovernor);
        Assert.Equal(640, deque.CommittedStorageBytes);
        Assert.Equal(640, funded.CurrentCommittedBytes);
        deque.Clear();
        Assert.Equal(128, funded.CurrentCommittedBytes);
        Assert.Equal(0, denied.CurrentAccountedBytes);
    }

    [Fact]
    public void AttachmentFundsPreviouslyUnownedNumericValues()
    {
        object value = new BigInteger(10);
        var deque = new PyDeque(new[] { value, value });
        var governor = new MemoryGovernor(384);
        deque.AttachMemoryGovernor(governor);
        Assert.Equal(384, governor.CurrentCommittedBytes);
        Assert.Equal(384, deque.CommittedStorageBytes);
        deque.RemoveAt(0);
        Assert.Equal(256, governor.CurrentCommittedBytes);
        deque.Clear();
        Assert.Equal(128, governor.CurrentCommittedBytes);
    }

    [Theory]
    [InlineData("append")]
    [InlineData("appendleft")]
    [InlineData("insert")]
    public void DeniedNodeGrowthPreservesContentsAndFunding(string operation)
    {
        object value = new BigInteger(1);
        var governor = new MemoryGovernor(256);
        var deque = new PyDeque(null, governor, null);
        deque.Append(value);
        var error = Assert.Throws<LythonRuntimeException>(() =>
        {
            if (operation == "append") deque.Append(value);
            else if (operation == "appendleft") deque.AppendLeft(value);
            else deque.Insert(0, value);
        });
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Single(deque.Iterate());
        Assert.Same(value, deque.GetItem(0));
        Assert.Equal(256, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void AbortedConstructionRefundsItsEntirePrivateStorage()
    {
        var governor = new MemoryGovernor(256);
        var error = Assert.Throws<LythonRuntimeException>(() =>
            new PyDeque(new object[] { new BigInteger(1), new BigInteger(2) }, null, governor, null));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(0, governor.CurrentAccountedBytes);
        Assert.Same(error, Assert.Throws<LythonRuntimeException>(() => new PyDeque(null, governor, null)));
        Assert.Equal(0, governor.CurrentAccountedBytes);
    }
}
