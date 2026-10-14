using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// Unknown tokens preserve their values across finite host batches. Large
/// retained parse collections still exhaust their execution allowance.
/// </summary>
public sealed class ArgparseUnknownAccountingScenarioTests : LifetimeScenario
{
    private const long UnknownBudgetBytes = 13631488;

    [Fact]
    public async Task UnknownTokensSurviveWithinEachBatch()
    {
        await AssertBatchCompletes("""
            import argparse
            p = argparse.ArgumentParser(prog="p")
            objs = []
            i = 0
            while i < 16:
                objs.append(p.parse_known_args(["--zzz", "abc"]))
                i = i + 1
            return len(objs) == 16 and all(x[1] == ["--zzz", "abc"] for x in objs)
            """, true, 65536);
    }

    [Fact]
    public async Task ThirtyThousandRetainedUnknownsDeny()
    {
        var script = new LythonEngine().Compile("""
            import argparse
            p = argparse.ArgumentParser(prog="p")
            objs = []
            i = 0
            while i < 30000:
                objs.append(p.parse_known_args(["--zzz", "abc"]))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = UnknownBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= UnknownBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= UnknownBudgetBytes);
    }

    [Fact]
    public async Task ParseKnownArgsBehaves()
    {
        var script = new LythonEngine().Compile("""
            import argparse
            p = argparse.ArgumentParser(prog="p")
            p.add_argument("--foo")
            ns, rest = p.parse_known_args(["--zzz", "abc", "--foo", "bar"])
            return [ns.foo, rest]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "bar", new List<object?> { "--zzz", "abc" } };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
