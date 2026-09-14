using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG11: each distinct live identity owns one registry entry (shared across
// repeated lookups, released with dropped objects); unowned identity storage
// must not grow beside the charged values.
public sealed class IdentityAccountingScenarioTests
{
    private const long SixteenMib = 16777216;

    [Fact]
    public async Task ManyIdentitiesStayCharged()
    {
        var script = new LythonEngine().Compile(
            "objs = [set() for i in range(100000)]\nfor obj in objs:\n    id(obj)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = SixteenMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= SixteenMib);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= SixteenMib);
    }

    [Fact]
    public async Task RepeatedIdLookupsShareCharge()
    {
        var script = new LythonEngine().Compile(
            "x = set()\ni = 0\nwhile i < 20000:\n    id(x)\n    i = i + 1\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(0);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 131072 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task IdentityValuesBehave()
    {
        var script = new LythonEngine().Compile("x = []\ny = []\nreturn [id(x) == id(x), id(x) == id(y)]\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new List<object?> { true, false };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
