using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: bytes results own lifetime like string results do.
public class BytesLifetimeScenarioTests : LifetimeScenario
{
    private long StreamingBudget => Budget(3145728);
    private long RetentionBudget => Budget(1048576);

    private LythonRunOptions Budgeted() => new() { MemoryBudget = new LythonMemoryBudget(StreamingBudget) };

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);

    [Fact]
    public async Task EncodedResultsDiscardCompletes()
        => await AssertCompletes(
            "b = 'x' * 1024\nfor i in range(" + BatchIterations(10000) + "):\n    y = b.encode()\nreturn 0\n", "0", Iterations(10000));

    [Fact]
    public async Task HexDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = b\"abc\".hex()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task HexSeparatorDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = b\"abc\".hex(\" \", 1)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task HexBehaves()
        => await AssertCompletes(
            "return b\"abc\".hex() + \"|\" + b\"abc\".hex(\" \", 1)\n", "616263|61 62 63");

    [Fact]
    public async Task RetainedHexDenied()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "objs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    objs.append(b\"abc\".hex())\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(RetentionBudget) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= RetentionBudget);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= RetentionBudget);
    }

    [Fact]
    public async Task PartitionDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = b\"abc\".partition(b\"b\")\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task RPartitionDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = b\"abc\".rpartition(b\"b\")\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task PartitionBehaves()
        => await AssertCompletes(
            "p = b\"a,b\".partition(b\",\")\nq = b\"a,b\".rpartition(b\",\")\nm = b\"abc\".partition(b\"z\")\nreturn p[0].hex() + p[1].hex() + p[2].hex() + \"|\" + q[0].hex() + q[1].hex() + q[2].hex() + \"|\" + m[0].hex() + m[1].hex() + m[2].hex()\n", "612c62|612c62|616263");

    [Fact]
    public async Task RetainedPartitionDenied()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "objs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    objs.append(b\"abc\".partition(b\"b\"))\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(RetentionBudget) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= RetentionBudget);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= RetentionBudget);
    }
}
