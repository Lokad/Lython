using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using System.Text;

namespace Lokad.Lython.Tests;

public sealed class RegexCacheCapacityAccountingTests
{
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
    public void RepeatedEvictionKeepsOnlyCapacityFunded(int replacements)
    {
        var pattern = Pattern();
        using var compiled = pattern.Regex;
        var cache = new RegexPatternCache();
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        var span = new LythonSourceSpan(0, 0, 0, 0);
        for (var i = 0; i < RegexPatternCache.MaxEntries + replacements; i++)
        {
            cache.GetOrAdd(new(PyString.FromString($"pattern-{i}"), new(32, false), 0), pattern, governor, span);
        }

        Assert.Equal(RegexPatternCache.MaxEntries, cache.Count);
        Assert.Equal(RegexPatternCache.MaxEntries * RegexPatternCache.SlotBytesPerEntry,
            governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.False(cache.TryGet(new(PyString.FromString("pattern-0"), new(32, false), 0), out _));
        Assert.True(cache.TryGet(new(PyString.FromString($"pattern-{RegexPatternCache.MaxEntries + replacements - 1}"), new(32, false), 0), out var latest));
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
            cache.GetOrAdd(new(PyString.FromString($"pattern-{i}"), new(32, false), 0), pattern, governor, span);
        }

        Assert.True(cache.TryGet(new(PyString.FromString("pattern-0"), new(32, false), 0), out _));
        cache.GetOrAdd(new(PyString.FromString("replacement"), new(32, false), 0), pattern, governor, span);
        Assert.True(cache.TryGet(new(PyString.FromString("replacement"), new(32, false), 0), out var replacement));
        Assert.Same(pattern, replacement);
        Assert.True(cache.TryGet(new(PyString.FromString("pattern-0"), new(32, false), 0), out _));
        Assert.False(cache.TryGet(new(PyString.FromString("pattern-1"), new(32, false), 0), out _));
        Assert.Equal(RegexPatternCache.MaxEntries, cache.Count);
        Assert.Equal(capacity, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.LastDeniedReservationBytes);

        cache.Clear(governor);
        Assert.Equal(0, governor.CurrentAccountedBytes);
        cache.GetOrAdd(new(PyString.FromString("after-purge"), new(32, false), 0), pattern, governor, span);
        Assert.Equal(RegexPatternCache.SlotBytesPerEntry, governor.CurrentCommittedBytes);
        Assert.True(cache.TryGet(new(PyString.FromString("after-purge"), new(32, false), 0), out _));
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
        var original = new RegexPatternKey(PyString.FromString("previous"), new(32, false), 0);
        var replacement = new RegexPatternKey(PyString.FromString("reentered"), new(32, false), 0);
        cache.GetOrAdd(original, pattern, governor, span);
        hook.AfterShrink = _bytes =>
        {
            hook.AfterShrink = null;
            Assert.False(cache.TryGet(original, out _));
            cache.GetOrAdd(replacement, pattern, governor, span);
        };

        cache.Clear(governor);

        Assert.True(cache.TryGet(replacement, out var retained));
        Assert.Same(pattern, retained);
        Assert.Equal(RegexPatternCache.SlotBytesPerEntry, budget.ReservedBytes);
        cache.Clear(governor);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void CachedPatternLookupUsesOnlyItsCurrentToken()
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
        cache.GetOrAdd(new(inserted, options, 0), pattern, governor, span);
        analysis.Add(Encoding.UTF8.GetBytes(text), options, pattern.Regex, 1, default, span);

        using var previous = new CancellationTokenSource();
        using var current = new CancellationTokenSource();
        Assert.True(cache.TryGet(new(lookup, options, 0), out var first, previous.Token, span));
        Assert.Same(pattern, first);
        Assert.Equal(pattern.Regex, analysis.Find(lookup, options, previous.Token, span)?.Program);
        previous.Cancel();
        var error = Assert.Throws<LythonRuntimeException>(() => cache.TryGet(new(lookup, options, 0), out _, previous.Token, span));
        Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
        Assert.Equal(span, error.Span);
        error = Assert.Throws<LythonRuntimeException>(() => analysis.Find(lookup, options, previous.Token, span));
        Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
        Assert.Equal(span, error.Span);
        Assert.True(cache.TryGet(new(lookup, options, 0), out var again, current.Token, span));
        Assert.Same(pattern, again);
        Assert.Equal(pattern.Regex, analysis.Find(lookup, options, current.Token, span)?.Program);
        cache.Clear(governor);
        Assert.Equal(0, governor.CurrentAccountedBytes);
    }
}
