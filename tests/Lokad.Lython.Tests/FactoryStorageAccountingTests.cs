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
    public void ListClearReleasesDiscardedBackingAndKeepsReplacementFunded()
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

    [Fact]
    public void DeniedListReplacementKeepsContentsAndOldBackingFunded()
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        var list = new PyList(Enumerable.Range(0, 20).Cast<object>().ToArray(), governor, null);
        var before = budget.ReservedBytes;
        Assert.True(owner.TryReserve(4096 - before, out var pressure));
        var failure = Assert.Throws<LythonRuntimeException>(list.Clear);
        Assert.Equal(20, list.Count);
        Assert.Equal(4096, budget.ReservedBytes);
        pressure.Dispose();
        Assert.Equal(before, budget.ReservedBytes);
        Assert.Same(failure, Assert.Throws<LythonRuntimeException>(list.Clear));
        Assert.Equal(20, list.Count);
        Assert.Equal(before, budget.ReservedBytes);
    }
}
