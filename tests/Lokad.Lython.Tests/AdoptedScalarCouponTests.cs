using System.Numerics;
using System.Reflection;
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
    public void AdoptAllDenialRestoresPreheldAlias()
    {
        // N27: the rollback record precedes each coupon, so a batch denied
        // after touching a pre-held alias unwinds to that alias alone.
        var (context, span) = Budgeted(2 * AdoptedScalarCoupons.CouponBytes + 8);
        var coupons = new AdoptedScalarCoupons();
        object box = new BigInteger(4);
        coupons.Adopt(box, context.MemoryGovernor, span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var values = new object[] { box, new BigInteger(5), new BigInteger(6) };
        Assert.Throws<LythonRuntimeException>(() => coupons.AdoptAll(values, context.MemoryGovernor, span));
        Assert.Equal(AdoptedScalarCoupons.CouponBytes, coupons.CommittedBytes);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        coupons.Release(box, context.MemoryGovernor);
        Assert.Equal(0, coupons.CommittedBytes);
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

    private static long PooledCharge(ChargeReclamationPool pool, object value)
    {
        // Reads the pool entry snapshot like a later drop sweep would: renames
        // fail loudly here by design.
        var table = typeof(ChargeReclamationPool)
            .GetField("TrackedStorage", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        var args = new object?[] { value, null };
        if (!(bool)table.GetType().GetMethod("TryGetValue")!.Invoke(table, args)!)
        {
            throw new InvalidOperationException("Value is not pool-tracked.");
        }

        return (long)args[1]!.GetType().GetProperty("ValueCharge")!.GetValue(args[1])!;
    }

    [Fact]
    public void ListRemovalRefreshesThePoolSnapshot()
    {
        // Removing adopted identities must refresh the tracked snapshot, or a
        // later drop sweep would release the stale (higher) charge and corrupt
        // the governor balance.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var governor = context.MemoryGovernor;
        var pool = new ChargeReclamationPool(governor);
        var items = new object[] { new BigInteger(1), new BigInteger(2) };
        var before = governor.CurrentCommittedBytes;
        var list = new PyList(items, governor, span);
        var coupons = 2 * AdoptedScalarCoupons.CouponBytes;
        pool.TrackFreshMutable(list, list.CommittedStorageBytes, span);
        Assert.Equal(list.CommittedStorageBytes, PooledCharge(pool, list));
        var trackedBefore = PooledCharge(pool, list);
        list.RemoveAt(0);
        list.RemoveAt(0);
        Assert.Equal(trackedBefore - coupons, PooledCharge(pool, list));
        Assert.Equal(list.CommittedStorageBytes, PooledCharge(pool, list));
        Assert.Equal(
            list.CommittedStorageBytes + ChargeReclamationPool.EntryChargeBytes + pool.CommittedBackingBytes,
            governor.CurrentCommittedBytes - before);
    }

    [Fact]
    public void SetDuplicateAddAdoptsOnce()
    {
        // Sets deduplicate: re-adding a held identity (or a structural twin)
        // retains nothing new, so no second coupon commits.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var governor = context.MemoryGovernor;
        var pool = new ChargeReclamationPool(governor);
        var set = new PySet(governor, span);
        pool.TrackFreshMutable(set, set.CommittedStorageBytes, span);
        object box = new BigInteger(7);
        Assert.True(set.Add(box));
        var trackedAfterFirst = PooledCharge(pool, set);
        Assert.False(set.Add(box));
        Assert.Equal(trackedAfterFirst, PooledCharge(pool, set));
        Assert.False(set.Add(new BigInteger(7)));
        Assert.Equal(trackedAfterFirst, PooledCharge(pool, set));
    }

    [Fact]
    public void SetRemovalRefreshesThePoolSnapshot()
    {
        // Like lists: released coupons must refresh the tracked snapshot, or a
        // later drop sweep would over-release.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var governor = context.MemoryGovernor;
        var pool = new ChargeReclamationPool(governor);
        var before = governor.CurrentCommittedBytes;
        var set = new PySet(new object[] { new BigInteger(1), new BigInteger(2) }, governor, span);
        var coupons = 2 * AdoptedScalarCoupons.CouponBytes;
        pool.TrackFreshMutable(set, set.CommittedStorageBytes, span);
        Assert.Equal(set.CommittedStorageBytes, PooledCharge(pool, set));
        var trackedBefore = PooledCharge(pool, set);
        Assert.True(set.Remove(new BigInteger(1)));
        Assert.True(set.Remove(new BigInteger(2)));
        Assert.Equal(trackedBefore - coupons, PooledCharge(pool, set));
        Assert.Equal(set.CommittedStorageBytes, PooledCharge(pool, set));
        Assert.Equal(
            set.CommittedStorageBytes + ChargeReclamationPool.EntryChargeBytes + pool.CommittedBackingBytes,
            governor.CurrentCommittedBytes - before);
    }

    [Fact]
    public void SetStructuralTwinRemovesHeldBox()
    {
        // Removal resolves the stored identity: discarding via an equal-but-
        // distinct box evicts the held box and releases exactly its coupon.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var governor = context.MemoryGovernor;
        var pool = new ChargeReclamationPool(governor);
        var set = new PySet(governor, span);
        object held = new BigInteger(7);
        Assert.True(set.Add(held));
        pool.TrackFreshMutable(set, set.CommittedStorageBytes, span);
        var trackedBefore = PooledCharge(pool, set);
        Assert.False(set.Add(new BigInteger(7)));
        Assert.True(set.Remove(new BigInteger(7)));
        Assert.Equal(trackedBefore - AdoptedScalarCoupons.CouponBytes, PooledCharge(pool, set));
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void FailedGrowthLeavesTableUnchanged()
    {
        // MG05: a denied coupon rolls the table insertion back, so failed growth
        // leaves no enlarged uncharged capacity: count and charges are exactly as
        // before, and a funded retry succeeds. Nothing is pool-tracked here, so
        // exhaustion relief short-circuits deterministically instead of pausing
        // for a collection.
        var host = new MockLythonHost();
        var span = new LythonSourceSpan(0, 0, 0, 0);
        static PySet BuildSet(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        {
            var set = new PySet(context.MemoryGovernor, span);
            for (var i = 0; i < 10; i++)
            {
                Assert.True(set.Add(new BigInteger(1000 + i)));
            }

            return set;
        }

        var measureContext = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var measured = BuildSet(measureContext, span);
        var baseline = measureContext.MemoryGovernor.CurrentCommittedBytes;
        var denyContext = new LythonRuntime.ExecutionContext(
            host, new LythonRunOptions { MaxExecutionMemoryBytes = baseline + AdoptedScalarCoupons.CouponBytes - 1 });
        var denied = BuildSet(denyContext, span);
        Assert.Equal(baseline, denyContext.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(10, denied.Count);
        var storedBefore = denied.CommittedStorageBytes;
        Assert.Throws<LythonRuntimeException>(() => denied.Add(new BigInteger(99999)));
        Assert.Equal(10, denied.Count);
        Assert.Equal(storedBefore, denied.CommittedStorageBytes);
        Assert.Equal(baseline, denyContext.MemoryGovernor.CurrentCommittedBytes);
        var retryContext = new LythonRuntime.ExecutionContext(
            host, new LythonRunOptions { MaxExecutionMemoryBytes = baseline + 65536 });
        var retried = BuildSet(retryContext, span);
        Assert.True(retried.Add(new BigInteger(99999)));
        Assert.Equal(11, retried.Count);
        Assert.Equal(measured.Count, retried.Count - 1);
    }

    [Fact]
    public void DictReplaceTurnsOverValueCoupon()
    {
        // Replacing a value adopts the incoming box and releases the displaced
        // one while the original key stays put: the tracked total is unchanged.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var governor = context.MemoryGovernor;
        var pool = new ChargeReclamationPool(governor);
        var dict = new PyDict(governor, span);
        object key = new BigInteger(1);
        dict.SetItem(key, new BigInteger(100));
        pool.TrackFreshMutable(dict, dict.CommittedStorageBytes, span);
        var trackedAfterInsert = PooledCharge(pool, dict);
        dict.SetItem(key, new BigInteger(200));
        Assert.Equal(trackedAfterInsert, PooledCharge(pool, dict));
        Assert.Equal(new BigInteger(200), dict.GetItem(key));
    }

    [Fact]
    public void DictTwinKeyRemovesValueOnly()
    {
        // Removing via a structural twin (7.0 for stored 7) evicts the held pair
        // but releases only the stored value coupon: the twin argument was never
        // adopted, and the stored key coupon strands conservatively till drop.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var governor = context.MemoryGovernor;
        var pool = new ChargeReclamationPool(governor);
        var dict = new PyDict(governor, span);
        object held = new BigInteger(7);
        dict.SetItem(held, new BigInteger(100));
        pool.TrackFreshMutable(dict, dict.CommittedStorageBytes, span);
        var trackedBefore = PooledCharge(pool, dict);
        Assert.True(dict.Remove(7.0));
        Assert.Equal(trackedBefore - AdoptedScalarCoupons.CouponBytes, PooledCharge(pool, dict));
        Assert.Equal(0, dict.Count);
    }

    [Fact]
    public void DictGrowThenShrinkToOneRetainsSingleCoupon()
    {
        // N27: the helper survives a grow/shrink cycle retaining one adopted identity (it is
        // discarded only when its committed total reaches zero). Six pairs stay on the small
        // store so the only moving charges are coupons; promotion replace-and-release has its
        // own coverage. Same-reference removals release exactly, leaving two coupons; dropping
        // the last identity refunds everything with no stranding.
        var (context, span) = Budgeted(1L << 20);
        var governor = context.MemoryGovernor;
        var dict = new PyDict(governor, span);
        var baseline = governor.CurrentCommittedBytes;
        var emptySnapshot = dict.CommittedStorageBytes;
        var keys = new object[6];
        var values = new object[6];
        for (var i = 0; i < 6; i++)
        {
            keys[i] = new BigInteger(i);
            values[i] = new BigInteger(1000 + i);
            dict.SetItem(keys[i], values[i]);
        }

        for (var i = 0; i < 5; i++)
        {
            Assert.True(dict.Remove(keys[i]));
        }

        Assert.Equal(1, dict.Length);
        var coupons = ReadScalarCoupons(dict);
        Assert.NotNull(coupons);
        Assert.Equal(2 * AdoptedScalarCoupons.CouponBytes, coupons!.CommittedBytes);
        Assert.Equal(baseline + (dict.CommittedStorageBytes - emptySnapshot), governor.CurrentCommittedBytes);
        Assert.True(dict.Remove(keys[5]));
        Assert.Null(ReadScalarCoupons(dict));
        Assert.Equal(baseline, governor.CurrentCommittedBytes);
        dict.SetItem(keys[2], values[2]);
        Assert.Equal(2 * AdoptedScalarCoupons.CouponBytes, ReadScalarCoupons(dict)!.CommittedBytes);
    }

    private static AdoptedScalarCoupons? ReadScalarCoupons(PyDict dict)
        => (AdoptedScalarCoupons?)typeof(PyDict)
            .GetField("_scalarCoupons", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(dict);

    [Fact]
    public void DictCopyAdoptsSharedPairs()
    {
        // Copies adopt shared identities again (bounded double charge), so each
        // side carries its own coupons: identical construction commits identical
        // totals, and the shared boxes stay identical across the copy.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var governor = context.MemoryGovernor;
        var before = governor.CurrentCommittedBytes;
        var source = new PyDict(governor, span);
        object key = new BigInteger(1);
        object value = new BigInteger(2);
        source.SetItem(key, value);
        var copy = new PyDict(source);
        Assert.Equal(source.CommittedStorageBytes, copy.CommittedStorageBytes);
        Assert.Equal(
            source.CommittedStorageBytes + copy.CommittedStorageBytes,
            governor.CurrentCommittedBytes - before);
        Assert.True(ReferenceEquals(copy.GetItem(key), value));
    }

    [Fact]
    public void DictFailedGrowthLeavesTableUnchanged()
    {
        // MG05: a denied coupon rolls the dict insertion back, so failed growth
        // leaves no enlarged uncharged capacity: count and charges are exactly as
        // before, and a funded retry succeeds. Nothing is pool-tracked here, so
        // exhaustion relief short-circuits deterministically.
        var host = new MockLythonHost();
        var span = new LythonSourceSpan(0, 0, 0, 0);
        static PyDict BuildDict(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        {
            var dict = new PyDict(context.MemoryGovernor, span);
            for (var i = 0; i < 10; i++)
            {
                dict.SetItem(new BigInteger(1000 + i), new BigInteger(i));
            }

            return dict;
        }

        var measureContext = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var measured = BuildDict(measureContext, span);
        Assert.Equal(10, measured.Count);
        var baseline = measureContext.MemoryGovernor.CurrentCommittedBytes;
        var denyContext = new LythonRuntime.ExecutionContext(
            host, new LythonRunOptions { MaxExecutionMemoryBytes = baseline + AdoptedScalarCoupons.CouponBytes - 1 });
        var denied = BuildDict(denyContext, span);
        Assert.Equal(baseline, denyContext.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(10, denied.Count);
        var storedBefore = denied.CommittedStorageBytes;
        Assert.Throws<LythonRuntimeException>(() => denied.SetItem(new BigInteger(99999), new BigInteger(1)));
        Assert.Equal(10, denied.Count);
        Assert.Equal(storedBefore, denied.CommittedStorageBytes);
        Assert.Equal(baseline, denyContext.MemoryGovernor.CurrentCommittedBytes);
        var retryContext = new LythonRuntime.ExecutionContext(
            host, new LythonRunOptions { MaxExecutionMemoryBytes = baseline + 65536 });
        var retried = BuildDict(retryContext, span);
        retried.SetItem(new BigInteger(99999), new BigInteger(1));
        Assert.Equal(11, retried.Count);
    }

    [Fact]
    public void TupleAdoptsDistinctOnce()
    {
        // Tuples adopt construction contents with per-identity dedup: distinct
        // boxes earn one coupon each, aliases share.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var governor = context.MemoryGovernor;
        var before = governor.CurrentCommittedBytes;
        object shared = new BigInteger(9);
        var tuple = new PyTuple(new object[] { new BigInteger(1), new BigInteger(2), shared, shared }, governor, span);
        var expected = PyTuple.EstimateApproximateBytes(4) + 3 * AdoptedScalarCoupons.CouponBytes;
        Assert.Equal(expected, tuple.CommittedStorageBytes);
        Assert.Equal(expected, governor.CurrentCommittedBytes - before);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void TupleDenialRefundsStorage()
    {
        // A denied construction coupon refunds the orphaned backing: nothing
        // stays committed and the tuple never publishes.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var governor = context.MemoryGovernor;
        var before = governor.CurrentCommittedBytes;
        var items = new object[] { new BigInteger(1), new BigInteger(2), new BigInteger(3) };
        var budget = PyTuple.EstimateApproximateBytes(items.Length) + AdoptedScalarCoupons.CouponBytes - 1;
        var tight = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = budget });
        Assert.Throws<LythonRuntimeException>(() => new PyTuple(items, tight.MemoryGovernor, span));
        Assert.Equal(0, tight.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void DequeEvictionTurnsOverCoupon()
    {
        // Bounded eviction adopts the incoming box and releases the evicted one:
        // node and coupon totals both stay flat across rotation.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var governor = context.MemoryGovernor;
        var deque = new PyDeque(2, governor, span);
        object a = new BigInteger(1);
        object b = new BigInteger(2);
        deque.Append(a);
        deque.Append(b);
        var tracked = deque.CommittedStorageBytes;
        deque.Append(new BigInteger(3));
        var current = deque.ToArray();
        Assert.Equal(2, current.Length);
        Assert.Same(b, current[0]);
        Assert.Equal(new BigInteger(3), current[1]);
        Assert.Equal(tracked, deque.CommittedStorageBytes);
    }

    [Fact]
    public void DequeFailedAppendRollsBackNode()
    {
        // A denied coupon rolls the node insertion back: count and charges are
        // exactly as before, and a funded retry succeeds. Nothing is
        // pool-tracked here, so exhaustion relief short-circuits deterministically.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        static PyDeque BuildDeque(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        {
            var deque = new PyDeque(null, context.MemoryGovernor, span);
            for (var i = 0; i < 5; i++)
            {
                deque.Append(new BigInteger(i));
            }

            return deque;
        }

        var measureContext = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        _ = BuildDeque(measureContext, span);
        var baseline = measureContext.MemoryGovernor.CurrentCommittedBytes;
        var denyContext = new LythonRuntime.ExecutionContext(
            host, new LythonRunOptions { MaxExecutionMemoryBytes = baseline + AdoptedScalarCoupons.CouponBytes - 1 });
        var denied = BuildDeque(denyContext, span);
        Assert.Equal(baseline, denyContext.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(5, denied.Count);
        var storedBefore = denied.CommittedStorageBytes;
        Assert.Throws<LythonRuntimeException>(() => denied.Append(new BigInteger(999)));
        Assert.Equal(5, denied.Count);
        Assert.Equal(storedBefore, denied.CommittedStorageBytes);
        Assert.Equal(baseline, denyContext.MemoryGovernor.CurrentCommittedBytes);
        var retryContext = new LythonRuntime.ExecutionContext(
            host, new LythonRunOptions { MaxExecutionMemoryBytes = baseline + 65536 });
        var retried = BuildDeque(retryContext, span);
        retried.Append(new BigInteger(999));
        Assert.Equal(6, retried.Count);
    }

    [Fact]
    public void DequeSetIndexTurnsOverCoupon()
    {
        // Indexed writes adopt the incoming box and release the displaced one.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var governor = context.MemoryGovernor;
        var deque = new PyDeque(null, governor, span);
        object kept = new BigInteger(1);
        deque.Append(kept);
        deque.Append(new BigInteger(2));
        var before = governor.CurrentCommittedBytes;
        deque.SetIndex(1, new BigInteger(3));
        Assert.True(ReferenceEquals(kept, deque.GetIndex(0)));
        Assert.Equal(new BigInteger(3), deque.GetIndex(1));
        Assert.Equal(before, governor.CurrentCommittedBytes);
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