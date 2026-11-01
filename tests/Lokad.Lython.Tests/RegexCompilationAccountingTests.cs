using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// Compilation and cached patterns retain their owners; invalid patterns close
/// backend scratch while Python error metadata stays funded until owner cleanup.
/// </summary>
public sealed class RegexCompilationAccountingTests
{
    private static object CreatePattern(object pattern, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => LythonRuntime.RegexCompiler.CreatePattern([pattern], "re.compile(pattern[, flags])", span, context);

    [Theory]
    [InlineData("a(b|c)*d")]
    [InlineData("(a)(a)(a)(a)(a)(a)(a)(a)(a)(a)")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void CompiledStorageAndCacheBorrowOwnerUntilCleanup(string source)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = owner }, regexProvider: Utf8RegexProvider.Instance);
            var span = new LythonSourceSpan(0, 0, 0, 0);
            var pattern = CreatePattern(PyString.FromString(source), context, span);
            using var compiled = Assert.IsType<LythonRuntime.RePatternObject>(pattern).Regex;
            var retained = budget.ReservedBytes;
            Assert.True(retained > 0);
            Assert.Same(pattern, CreatePattern(PyString.FromString(source), context, span));
            Assert.Equal(retained, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void CacheSlotDenialIsAResourceFailureWithoutCachePublication()
    {
        // Coverage item: denial rollback in regex compilation caching. A denied
        // slot charge fails before publication; a separate funded owner
        // caches the same key facts normally.
        var host = new MockLythonHost();
        using var owner = new LythonMemoryBudget().CreateScope();
        var funded = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var pattern = Assert.IsType<LythonRuntime.RePatternObject>(CreatePattern(PyString.FromString("a"), funded, span));
        using var compiled = pattern.Regex;
        var cache = new RegexPatternCache();
        Assert.Equal("MemoryError", Assert.Throws<LythonRuntimeException>(() =>
            cache.Add(PyString.FromString("a"), new(32, false), pattern, new MemoryGovernor(new LythonMemoryBudget(0)), span)).ExceptionType);
        Assert.Equal(0, cache.Count);
        cache.Add(PyString.FromString("a"), new(32, false), pattern, funded.MemoryGovernor, span);
        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet(PyString.FromString("a"), new(32, false), out var cached));
        Assert.Same(pattern, cached);
    }

    [Fact]
    public void FailedCompilationFundsErrorMetadataUntilOwnerCleanup()
    {
        var host = new MockLythonHost();
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = owner }, regexProvider: Utf8RegexProvider.Instance);
            var span = new LythonSourceSpan(0, 0, 0, 0);
            var baseline = budget.ReservedBytes;
            var error = Assert.Throws<LythonRuntimeException>(() => CreatePattern(PyString.FromString("a([invalid"), context, span));
            Assert.Equal("PatternError", error.ExceptionType);
            Assert.NotNull(error.OriginalPythonException?.CustomDict);
            // The exception can escape/carry its original pattern and fields.
            // Refund backend scratch, but keep this new retained payload funded.
            Assert.True(budget.ReservedBytes > baseline);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
