using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG22: the shared normalization memo outlives every container it records:
/// one 64B table slot per distinct entry rides a transient reservation for the
/// run. A wide DAG of small host lists isolates memo growth from content.
/// </summary>
public sealed class GlobalsMemoAccountingScenarioTests
{
    private static Dictionary<string, object?> DagGlobals(int count)
    {
        var big = new List<object?>();
        for (var i = 0; i < count; i++)
        {
            big.Add(new List<object?> { (object)(long)i });
        }

        return new Dictionary<string, object?> { ["big"] = big };
    }

    [Fact]
    public async Task WideDagMemoStaysCharged()
    {
        var script = new LythonEngine().Compile("return len(big)\n");
        Assert.True(script.IsValid);
        // Calibration: pre-fix peak is 22400064 (content only), so this fits;
        // the 64B-per-entry backstop (+6400064) pushes post-fix past 28.8MB.
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 26000000, Globals = DagGlobals(100000) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task RoomyDagGlobalsSucceed()
    {
        var script = new LythonEngine().Compile("return [len(big), big[0][0], big[99999][0]]\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 67108864, Globals = DagGlobals(100000) };
        var expected = new List<object?> { new BigInteger(100000), (object)(long)0, (object)(long)99999 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
