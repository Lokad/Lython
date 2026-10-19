using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG09: each compilation commits the retained-state allowance and releases
/// its scratch when the pattern is invalid.
/// </summary>
public sealed class RegexCompilationAccountingTests
{
    private static object CreatePattern(object pattern, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var compiler = typeof(LythonRuntime).GetNestedType("RegexCompiler", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RegexCompiler not found.");
        var method = compiler.GetMethod("CreatePattern", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException("CreatePattern not found.");
        return method.Invoke(compiler, new object?[] { new object[] { pattern }, "re.compile(pattern[, flags])", span, context })
            ?? throw new InvalidOperationException("CreatePattern returned null.");
    }

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
        // 64 B slot charge fails before publication; a separate funded owner
        // caches the same key facts normally.
        var host = new MockLythonHost();
        var funded = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var pattern = Assert.IsType<LythonRuntime.RePatternObject>(CreatePattern(PyString.FromString("a"), funded, span));
        var cache = new RegexPatternCache();
        Assert.Equal("MemoryError", Assert.Throws<LythonRuntimeException>(() =>
            cache.Add("a", 32, pattern, new MemoryGovernor(new LythonMemoryBudget(0)), span)).ExceptionType);
        Assert.Equal(0, cache.Count);
        cache.Add("a", 32, pattern, funded.MemoryGovernor, span);
        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet("a", 32, out var cached));
        Assert.Same(pattern, cached);
    }

    [Fact]
    public void FailedCompilationReleasesScratch()
    {
        var host = new MockLythonHost();
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = owner }, regexProvider: Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var baseline = budget.ReservedBytes;
        Assert.ThrowsAny<Exception>(() => CreatePattern(PyString.FromString("a([invalid"), context, span));
        Assert.Equal(baseline, budget.ReservedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
