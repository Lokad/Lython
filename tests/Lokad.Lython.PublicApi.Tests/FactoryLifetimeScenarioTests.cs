using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: comprehension results own lifetime like displays do.
public sealed class FactoryLifetimeScenarioTests
{    private const long ThreeMib = 3145728;

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
    }    [Fact]
    public async Task ListComprehensionsDiscardCompletes()
        => await AssertCompletes(
            "for i in range(100000):\n    x = [j for j in range(2)]\nreturn 0\n", "0");

    [Fact]
    public async Task DictComprehensionsDiscardCompletes()
        => await AssertCompletes(
            "for i in range(100000):\n    x = {j: j for j in range(2)}\nreturn 0\n", "0");

    [Fact]
    public async Task ListDisplaysDiscardCompletes()
        => await AssertCompletes(
            "for i in range(100000):\n    x = [1, 2]\nreturn 0\n", "0");

    [Fact]
    public async Task SetComprehensionOverRangeCompletes()
        => await AssertCompletes(
            "for i in range(100000):\n    x = {j for j in range(2)}\nreturn 0\n", "0");

    [Fact]
    public async Task SetComprehensionOverTupleCompletes()
        => await AssertCompletes(
            "for i in range(100000):\n    x = {j for j in (1,)}\nreturn 0\n", "0");
}
