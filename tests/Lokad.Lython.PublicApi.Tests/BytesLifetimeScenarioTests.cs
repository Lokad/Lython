using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: bytes results own lifetime like string results do.
public sealed class BytesLifetimeScenarioTests
{
    private const long ThreeMib = 3145728;
    private const long OneMib = 1048576;

    private static LythonRunOptions Budgeted() => new() { MaxExecutionMemoryBytes = ThreeMib };

    private static async Task AssertCompletes(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), Budgeted());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue?.ToString());
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue?.ToString());
    }

    [Fact]
    public async Task EncodedResultsDiscardCompletes()
        => await AssertCompletes(
            "b = 'x' * 1024\nfor i in range(10000):\n    y = b.encode()\nreturn 0\n", "0");

    [Fact]
    public async Task HexDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = b\"abc\".hex()\nreturn 0\n", "0");

    [Fact]
    public async Task HexSeparatorDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = b\"abc\".hex(\" \", 1)\nreturn 0\n", "0");

    [Fact]
    public async Task HexBehaves()
        => await AssertCompletes(
            "return b\"abc\".hex() + \"|\" + b\"abc\".hex(\" \", 1)\n", "616263|61 62 63");

    [Fact]
    public async Task RetainedHexDenied()
    {
        var script = new LythonEngine().Compile(
            "objs = []\ni = 0\nwhile i < 20000:\n    objs.append(b\"abc\".hex())\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = OneMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= OneMib);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= OneMib);
    }
}
