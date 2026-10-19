using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG03: an except-as binding retains the exception value through guest code,
// so the record shell must own governor charges there. Raising and unbound
// catching stay free: only retention pays, never hot control flow. Pre-fix,
// 20000 retained empty raises peak near 1.2MB; owning the shell trips the
// budget below.
public sealed class ExceptionBindAccountingScenarioTests
:LifetimeScenario{
    [Fact]
    public async Task ManyRetainedBoundExceptionsStayCharged()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "es = []\n" +
            "i = 0\n" +
            "while i < 20000:\n" +
            "    try:\n" +
            "        raise ValueError()\n" +
            "    except ValueError as e:\n" +
            "        es.append(e)\n" +
            "    i = i + 1\n" +
            "return len(es)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1835008) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task UnboundCatchesFitRoomyBudget()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "i = 0\n" +
            "while i < 16:\n" +
            "    try:\n" +
            "        raise ValueError()\n" +
            "    except ValueError:\n" +
            "        pass\n" +
            "    i = i + 1\n" +
            "return i\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(4194304) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(16), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(16), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BoundButDroppedFitRoomyBudget()
    {
        await AssertBatchCompletes("i = 0\n" +
            "while i < 8:\n" +
            "    try:\n" +
            "        raise ValueError()\n" +
            "    except ValueError as e:\n" +
            "        pass\n" +
            "    i = i + 1\n" +
            "return i\n", new System.Numerics.BigInteger(8), 6291456);
    }
    [Fact]
    public async Task BoundExceptionStillUsable()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "try:\n" +
            "    raise ValueError('oops')\n" +
            "except ValueError as e:\n" +
            "    outcome = [str(e), e.args]\n" +
            "return outcome\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { "oops", new List<object?> { "oops" } };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
