using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: nested definitions own lifetime like lambdas do: the governed name,
// freshly retained closure contexts/cells and the value ride one pool coupon.
public sealed class NestedFunctionLifetimeScenarioTests
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
    public async Task NestedDefDiscardCompletes()
        => await AssertCompletes(
            "def outer():\n    def inner():\n        return 1\n    return inner\nfor i in range(50000):\n    y = outer()\nreturn 0\n", "0");

    [Fact]
    public async Task NestedDefCapturingDiscardCompletes()
        => await AssertCompletes(
            "def outer():\n    x = 1\n    def inner():\n        return x\n    return inner\nfor i in range(50000):\n    y = outer()()\nreturn 0\n", "0");

    [Fact]
    public async Task ModuleLoopDefDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    def f():\n        return 1\nreturn 0\n", "0");

    [Fact]
    public async Task NestedDefBehaves()
        => await AssertCompletes(
            "def outer(v):\n    x = 1\n    def inner():\n        return v * 2 + x\n    return inner\nreturn str(outer(21)())\n", "43");

    [Fact]
    public async Task RetainedNestedDefDenied()
    {
        var script = new LythonEngine().Compile(
            "def outer():\n    def inner():\n        return 1\n    return inner\nobjs = []\ni = 0\nwhile i < 20000:\n    objs.append(outer())\n    i = i + 1\nreturn len(objs)\n");
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