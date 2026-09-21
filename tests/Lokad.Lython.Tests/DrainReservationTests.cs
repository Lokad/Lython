using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// R12: shared drains fund each backing-growth increment exactly once
// (funded capacity tracked apart from observed capacity), deny before growth,
// and release everything when the consumer fails. All assertions pin
// deterministic governor peaks, not GC slopes.
public sealed class DrainReservationTests
{
    private static LythonRuntime.ExecutionContext NewContext(long? budget, out MemoryGovernor governor)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = budget });
        governor = context.MemoryGovernor;
        return context;
    }

    private static IEnumerable<object> Items(int count)
    {
        for (var i = 0; i < count; i++)
        {
            yield return i;
        }
    }

    private static async IAsyncEnumerable<object> ItemsAsync(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 128)]
    [InlineData(5, 208)]
    [InlineData(8, 256)]
    [InlineData(9, 400)]
    [InlineData(16, 512)]
    [InlineData(17, 784)]
    public void DrainFundsEachGrowthOnce(int count, long expectedPeakReserved)
    {
        // Growth funds 64, 64, 128, 256... at capacities 4, 8, 16, 32, plus the
        // final 16-per-item copy coexistence. Double-charging any increment
        // would exceed these peaks (e.g. 400 instead of 208 for five items).
        var context = NewContext(null, out var governor);
        var result = PyIteration.Drain(Items(count), new LythonSourceSpan(0, 0, 0, 0), context);
        Assert.Equal(count, result.Count);
        Assert.Equal(expectedPeakReserved, governor.PeakReservedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 208)]
    [InlineData(9, 400)]
    [InlineData(17, 784)]
    public async Task DrainAsyncMatchesSyncPeaks(int count, long expectedPeakReserved)
    {
        var context = NewContext(null, out var governor);
        var result = await PyIteration.DrainAsync(ItemsAsync(count), new LythonSourceSpan(0, 0, 0, 0), context);
        Assert.Equal(count, result.Count);
        Assert.Equal(expectedPeakReserved, governor.PeakReservedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void DrainGrowthDenialReleasesEverything()
    {
        // Nine items need 400 (256 growth + 144 final); 200 denies mid-growth.
        var context = NewContext(200, out var governor);
        var failure = Assert.Throws<LythonRuntimeException>(() =>
            PyIteration.Drain(Items(9), new LythonSourceSpan(0, 0, 0, 0), context));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void DrainFinalCopyDenialReleasesEverything()
    {
        // Growth (256) fits 300 but the final 144 coexistence does not.
        var context = NewContext(300, out var governor);
        var failure = Assert.Throws<LythonRuntimeException>(() =>
            PyIteration.Drain(Items(9), new LythonSourceSpan(0, 0, 0, 0), context));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public async Task DrainAsyncGrowthDenialReleasesEverything()
    {
        var context = NewContext(200, out var governor);
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            await PyIteration.DrainAsync(ItemsAsync(9), new LythonSourceSpan(0, 0, 0, 0), context));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void DrainConsumerFailureReleasesEverything()
    {
        var context = NewContext(null, out var governor);
        Assert.Throws<InvalidOperationException>(() =>
            PyIteration.Drain(FailingItems(5, 3), new LythonSourceSpan(0, 0, 0, 0), context));
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public async Task DrainAsyncConsumerFailureReleasesEverything()
    {
        var context = NewContext(null, out var governor);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await PyIteration.DrainAsync(FailingItemsAsync(5, 3), new LythonSourceSpan(0, 0, 0, 0), context));
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void NestedDrainsComposeExactly()
    {
        // An inner drain on the third pull nests its reservation inside the
        // outer one: outer growths (64+64, then final 96) plus inner growths
        // (64+64, then final 80) peak together while both are live.
        var context = NewContext(null, out var governor);
        var outerSeen = 0;
        IEnumerable<object> Outer()
        {
            for (var i = 0; i < 6; i++)
            {
                outerSeen++;
                if (outerSeen == 3)
                {
                    var inner = PyIteration.Drain(Items(5), new LythonSourceSpan(0, 0, 0, 0), context);
                    Assert.Equal(5, inner.Count);
                }

                yield return i;
            }
        }

        var result = PyIteration.Drain(Outer(), new LythonSourceSpan(0, 0, 0, 0), context);
        Assert.Equal(6, result.Count);
        Assert.Equal(6, outerSeen);
        // Outer funded 64 before the inner drain ran; the inner run peaked at
        // 64 + 208 = 272 beside it; the outer final copy later reached 224.
        Assert.Equal(272, governor.PeakReservedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void MaterializeSliceUnknownLengthFundsOnce()
    {
        var context = NewContext(null, out var governor);
        var source = Items(20).ToList();
        var result = PySequenceMaterialization.MaterializeSlice(source, LazyIndices(9), governor, null);
        Assert.Equal(9, result.Length);
        Assert.Equal(400, governor.PeakReservedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void MaterializeSliceDenialReleasesEverything()
    {
        var context = NewContext(200, out var governor);
        var source = Items(20).ToList();
        Assert.Throws<LythonRuntimeException>(() =>
            PySequenceMaterialization.MaterializeSlice(source, LazyIndices(9), governor, null));
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    private static IEnumerable<object> FailingItems(int count, int failAt)
    {
        for (var i = 0; i < count; i++)
        {
            if (i == failAt)
            {
                throw new InvalidOperationException("consumer failure");
            }

            yield return i;
        }
    }

    private static async IAsyncEnumerable<object> FailingItemsAsync(int count, int failAt)
    {
        for (var i = 0; i < count; i++)
        {
            await Task.Yield();
            if (i == failAt)
            {
                throw new InvalidOperationException("consumer failure");
            }

            yield return i;
        }
    }

    private static IEnumerable<int> LazyIndices(int count)
    {
        for (var i = 0; i < count; i++)
        {
            yield return i;
        }
    }
}
