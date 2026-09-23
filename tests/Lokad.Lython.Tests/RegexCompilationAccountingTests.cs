using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
// M05: adopted compilation results additionally hold one 128 B pool entry plus
// one 32 B young-tier growth beside the allowance below.
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

    [Fact]
    public void SuccessfulCompilationCommitsAllowanceExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        _ = CreatePattern(PyString.FromString("a(b|c)*d"), context, span);
        // N19: allowance and pool charges as before, plus one 64B cache slot.
        Assert.Equal(65760L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void GroupAndLengthTermsCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        // Ten groups: 65536 + 9 x 2048, length inside the free envelope, plus one
        // 64B cache slot (N19).
        _ = CreatePattern(PyString.FromString(string.Concat(Enumerable.Repeat("(a)", 10))), context, span);
        Assert.Equal(84192L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void LongPatternLengthTermCommitsExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        // 100 chars, no groups: 65536 + 36 x 256, plus one 64B cache slot (N19).
        _ = CreatePattern(PyString.FromString(new string('a', 100)), context, span);
        Assert.Equal(74976L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void CacheSlotDenial_SkipsCachingWithoutFailure()
    {
        // Coverage item: denial rollback in regex compilation caching. A denied
        // 64 B slot charge skips caching silently; the compiled result stays usable
        // and a funded twin caches the same key facts normally.
        var host = new MockLythonHost();
        var funded = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var pattern = Assert.IsType<LythonRuntime.RePatternObject>(CreatePattern(PyString.FromString("a"), funded, span));
        var cache = new RegexPatternCache();
        cache.Add("a", 32, pattern, new MemoryGovernor(0), span);
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
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        Assert.ThrowsAny<Exception>(() => CreatePattern(PyString.FromString("a([invalid"), context, span));
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}