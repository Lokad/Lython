using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// Repeated wildcard matching uses its admitted inputs without accumulating
/// per-character working storage; selected names retain their ordinary owner.
/// </summary>
public sealed class FnMatchStreamingScenarioTests
{
    private const long MatchBudgetBytes = 1048576;

    [Fact]
    public async Task TranslationNeedsNoProviderAndRetainedResultsStayFunded()
    {
        var budget = new LythonMemoryBudget(MatchBudgetBytes);
        using (var script = new LythonEngine().Compile("""
            import fnmatch,glob
            return [fnmatch.translate('é[α-ω]?.txt'),glob.translate('src/**/é?.txt',recursive=True,seps='/')]
            """, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            using (var sync = script.Run(new MockLythonHost()))
            {
                Assert.True(sync.Success, sync.Failure?.Message);
                Assert.True(budget.ReservedBytes > baseline);
                using var asynchronous = await script.RunAsync(new MockLythonHost());
                Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
                Assert.Equal(Assert.IsType<List<object?>>(sync.ReturnValue), Assert.IsType<List<object?>>(asynchronous.ReturnValue));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task TranslatedPatternsKeepPythonSearchAndEndSemantics()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import fnmatch,glob,re
            values=[]
            for pattern in [fnmatch.translate('x'),glob.translate('x',include_hidden=True,seps='/')]:
                values.append(re.match(pattern,'prefixx') is not None)
                values.append(re.search(pattern,'prefixx') is not None)
                values.append(re.fullmatch(pattern,'prefixx') is not None)
                values.append(re.match(pattern,'x\n') is not None)
            return values
            """);
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
        var expected = new List<object?> { false, true, false, false, false, true, false, false };
        using var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        using var asynchronous = await script.RunAsync(new MockLythonHost());
        Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
        Assert.Equal(expected, asynchronous.ReturnValue);
    }

    // Frozen CPython 3.13 fnmatchcase values, independent of host case folding.
    [Theory]
    [InlineData("😀", "?", true)]
    [InlineData("e\u0301", "?", false)]
    [InlineData("e\u0301", "??", true)]
    [InlineData("κa😀", "*κ??", true)]
    [InlineData("🙏", "[😀-🙏]", true)]
    [InlineData("🚀", "[😀-🙏]", false)]
    [InlineData("é", "[à-ê]", true)]
    [InlineData("😀", "[!a-z]", true)]
    [InlineData("-", "[-é]", true)]
    [InlineData("]", "[]]", true)]
    [InlineData("[!]", "[!]", true)]
    [InlineData("x", "[!z-a]", true)]
    [InlineData("b", "[a--c]", false)]
    [InlineData("c", "[a--c]", true)]
    [InlineData("α\nβ", "α*β", true)]
    [InlineData("x\n", "x", false)]
    [InlineData("^", "[^a]", true)]
    [InlineData("b", "[^a]", false)]
    public async Task UnicodeAndClassGoldens(string name, string pattern, bool expected)
    {
        using var script = new LythonEngine().Compile("import fnmatch\nreturn fnmatch.fnmatchcase(name,pattern)");
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
        var globals = new Dictionary<string, object?> { ["name"] = name, ["pattern"] = pattern };
        using var sync = script.Run(new MockLythonHost(), globals);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        using var asynchronous = await script.RunAsync(new MockLythonHost(), globals);
        Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
        Assert.Equal(expected, asynchronous.ReturnValue);

        using var translated = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import fnmatch,glob,re
            patterns=[fnmatch.translate(pattern),glob.translate(pattern,include_hidden=True,seps='/')]
            values=[]
            for value in patterns:
                values.append(re.match(value,name) is not None)
                values.append(re.fullmatch(value,name) is not None)
            return values
            """);
        Assert.True(translated.IsValid, string.Join(";", translated.Diagnostics));
        foreach (var asyncApi in new[] { false, true })
        {
            using var result = asyncApi
                ? await translated.RunAsync(new MockLythonHost(), globals)
                : translated.Run(new MockLythonHost(), globals);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new List<object?> { expected, expected, expected, expected }, result.ReturnValue);
        }
    }

    [Fact]
    public async Task RepeatedNonAsciiMatchesFit()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
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
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(MatchBudgetBytes) };
        using (var sync = script.Run(new MockLythonHost(), options))
        {
            Assert.True(sync.Success, sync.Failure?.ExceptionType + sync.Failure?.Message);
            Assert.Equal(new System.Numerics.BigInteger(20000), sync.ReturnValue);
        }
        Assert.Equal(0, Assert.IsType<LythonMemoryBudget>(options.MemoryBudget).ReservedBytes);

        using (var asyncResult = await script.RunAsync(new MockLythonHost(), options))
        {
            Assert.True(asyncResult.Success, asyncResult.Failure?.ExceptionType + asyncResult.Failure?.Message);
            Assert.Equal(new System.Numerics.BigInteger(20000), asyncResult.ReturnValue);
        }
        Assert.Equal(0, Assert.IsType<LythonMemoryBudget>(options.MemoryBudget).ReservedBytes);
    }

    [Fact]
    public async Task NonAsciiMatchesBehave()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import fnmatch
            return [fnmatch.fnmatch("héllo", "h?llo"), fnmatch.fnmatch("école", "*.txt"), fnmatch.fnmatch("abc", "a*"), fnmatch.fnmatch("é", "[é]"), fnmatch.fnmatchcase("ABC", "a*")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, false, true, true, false };
        using var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        using var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task AstralMatchesBehave()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import fnmatch
            return [fnmatch.fnmatch("😀x", "?x"), fnmatch.fnmatch("😀😀", "*"), fnmatch.fnmatch("a😀b", "a*b")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, true };
        using var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        using var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
