using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG06: non-ASCII rune views are cached per string lifetime instead of
/// rematerialized with durable commits on every match, so repeated matching
/// stays flat instead of accumulating committed scratch.
/// </summary>
public sealed class FnMatchRuneCacheAccountingScenarioTests
{
    // 20k repeated matches over a hoisted 100-rune name rematerialize ~13KB
    // of committed scratch per call pre-fix and trip 1MB; the once-per-string
    // cache fits post-fix.
    private const long MatchBudgetBytes = 1048576;

    [Fact]
    public async Task RepeatedNonAsciiMatchesFit()
    {
        var script = new LythonEngine().Compile("""
            import fnmatch
            name = "é" * 100
            pat = "*x"
            i = 0
            while i < 20000:
                fnmatch.fnmatch(name, pat)
                i = i + 1
            return i
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = MatchBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.ExceptionType + sync.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(20000), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.ExceptionType + asyncResult.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(20000), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NonAsciiMatchesBehave()
    {
        var script = new LythonEngine().Compile("""
            import fnmatch
            return [fnmatch.fnmatch("héllo", "h?llo"), fnmatch.fnmatch("école", "*.txt"), fnmatch.fnmatch("abc", "a*"), fnmatch.fnmatch("é", "[é]"), fnmatch.fnmatchcase("ABC", "a*")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, false, true, true, false };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task AstralMatchesBehave()
    {
        var script = new LythonEngine().Compile("""
            import fnmatch
            return [fnmatch.fnmatch("😀x", "?x"), fnmatch.fnmatch("😀😀", "*"), fnmatch.fnmatch("a😀b", "a*b")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
