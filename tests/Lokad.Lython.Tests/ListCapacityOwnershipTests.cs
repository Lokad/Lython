using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ListCapacityOwnershipTests
{
    [Fact]
    public void CapacityFundsMixedValuesWithoutClassifyingOrLosingAliases()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(4096));
        object value = new BigInteger(7);
        var list = new PyList([value, value, true, false, new object()], governor);
        var charge = list.CommittedStorageBytes;
        Assert.Equal(new PyList([], governor).CommittedStorageBytes, charge);
        Assert.Same(value, list[0]);
        Assert.Same(value, list[1]);
        list[2] = value;
        list.RemoveAt(0);
        list.Add(new object());
        Assert.Equal(charge, list.CommittedStorageBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void LargePayloadKeepsItsIndependentFunding()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(), regexProvider: Utf8RegexProvider.Instance);
        object value = BigInteger.One << 4000;
        LythonRuntime.OwnFreshInteger(value, context.MemoryGovernor, null);
        var payload = context.MemoryGovernor.CurrentCommittedBytes;
        var list = new PyList([value, value], context.MemoryGovernor);
        Assert.Equal(payload + list.CommittedStorageBytes, context.MemoryGovernor.CurrentCommittedBytes);
        list.RemoveAt(0);
        Assert.Same(value, list[0]);
        Assert.Equal(payload + list.CommittedStorageBytes, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Theory]
    [InlineData("append")]
    [InlineData("insert")]
    [InlineData("extend")]
    [InlineData("repeat")]
    public void DeniedCapacityGrowthPreservesContentsAndFunding(string operation)
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(PyListStorage.EstimateCapacityBytes(8)));
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
        Assert.Same(governor.AdmissionFailure, failure);
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(8, list.Count);
        foreach (var item in list) Assert.Same(value, item);
        Assert.Equal(before, governor.CurrentCommittedBytes);
        Assert.Equal(list.CommittedStorageBytes, before);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(12)]
    public void AttachmentFundsExistingBackingAndPreservesFirstOwner(int count)
    {
        object value = new BigInteger(7);
        var list = new PyList(Enumerable.Repeat(value, count));
        var governor = new MemoryGovernor(new LythonMemoryBudget(4096));
        list.AttachMemoryGovernor(governor);
        var before = governor.CurrentCommittedBytes;
        Assert.True(before >= PyListStorage.EstimateCapacityBytes(count));
        list.RemoveAt(0);
        Assert.Equal(before, governor.CurrentCommittedBytes);
        Assert.Same(value, list[0]);
        var other = new MemoryGovernor(new LythonMemoryBudget(4096));
        list.AttachMemoryGovernor(other);
        Assert.Same(governor, list.OwnerMemoryGovernor);
        Assert.Equal(0, other.CurrentAccountedBytes);
        Assert.Equal(list.CommittedStorageBytes, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void DeniedAttachmentLeavesNonnumericBackingAndOwnerUnchanged()
    {
        var value = new object();
        var list = new PyList([value]);
        var governor = new MemoryGovernor(new LythonMemoryBudget(PyListStorage.EstimateCapacityBytes(8) - 1));
        var failure = Assert.Throws<LythonRuntimeException>(() => list.AttachMemoryGovernor(governor));
        Assert.Same(governor.AdmissionFailure, failure);
        Assert.Null(list.OwnerMemoryGovernor);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Same(value, Assert.Single(list));
    }
}
