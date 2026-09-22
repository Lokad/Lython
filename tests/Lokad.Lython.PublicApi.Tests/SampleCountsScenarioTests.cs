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
    public async Task CountedSampling_MapsSelectedPositionOnCollision()
    {
        // N14: on a Floyd-selection collision the resume slot is the fresh
        // position, so a full draw over [x, y] x [2, 2] holds exact counts.
        var script = new LythonEngine().Compile("""
            import random
            return random.sample(["x", "y"], k=4, counts=[2, 2]).count("x")
            """);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(2), sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(2), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CountedSampling_FullDrawHoldsMultiplicitiesAcrossSeeds()
    {
        // Invariant over many seeds rather than one pinned sequence: every
        // full draw must reproduce the pool multiplicities exactly.
        var script = new LythonEngine().Compile("""
            import random
            bad = 0
            for seed in range(50):
                random.seed(seed)
                if sorted(random.sample(["x", "y"], k=4, counts=[2, 2])) != ["x", "x", "y", "y"]:
                    bad = bad + 1
            return bad
            """);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(0), sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(0), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CountedSampling_ZeroSkewedAndEmptyCounts()
    {
        var script = new LythonEngine().Compile("""
            import random
            random.seed(7)
            partial = random.sample(["a", "b", "c"], k=2, counts=[3, 0, 2])
            empty = random.sample(["a"], k=0, counts=[5])
            random.seed(3)
            skewed = random.sample(["p", "q"], k=6, counts=[5, 1]).count("p")
            return [len(partial), "b" in partial, empty, skewed]
            """);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        var expected = new List<object?>
        {
            new BigInteger(2),
            false,
            new List<object?>(),
            new BigInteger(5),
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CountedSampling_PartialDrawStaysWithinCounts()
    {
        var script = new LythonEngine().Compile("""
            import random
            random.seed(11)
            r = random.sample(["x", "y"], k=2, counts=[2, 2])
            return [len(r), r.count("x") <= 2, r.count("y") <= 2]
            """);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        var expected = new List<object?> { new BigInteger(2), true, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
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
        // MG15: large takes still drain through the shared governed
        // sequence. 100,000 refs used to materialize free; now the backing
        // alone exceeds the budget. Small takes serve Floyd positions
        // without draining (see RandomDirectSelectionTests).
        var script = new LythonEngine().Compile(
            """
            import random
            return random.sample(range(100000), 50000)
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
