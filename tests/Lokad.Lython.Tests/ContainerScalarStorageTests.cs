using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Live container storage allowances: outgoing mutations refresh ownership
// snapshots; denied growth leaves existing storage and metadata funded.
public sealed class ContainerScalarStorageTests
{
    private static (LythonRuntime.ExecutionContext Context, LythonSourceSpan Span) Budgeted(long maxBytes)
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = maxBytes });
        return (context, new LythonSourceSpan(0, 0, 0, 0));
    }






    private static long StorageCharge(object value)
    {
        var owner = Assert.IsAssignableFrom<IPyOwnershipSnapshot>(value);
        Assert.True(owner.TrySnapshotOwnership(out var bytes));
        return bytes;
    }

    [Fact]
    public void ListRemovalUpdatesItsStorageEstimate()
    {
        // Removing numeric slots updates the retained storage estimate and
        // returns their allowance without changing backing capacity.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var governor = context.MemoryGovernor;
        var items = new object[] { new BigInteger(1), new BigInteger(2) };
        var before = governor.CurrentCommittedBytes;
        var list = new PyList(items, governor, span);
        var coupons = 2 * RuntimeMemoryEstimates.ScalarStorageBytes;
        Assert.Equal(list.CommittedStorageBytes, StorageCharge(list));
        var trackedBefore = StorageCharge(list);
        list.RemoveAt(0);
        list.RemoveAt(0);
        Assert.Equal(trackedBefore - coupons, StorageCharge(list));
        Assert.Equal(list.CommittedStorageBytes, StorageCharge(list));
        Assert.Equal(
            list.CommittedStorageBytes,
            governor.CurrentCommittedBytes - before);
    }

    [Theory]
    [InlineData(0, 5, 2)]
    [InlineData(4, -1, -2)]
    public void ListSteppedDeletionReleasesRemovedIdentities(int start, int end, int step)
    {
        var governor = new MemoryGovernor(65536);
        var items = Enumerable.Range(0, 5).Select(i => (object)new BigInteger(i)).ToArray();
        var list = new PyList(items, governor);
        var backing = list.CommittedStorageBytes - 5 * RuntimeMemoryEstimates.ScalarStorageBytes;

        list.DeleteSlice(new PyIndexing.SliceBounds(start, end, step));
        Assert.Equal(2, list.Count);
        Assert.Same(items[1], list[0]);
        Assert.Same(items[3], list[1]);
        list.RemoveAt(0);
        list.RemoveAt(0);

        Assert.Empty(list);
        Assert.Equal(backing, list.CommittedStorageBytes);
        Assert.Equal(backing, StorageCharge(list));
        Assert.Equal(backing, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void ListSteppedDeletionReleasesRemovedIdentitiesWithArrayStorage()
    {
        var governor = new MemoryGovernor(65536);
        var items = Enumerable.Range(0, 32).Select(i => (object)new BigInteger(i)).ToArray();
        var list = new PyList(items, governor);
        var backing = list.CommittedStorageBytes - items.Length * RuntimeMemoryEstimates.ScalarStorageBytes;

        list.DeleteSlice(new PyIndexing.SliceBounds(0, items.Length, 2));
        Assert.Equal(16, list.Count);
        for (var index = 0; index < list.Count; index++) Assert.Same(items[index * 2 + 1], list[index]);
        while (list.Count != 0) list.RemoveAt(0);

        Assert.Equal(backing, list.CommittedStorageBytes);
        Assert.Equal(backing, StorageCharge(list));
        Assert.Equal(backing, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void ListSteppedDeletionReleasesEachNumericSlot()
    {
        var governor = new MemoryGovernor(65536);
        object removed = new BigInteger(1);
        object kept = new BigInteger(2);
        var list = new PyList(new object[] { removed, kept, removed, kept, new BigInteger(3) }, governor);
        var backing = list.CommittedStorageBytes - 5 * RuntimeMemoryEstimates.ScalarStorageBytes;

        list.DeleteSlice(new PyIndexing.SliceBounds(0, 5, 2));
        Assert.Equal(2, list.Count);
        Assert.Same(kept, list[0]);
        Assert.Same(kept, list[1]);
        Assert.Equal(backing + 2 * RuntimeMemoryEstimates.ScalarStorageBytes, list.CommittedStorageBytes);
        Assert.Equal(list.CommittedStorageBytes, StorageCharge(list));
        list.RemoveAt(0);
        Assert.Equal(backing + RuntimeMemoryEstimates.ScalarStorageBytes, list.CommittedStorageBytes);
        list.RemoveAt(0);
        Assert.Equal(backing, list.CommittedStorageBytes);
        Assert.Equal(backing, StorageCharge(list));
        Assert.Equal(backing, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
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
        var set = new PySet(governor, span);
        object box = new BigInteger(7);
        Assert.True(set.Add(box));
        var trackedAfterFirst = StorageCharge(set);
        Assert.False(set.Add(box));
        Assert.Equal(trackedAfterFirst, StorageCharge(set));
        Assert.False(set.Add(new BigInteger(7)));
        Assert.Equal(trackedAfterFirst, StorageCharge(set));
    }

    [Fact]
    public void SetRemovalUpdatesItsStorageEstimate()
    {
        // Removing numeric slots updates the retained storage estimate while
        // keeping the set backing funded.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var governor = context.MemoryGovernor;
        var before = governor.CurrentCommittedBytes;
        var set = new PySet(new object[] { new BigInteger(1), new BigInteger(2) }, governor, span);
        var coupons = 2 * RuntimeMemoryEstimates.ScalarStorageBytes;
        Assert.Equal(set.CommittedStorageBytes, StorageCharge(set));
        var trackedBefore = StorageCharge(set);
        Assert.True(set.Remove(new BigInteger(1)));
        Assert.True(set.Remove(new BigInteger(2)));
        Assert.Equal(trackedBefore - coupons, StorageCharge(set));
        Assert.Equal(set.CommittedStorageBytes, StorageCharge(set));
        Assert.Equal(
            set.CommittedStorageBytes,
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
        var set = new PySet(governor, span);
        object held = new BigInteger(7);
        Assert.True(set.Add(held));
        var trackedBefore = StorageCharge(set);
        Assert.False(set.Add(new BigInteger(7)));
        Assert.True(set.Remove(new BigInteger(7)));
        Assert.Equal(trackedBefore - RuntimeMemoryEstimates.ScalarStorageBytes, StorageCharge(set));
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
            host, new LythonRunOptions { MaxExecutionMemoryBytes = baseline + RuntimeMemoryEstimates.ScalarStorageBytes - 1 });
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
        var dict = new PyDict(governor, span);
        object key = new BigInteger(1);
        dict.SetItem(key, new BigInteger(100));
        var trackedAfterInsert = StorageCharge(dict);
        dict.SetItem(key, new BigInteger(200));
        Assert.Equal(trackedAfterInsert, StorageCharge(dict));
        Assert.Equal(new BigInteger(200), dict.GetItem(key));
    }

    [Fact]
    public void DictTwinKeyRemovalReleasesTheWholeEntry()
    {
        // Removing via a structural twin (7.0 for stored 7) evicts the held pair
        // and releases its allowance without retaining a key identity map.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var governor = context.MemoryGovernor;
        var dict = new PyDict(governor, span);
        object held = new BigInteger(7);
        dict.SetItem(held, new BigInteger(100));
        var trackedBefore = StorageCharge(dict);
        Assert.True(dict.Remove(7.0));
        Assert.Equal(trackedBefore - 128L, StorageCharge(dict));
        Assert.Equal(0, dict.Count);
    }

    [Fact]
    public void DictGrowThenShrinkRetainsOnlyLiveEntryAllowances()
    {
        // Six pairs stay on the small backing. Removing each entry releases its
        // allowance; the backing remains funded through empty/refill cycles.
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
        Assert.Equal(emptySnapshot + 128L, dict.CommittedStorageBytes);
        Assert.Equal(baseline + (dict.CommittedStorageBytes - emptySnapshot), governor.CurrentCommittedBytes);
        Assert.True(dict.Remove(keys[5]));
        Assert.Equal(emptySnapshot, dict.CommittedStorageBytes);
        Assert.Equal(baseline, governor.CurrentCommittedBytes);
        dict.SetItem(keys[2], values[2]);
        Assert.Equal(emptySnapshot + 128L, dict.CommittedStorageBytes);
    }

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
            host, new LythonRunOptions { MaxExecutionMemoryBytes = baseline + RuntimeMemoryEstimates.ScalarStorageBytes - 1 });
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
    public void TupleChargesEachRetainedNumericSlot()
    {
        // Immutable storage uses a conservative per-slot estimate; aliases
        // retain Python identity and have no identity-accounting table.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var governor = context.MemoryGovernor;
        var before = governor.CurrentCommittedBytes;
        object shared = new BigInteger(9);
        var tuple = new PyTuple(new object[] { new BigInteger(1), new BigInteger(2), shared, shared }, governor, span);
        var expected = PyTuple.EstimateApproximateBytes(4) + 4 * 64L;
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
        var budget = PyTuple.EstimateApproximateBytes(items.Length) + RuntimeMemoryEstimates.ScalarStorageBytes - 1;
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
            host, new LythonRunOptions { MaxExecutionMemoryBytes = baseline + RuntimeMemoryEstimates.ScalarStorageBytes - 1 });
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

}
