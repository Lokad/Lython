using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: ChainMap merged views are live shells: two thousand layers over one
/// shared ten-key map resolve within a 512KiB budget in both modes because no
/// merge scratch is reserved anymore.
/// </summary>
public sealed class ChainMapMergeAccountingScenarioTests
{
    [Fact]
    public async Task MergedKeysAvoidMergeScratch()
    {
        var script = new LythonEngine().Compile(
            """
            import collections
            m = {0: 0, 1: 1, 2: 2, 3: 3, 4: 4, 5: 5, 6: 6, 7: 7, 8: 8, 9: 9}
            cm = collections.ChainMap(m)
            i = 0
            while i < 1999:
                cm = cm.new_child(m)
                i = i + 1
            return len(cm.keys())
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 524288 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(10), sync.ReturnValue);
        Assert.True(sync.PeakExecutionMemoryBytes <= 524288);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(10), asyncResult.ReturnValue);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= 524288);
    }

    [Fact]
    public async Task MergedViewBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import collections
            cm = collections.ChainMap({0: 10, 1: 11}, {1: 21, 2: 22})
            return [len(cm.keys()), len(cm.values()), len(cm.items()), len(cm), cm[1]]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(3), new BigInteger(3), new BigInteger(3), new BigInteger(3), new BigInteger(11) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
