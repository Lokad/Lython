using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG05: slice assignment drains the right-hand side into an array that
/// duplicates the retained content beside it while SetSlice runs. One empty
/// list plus a big range isolates the drain copy (small-int elements stay
/// free, both modes share the funnel).
/// </summary>
public sealed class SliceAssignAccountingScenarioTests
{
    private const string BuildSliceAssign =
        """
        lst = []
        lst[0:0] = range(30000)
        return len(lst)
        """;

    [Fact]
    public async Task SliceAssignDrainStaysCharged()
    {
        var script = new LythonEngine().Compile(BuildSliceAssign);
        Assert.True(script.IsValid);
        // Calibration: pre-fix peaks are exactly 480320 in both modes (content
        // only), so this fits; the drain-copy backstop (+480000) trips post-fix.
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 700000 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task RoomySliceAssignSucceeds()
    {
        var script = new LythonEngine().Compile(BuildSliceAssign);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 4194304 };
        var expected = new BigInteger(30000);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SliceAssignBehaviorStaysCompatible()
    {
        var script = new LythonEngine().Compile(
            """
            lst = [1, 2, 3, 4]
            lst[1:3] = [20, 30, 40]
            a = list(lst)
            lst[::2] = [7, 8, 9]
            return [a, list(lst)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new List<object?> { new BigInteger(1), new BigInteger(20), new BigInteger(30), new BigInteger(40), new BigInteger(4) },
            new List<object?> { new BigInteger(7), new BigInteger(20), new BigInteger(8), new BigInteger(40), new BigInteger(9) },
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
