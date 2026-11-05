using Lokad.Lython.Runtime;
namespace Lokad.Lython.Tests;

public sealed class FactoryStorageAccountingTests
{
    [Fact]
    public void TupleStorageStaysFundedUntilItsOwnerEnds()
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        var tuple = new PyTuple(new object[] { 1, 2, 3 }, governor, null);
        Assert.Equal(tuple.CommittedStorageBytes, budget.ReservedBytes);
        Assert.Equal(3, tuple.Count);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ListClearReleasesDiscardedBackingAndKeepsMetadataFunded()
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        var list = new PyList(Enumerable.Range(0, 20).Cast<object>().ToArray(), governor, null);
        var before = budget.ReservedBytes;
        list.Clear();
        Assert.Empty(list);
        Assert.Equal(list.CommittedStorageBytes, budget.ReservedBytes);
        Assert.True(before > budget.ReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

}
