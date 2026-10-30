using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeRegexCacheTests
{
    [Theory]
    [InlineData("alpha")]
    [InlineData("é")]
    [InlineData("😀")]
    [InlineData("a\u0301")]
    [InlineData("\0")]
    public async Task EqualNativeContentReusesPatternsAndPurgePreservesRetainedUsers(string pattern)
    {
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import re
            a=re.compile(pattern)
            b=re.compile(('x'+pattern)[1:])
            print(a is b,a.pattern==pattern,a.fullmatch(pattern) is not None)
            re.purge()
            print(a is re.compile(pattern),a.fullmatch(pattern).group()==pattern)
            """, budget);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["pattern"] = pattern } };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("True True True\nFalse True\n", result.StandardOutput);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task ParsingVerboseIsPartOfTheCompileOptionsEvenWhenReportedFlagBitsAgree()
    {
        // Frozen CPython 3.13 output: the dispatched parsing bit can differ from
        // the final integer flags. Sharing those programs changes match results.
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import re
            class Flag:
             def __init__(self,verbose):self.verbose=verbose
             def __and__(self,other):return 64 if other==64 and self.verbose else 0
             def __or__(self,other):return 32
             def __ror__(self,other):return 32
            a=re.compile(pattern,Flag(False))
            b=re.compile(pattern,Flag(True))
            print(a is b,a.flags,b.flags,a.fullmatch(' a ') is not None,b.fullmatch('a') is not None)
            """);
        foreach (var asynchronous in new[] { false, true })
        {
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["pattern"] = " a " } };
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("False 32 32 True True\n", result.StandardOutput);
        }
    }
}
