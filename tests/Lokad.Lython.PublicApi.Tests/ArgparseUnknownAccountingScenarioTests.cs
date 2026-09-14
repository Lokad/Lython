using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: parse_known_args unknown tokens own their string payload beside the
/// already-governed list backing, like argparse token conversion. Twenty
/// thousand retained parses complete under a 13MB budget now that dropped
/// argument-list temporaries release; the near-budget peak proves the unknowns
/// stay charged. Thirty thousand retained parses still exceed it.
/// </summary>
public sealed class ArgparseUnknownAccountingScenarioTests
{
    private const long UnknownBudgetBytes = 13631488;

    [Fact]
    public async Task ManyRetainedUnknownsStayCharged()
    {
        // Twenty thousand retained parses complete now that dropped
        // argument-list temporaries release; the near-budget peak (about 13.5MB
        // measured) proves the unknown tokens stay charged: uncharged unknowns
        // would peak near 8MB instead.
        var script = new LythonEngine().Compile("""
            import argparse
            p = argparse.ArgumentParser(prog="p")
            objs = []
            i = 0
            while i < 20000:
                objs.append(p.parse_known_args(["--zzz", "abc"]))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var expected = new BigInteger(20000);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = UnknownBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        Assert.True(sync.PeakExecutionMemoryBytes <= UnknownBudgetBytes);
        Assert.True(sync.PeakExecutionMemoryBytes >= 12000000, "peak=" + sync.PeakExecutionMemoryBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= UnknownBudgetBytes);
        Assert.True(asyncResult.PeakExecutionMemoryBytes >= 12000000, "peak=" + asyncResult.PeakExecutionMemoryBytes);
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