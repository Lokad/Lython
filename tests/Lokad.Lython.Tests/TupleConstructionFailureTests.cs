using System.Collections;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class TupleConstructionFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FactoryFailureRefundsScratchWithoutReleasingOtherStorage(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(65536);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = owner });
        var governor = context.MemoryGovernor;
        using (var other = governor.ReserveTemporary(17, null))
        {
            var baseline = budget.ReservedBytes;
            var committed = governor.CurrentCommittedBytes;
            var expected = new InvalidOperationException("factory stopped");
            object Item(int i) => i == 0 ? PyNone.Instance : throw expected;
            async ValueTask<object> ItemAsync(int i)
            {
                await Task.Yield();
                return Item(i);
            }
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                if (asynchronous) await LythonRuntime.CreateTupleAsync(2, ItemAsync, context, null);
                else LythonRuntime.CreateTuple(2, Item, context, null);
            });
            Assert.Same(expected, failure);
            Assert.Equal(committed, governor.CurrentCommittedBytes);
            Assert.Equal(17, governor.CurrentReservedBytes);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnumerationFailureRefundsConstructionWithoutReleasingAnotherReservation(bool counted)
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(4096));
        using var other = governor.ReserveTemporary(17, null);
        var expected = new InvalidOperationException("source stopped");
        static IEnumerable<object> Values(Exception failure)
        {
            yield return new BigInteger(7);
            throw failure;
        }
        IEnumerable<object> values = Values(expected);
        if (counted) values = new CountedSource(values);
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => new PyTuple(values, governor)));
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(17, governor.CurrentReservedBytes);
        var recovered = new PyTuple(new object[] { PyNone.Instance }, governor);
        Assert.Single(recovered);
        Assert.Equal(48, governor.CurrentCommittedBytes);
        Assert.Equal(17, governor.CurrentReservedBytes);
        GC.KeepAlive(recovered);
    }

    private sealed class CountedSource(IEnumerable<object> source) : IReadOnlyCollection<object>
    {
        public int Count => 2;
        public IEnumerator<object> GetEnumerator() => source.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
