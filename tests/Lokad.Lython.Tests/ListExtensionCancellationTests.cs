using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ListExtensionCancellationTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InFlightCancellationStopsStoresAndDisposesKnownSource(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(128 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var list = new PyList([], context.MemoryGovernor, Span);
        var first = new object();
        var afterCancellation = new object();
        var source = new KnownSource(2, Values());
        IEnumerable<object> Values()
        {
            yield return first;
            cancellation.Cancel();
            yield return afterCancellation;
        }
        var failure = asynchronous
            ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await list.AddRangeAsync(source, context, Span))
            : Assert.Throws<LythonRuntimeException>(() => list.AddRange(source, context, Span));
        Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
        Assert.Contains(first, list);
        Assert.DoesNotContain(afterCancellation, list);
        Assert.True(source.Disposed);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(budget.ReservedBytes >= list.CommittedStorageBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void UnderestimatedKnownCountCannotGrowPastAdmittedCapacity()
    {
        var budget = new LythonMemoryBudget(PyListStorage.EstimateCapacityBytes(9) + PyListStorage.EstimateCapacityBytes(18));
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        var alias = new object();
        var list = new PyList(Enumerable.Repeat(alias, 9), governor);
        var source = new KnownSource(1, Enumerable.Repeat(alias, 32));
        var failure = Assert.Throws<LythonRuntimeException>(() => list.AddRange(source));
        Assert.Same(governor.AdmissionFailure, failure);
        Assert.True(source.Disposed);
        Assert.InRange(list.Count, 10, 40);
        foreach (var value in list) Assert.Same(alias, value);
        Assert.Equal(list.CommittedStorageBytes, governor.CurrentCommittedBytes);
        Assert.Equal(governor.CurrentAccountedBytes, budget.ReservedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class KnownSource(int count, IEnumerable<object> values) : IReadOnlyCollection<object>
    {
        public int Count => count;
        public bool Disposed;
        public IEnumerator<object> GetEnumerator()
        {
            try { foreach (var value in values) yield return value; }
            finally { Disposed = true; }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
