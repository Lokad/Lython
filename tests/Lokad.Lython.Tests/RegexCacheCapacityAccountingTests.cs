using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using System.Text;

namespace Lokad.Lython.Tests;

public sealed class RegexCacheCapacityAccountingTests
{
    private static RegexPatternCache.Key Key(string text)
        => RegexPatternCache.Key.Exact(PyString.FromString(text), null, 0, default, null);

    // Reuse an already constructed pattern so these controls isolate cache
    // storage funding from compiled-pattern ownership and collection timing.
    private static LythonRuntime.RePatternObject Pattern()
    {
        var options = new LythonRegexOptions(32, false);
        return new(PyString.FromString("a"), 32,
            Utf8RegexProvider.Instance.Compile("a"u8.ToArray(), options, new LythonMemoryBudget(), default), 1,
            new PyDict(), options);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(512)]
    [InlineData(1536)]
    public async Task RepeatedEvictionKeepsOnlyCapacityFunded(int replacements)
    {
        var pattern = Pattern();
        using var compiled = pattern.Regex;
        var cache = new RegexPatternCache();
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        var span = new LythonSourceSpan(0, 0, 0, 0);
        for (var i = 0; i < RegexPatternCache.MaxEntries + replacements; i++)
        {
            Assert.Same(pattern, await cache.GetOrAdd(Key($"pattern-{i}"), pattern, governor, span));
        }

        Assert.Equal(RegexPatternCache.MaxEntries, cache.Count);
        Assert.Equal(RegexPatternCache.MaxEntries * RegexPatternCache.SlotBytesPerEntry,
            governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Null(await cache.Find(Key("pattern-0"), default, span));
        Assert.Same(pattern, await cache.Find(Key($"pattern-{RegexPatternCache.MaxEntries + replacements - 1}"), default, span));

        cache.Clear(governor);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, governor.CurrentAccountedBytes);
    }

    [Fact]
    public async Task FullCacheEvictsWithoutAdditionalBudgetAndPurgeRefundsCapacity()
    {
        var pattern = Pattern();
        using var compiled = pattern.Regex;
        var cache = new RegexPatternCache();
        var capacity = RegexPatternCache.MaxEntries * RegexPatternCache.SlotBytesPerEntry;
        var governor = new MemoryGovernor(new LythonMemoryBudget(capacity));
        var span = new LythonSourceSpan(0, 0, 0, 0);
        for (var i = 0; i < RegexPatternCache.MaxEntries; i++)
        {
            Assert.Same(pattern, await cache.GetOrAdd(Key($"pattern-{i}"), pattern, governor, span));
        }

        Assert.Same(pattern, await cache.Find(Key("pattern-0"), default, span));
        Assert.Same(pattern, await cache.GetOrAdd(Key("replacement"), pattern, governor, span));
        Assert.Same(pattern, await cache.Find(Key("replacement"), default, span));
        Assert.Same(pattern, await cache.Find(Key("pattern-0"), default, span));
        Assert.Null(await cache.Find(Key("pattern-1"), default, span));
        Assert.Equal(RegexPatternCache.MaxEntries, cache.Count);
        Assert.Equal(capacity, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.LastDeniedReservationBytes);

        cache.Clear(governor);
        Assert.Equal(0, governor.CurrentAccountedBytes);
        Assert.Same(pattern, await cache.GetOrAdd(Key("after-purge"), pattern, governor, span));
        Assert.Equal(RegexPatternCache.SlotBytesPerEntry, governor.CurrentCommittedBytes);
        Assert.Same(pattern, await cache.Find(Key("after-purge"), default, span));
    }

    [Fact]
    public void PurgeRefundKeepsCapacityAdmittedDuringReentry()
    {
        var pattern = Pattern();
        using var compiled = pattern.Regex;
        var budget = new LythonMemoryBudget(2 * RegexPatternCache.SlotBytesPerEntry);
        var hook = new AdmissionHookMemoryBudget(budget);
        var governor = new MemoryGovernor(hook);
        var cache = new RegexPatternCache();
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var original = Key("previous");
        var replacement = Key("reentered");
        Assert.Same(pattern, Completed(cache.GetOrAdd(original, pattern, governor, span)));
        hook.AfterShrink = _bytes =>
        {
            hook.AfterShrink = null;
            Assert.Null(Completed(cache.Find(original, default, span)));
            Assert.Same(pattern, Completed(cache.GetOrAdd(replacement, pattern, governor, span)));
        };

        cache.Clear(governor);

        Assert.Same(pattern, Completed(cache.Find(replacement, default, span)));
        Assert.Equal(RegexPatternCache.SlotBytesPerEntry, budget.ReservedBytes);
        cache.Clear(governor);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static T Completed<T>(ValueTask<T> result)
    {
        Assert.True(result.IsCompletedSuccessfully);
        return result.Result;
    }

    [Fact]
    public async Task CachedPatternLookupUsesOnlyItsCurrentToken()
    {
        var pattern = Pattern();
        using var analysis = new CompiledRegexPrograms(null);
        var options = new LythonRegexOptions(32, false);
        var span = new LythonSourceSpan(1, 6, 1, 2);
        var text = new string('é', 6144);
        var inserted = PyString.FromString(text);
        var lookup = PyString.FromString(text);
        Assert.NotSame(inserted, lookup);
        var cache = new RegexPatternCache();
        var governor = new MemoryGovernor(null);
        var key = RegexPatternCache.Key.Exact(lookup, null, 0, default, span);
        Assert.Same(pattern, await cache.GetOrAdd(RegexPatternCache.Key.Exact(inserted, null, 0, default, span), pattern, governor, span));
        analysis.Add(Encoding.UTF8.GetBytes(text), options, pattern.Regex, 1, new([], 0), default, span);

        using var previous = new CancellationTokenSource();
        using var current = new CancellationTokenSource();
        Assert.Same(pattern, await cache.Find(key, previous.Token, span));
        Assert.Equal(pattern.Regex, analysis.Find(lookup, options, previous.Token, span)?.Program);
        previous.Cancel();
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            _ = await cache.Find(key, previous.Token, span);
        });
        Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
        Assert.Equal(span, error.Span);
        error = Assert.Throws<LythonRuntimeException>(() => analysis.Find(lookup, options, previous.Token, span));
        Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
        Assert.Equal(span, error.Span);
        Assert.Same(pattern, await cache.Find(key, current.Token, span));
        Assert.Equal(pattern.Regex, analysis.Find(lookup, options, current.Token, span)?.Program);
        cache.Clear(governor);
        Assert.Equal(0, governor.CurrentAccountedBytes);
    }
}
