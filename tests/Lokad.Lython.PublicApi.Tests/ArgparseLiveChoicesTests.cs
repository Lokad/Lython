using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N03 (argparse part): choices stay live like CPython and never box upfront.
// Membership streams at parse time with work checks, so huge ranges deny
// before retaining far above budget.
public sealed class ArgparseLiveChoicesTests
{
    private static LythonRunOptions Tiny() => new()
    {
        MaxExecutionMemoryBytes = 65536,
        MaxExecutionSteps = 100,
        MaxCollectionSize = 10,
    };

    [Fact]
    public void AppendedChoiceAppliesLikeCPython()
    {
        const string code = """
            import argparse
            p = argparse.ArgumentParser()
            c = [1, 2]
            p.add_argument("--x", type=int, choices=c)
            c.append(3)
            return p.parse_args(["--x", "3"]).x
            """;
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);
        var result = script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(3), result.ReturnValue);
    }

    [Fact]
    public void HugeRangeChoicesDenyUnderTinyLimits()
    {
        // Use an actual token forcing a full scan miss to trigger the drain.
        const string miss = """
            import argparse
            p = argparse.ArgumentParser()
            p.add_argument("--x", type=int, choices=range(200000))
            return p.parse_args(["--x", "999999"]).x
            """;
        var script = new LythonEngine().Compile(miss);
        Assert.True(script.IsValid);
        var result = script.Run(new MockLythonHost(), Tiny());
        Assert.False(result.Success);
        Assert.True(result.Failure?.ExceptionType is "MemoryError" or "RuntimeError", result.Failure?.ExceptionType);
    }

    [Fact]
    public void FundedRangeChoicesAcceptHitAndRejectMiss()
    {
        const string hit = """
            import argparse
            p = argparse.ArgumentParser()
            p.add_argument("--x", type=int, choices=range(5))
            return p.parse_args(["--x", "3"]).x
            """;
        var script = new LythonEngine().Compile(hit);
        var ok = script.Run(new MockLythonHost());
        Assert.True(ok.Success, ok.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(3), ok.ReturnValue);

        const string miss = """
            import argparse
            p = argparse.ArgumentParser()
            p.add_argument("--x", type=int, choices=[1, 2])
            return p.parse_args(["--x", "3"]).x
            """;
        var bad = new LythonEngine().Compile(miss);
        var denied = bad.Run(new MockLythonHost());
        Assert.False(denied.Success);
    }

    [Fact]
    public void InvalidChoicesElementTypeFailsParse()
    {
        const string code = """
            import argparse
            p = argparse.ArgumentParser()
            p.add_argument("--x", choices=[1, 2])
            return p.parse_args(["--x", "3"]).x
            """;
        var script = new LythonEngine().Compile(code);
        var result = script.Run(new MockLythonHost());
        Assert.False(result.Success);
    }
}
