using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: constructed function objects own their storage. Each executed def or
/// lambda builds a wrapper plus binding plan and default map that survive as
/// long as the function is retained; twenty thousand retained plain functions
/// must exceed a 1MiB budget in both modes.
/// </summary>
public sealed class FunctionValueAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedFunctionsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            fs = []
            i = 0
            while i < 20000:
                def f():
                    return 1
                fs.append(f)
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ManyRetainedLambdasStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            fs = []
            i = 0
            while i < 20000:
                fs.append(lambda: 1)
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task FunctionBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            def f(a, b=2):
                return a + b
            g = lambda x: x * 2
            return [f(1), f(1, 1), g(21)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(3), new BigInteger(2), new BigInteger(42) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}