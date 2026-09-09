using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: per-call **kwargs key payload stays charged like other constructed
/// strings. Two thousand retained single-keyword dicts must exceed a 512KiB
/// budget in both modes.
/// </summary>
public sealed class KwargsKeyAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedKwargsKeysStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            def f(**kw):
                return kw
            ds = []
            i = 0
            while i < 2000:
                ds.append(f(k=i))
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 524288 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task KwargsBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            def f(**kw):
                return kw
            return [f(x=40 + 2)["x"], len(f())]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(42), new BigInteger(0) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
