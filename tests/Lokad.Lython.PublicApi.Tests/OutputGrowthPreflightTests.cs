using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N04: output growth is charged before it can allocate. Quantile/sample
// counts preflight collection/memory, numeric loops check work, and shlex
// join governs variable string growth including quoting/encoding overlap.
public sealed class OutputGrowthPreflightTests
{
    private static LythonRunOptions Tiny() => new()
    {
        MaxExecutionMemoryBytes = 65536,
        MaxExecutionSteps = 100,
        MaxCollectionSize = 10,
    };

    private static void AssertDenied(LythonExecutionResult result)
    {
        Assert.False(result.Success);
        Assert.True(result.Failure?.ExceptionType is "MemoryError" or "RuntimeError", result.Failure?.ExceptionType);
    }

    [Fact]
    public async Task FundedQuantiles_MatchesBothModes()
    {
        const string code = "import statistics\nreturn statistics.quantiles([1, 2, 3, 4], n=4)\n";
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
        Assert.True(((System.Collections.IList)sync.ReturnValue!).Count == 3);
    }

    [Fact]
    public async Task QuantilesN1_ReturnsEmpty()
    {
        const string code = "import statistics\nreturn statistics.quantiles([1, 2], n=1)\n";
        var script = new LythonEngine().Compile(code);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Empty((System.Collections.IEnumerable)sync.ReturnValue!);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task HugeQuantiles_DeniesUnderTinyLimits()
    {
        const string code = "import statistics\nreturn len(statistics.quantiles([1, 2], n=200000))\n";
        var script = new LythonEngine().Compile(code);
        AssertDenied(script.Run(new MockLythonHost(), Tiny()));
        AssertDenied(await script.RunAsync(new MockLythonHost(), Tiny()));
        // Subsequent funded run still succeeds (no poisoned reservation).
        var funded = script.Run(new MockLythonHost());
        Assert.True(funded.Success, funded.Failure?.Message);
    }

    [Fact]
    public async Task FundedNormalDistQuantiles_MatchesBothModes()
    {
        const string code = "import statistics\nreturn len(statistics.NormalDist().quantiles(10))\n";
        var script = new LythonEngine().Compile(code);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(9), sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task HugeNormalDistQuantiles_DeniesUnderTinyLimits()
    {
        const string code = "import statistics\nreturn len(statistics.NormalDist().quantiles(200000))\n";
        var script = new LythonEngine().Compile(code);
        AssertDenied(script.Run(new MockLythonHost(), Tiny()));
        AssertDenied(await script.RunAsync(new MockLythonHost(), Tiny()));
    }

    [Fact]
    public async Task HugeNormalDistSamples_DeniesUnderTinyLimits()
    {
        const string code = "import statistics\nreturn len(statistics.NormalDist().samples(200000))\n";
        var script = new LythonEngine().Compile(code);
        AssertDenied(script.Run(new MockLythonHost(), Tiny()));
        AssertDenied(await script.RunAsync(new MockLythonHost(), Tiny()));
    }

    [Fact]
    public async Task FundedNormalDistSamples_Succeeds()
    {
        const string code = "import statistics\nreturn len(statistics.NormalDist().samples(5))\n";
        var script = new LythonEngine().Compile(code);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(5), sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task SamplesZero_ReturnsEmpty()
    {
        const string code = "import statistics\nreturn statistics.NormalDist().samples(0)\n";
        var script = new LythonEngine().Compile(code);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Empty((System.Collections.IEnumerable)sync.ReturnValue!);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public void QuotedJoin_PreservesQuoting()
    {
        const string code = "import shlex\nwords = [\"a\", \"b c\", \"d'e\", \"\"]\nreturn shlex.split(shlex.join(words)) == words\n";
        var script = new LythonEngine().Compile(code);
        var result = script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(true, result.ReturnValue);
    }

    [Fact]
    public void FundedJoin_MatchesExpected()
    {
        const string code = "import shlex\nreturn shlex.join([\"a\", \"b\"])\n";
        var script = new LythonEngine().Compile(code);
        var result = script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("a b", result.ReturnValue);
    }

    [Fact]
    public void HugeJoin_DeniesUnderTinyLimits()
    {
        const string code = "import shlex, itertools\nreturn len(shlex.join(itertools.repeat(\"a\" * 10000, 1000)))\n";
        var script = new LythonEngine().Compile(code);
        AssertDenied(script.Run(new MockLythonHost(), Tiny()));
    }
}
