using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// M04: tracked ownership must stay current across growth and cache builds:
// pool coupons snapshot construction alone, so later growth and rune-cache
// charges strand on every dropped value until the budget trips.
public sealed class GrowthReclamationScenarioTests
{
    private const long ThreeMib = 3145728;

    private static LythonRunOptions Budgeted() => new() { MaxExecutionMemoryBytes = ThreeMib };

    [Fact]
    public async Task ListGrowDiscardCompletes()
    {
        var script = new LythonEngine().Compile(
            "for i in range(2000):\n    x = []\n    for j in range(1000):\n        x.append(None)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(0);
        var sync = script.Run(new MockLythonHost(), Budgeted());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ListRepeatCompletes()
    {
        var script = new LythonEngine().Compile(
            "for i in range(2000):\n    x = [None] * 1000\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(0);
        var sync = script.Run(new MockLythonHost(), Budgeted());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DictGrowDiscardCompletes()
    {
        var script = new LythonEngine().Compile(
            "for i in range(1000):\n    d = {}\n    for j in range(100):\n        d[j] = j\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(0);
        var sync = script.Run(new MockLythonHost(), Budgeted());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SetGrowDiscardCompletes()
    {
        var script = new LythonEngine().Compile(
            "for i in range(10000):\n    s = set()\n    for j in range(100):\n        s.add(j)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(0);
        var sync = script.Run(new MockLythonHost(), Budgeted());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NonAsciiSliceDiscardCompletes()
    {
        var script = new LythonEngine().Compile(
            "for i in range(2000):\n    x = '\u00e9' * 1000\n    y = x[1:]\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(0);
        var sync = script.Run(new MockLythonHost(), Budgeted());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task AsciiSliceDiscardCompletes()
    {
        var script = new LythonEngine().Compile(
            "for i in range(2000):\n    x = 'a' * 1000\n    y = x[1:]\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(0);
        var sync = script.Run(new MockLythonHost(), Budgeted());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RetainedListGrowthDenies()
    {
        // Honest retention still trips: a fully retained 100k list cannot fit 1 MiB.
        var script = new LythonEngine().Compile(
            "x = []\nfor j in range(100000):\n    x.append(j)\nreturn len(x)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task AliasedListGrowthShared()
    {
        // Growth through one alias is visible through the other with a single
        // backing charge: coupons re-snapshot, they never double-commit.
        var script = new LythonEngine().Compile(
            "x = []\nfor j in range(100):\n    x.append(j)\ny = x\nfor j in range(100, 200):\n    y.append(j)\nreturn len(x)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(200);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ListGrowDiscard1xCompletes()
    {
        // Slope control: one tenth of the iterations under the same budget (the 10x case is ListGrowDiscardCompletes).
        var script = new LythonEngine().Compile(
            "for i in range(200):\n    x = []\n    for j in range(1000):\n        x.append(None)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(0);
        var sync = script.Run(new MockLythonHost(), Budgeted());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DeniedAppendKeepsListUsable()
    {
        // A denied append adds nothing: the list stays usable afterwards.
        var script = new LythonEngine().Compile(
            "x = []\ndenied = False\ntry:\n    for j in range(10000000):\n        x.append(j)\nexcept MemoryError:\n    denied = True\nreturn (denied, len(x) > 1000)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), Budgeted());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { true, true }, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { true, true }, asyncResult.ReturnValue);
    }
}
