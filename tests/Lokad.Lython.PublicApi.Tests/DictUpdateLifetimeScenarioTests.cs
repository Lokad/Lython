using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: dict.update kwargs keys own lifetime like the dict-ctor kwargs keys do.
public sealed class DictUpdateLifetimeScenarioTests
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
    public async Task UpdateKwargsDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    d = {}\n    d.update(a=1)\nreturn 0\n", "0");

    [Fact]
    public async Task UpdateKwargsDroppedReceiverDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    {}.update(a=1)\nreturn 0\n", "0");

    [Fact]
    public async Task UpdateMappingDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    d = {}\n    d.update({'a': 1})\nreturn 0\n", "0");

    [Fact]
    public async Task UpdateBehaves()
        => await AssertCompletes(
            "d = {}\nd.update(a=1, b=2)\nd.update({'c': 3})\nreturn str(d)\n", "{'a': 1, 'b': 2, 'c': 3}");

    [Fact]
    public async Task RetainedUpdateDenied()
    {
        var script = new LythonEngine().Compile(
            "objs = []\ni = 0\nwhile i < 20000:\n    d = {}\n    d.update(a=1)\n    objs.append(d)\n    i = i + 1\nreturn len(objs)\n");
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