using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Tests;

public sealed class RegexCacheCapacityAccountingTests
{
    // Reuse an already constructed pattern so these controls isolate cache
    // storage funding from compiled-pattern ownership and collection timing.
    private static LythonRuntime.RePatternObject Pattern() => new(
        PyString.FromString("a"), PythonReCompileOptions.None, 32,
        new Utf8PythonRegex("a"u8, PythonReCompileOptions.None), 1,
        new Dictionary<string, int>());

    [Theory]
    [InlineData(1)]
    [InlineData(512)]
    [InlineData(1536)]
    public void RepeatedEvictionKeepsOnlyCapacityFunded(int replacements)
    {
        var pattern = Pattern();
        var cache = new RegexPatternCache();
        var governor = new MemoryGovernor(null);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        for (var i = 0; i < RegexPatternCache.MaxEntries + replacements; i++)
        {
            cache.Add($"pattern-{i}", 32, pattern, governor, span);
        }

        Assert.Equal(RegexPatternCache.MaxEntries, cache.Count);
        Assert.Equal(RegexPatternCache.MaxEntries * RegexPatternCache.SlotBytesPerEntry,
            governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.False(cache.TryGet("pattern-0", 32, out _));
        Assert.True(cache.TryGet($"pattern-{RegexPatternCache.MaxEntries + replacements - 1}", 32, out var latest));
        Assert.Same(pattern, latest);

        cache.Clear(governor);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, governor.CurrentAccountedBytes);
    }

    [Fact]
    public void FullCacheEvictsWithoutAdditionalBudgetAndPurgeRefundsCapacity()
    {
        var pattern = Pattern();
        var cache = new RegexPatternCache();
        var capacity = RegexPatternCache.MaxEntries * RegexPatternCache.SlotBytesPerEntry;
        var governor = new MemoryGovernor(capacity);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        for (var i = 0; i < RegexPatternCache.MaxEntries; i++)
        {
            cache.Add($"pattern-{i}", 32, pattern, governor, span);
        }

        Assert.True(cache.TryGet("pattern-0", 32, out _));
        cache.Add("replacement", 32, pattern, governor, span);
        Assert.True(cache.TryGet("replacement", 32, out var replacement));
        Assert.Same(pattern, replacement);
        Assert.True(cache.TryGet("pattern-0", 32, out _));
        Assert.False(cache.TryGet("pattern-1", 32, out _));
        Assert.Equal(RegexPatternCache.MaxEntries, cache.Count);
        Assert.Equal(capacity, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.LastDeniedReservationBytes);

        cache.Clear(governor);
        Assert.Equal(0, governor.CurrentAccountedBytes);
        cache.Add("after-purge", 32, pattern, governor, span);
        Assert.Equal(RegexPatternCache.SlotBytesPerEntry, governor.CurrentCommittedBytes);
        Assert.True(cache.TryGet("after-purge", 32, out _));
    }
}
