using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: the ChainMap maps attribute list owns its backing. Observation checks
/// each unowned value independently without accumulating, so two thousand
/// retained single-map lists must exceed a 256KiB budget in both modes.
/// </summary>
public sealed class ChainMapTablesAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedMapTablesStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import collections
            m = {0: 0}
            cm = collections.ChainMap(m)
            cms = []
            i = 0
            while i < 2000:
                cms.append(cm.maps)
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task MapsAttributeBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import collections
            cm = collections.ChainMap({0: 10}, {1: 11})
            ms = cm.maps
            return [len(ms), ms[0][0], ms[1][1]]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(2), new BigInteger(10), new BigInteger(11) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
