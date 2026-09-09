using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: ChainMap dict-unpacking and len() reserve the transient merge peak.
/// Unpacking or measuring two thousand shared layers must exceed a 512KiB
/// budget in both modes, while the ten-key result stays tiny.
/// </summary>
public sealed class ChainMapUnpackingAccountingScenarioTests
{
    [Fact]
    public async Task UnpackedChainMapRespectsTransientBudget()
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
            d = {**cm}
            return len(d)
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
    public async Task MeasuredChainMapRespectsTransientBudget()
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
            return len(cm)
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
    public async Task UnpackedChainMapBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import collections
            cm = collections.ChainMap({0: 10, 1: 11}, {1: 21, 2: 22})
            d = {**cm}
            return [len(d), d[0], d[1], d[2], len(cm)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(3), new BigInteger(10), new BigInteger(11), new BigInteger(22), new BigInteger(3) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
