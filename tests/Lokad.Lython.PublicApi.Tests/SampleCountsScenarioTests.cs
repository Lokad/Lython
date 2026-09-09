using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG15: counted sampling draws distinct expanded positions through cumulative
/// bounds (Floyd) instead of expanding counts into one reference per
/// occurrence, so live structures scale with pools plus picks.
/// </summary>
public sealed class SampleCountsScenarioTests
{
    [Fact]
    public async Task CountedSamplingWithoutExpansion()
    {
        // MG15 probe: one pick out of a million expanded copies used to build
        // the whole expansion. Now only pools, one bound and one pick exist.
        var script = new LythonEngine().Compile(
            """
            import random
            return random.sample(["x"], k=1, counts=[1000000])
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var expected = new List<object?> { "x" };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task CountedSamplingBoundedByCounts()
    {
        // MG15: the counts drain itself commits per growth, so a lazy
        // 200,000-long counts sequence fails on its own backing with an
        // empty total and no picks at all.
        var script = new LythonEngine().Compile(
            """
            import itertools
            import random
            return random.sample(range(200000), 0, counts=itertools.repeat(0, 200000))
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task CountedSamplingBoundedByPicks()
    {
        // MG15: the distinct-position set scales with picks, so half a million
        // picks fail on their own table while pools stay tiny.
        var script = new LythonEngine().Compile(
            """
            import random
            return random.sample(["a", "b"], 500000, counts=[300000, 300000])
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task CountedSamplingKeepsValueAndErrorContracts()
    {
        // MG15: position mapping must resolve pools exactly (degenerate
        // single-pool selections are RNG-independent) and preserve every
        // counts error contract.
        var script = new LythonEngine().Compile(
            """
            import random
            results = []
            s = random.sample(["a", "b", "c"], 2, counts=[0, 5, 0])
            results.append(s[0])
            results.append(s[1])
            try:
                random.sample(["a", "b"], 1, counts=[1])
            except ValueError:
                results.append("parity")
            try:
                random.sample(["a", "b"], 1, counts=[1, -1])
            except ValueError:
                results.append("negative")
            try:
                random.sample(["a"], 2, counts=[1])
            except ValueError:
                results.append("toobig")
            try:
                random.sample(["a", "b"], 1, counts=[1073741824, 1073741824])
            except OverflowError:
                results.append("overflow")
            return results
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?> { "b", "b", "parity", "negative", "toobig", "overflow" };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
    [Fact]
    public async Task PopulationDrainStaysCharged()
    {
        // MG15: sample and choices populations drain through the shared
        // governed sequence. 100,000 refs used to materialize free; now the
        // backing alone exceeds the budget.
        var script = new LythonEngine().Compile(
            """
            import random
            return random.sample(range(100000), 5)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task SamplingKeepsValueAndErrorContracts()
    {
        // MG15: governing populations and weights must preserve sample shapes
        // and the oversized/mismatched error contracts.
        var script = new LythonEngine().Compile(
            """
            import random
            results = []
            results.append(len(random.sample([1, 2, 3, 4], 2)))
            results.append(len(random.choices([10, 20, 30], k=2)))
            try:
                random.sample([1, 2], 5)
            except ValueError:
                results.append("toobig")
            try:
                random.sample([1, 2], 1, counts=[1])
            except ValueError:
                results.append("parity")
            return results
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?> { new BigInteger(2), new BigInteger(2), "toobig", "parity" };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
}
