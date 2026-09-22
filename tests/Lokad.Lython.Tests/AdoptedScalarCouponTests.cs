using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// N06: container-adopted distinct-scalar coupons. One uniform coupon per
// distinct identity (reference equality); aliases share it through refcounts;
// pool-owned boxes stay under their existing owner; denial leaves nothing
// committed and nothing recorded.
public sealed class AdoptedScalarCouponTests
{
    private static (LythonRuntime.ExecutionContext Context, LythonSourceSpan Span) Budgeted(long maxBytes)
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = maxBytes });
        return (context, new LythonSourceSpan(0, 0, 0, 0));
    }

    [Fact]
    public void DistinctIdentitiesCommitOneCouponEach()
    {
        var (context, span) = Budgeted(65536);
        var coupons = new AdoptedScalarCoupons();
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        coupons.Adopt(new BigInteger(1), context.MemoryGovernor, span);
        coupons.Adopt(2.5, context.MemoryGovernor, span);
        coupons.Adopt(7, context.MemoryGovernor, span);
        Assert.Equal(3 * AdoptedScalarCoupons.CouponBytes, coupons.CommittedBytes);
        Assert.Equal(3 * AdoptedScalarCoupons.CouponBytes, context.MemoryGovernor.CurrentCommittedBytes - before);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void AliasOfOneBoxSharesASingleCoupon()
    {
        var (context, span) = Budgeted(65536);
        var coupons = new AdoptedScalarCoupons();
        object box = new BigInteger(9);
        coupons.Adopt(box, context.MemoryGovernor, span);
        coupons.Adopt(box, context.MemoryGovernor, span);
        coupons.Adopt(box, context.MemoryGovernor, span);
        Assert.Equal(AdoptedScalarCoupons.CouponBytes, coupons.CommittedBytes);
        coupons.Release(box, context.MemoryGovernor);
        Assert.Equal(AdoptedScalarCoupons.CouponBytes, coupons.CommittedBytes);
        coupons.Release(box, context.MemoryGovernor);
        coupons.Release(box, context.MemoryGovernor);
        Assert.Equal(0, coupons.CommittedBytes);
    }

    [Fact]
    public void NonScalarsStayFree()
    {
        var (context, span) = Budgeted(65536);
        var coupons = new AdoptedScalarCoupons();
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        coupons.Adopt("text", context.MemoryGovernor, span);
        coupons.Adopt(true, context.MemoryGovernor, span);
        coupons.Adopt(null, context.MemoryGovernor, span);
        coupons.Adopt(new object(), context.MemoryGovernor, span);
        Assert.Equal(0, coupons.CommittedBytes);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void PoolOwnedBoxStaysUnderItsOwner()
    {
        var (context, span) = Budgeted(65536);
        var pool = new ChargeReclamationPool(context.MemoryGovernor);
        object big = BigInteger.Pow(2, 100);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        LythonRuntime.OwnFreshInteger(big, context.MemoryGovernor, pool, span);
        var owned = context.MemoryGovernor.CurrentCommittedBytes - before;
        Assert.True(owned > 0);
        var coupons = new AdoptedScalarCoupons();
        coupons.Adopt(big, context.MemoryGovernor, span);
        Assert.Equal(0, coupons.CommittedBytes);
        Assert.Equal(owned, context.MemoryGovernor.CurrentCommittedBytes - before);
    }

    [Fact]
    public void AdoptDenialCommitsAndRecordsNothing()
    {
        var (context, span) = Budgeted(AdoptedScalarCoupons.CouponBytes + 8);
        var coupons = new AdoptedScalarCoupons();
        coupons.Adopt(new BigInteger(1), context.MemoryGovernor, span);
        Assert.Throws<LythonRuntimeException>(() => coupons.Adopt(new BigInteger(2), context.MemoryGovernor, span));
        Assert.Equal(AdoptedScalarCoupons.CouponBytes, coupons.CommittedBytes);
        coupons.Release(new BigInteger(1), context.MemoryGovernor);
        Assert.Equal(AdoptedScalarCoupons.CouponBytes, coupons.CommittedBytes);
    }

    [Fact]
    public void AdoptAllDenialRollsBackTheBatch()
    {
        var (context, span) = Budgeted(AdoptedScalarCoupons.CouponBytes + 8);
        var coupons = new AdoptedScalarCoupons();
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var values = new object[] { new BigInteger(1), new BigInteger(2), new BigInteger(3) };
        Assert.Throws<LythonRuntimeException>(() => coupons.AdoptAll(values, context.MemoryGovernor, span));
        Assert.Equal(0, coupons.CommittedBytes);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void AddRefMovesOnlyRefcounts()
    {
        var (context, span) = Budgeted(65536);
        var coupons = new AdoptedScalarCoupons();
        object box = new BigInteger(4);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        coupons.Adopt(box, context.MemoryGovernor, span);
        coupons.AddRef(box, 99);
        Assert.Equal(AdoptedScalarCoupons.CouponBytes, coupons.CommittedBytes);
        Assert.Equal(AdoptedScalarCoupons.CouponBytes, context.MemoryGovernor.CurrentCommittedBytes - before);
        for (var i = 0; i < 99; i++)
        {
            coupons.Release(box, context.MemoryGovernor);
        }

        Assert.Equal(AdoptedScalarCoupons.CouponBytes, coupons.CommittedBytes);
        coupons.Release(box, context.MemoryGovernor);
        Assert.Equal(0, coupons.CommittedBytes);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void ReleaseAllDropsEveryCoupon()
    {
        var (context, span) = Budgeted(65536);
        var coupons = new AdoptedScalarCoupons();
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        coupons.Adopt(new BigInteger(1), context.MemoryGovernor, span);
        coupons.Adopt(1.5, context.MemoryGovernor, span);
        coupons.ReleaseAll(context.MemoryGovernor);
        Assert.Equal(0, coupons.CommittedBytes);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
    }
}