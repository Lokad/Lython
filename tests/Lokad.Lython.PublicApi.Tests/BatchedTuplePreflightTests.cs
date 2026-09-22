using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N02: sized batching and tuple repetition deny before large allocation,
// grow with consumed input, and preserve empty/short/strict/funded behavior
// in both modes. Allocation traffic is pinned white-box; here we pin
// observable results, denial, and repeat-pull/delayed composition.
public sealed class BatchedTuplePreflightTests
{
    private static LythonRunOptions Tiny() => new()
    {
        MaxExecutionMemoryBytes = 65536,
        MaxExecutionSteps = 100,
        MaxCollectionSize = 10,
    };

    private static void AssertError(LythonExecutionResult result, string type, string fragment)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(type, result.Failure!.ExceptionType);
        Assert.Contains(fragment, result.Failure!.Message, StringComparison.Ordinal);
    }

    private static async Task AssertBothModes(string source, object? expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task EmptyBatchedHugeSize_ReturnsNone()
        => await AssertBothModes(
            "import itertools\nreturn next(itertools.batched([], 2000000), None)\n",
            null);

    [Fact]
    public async Task ShortBatchedHugeSize_ReturnsSingle()
        => await AssertBothModes(
            "import itertools\nreturn next(itertools.batched([1], 2000000))\n",
            new object?[] { new BigInteger(1) });

    [Fact]
    public async Task EmptyBatchedHugeSize_SucceedsUnderTinyLimits()
    {
        const string code = "import itertools\nreturn next(itertools.batched([], 2000000), None)\n";
        var script = new LythonEngine().Compile(code);
        var sync = script.Run(new MockLythonHost(), Tiny());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Null(sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), Tiny());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Null(asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ShortBatchedHugeSize_SucceedsUnderTinyLimits()
    {
        const string code = "import itertools\nreturn next(itertools.batched([1], 2000000))\n";
        var script = new LythonEngine().Compile(code);
        var expected = new object?[] { new BigInteger(1) };
        var sync = script.Run(new MockLythonHost(), Tiny());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), Tiny());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task FundedBatched_MatchesBothModes()
        => await AssertBothModes(
            "import itertools\nreturn list(itertools.batched([1, 2, 3], 2))\n",
            new List<object?> { new List<object?> { new BigInteger(1), new BigInteger(2) }, new List<object?> { new BigInteger(3) } });

    [Fact]
    public async Task StrictShortfall_ThrowsValueError()
    {
        const string code = "import itertools\nreturn list(itertools.batched([1], 2, strict=True))\n";
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);
        AssertError(script.Run(new MockLythonHost()), "ValueError", "incomplete batch");
        AssertError(await script.RunAsync(new MockLythonHost()), "ValueError", "incomplete batch");
    }

    [Fact]
    public async Task BatchedRepeatPulls_AfterExhaustionStayExhausted()
        => await AssertBothModes(
            "import itertools\nit = itertools.batched([1, 2, 3], 2)\nfirst = list(it)\nsecond = list(it)\nthird = next(it, None)\nreturn [first, second, third]\n",
            new List<object?>
            {
                new List<object?> { new List<object?> { new BigInteger(1), new BigInteger(2) }, new List<object?> { new BigInteger(3) } },
                new List<object?>(),
                null,
            });

    [Fact]
    public void BatchedHugeSource_DeniesUnderTinyCollectionLimit()
    {
        const string code = "import itertools\nreturn next(itertools.batched(range(200000), 200000))\n";
        var script = new LythonEngine().Compile(code);
        AssertError(script.Run(new MockLythonHost(), Tiny()), "RuntimeError", "maximum collection size exceeded");
    }

    [Fact]
    public async Task BatchedHugeSource_DeniesUnderTinyCollectionLimitAsync()
    {
        const string code = "import itertools\nreturn next(itertools.batched(range(200000), 200000))\n";
        var script = new LythonEngine().Compile(code);
        AssertError(await script.RunAsync(new MockLythonHost(), Tiny()), "RuntimeError", "maximum collection size exceeded");
    }

    [Fact]
    public void TupleRepeatHuge_DeniesUnderTinyLimits()
    {
        const string code = "x = (0,) * 2000000\nreturn len(x)\n";
        var script = new LythonEngine().Compile(code);
        var result = script.Run(new MockLythonHost(), Tiny());
        Assert.False(result.Success);
        Assert.True(result.Failure?.ExceptionType is "MemoryError" or "RuntimeError", result.Failure?.ExceptionType);
    }

    [Fact]
    public async Task TupleRepeatHuge_DeniesUnderTinyLimitsAsync()
    {
        const string code = "x = (0,) * 2000000\nreturn len(x)\n";
        var script = new LythonEngine().Compile(code);
        var result = await script.RunAsync(new MockLythonHost(), Tiny());
        Assert.False(result.Success);
        Assert.True(result.Failure?.ExceptionType is "MemoryError" or "RuntimeError", result.Failure?.ExceptionType);
    }

    [Fact]
    public async Task FundedTupleRepeat_MatchesBothModes()
        => await AssertBothModes(
            "x = (1, 2) * 3\nreturn [len(x), x[0], x[5]]\n",
            new List<object?> { new BigInteger(6), new BigInteger(1), new BigInteger(2) });

    [Fact]
    public async Task EmptyTupleRepeat_ReturnsEmpty()
        => await AssertBothModes(
            "return [(0,) * 0, () * 2000000]\n",
            new List<object?> { new List<object?>(), new List<object?>() });

    [Fact]
    public async Task BatchedOverDelayedFile_ComposesAsync()
    {
        const string code = "with open(\"/r.txt\") as f:\n    return list(itertools.batched((line.strip() for line in f), 2))\n";
        const string full = "import itertools\n" + code;
        var script = new LythonEngine().Compile(full);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/r.txt", "a\nb\nc\n");
        var result = await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal(
            new List<object?> { new List<object?> { "a", "b" }, new List<object?> { "c" } },
            result.ReturnValue);
    }

    [Fact]
    public void BatchedOverDelayedFile_FailsFastSync()
    {
        const string full = "import itertools\nwith open(\"/r.txt\") as f:\n    return list(itertools.batched((line.strip() for line in f), 2))\n";
        var script = new LythonEngine().Compile(full);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/r.txt", "a\nb\nc\n");
        var result = script.Run(host);
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("use RunAsync", result.Failure?.Message, StringComparison.Ordinal);
    }
}
