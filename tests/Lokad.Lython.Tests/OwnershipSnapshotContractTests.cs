using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

// R16: ownership snapshots are a common internal contract. Each governed
// value reports its exact pool coupon through IPyOwnershipSnapshot, so
// TrackCallResult dispatches on the contract instead of a pool-side
// concrete-type switch. These tests pin the per-type rules (amounts,
// owned/unowned distinction, fixed view shells, alias dedup) and the
// Debug pairing (every tracked coupon releases exactly once).
public sealed class OwnershipSnapshotContractTests
{
    [Fact]
    public void CallResultSnapshotsOwnedStringCharge()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var text = PyString.FromString("abc", governor);

        var snapshot = Assert.IsAssignableFrom<IPyOwnershipSnapshot>(text);
        Assert.True(snapshot.TrySnapshotOwnership(out var charge));
        Assert.Equal(text.CommittedOwnedBytes, charge);

        pool.TrackCallResult(text);
        Assert.Equal(1, pool.Count);
        Assert.Equal(
            text.CommittedOwnedBytes + ChargeReclamationPool.EntryChargeBytes + pool.CommittedBackingBytes,
            governor.CurrentCommittedBytes);
        GC.KeepAlive(text);
    }

    [Fact]
    public void CallResultSnapshotsContainerBacking()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var list = new PyList([1, 2], governor);
        var tuple = new PyTuple([1], governor);
        var set = new PySet([PyString.FromString("a")], governor);

        Assert.Equal(list.CommittedStorageBytes, SnapshotCharge(list));
        Assert.Equal(tuple.CommittedStorageBytes, SnapshotCharge(tuple));
        Assert.Equal(set.CommittedStorageBytes, SnapshotCharge(set));

        pool.TrackCallResult(list);
        pool.TrackCallResult(tuple);
        pool.TrackCallResult(set);
        Assert.Equal(3, pool.Count);
        Assert.Equal(
            list.CommittedStorageBytes + tuple.CommittedStorageBytes + set.CommittedStorageBytes
                + (3 * ChargeReclamationPool.EntryChargeBytes) + pool.CommittedBackingBytes,
            governor.CurrentCommittedBytes);
        GC.KeepAlive(list);
        GC.KeepAlive(tuple);
        GC.KeepAlive(set);
    }

    [Fact]
    public void CallResultSnapshotsGovernedDictDequeAndCounter()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var dict = new PyDict(governor);
        dict.SetItem("k", 1);
        var deque = new PyDeque([1], null, governor, null);
        var counter = new PyCounter(new PyCounter(), governor);

        Assert.Equal(dict.CommittedStorageBytes, SnapshotCharge(dict));
        Assert.Equal(deque.CommittedStorageBytes, SnapshotCharge(deque));
        Assert.Equal(counter.CommittedStorageBytes, SnapshotCharge(counter));

        pool.TrackCallResult(dict);
        pool.TrackCallResult(deque);
        pool.TrackCallResult(counter);
        Assert.Equal(3, pool.Count);
        GC.KeepAlive(dict);
        GC.KeepAlive(deque);
        GC.KeepAlive(counter);
    }

    [Fact]
    public void CallResultSnapshotsDefaultDictShell()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var defaultdict = new PyDefaultDict(PyNone.Instance, governor);

        Assert.Equal(defaultdict.CommittedStorageBytes, SnapshotCharge(defaultdict));

        pool.TrackCallResult(defaultdict);
        Assert.Equal(1, pool.Count);
        GC.KeepAlive(defaultdict);
    }

    [Fact]
    public void ViewSnapshotsAreFixedShell()
    {
        var governor = new MemoryGovernor(null);
        var dict = new PyDict(governor);
        object[] views = [
            new LythonRuntime.DictKeysView(dict),
            new LythonRuntime.DictValuesView(dict),
            new LythonRuntime.DictItemsView(dict),
        ];
        foreach (var view in views)
        {
            var snapshot = Assert.IsAssignableFrom<IPyOwnershipSnapshot>(view);
            Assert.True(snapshot.TrySnapshotOwnership(out var charge));
            Assert.Equal(OwnershipSnapshot.ViewShellBytes, charge);

            var viewPool = new ChargeReclamationPool(governor);
            viewPool.TrackCallResult(view);
            Assert.Equal(1, viewPool.Count);
            GC.KeepAlive(view);
        }

        GC.KeepAlive(dict);
    }

    [Fact]
    public void FreshInstanceSnapshotsZeroAttributeBytes()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var instance = new PyInstance(
            new PyType("T", [], new Dictionary<string, object>()),
            governor,
            null);

        var snapshot = Assert.IsAssignableFrom<IPyOwnershipSnapshot>(instance);
        Assert.True(snapshot.TrySnapshotOwnership(out var charge));
        Assert.Equal(0, charge);

        // Owned but empty: no coupon, so nothing registers.
        pool.TrackCallResult(instance);
        Assert.Equal(0, pool.Count);
        GC.KeepAlive(instance);
    }

    [Fact]
    public void CallResultSkipsUnownedAndUnknownValues()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var plainText = PyString.FromString("abc");
        var plainList = new PyList();

        Assert.False(Assert.IsAssignableFrom<IPyOwnershipSnapshot>(plainText).TrySnapshotOwnership(out _));
        Assert.False(Assert.IsAssignableFrom<IPyOwnershipSnapshot>(plainList).TrySnapshotOwnership(out _));

        pool.TrackCallResult(plainText);
        pool.TrackCallResult(plainList);
        pool.TrackCallResult(new System.Numerics.BigInteger(5));
        pool.TrackCallResult(PyNone.Instance);
        Assert.Equal(0, pool.Count);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        GC.KeepAlive(plainText);
        GC.KeepAlive(plainList);
    }

    [Fact]
    public void CallResultDedupsAliases()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var list = new PyList([1], governor);

        pool.TrackCallResult(list);
        pool.TrackCallResult(list);
        Assert.Equal(1, pool.Count);
        Assert.Equal(
            list.CommittedStorageBytes + ChargeReclamationPool.EntryChargeBytes + pool.CommittedBackingBytes,
            governor.CurrentCommittedBytes);
        GC.KeepAlive(list);
    }

    private static long SnapshotCharge(object value)
    {
        var snapshot = Assert.IsAssignableFrom<IPyOwnershipSnapshot>(value);
        Assert.True(snapshot.TrySnapshotOwnership(out var charge));
        return charge;
    }
}
