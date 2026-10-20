using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// Pair validation drains iterators without retaining their contents. Reusing
/// one boxed value separates validation storage from allocations by guest code.
/// </summary>
public sealed class DictUpdatePairsAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    [Fact]
    public void OversizedPairAllocatesNoProportionalTransient()
        => AssertBoundedPairStorage(4096);

    [Fact]
    [Trait("Category", "Stress")]
    public void MillionItemPairAllocatesNoProportionalTransient()
        => AssertBoundedPairStorage(1_000_000);

    private static void AssertBoundedPairStorage(int count)
    {
        var budget = new LythonMemoryBudget(256 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = owner });
            var target = new PyDict(context.MemoryGovernor, Span);
            var pair = new RepeatedPair(count);
            object[] source = [pair];
            var baseline = budget.ReservedBytes;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var failure = Assert.Throws<LythonRuntimeException>(() =>
                LythonRuntime.UpdateDictionaryFromSource(target, source, context, Span));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal("ValueError", failure.ExceptionType);
            Assert.Equal($"dictionary update sequence element #0 has length {count}; 2 is required", failure.Message);
            Assert.Equal(count, pair.Produced);
            Assert.True(pair.Disposed);
            Assert.Equal(0, target.Count);
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.True(allocated < 16384, $"pair validation allocated {allocated} bytes before failing");
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationStopsAnUnboundedPairAndDisposesItWithoutInsertion(bool asynchronous)
    {
        using var cancellation = new CancellationTokenSource();
        var budget = new LythonMemoryBudget(256 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var target = new PyDict(context.MemoryGovernor, Span);
            var pair = new RepeatedPair(null, cancellation);
            object[] source = [pair];
            var baseline = budget.ReservedBytes;
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous)
                    await LythonRuntime.UpdateDictionaryFromSourceAsync(target, source, context, Span);
                else
                    LythonRuntime.UpdateDictionaryFromSource(target, source, context, Span);
            });
            Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
            Assert.InRange(pair.Produced, 256, 512);
            Assert.True(pair.Disposed);
            Assert.Equal(0, target.Count);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class RepeatedPair(int? count, CancellationTokenSource? cancellation = null) : IPyIterableValue
    {
        private static readonly object Value = BigInteger.Zero;
        public int Produced { get; private set; }
        public bool Disposed { get; private set; }

        public IEnumerable<object> Iterate()
        {
            try
            {
                while (count is null || Produced < count)
                {
                    if (++Produced == 256) cancellation?.Cancel();
                    yield return Value;
                }
            }
            finally { Disposed = true; }
        }
    }
}
