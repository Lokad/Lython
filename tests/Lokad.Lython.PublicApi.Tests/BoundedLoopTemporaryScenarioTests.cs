using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG03/MG06/MG08: dropped concatenation temporaries release through the
// reclamation pool (with an exhaustion backstop that proves denials against
// live retention), so a bounded loop over fixed-size inputs completes while
// genuinely retained output still trips the budget.
public sealed class BoundedLoopTemporaryScenarioTests
{
    private const long ThreeMib = 3145728;

    private static void AssertMemoryError(LythonExecutionResult result)
    {
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.True(result.PeakExecutionMemoryBytes <= ThreeMib);
    }

    [Fact]
    public async Task BoundedConcatLoopCompletes()
    {
        var script = new LythonEngine().Compile(
            "b = 'a' * 1000\nx = ''\nfor i in range(10000):\n    x = b + b\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(0);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BoundedRepeatLoopCompletes()
    {
        var script = new LythonEngine().Compile(
            "b = 'a' * 1000\nx = ''\nfor i in range(10000):\n    x = b * 2\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(0);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RetainedConcatOutputStaysCharged()
    {
        var script = new LythonEngine().Compile(
            "b = 'a' * 1000\nxs = []\nfor i in range(10000):\n    xs.append(b + b)\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        AssertMemoryError(script.Run(new MockLythonHost(), options));
        AssertMemoryError(await script.RunAsync(new MockLythonHost(), options));
    }

    [Fact]
    public async Task AliasedConcatResultsShare()
    {
        var script = new LythonEngine().Compile(
            "b = 'a' * 1000\nbase = b + b\nxs = []\nfor i in range(10000):\n    xs.append(base)\nreturn len(xs)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(10000);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CaughtConcatFailureAllowsReuse()
    {
        var script = new LythonEngine().Compile(
            "b = 'a' * 1000\nxs = []\nok = False\ni = 0\nwhile i < 10000:\n    try:\n        xs.append(b + b)\n    except MemoryError:\n        ok = True\n        break\n    i = i + 1\nreturn ok\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(true, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(true, asyncResult.ReturnValue);
    }
}
