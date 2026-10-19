using System.Numerics;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class SmallTupleAdoptionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ChargesNumericSlotsWithoutChangingAliasesAcrossTupleArities(int construction)
    {
        object shared = new BigInteger(7);
        object equal = new BigInteger(7);
        foreach (var items in new[]
        {
            Array.Empty<object>(), new[] { shared }, new[] { shared, shared },
            new[] { shared, equal }, new[] { shared, shared, equal },
            new object[] { true, PyNone.Instance }, new object[] { shared, true }
        })
        {
            var governor = new MemoryGovernor(new LythonMemoryBudget(null));
            var tuple = Make(items, governor, construction);
            var slots = items.Count(item => RuntimeMemoryEstimates.EstimateScalarStorageBytes(item) > 0);
            var expected = PyTuple.EstimateApproximateBytes(items.Length) + slots * 64L;
            Assert.Equal(expected, tuple.CommittedStorageBytes);
            Assert.Equal(expected, governor.CurrentCommittedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
            for (var i = 0; i < items.Length; i++) Assert.Same(items[i], tuple[i]);
            GC.KeepAlive(tuple);
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    public void DeniedStorageRefundsConstructionWithoutAdmittingFurtherWork(int construction, int fundedCoupons)
    {
        object[] items = [new BigInteger(1), new BigInteger(2)];
        var budget = PyTuple.EstimateApproximateBytes(2) + (fundedCoupons + 1) * RuntimeMemoryEstimates.ScalarStorageBytes - 1;
        var governor = new MemoryGovernor(new LythonMemoryBudget(budget));
        var failure = Assert.Throws<LythonRuntimeException>(() => Make(items, governor, construction));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Same(failure, Assert.Throws<LythonRuntimeException>(() => new PyTuple(new object[] { items[0] }, governor)));
        Assert.Equal(0, governor.CurrentAccountedBytes);
        GC.KeepAlive(items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NumericAliasesKeepIndependentPayloadAndConservativeSlotFunding(int construction)
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        object large = BigInteger.One << 100;
        LythonRuntime.OwnFreshInteger(large, governor, null);
        var before = governor.CurrentCommittedBytes;
        var alias = Make([large, large], governor, construction);
        Assert.Equal(PyTuple.EstimateApproximateBytes(2) + 2 * RuntimeMemoryEstimates.EstimateScalarStorageBytes(large), alias.CommittedStorageBytes);
        Assert.Equal(alias.CommittedStorageBytes, governor.CurrentCommittedBytes - before);
        before = governor.CurrentCommittedBytes;
        var mixed = Make([large, new BigInteger(1)], governor, construction);
        Assert.Equal(PyTuple.EstimateApproximateBytes(2) + RuntimeMemoryEstimates.EstimateScalarStorageBytes(large) + 64, mixed.CommittedStorageBytes);
        Assert.Equal(mixed.CommittedStorageBytes, governor.CurrentCommittedBytes - before);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(alias);
        GC.KeepAlive(mixed);
    }

    private static PyTuple Make(object[] items, MemoryGovernor governor, int construction) => construction switch
    {
        0 => new PyTuple(items, governor),
        1 => new PyTuple(items.Select(static value => value), governor),
        2 => PyTuple.FromOwnedArray(items, governor, null),
        _ => throw new ArgumentOutOfRangeException(nameof(construction))
    };
}
