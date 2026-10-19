using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class SetNumericStoragePolicyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BooleanTwinRemovalReleasesTheStoredNumericMember(bool truth)
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(4096));
        var set = new PySet(governor);
        Assert.True(set.Add(truth ? BigInteger.One : BigInteger.Zero));
        var before = governor.CurrentCommittedBytes;
        Assert.True(set.Remove(truth));
        Assert.Equal(0, set.Count);
        Assert.Equal(before - 64, governor.CurrentCommittedBytes);
        Assert.Equal(set.CommittedStorageBytes, governor.CurrentCommittedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NumericMembersPayTheSameAllowanceRegardlessOfPayloadOwnership(bool owned)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        object value = BigInteger.One << 4000;
        if (owned)
            LythonRuntime.OwnFreshInteger(value, context.MemoryGovernor, null);
        var set = new PySet(context.MemoryGovernor);
        Assert.True(set.Add(value));
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(set.Remove(value));
        Assert.Equal(before - 64, context.MemoryGovernor.CurrentCommittedBytes);
        GC.KeepAlive(value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(128)]
    public void DeniedAttachmentPreservesItsUngovernedOwner(long budget)
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(budget));
        var set = new PySet(new object[] { BigInteger.One, new BigInteger(2) });
        var failure = Assert.Throws<LythonRuntimeException>(() => set.AttachMemoryGovernor(governor));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Null(set.OwnerMemoryGovernor);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void AttachmentFundsTheSameStorageAsGovernedConstruction()
    {
        object[] values = [BigInteger.One, new BigInteger(2)];
        var attachedGovernor = new MemoryGovernor(new LythonMemoryBudget(4096));
        var attached = new PySet(values);
        attached.AttachMemoryGovernor(attachedGovernor);
        var constructed = new PySet(values, new MemoryGovernor(new LythonMemoryBudget(4096)));
        Assert.Equal(constructed.CommittedStorageBytes, attached.CommittedStorageBytes);
        Assert.Equal(attached.CommittedStorageBytes, attachedGovernor.CurrentCommittedBytes);
        attached.AttachMemoryGovernor(attachedGovernor);
        Assert.Equal(attached.CommittedStorageBytes, attachedGovernor.CurrentCommittedBytes);
        attached.Clear();
        Assert.Equal(128, attachedGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void BoundedClearAndRefillReleasesLiveStorage()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(256 * 1024));
        var set = new PySet(governor);
        for (var pass = 0; pass < 100; pass++)
        {
            for (var i = 0; i < 1000; i++) Assert.True(set.Add(new BigInteger(i)));
            set.Clear();
            Assert.Equal(128, governor.CurrentCommittedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NumericTwinRemovalReleasesTheFundedBooleanAllowance(bool truth)
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(4096));
        var set = new PySet(governor);
        Assert.True(set.Add(truth));
        var before = governor.CurrentCommittedBytes;
        Assert.True(set.Remove(truth ? BigInteger.One : BigInteger.Zero));
        Assert.Equal(before - 64, governor.CurrentCommittedBytes);
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void BooleanTwinRemovalKeepsTheActualStorageFunding()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(4096));
        var set = new PySet(new object[] { BigInteger.One }, governor);
        var before = governor.CurrentCommittedBytes;
        Assert.True(set.Remove(true));
        Assert.Equal(before - 64, governor.CurrentCommittedBytes);
        Assert.Equal(set.CommittedStorageBytes, governor.CurrentCommittedBytes);
        GC.KeepAlive(set);
    }

    [Fact]
    public void AttachedProtocolCapacityRemainsFundedUntilClear()
    {
        var root = new PyType("object", [], new());
        var type = new PyType("Key", [root], new() { ["__hash__"] = new ConstantHash() });
        var member = new PyInstance(type);
        var set = new PySet();
        Assert.True(set.Add(member));
        var governor = new MemoryGovernor(new LythonMemoryBudget(4096));
        set.AttachMemoryGovernor(governor);
        var before = governor.CurrentCommittedBytes;
        Assert.True(before >= 128 + 64 + 96);
        Assert.True(set.Remove(member));
        Assert.Equal(before - 64, governor.CurrentCommittedBytes);
        Assert.Equal(set.CommittedStorageBytes, governor.CurrentCommittedBytes);
        set.Clear();
        Assert.Equal(128, governor.CurrentCommittedBytes);
        GC.KeepAlive(member);
    }

    private sealed class ConstantHash : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => new BigInteger(7);
    }
}
