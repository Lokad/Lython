using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N24: findall results expose ordinary list behavior through the
// already-governed inner list instead of a regex-only method table.
public sealed class ReFindAllListBehaviorTests
{
    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    [Fact]
    public async Task ModuleFindall_Count()
    {
        var script = Compile("""
            import re
            return re.findall("a", "aba").count("a")
            """);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(2), sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(2), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CompiledPatternFindall_Count()
    {
        var script = Compile("""
            import re
            return re.compile("a").findall("aba").count("a")
            """);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(2), sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(2), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NoMatches_CountAndLen()
    {
        var script = Compile("""
            import re
            r = re.findall("z", "aba")
            return [len(r), r.count("a")]
            """);
        var expected = new List<object?> { new BigInteger(0), new BigInteger(0) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CaptureGroups_ProjectLikeLists()
    {
        var script = Compile("""
            import re
            return [re.findall("(a)", "aba"), re.findall("(a)(b)?", "ab aba")]
            """);
        var expected = new List<object?>
        {
            new List<object?> { "a", "a" },
            new List<object?>
            {
                new List<object?> { "a", "b" },
                new List<object?> { "a", "b" },
                new List<object?> { "a", "" },
            },
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task Mutation_CountIndexPopLen()
    {
        var script = Compile("""
            import re
            r = re.findall("a", "aba")
            r.append("c")
            r.sort(reverse=True)
            return [r, r.index("a"), r.pop(), len(r)]
            """);
        var expected = new List<object?>
        {
            new List<object?> { "c", "a" },
            new BigInteger(1),
            "a",
            new BigInteger(2),
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task IndexingAndSlicing()
    {
        var script = Compile("""
            import re
            r = re.findall("a", "aba")
            return [r[0], r[-1], r[0:2], len(r)]
            """);
        var expected = new List<object?>
        {
            "a",
            "a",
            new List<object?> { "a", "a" },
            new BigInteger(2),
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RetainedResults_RespectMemoryBudget()
    {
        var script = Compile("""
            import re
            r = re.findall("a", "aba" * 20000)
            return [len(r), r.count("a")]
            """);
        var expected = new List<object?> { new BigInteger(40000), new BigInteger(40000) };
        var funded = script.Run(new MockLythonHost());
        Assert.True(funded.Success, funded.Failure?.Message);
        Assert.Equal(expected, funded.ReturnValue);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 3000000 };
        var denied = script.Run(new MockLythonHost(), options);
        Assert.False(denied.Success);
        Assert.Equal("MemoryError", denied.Failure?.ExceptionType);
        var fundedAsync = await script.RunAsync(new MockLythonHost());
        Assert.True(fundedAsync.Success, fundedAsync.Failure?.Message);
        Assert.Equal(expected, fundedAsync.ReturnValue);
        var deniedAsync = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(deniedAsync.Success);
        Assert.Equal("MemoryError", deniedAsync.Failure?.ExceptionType);
    }
}
