using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CounterElementsCancellationTests
{
    [Fact]
    public void RepeatedPullObservesTheActiveCancellationToken()
    {
        var budget = new LythonMemoryBudget(32768);
        using (var owner = budget.CreateScope())
        {
            using var cancellation = new CancellationTokenSource();
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var counter = new PyCounter(context.MemoryGovernor);
            counter.SetItem(BigInteger.One, new BigInteger(long.MaxValue));
            var iterator = new PyCounterElementsIterator(counter, context, new LythonSourceSpan(0, 0, 0, 0));
            Assert.True(iterator.TryMoveNext(out var value));
            Assert.Equal(BigInteger.One, value);
            cancellation.Cancel();
            var error = Assert.Throws<LythonRuntimeException>(() => iterator.TryMoveNext(out _));
            Assert.Equal("execution canceled", error.Message);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
