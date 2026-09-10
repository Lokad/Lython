using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: format_help/format_usage own their rendered text instead of escaping
/// (print paths keep the transient form since output capture governs
/// downstream). Twenty thousand retained helps must exceed a 2MB budget in
/// both modes; pre-fix they fit in ~0.6MB of backing storage alone.
/// </summary>
public sealed class ArgparseHelpAccountingScenarioTests
{
    private const long HelpBudgetBytes = 2097152;

    [Fact]
    public async Task ManyRetainedHelpsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import argparse
            p = argparse.ArgumentParser(prog="p", description="Do things.")
            p.add_argument("--foo", help="Foo.")
            objs = []
            i = 0
            while i < 20000:
                objs.append(p.format_help())
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = HelpBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= HelpBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= HelpBudgetBytes);
    }

    [Fact]
    public async Task FormatHelpBehaves()
    {
        var script = new LythonEngine().Compile("""
            import argparse
            p = argparse.ArgumentParser(prog="p", description="Do things.")
            p.add_argument("--foo", help="Foo.")
            h = p.format_help()
            return [p.format_usage(), "Do things." in h, "--foo FOO" in h, h.startswith("usage: p")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "usage: p [--help] [--foo FOO]\n", true, true, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}