using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Leases keep private backing funded until users finish, deny before growth,
// and return scratch on failure or disposal.
public sealed class DrainReservationTests
{
    private static LythonRuntime.ExecutionContext NewContext(long? budget, out MemoryGovernor governor)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(budget) },regexProvider:Utf8RegexProvider.Instance);
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
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(16)]
    [InlineData(17)]
    public void DrainReturnsValuesAndReleasesScratch(int count)
    {
        var context = NewContext(null, out var governor);
        using var result = PyIteration.DrainLeased(Items(count), new LythonSourceSpan(0, 0, 0, 0), context);
        Assert.Equal(Enumerable.Range(0, count).Cast<object>(), result.Items);
        Assert.True(governor.CurrentReservedBytes >= 32L + 16L * count);
        result.Dispose();
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(9)]
    [InlineData(17)]
    public async Task DrainAsyncReturnsValuesAndReleasesScratch(int count)
    {
        var context = NewContext(null, out var governor);
        using var result = await PyIteration.DrainLeasedAsync(ItemsAsync(count), new LythonSourceSpan(0, 0, 0, 0), context);
        Assert.Equal(Enumerable.Range(0, count).Cast<object>(), result.Items);
        Assert.True(governor.CurrentReservedBytes >= 32L + 16L * count);
        result.Dispose();
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void DrainGrowthDenialReleasesEverything()
    {
        // Growth must fund old and new backings together; denial unwinds both.
        var context = NewContext(200, out var governor);
        var failure = Assert.Throws<LythonRuntimeException>(() =>
            PyIteration.DrainLeased(Items(9), new LythonSourceSpan(0, 0, 0, 0), context));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void RefusedNestedDrainPreservesTheOuterLease()
    {
        var context = NewContext(300, out var governor);
        using var outer = PyIteration.DrainLeased(Items(5), new(0, 0, 0, 0), context);
        var held = governor.CurrentReservedBytes;
        Assert.Throws<LythonRuntimeException>(() =>
            PyIteration.DrainLeased(Items(5), new(0, 0, 0, 0), context));
        Assert.Equal(5, outer.Items.Count);
        Assert.Equal(held, governor.CurrentReservedBytes);
        outer.Dispose();
        Assert.Equal(0, governor.CurrentAccountedBytes);
    }

    [Fact]
    public async Task DrainAsyncGrowthDenialReleasesEverything()
    {
        var context = NewContext(200, out var governor);
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            await PyIteration.DrainLeasedAsync(ItemsAsync(9), new LythonSourceSpan(0, 0, 0, 0), context));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void DrainConsumerFailureReleasesEverything()
    {
        var context = NewContext(null, out var governor);
        Assert.Throws<InvalidOperationException>(() =>
            PyIteration.DrainLeased(FailingItems(5, 3), new LythonSourceSpan(0, 0, 0, 0), context));
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public async Task DrainAsyncConsumerFailureReleasesEverything()
    {
        var context = NewContext(null, out var governor);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await PyIteration.DrainLeasedAsync(FailingItemsAsync(5, 3), new LythonSourceSpan(0, 0, 0, 0), context));
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void NestedDrainsComposeExactly()
    {
        // Inner backing stays charged alongside the outer acquisition until
        // its lease ends; disposing it must leave the outer balance unchanged.
        var context = NewContext(null, out var governor);
        var outerSeen = 0;
        IEnumerable<object> Outer()
        {
            for (var i = 0; i < 6; i++)
            {
                outerSeen++;
                if (outerSeen == 3)
                {
                    var held = governor.CurrentReservedBytes;
                    Assert.True(held > 0);
                    using var inner = PyIteration.DrainLeased(Items(5), new LythonSourceSpan(0, 0, 0, 0), context);
                    Assert.Equal(5, inner.Items.Count);
                    Assert.True(governor.CurrentReservedBytes > held);
                    inner.Dispose();
                    Assert.Equal(held, governor.CurrentReservedBytes);
                }

                yield return i;
            }
        }

        using var result = PyIteration.DrainLeased(Outer(), new LythonSourceSpan(0, 0, 0, 0), context);
        Assert.Equal(6, result.Items.Count);
        result.Dispose();
        Assert.Equal(6, outerSeen);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void MaterializeSliceUnknownLengthFundsOnce()
    {
        var context = NewContext(null, out var governor);
        var source = Items(20).ToList();
        var result = PySequenceMaterialization.MaterializeSlice(source, LazyIndices(9), governor, null);
        Assert.Equal(9, result.Length);
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
