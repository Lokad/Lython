using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: argument binding must not retain scratch per call. The variadic
/// overflow list is only needed when extra positionals actually arrive; every
/// other call drops it, so building it eagerly leaks governed memory per
/// invocation. A few thousand no-op calls fit a small budget once scratch is
/// lazy, in both modes.
/// </summary>
public sealed class CallBinderAccountingScenarioTests
{
    [Fact]
    public async Task ManyCallsWithoutOverflowStayFlat()
    {
        var script = new LythonEngine().Compile(
            """
            def outer():
                pass
            i = 0
            while i < 2000:
                outer()
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(0), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(0), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task VariadicOverflowStillProjects()
    {
        var script = new LythonEngine().Compile(
            """
            def f(*a):
                return len(a)
            def g(a, *b):
                return [a, len(b)]
            return [f(), f(1, 2, 3), g(9), g(9, 8)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(0), new BigInteger(3), new List<object?> { new BigInteger(9), new BigInteger(0) }, new List<object?> { new BigInteger(9), new BigInteger(1) } };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}