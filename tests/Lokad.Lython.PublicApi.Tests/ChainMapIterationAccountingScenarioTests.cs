using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: generic for-loop/list()/any() iteration builds the same merged list
/// as the key views. Two thousand shared layers hold ten unique keys, so the
/// merge scratch dwarfs every retained value.
/// </summary>
public sealed class ChainMapIterationAccountingScenarioTests
{
    private const string BuildSharedMaps =
        """
        import collections
        m = {0: 0, 1: 1, 2: 2, 3: 3, 4: 4, 5: 5, 6: 6, 7: 7, 8: 8, 9: 9}
        cm = collections.ChainMap(*([m] * 2000))
        """;

    [Fact]
    public async Task ListedChainMapAdmitsItsInputAndRetainedLayerStorage()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(BuildSharedMaps + "\nreturn len(list(cm))\n");
        Assert.True(script.IsValid);
        // Input, argument and retained layer capacity share the allowance;
        // this intentionally does not claim to isolate the merge phase.
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(32768) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task RoomyListedChainMapSucceeds()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(BuildSharedMaps + "\nreturn len(list(cm))\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(4194304) };
        var expected = new BigInteger(10);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ChainMapIterationBehaviorsStillProject()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            import collections
            cm = collections.ChainMap({0: 10, 1: 11}, {1: 21, 2: 22})
            total = 0
            for k in cm:
                total = total + k
            return [total, any(cm), len([x for x in cm])]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(3), true, new BigInteger(3) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
