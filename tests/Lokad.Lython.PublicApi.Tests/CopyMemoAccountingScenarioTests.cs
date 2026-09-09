using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG20: per-operation copy memos release their scratch when the copy
/// completes, so repeated small copies cannot accumulate memo charges.
/// </summary>
public sealed class CopyMemoAccountingScenarioTests
{
    [Fact]
    public async Task RepeatedScalarCopiesHoldNoMemoCharge()
    {
        // Scalar copies never touch the memo, so only its construction backing
        // can accumulate: 500 operations must fit in 64KiB.
        var script = new LythonEngine().Compile(
            """
            import copy
            i = 0
            while i < 500:
                m = copy.deepcopy(1)
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task RepeatedSmallCopiesReleaseMemoScratch()
    {
        // One shared input list isolates per-operation costs: each result
        // leaks its own backing either way, but memo scratch must not add up.
        var script = new LythonEngine().Compile(
            """
            import copy
            src = [1, 2]
            i = 0
            while i < 250:
                m = copy.deepcopy(src)
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task DeepCopySharingAndCyclesBehave()
    {
        var script = new LythonEngine().Compile(
            """
            import copy
            shared = [1]
            outer = [shared, shared]
            m = copy.deepcopy(outer)
            cycle = []
            cycle.append(cycle)
            mc = copy.deepcopy(cycle)
            return [m[0] is m[1], mc[0] is mc]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}