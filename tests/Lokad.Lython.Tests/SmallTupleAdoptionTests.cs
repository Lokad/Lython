using System.Numerics;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class SmallTupleAdoptionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ChargesDistinctIdentitiesAndAliasesAcrossTheSmallTupleBoundary(int construction)
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
            var governor = new MemoryGovernor(null);
            var tuple = Make(items, governor, construction);
            var distinct = items.Where(AdoptedScalarCoupons.IsAdoptableScalar)
                .Distinct(ReferenceEqualityComparer.Instance).Count();
            var expected = PyTuple.EstimateApproximateBytes(items.Length) + distinct * AdoptedScalarCoupons.CouponBytes;
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
    public void FirstOrSecondCouponDenialRefundsConstructionAndAllowsRecovery(int construction, int fundedCoupons)
    {
        object[] items = [new BigInteger(1), new BigInteger(2)];
        var budget = PyTuple.EstimateApproximateBytes(2) + (fundedCoupons + 1) * AdoptedScalarCoupons.CouponBytes - 1;
        var governor = new MemoryGovernor(budget);
        var failure = Assert.Throws<LythonRuntimeException>(() => Make(items, governor, construction));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        var recovered = Make([items[0]], governor, construction);
        Assert.Equal(PyTuple.EstimateApproximateBytes(1) + AdoptedScalarCoupons.CouponBytes, governor.CurrentCommittedBytes);
        Assert.Same(items[0], recovered[0]);
        GC.KeepAlive(items);
        GC.KeepAlive(recovered);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void PoolOwnedScalarsStayChargedToTheirOriginalOwner(int construction)
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        object large = BigInteger.One << 100;
        LythonRuntime.OwnFreshInteger(large, governor, pool, null);
        var before = governor.CurrentCommittedBytes;
        var alias = Make([large, large], governor, construction);
        Assert.Equal(PyTuple.EstimateApproximateBytes(2), alias.CommittedStorageBytes);
        Assert.Equal(alias.CommittedStorageBytes, governor.CurrentCommittedBytes - before);
        before = governor.CurrentCommittedBytes;
        var mixed = Make([large, new BigInteger(1)], governor, construction);
        Assert.Equal(PyTuple.EstimateApproximateBytes(2) + AdoptedScalarCoupons.CouponBytes, mixed.CommittedStorageBytes);
        Assert.Equal(mixed.CommittedStorageBytes, governor.CurrentCommittedBytes - before);
        Assert.True(pool.IsTracked(large));
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
