using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG03: text freshly converted from unowned CLR sources (integer renders,
/// parsed argument tokens) is owned at construction instead of escaping.
/// </summary>
public sealed class ConvertedTextAccountingScenarioTests
{
    // 20k retained renders own ~130B plus a 16B list slot each, so they fit
    // 1.5MB pre-fix and trip post-fix. 3k parsed namespaces own their values
    // on top of table slots.
    private const long RenderBudgetBytes = 1572864;
    // 3k parsed namespaces own table slots (~144B with the outer slot) plus
    // the ~131B token value, so they fit 600KB pre-fix and trip post-fix.
    private const long ParseBudgetBytes = 600000;

    [Fact]
    public async Task ManyRetainedHexRendersStayCharged()
    {
        var script = new LythonEngine().Compile("""
            objs = []
            i = 0
            while i < 20000:
                objs.append(hex(255))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = RenderBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= RenderBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= RenderBudgetBytes);
    }

    [Fact]
    public async Task ManyParsedNamespacesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import argparse
            p = argparse.ArgumentParser(prog="p")
            p.add_argument("--foo")
            args = ["--foo", "bar"]
            objs = []
            i = 0
            while i < 3000:
                objs.append(p.parse_args(args))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ParseBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ConvertedTextBehaves()
    {
        var script = new LythonEngine().Compile("""
            import argparse
            p = argparse.ArgumentParser(prog="p")
            p.add_argument("--foo")
            r = p.parse_args(["--foo", "bar"])
            return [hex(255), oct(8), bin(5), r.foo]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "0xff", "0o10", "0b101", "bar" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}