using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeRegexCacheTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MatchRepresentationUsesBoundedCaptureAndScalarOffsets(bool asynchronous)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import re
            pattern = re.compile('[^!]*')
            for capture in ['', 'é😀'*35, '\t'*45, 'a'*60+"'", 'a'*60+"'"+'"']:
                match = pattern.match('é!' + capture + '!', 2)
                expected = '<re.Match object; span=(2, ' + str(2 + len(capture)) + '), match=' + repr(capture)[:50] + '>'
                assert repr(match) == expected
                assert str(match) == expected
                assert match.group() == capture
            """);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var host = new MockLythonHost();
        using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PatternRepresentationUsesEffectiveFlagsAndScalarPrefix(bool asynchronous)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import re
            assert repr(re.compile('(?im)é')) == "re.compile('(?im)é', re.IGNORECASE|re.MULTILINE)"
            assert str(re.compile('a', 1026)) == "re.compile('a', re.IGNORECASE|0x400)"
            assert repr(re.compile('x', re.ASCII | re.DOTALL)) == "re.compile('x', re.DOTALL|re.ASCII)"
            for pattern in ['é😀'*110, '\t'*120, 'a'*210+"'", 'a'*210+"'"+'"', 'é'*198, 'é'*199]:
                assert repr(re.compile(pattern)) == 're.compile(' + repr(pattern)[:200] + ')'
            """);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var host = new MockLythonHost();
        using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.True(result.Success, result.Failure?.Message);
    }

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
