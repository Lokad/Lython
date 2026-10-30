using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class RegexCacheCapacityAccountingTests
{
    // Reuse an already constructed pattern so these controls isolate cache
    // storage funding from compiled-pattern ownership and collection timing.
    private static LythonRuntime.RePatternObject Pattern() => new(
        PyString.FromString("a"), 32,
        Utf8RegexProvider.Instance.Compile("a"u8.ToArray(), new(32, false), new LythonMemoryBudget(), default), 1,
        new Dictionary<PyString, int>());

    [Theory]
    [InlineData(1)]
    [InlineData(512)]
    [InlineData(1536)]
    public void RepeatedEvictionKeepsOnlyCapacityFunded(int replacements)
    {
        var pattern = Pattern();
        using var compiled = pattern.Regex;
        var cache = new RegexPatternCache();
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        var span = new LythonSourceSpan(0, 0, 0, 0);
        for (var i = 0; i < RegexPatternCache.MaxEntries + replacements; i++)
        {
            cache.Add(PyString.FromString($"pattern-{i}"), new(32, false), pattern, governor, span);
        }

        Assert.Equal(RegexPatternCache.MaxEntries, cache.Count);
        Assert.Equal(RegexPatternCache.MaxEntries * RegexPatternCache.SlotBytesPerEntry,
            governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.False(cache.TryGet(PyString.FromString("pattern-0"), new(32, false), out _));
        Assert.True(cache.TryGet(PyString.FromString($"pattern-{RegexPatternCache.MaxEntries + replacements - 1}"), new(32, false), out var latest));
        Assert.Same(pattern, latest);

        cache.Clear(governor);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, governor.CurrentAccountedBytes);
    }

    [Fact]
    public void FullCacheEvictsWithoutAdditionalBudgetAndPurgeRefundsCapacity()
    {
        var pattern = Pattern();
        using var compiled = pattern.Regex;
        var cache = new RegexPatternCache();
        var capacity = RegexPatternCache.MaxEntries * RegexPatternCache.SlotBytesPerEntry;
        var governor = new MemoryGovernor(new LythonMemoryBudget(capacity));
        var span = new LythonSourceSpan(0, 0, 0, 0);
        for (var i = 0; i < RegexPatternCache.MaxEntries; i++)
        {
            cache.Add(PyString.FromString($"pattern-{i}"), new(32, false), pattern, governor, span);
        }

        Assert.True(cache.TryGet(PyString.FromString("pattern-0"), new(32, false), out _));
        cache.Add(PyString.FromString("replacement"), new(32, false), pattern, governor, span);
        Assert.True(cache.TryGet(PyString.FromString("replacement"), new(32, false), out var replacement));
        Assert.Same(pattern, replacement);
        Assert.True(cache.TryGet(PyString.FromString("pattern-0"), new(32, false), out _));
        Assert.False(cache.TryGet(PyString.FromString("pattern-1"), new(32, false), out _));
        Assert.Equal(RegexPatternCache.MaxEntries, cache.Count);
        Assert.Equal(capacity, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.LastDeniedReservationBytes);

        cache.Clear(governor);
        Assert.Equal(0, governor.CurrentAccountedBytes);
        cache.Add(PyString.FromString("after-purge"), new(32, false), pattern, governor, span);
        Assert.Equal(RegexPatternCache.SlotBytesPerEntry, governor.CurrentCommittedBytes);
        Assert.True(cache.TryGet(PyString.FromString("after-purge"), new(32, false), out _));
    }
}
