using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG22: normalizing host globals duplicates sequence buffers beside the
/// retained copy: one 16B slot per item rides a transient reservation.
/// A single large host list isolates the copy (host longs stay free).
/// </summary>
public sealed class GlobalsNormalizationAccountingScenarioTests
{
    private static Dictionary<string, object?> BigGlobals(int count)
    {
        var big = new List<object?>();
        for (var i = 0; i < count; i++)
        {
            big.Add((object)(long)i);
        }

        return new Dictionary<string, object?> { ["big"] = big };
    }

    [Fact]
    public async Task BigHostListStaysCharged()
    {
        var script = new LythonEngine().Compile("return len(big)\n");
        Assert.True(script.IsValid);
        // Calibration: pre-fix peak is exactly 8000064 (content only), so this
        // fits; the 16B-per-item drain copy (+8000000) trips post-fix.
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 12000000, Globals = BigGlobals(500000) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task RoomyHostGlobalsSucceed()
    {
        var script = new LythonEngine().Compile("return len(big)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 33554432, Globals = BigGlobals(500000) };
        var expected = new BigInteger(500000);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task MixedHostGlobalsNormalize()
    {
        var globals = new Dictionary<string, object?>
        {
            ["tup"] = new object?[] { (object)(long)1, (object)"two" },
            ["nested"] = new List<object?> { new List<object?> { (object)(long)3 } },
            ["name"] = "hello",
        };
        var script = new LythonEngine().Compile("return [tup[0], tup[1], nested[0][0], name]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { (object)(long)1, "two", (object)(long)3, "hello" };
        var sync = script.Run(new MockLythonHost(), new LythonRunOptions { Globals = globals });
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), new LythonRunOptions { Globals = globals });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
