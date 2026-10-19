using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

// Unpublished transaction values report their current funding for rollback.
// Published values remain batch-owned; these snapshots do not discover aliases.
public sealed class OwnershipSnapshotContractTests
{
    [Fact]
    public void CallResultSnapshotsOwnedStringCharge()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        var text = PyString.FromString("abc", governor);

        var snapshot = Assert.IsAssignableFrom<IPyOwnershipSnapshot>(text);
        Assert.True(snapshot.TrySnapshotOwnership(out var charge));
        Assert.Equal(text.CommittedOwnedBytes, charge);
        Assert.Equal(
            text.CommittedOwnedBytes,
            governor.CurrentCommittedBytes);
        GC.KeepAlive(text);
    }

    [Fact]
    public void CallResultSnapshotsContainerBacking()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        var list = new PyList([1, 2], governor);
        var tuple = new PyTuple([1], governor);
        var set = new PySet([PyString.FromString("a")], governor);

        Assert.Equal(list.CommittedStorageBytes, SnapshotCharge(list));
        Assert.Equal(tuple.CommittedStorageBytes, SnapshotCharge(tuple));
        Assert.Equal(set.CommittedStorageBytes, SnapshotCharge(set));
        Assert.Equal(
            list.CommittedStorageBytes + tuple.CommittedStorageBytes + set.CommittedStorageBytes,
            governor.CurrentCommittedBytes);
        GC.KeepAlive(list);
        GC.KeepAlive(tuple);
        GC.KeepAlive(set);
    }

    [Fact]
    public void CallResultSnapshotsGovernedDictDequeAndCounter()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        var dict = new PyDict(governor);
        dict.SetItem("k", 1);
        var deque = new PyDeque([1], null, governor, null);
        var counter = new PyCounter(new PyCounter(), governor);

        Assert.Equal(dict.CommittedStorageBytes, SnapshotCharge(dict));
        Assert.Equal(deque.CommittedStorageBytes, SnapshotCharge(deque));
        Assert.Equal(counter.CommittedStorageBytes, SnapshotCharge(counter));
        GC.KeepAlive(dict);
        GC.KeepAlive(deque);
        GC.KeepAlive(counter);
    }

    [Fact]
    public void CallResultSnapshotsDefaultDictShell()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        var defaultdict = new PyDefaultDict(PyNone.Instance, governor);

        Assert.Equal(defaultdict.CommittedStorageBytes, SnapshotCharge(defaultdict));
        GC.KeepAlive(defaultdict);
    }

    [Fact]
    public void ViewSnapshotsAreFixedShell()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
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

            GC.KeepAlive(view);
        }

        GC.KeepAlive(dict);
    }

    [Fact]
    public void FreshInstanceSnapshotsItsFundedShell()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        var instance = new PyInstance(
            new PyType("T", [], new Dictionary<string, object>()),
            governor,
            null);

        var snapshot = Assert.IsAssignableFrom<IPyOwnershipSnapshot>(instance);
        Assert.True(snapshot.TrySnapshotOwnership(out var charge));
        Assert.Equal(160, charge);
        GC.KeepAlive(instance);
    }

    [Fact]
    public void CallResultSkipsUnownedAndUnknownValues()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        var plainText = PyString.FromString("abc");
        var plainList = new PyList();

        Assert.False(Assert.IsAssignableFrom<IPyOwnershipSnapshot>(plainText).TrySnapshotOwnership(out _));
        Assert.False(Assert.IsAssignableFrom<IPyOwnershipSnapshot>(plainList).TrySnapshotOwnership(out _));
        Assert.Equal(0, governor.CurrentCommittedBytes);
        GC.KeepAlive(plainText);
        GC.KeepAlive(plainList);
    }

    private static long SnapshotCharge(object value)
    {
        var snapshot = Assert.IsAssignableFrom<IPyOwnershipSnapshot>(value);
        Assert.True(snapshot.TrySnapshotOwnership(out var charge));
        return charge;
    }
}
