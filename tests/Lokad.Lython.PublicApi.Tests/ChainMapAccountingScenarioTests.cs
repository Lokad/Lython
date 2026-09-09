using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: every dictionary reachable through a ChainMap is governed, so bulk
/// writes through parents or converted default dicts cannot bypass the
/// execution memory budget. Integer keys isolate backing growth from key
/// literal costs.
/// </summary>
public sealed class ChainMapAccountingScenarioTests
{
    [Fact]
    public async Task ManyParentsEntriesStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            from collections import ChainMap
            cm = ChainMap()
            p = cm.parents
            i = 0
            while i < 20000:
                p[i] = i
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
    public async Task ManyDefaultDictEntriesStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            from collections import ChainMap, defaultdict
            d = defaultdict(int)
            i = 0
            while i < 20000:
                d[i] = i
                i = i + 1
            cm = ChainMap(d)
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
    public async Task ChainMapBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            from collections import ChainMap, defaultdict
            d = defaultdict(int)
            d["a"] = 1
            cm = ChainMap(d)
            cm["b"] = 2
            return [cm["a"], cm["b"], len(cm.parents)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(1), new BigInteger(2), new BigInteger(0) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}