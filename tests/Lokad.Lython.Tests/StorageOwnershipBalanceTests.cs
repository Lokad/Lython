using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Construction funding and retained storage balances. Snapshot checks remain
// only for values actually used by unpublished construction rollback.
public sealed class StorageOwnershipBalanceTests
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
    public void ContainerBackingStaysFunded()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        var list = new PyList([1, 2], governor);
        var tuple = new PyTuple([1], governor);
        var set = new PySet([PyString.FromString("a")], governor);

        Assert.Equal(
            list.CommittedStorageBytes + tuple.CommittedStorageBytes + set.CommittedStorageBytes,
            governor.CurrentCommittedBytes);
        GC.KeepAlive(list);
        GC.KeepAlive(tuple);
        GC.KeepAlive(set);
    }

    [Fact]
    public void DictDequeAndCounterRemainFunded()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        var dict = new PyDict(governor);
        dict.SetItem("k", 1);
        var deque = new PyDeque([1], null, governor, null);
        var counter = new PyCounter(new PyCounter(), governor);

        Assert.Equal(dict.CommittedStorageBytes + deque.CommittedStorageBytes + counter.CommittedStorageBytes, governor.CurrentCommittedBytes);
        GC.KeepAlive(dict);
        GC.KeepAlive(deque);
        GC.KeepAlive(counter);
    }

    [Fact]
    public void DefaultDictShellRemainsFunded()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        var defaultdict = new PyDefaultDict(PyNone.Instance, governor);

        Assert.Equal(defaultdict.CommittedStorageBytes, governor.CurrentCommittedBytes);
        GC.KeepAlive(defaultdict);
    }

    [Fact]
    public void ViewsFundTheirShells()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var governor = context.MemoryGovernor;
        var dict = new PyDict(governor);
        var before = governor.CurrentCommittedBytes;
        var views = new List<object>();
        foreach (var name in new[] { "keys", "values", "items" })
        {
            Assert.True(LythonRuntime.DictMembers.TryGetMember(dict, name, out var member));
            views.Add(Assert.IsAssignableFrom<LythonRuntime.ICallable>(member).Invoke([], new(0, 0, 0, 0), context));
        }
        Assert.Equal(before + 3 * 64L, governor.CurrentCommittedBytes);
        GC.KeepAlive(views);

        GC.KeepAlive(dict);
    }

    [Fact]
    public void FreshInstanceFundsItsShell()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        var instance = new PyInstance(
            new PyType("T", [], new Dictionary<string, object>()),
            governor,
            null);

        Assert.Equal(160, governor.CurrentCommittedBytes);
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

}
