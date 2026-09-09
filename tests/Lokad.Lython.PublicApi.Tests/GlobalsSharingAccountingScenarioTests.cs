using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG22: host globals normalize with sharing preserved, so one object
/// aliased across many globals is converted and charged once instead of per
/// occurrence. Cycles still fail explicitly.
/// </summary>
public sealed class GlobalsSharingAccountingScenarioTests
{
    private static Dictionary<string, object?> SharedGlobals()
    {
        var big = new List<object?>();
        for (var i = 0; i < 5000; i++)
        {
            big.Add((object)(long)i);
        }

        var globals = new Dictionary<string, object?>();
        for (var i = 0; i < 200; i++)
        {
            globals["v" + i] = big;
        }

        return globals;
    }

    [Fact]
    public async Task SharedGlobalsFitOnce()
    {
        var script = new LythonEngine().Compile("return len(v0)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144, Globals = SharedGlobals() };
        var expected = new BigInteger(5000);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SharedGlobalsKeepIdentity()
    {
        var script = new LythonEngine().Compile("return [v0 is v199, v0[0], v199[4999]]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, (object)(long)0, (object)(long)4999 };
        var sync = script.Run(new MockLythonHost(), new LythonRunOptions { Globals = SharedGlobals() });
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), new LythonRunOptions { Globals = SharedGlobals() });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CyclicGlobalsStillFail()
    {
        var cyclic = new List<object?>();
        cyclic.Add(cyclic);
        var globals = new Dictionary<string, object?> { ["c"] = cyclic };
        var script = new LythonEngine().Compile("return 0\n");
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost(), new LythonRunOptions { Globals = globals });
        Assert.False(sync.Success);
        Assert.Equal("TypeError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), new LythonRunOptions { Globals = globals });
        Assert.False(asyncResult.Success);
        Assert.Equal("TypeError", asyncResult.Failure?.ExceptionType);
    }
}