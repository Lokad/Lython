using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class StableSortCancellationTests
{
    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 128)]
    [InlineData(true, 128)]
    public async Task CancellationDuringComparisonStopsBeforeSortReturns(bool asynchronous, int size)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { CancellationToken = cancellation.Token, MaxExecutionMemoryBytes = 16384 });
        using (var buffer = new PyStableSort.Buffer(context.MemoryGovernor, null))
        {
            for (var i = size - 1; i >= 0; i--) buffer.Add(new PyStableSort.Entry(i, i), null);
            bool Compare(object candidate, object current)
            {
                cancellation.Cancel();
                return (int)candidate < (int)current;
            }
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await buffer.SortAsync(false,
                    (left, right) => ValueTask.FromResult(Compare(left, right)), context, null);
                else buffer.Sort(false, Compare, context, null);
            });
            Assert.Equal("RuntimeError", failure.ExceptionType);
            Assert.Contains("execution canceled", failure.Message);
        }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAtTheLastComparisonInterruptsTheRemainingCopyTail(bool asynchronous)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { CancellationToken = cancellation.Token, MaxExecutionMemoryBytes = 16384 });
        using (var buffer = new PyStableSort.Buffer(context.MemoryGovernor, null))
        {
            for (var i = 64; i < 128; i++) buffer.Add(new PyStableSort.Entry(i, i), null);
            for (var i = 0; i < 64; i++) buffer.Add(new PyStableSort.Entry(i, i), null);
            bool Compare(object candidate, object current)
            {
                if ((int)candidate == 63 && (int)current == 64) cancellation.Cancel();
                return (int)candidate < (int)current;
            }
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await buffer.SortAsync(false,
                    (left, right) => ValueTask.FromResult(Compare(left, right)), context, null);
                else buffer.Sort(false, Compare, context, null);
            });
            Assert.Equal("RuntimeError", failure.ExceptionType);
            Assert.Contains("execution canceled", failure.Message);
            // The original half-runs remain: the canceled merge cannot publish
            // the completely sorted scratch back into the result buffer.
            Assert.Equal(64, (int)buffer.First());
        }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
