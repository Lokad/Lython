using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG05: starmap per-element argument arrays scale with the element, not the
/// output: one 24B slot per item, live beside the drained element while the
/// call runs. A single huge tuple isolates the copy from result retention.
/// </summary>
public sealed class StarmapArgsAccountingScenarioTests
{
    private const string BuildHugeCall =
        """
        import itertools
        big = list(range(30000))
        it = itertools.starmap(lambda *a: 0, [big])
        return next(it)
        """;

    [Fact]
    public async Task HugeStarmapCallStaysCharged()
    {
        var script = new LythonEngine().Compile(BuildHugeCall);
        Assert.True(script.IsValid);
        // Calibration: pre-fix peaks are 1661728 (sync) and 2009024 (async),
        // so both fit; the 24B-per-arg backstop (+720000) trips both post-fix.
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 2200000 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task RoomyStarmapCallSucceeds()
    {
        var script = new LythonEngine().Compile(BuildHugeCall);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 8388608 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(0), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(0), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task StarmapBehaviorStaysCompatible()
    {
        var script = new LythonEngine().Compile(
            """
            import itertools
            r = list(itertools.starmap(lambda a, b: a + b, [(1, 2), (3, 4)]))
            e = list(itertools.starmap(lambda *a: len(a), [(), (1,)]))
            return [r, e]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new List<object?> { new BigInteger(3), new BigInteger(7) },
            new List<object?> { new BigInteger(0), new BigInteger(1) },
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
