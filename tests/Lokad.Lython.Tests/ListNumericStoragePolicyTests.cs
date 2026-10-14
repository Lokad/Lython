using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Prospective conservative storage policy. The original production is expected
// to disagree: these pin intentional budget changes rather than Python faults.
public sealed class ListNumericStoragePolicyTests
{
    [Fact]
    public void EachOccupiedNumericAliasCarriesAStorageAllowance()
    {
        var governor = new MemoryGovernor(4096);
        var backing = new PyList([], governor).CommittedStorageBytes;
        object value = new BigInteger(7);
        var list = new PyList([value, value], governor);
        Assert.Equal(backing + 128, list.CommittedStorageBytes);
        Assert.Same(value, list[0]);
        Assert.Same(value, list[1]);
    }

    [Fact]
    public void TrackedPayloadDoesNotChangeTheSlotPolicy()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        object value = BigInteger.One << 4000;
        LythonRuntime.OwnFreshInteger(value, context.MemoryGovernor, null);
        var backing = new PyList([], context.MemoryGovernor).CommittedStorageBytes;
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var list = new PyList([value, value], context.MemoryGovernor);
        Assert.Equal(backing + 128, list.CommittedStorageBytes);
        Assert.Equal(list.CommittedStorageBytes, context.MemoryGovernor.CurrentCommittedBytes - before);
        GC.KeepAlive(value);
    }

    [Theory]
    [InlineData("append")]
    [InlineData("insert")]
    [InlineData("extend")]
    public void PinnedBudgetDeniesAnotherAliasBeforeMutation(string operation)
    {
        var governor = new MemoryGovernor(256);
        object value = new BigInteger(7);
        var list = new PyList([value], governor);
        var before = governor.CurrentCommittedBytes;
        var failure = Assert.Throws<LythonRuntimeException>(() =>
        {
            if (operation == "append") list.Add(value);
            else if (operation == "insert") list.Insert(0, value);
            else list.AddRange(new object[] { value });
        });
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Single(list);
        Assert.Same(value, list[0]);
        Assert.Equal(before, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void PinnedBudgetDeniesRepeatedSlotsBeforeMutation()
    {
        var governor = new MemoryGovernor(256);
        object value = new BigInteger(7);
        var list = new PyList([value], governor);
        var before = governor.CurrentCommittedBytes;
        var failure = Assert.Throws<LythonRuntimeException>(() => list.RepeatInPlace(2, new LythonSourceSpan(0, 0, 0, 0)));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Single(list);
        Assert.Same(value, list[0]);
        Assert.Equal(before, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void NumericReplacementTransfersItsExistingSlotAllowance()
    {
        var governor = new MemoryGovernor(256);
        var list = new PyList([new BigInteger(7)], governor);
        var before = governor.CurrentCommittedBytes;
        object replacement = new BigInteger(9);
        list[0] = replacement;
        Assert.Same(replacement, list[0]);
        Assert.Equal(before, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void RemovingOneAliasReleasesItsSlotAllowance()
    {
        var governor = new MemoryGovernor(4096);
        object value = new BigInteger(7);
        var list = new PyList([value, value], governor);
        var before = governor.CurrentCommittedBytes;
        list.RemoveAt(0);
        Assert.Equal(before - 64, governor.CurrentCommittedBytes);
        Assert.Same(value, list[0]);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void NonnumericSlotsKeepTheBackingFeeAndBooleanSlotsPayAllowances()
    {
        var governor = new MemoryGovernor(4096);
        var backing = new PyList([], governor).CommittedStorageBytes;
        var list = new PyList([true, false, new object(), new object()], governor);
        Assert.Equal(backing + 128, list.CommittedStorageBytes);
        list.Add(new object());
        Assert.Equal(backing + 128, list.CommittedStorageBytes);
    }

    [Fact]
    public void AttachedGovernorFundsExistingSlotsBeforeTheirRemoval()
    {
        object value = new BigInteger(7);
        var list = new PyList([value, value]);
        var governor = new MemoryGovernor(4096);
        list.AttachMemoryGovernor(governor);
        Assert.Equal(128, governor.CurrentCommittedBytes);
        list.RemoveAt(0);
        Assert.Equal(64, governor.CurrentCommittedBytes);
        Assert.Same(value, list[0]);
        Assert.Equal(list.CommittedStorageBytes, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void DeniedGovernorAttachmentLeavesTheListUnowned()
    {
        object value = new BigInteger(7);
        var list = new PyList([value, value]);
        var governor = new MemoryGovernor(64);
        var failure = Assert.Throws<LythonRuntimeException>(() => list.AttachMemoryGovernor(governor));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Null(list.OwnerMemoryGovernor);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        list.RemoveAt(0);
        Assert.Single(list);
        Assert.Same(value, list[0]);
    }

    [Theory]
    [InlineData("append", 800)]
    [InlineData("insert", 800)]
    [InlineData("extend", 850)]
    [InlineData("repeat", 1250)]
    public void DeniedBackingGrowthRefundsItsNumericDelta(string operation, long budget)
    {
        var governor = new MemoryGovernor(budget);
        object value = new BigInteger(7);
        var list = new PyList(Enumerable.Repeat(value, 8), governor);
        var before = governor.CurrentCommittedBytes;
        var failure = Assert.Throws<LythonRuntimeException>(() =>
        {
            if (operation == "append") list.Add(value);
            else if (operation == "insert") list.Insert(0, value);
            else if (operation == "extend") list.AddRange(new object[] { value, value });
            else list.RepeatInPlace(2, new LythonSourceSpan(0, 0, 0, 0));
        });
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(8, list.Count);
        foreach (var item in list) Assert.Same(value, item);
        Assert.Equal(before, governor.CurrentCommittedBytes);
        Assert.Equal(list.CommittedStorageBytes, before);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }
}
