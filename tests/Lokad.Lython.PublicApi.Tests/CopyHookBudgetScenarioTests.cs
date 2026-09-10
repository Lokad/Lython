using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG20: copy hooks run ordinary governed code: values built inside a hook
/// charge like any construction, and a hook failure releases the memo scratch
/// and poisons nothing — later copies work. Deep hook-invocation chains stay
/// out of reach until MG25 bounds call recursion.
/// </summary>
public sealed class CopyHookBudgetScenarioTests
{
    [Fact]
    public async Task HookBuiltResultsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import copy
            class Big:
                def __deepcopy__(self, memo):
                    return list(range(100000))
            return len(copy.deepcopy(Big()))
            """);
        Assert.True(script.IsValid);
        // No antidote needed: hook-built values trip the same budget as any
        // retained construction (both modes peak identically).
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task HookFailureCleansUp()
    {
        var script = new LythonEngine().Compile(
            """
            import copy
            results = []
            class Boom:
                def __deepcopy__(self, memo):
                    raise ValueError("boom")
            try:
                copy.deepcopy([Boom()])
                results.append("no-error")
            except ValueError:
                results.append("ValueError")
            results.append(copy.deepcopy([[9]]))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "ValueError", new List<object?> { new List<object?> { new BigInteger(9) } } };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
