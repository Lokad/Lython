using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG20: wide graphs copy under the same budget discipline as deep ones.
/// Twenty thousand distinct one-element lists fail a 4MiB budget; the original
/// success scale stays in Stress with explicit capacity headroom.
/// Memo scratch, source/clone capacity and conservative numeric-box headroom
/// remain funded; the test does not assume identity-deduplicated accounting.
/// </summary>
public sealed class CopyWideDagAccountingScenarioTests
{
    private const string BuildWideGraph =
        """
        import copy
        v = [[i] for i in range(20000)]
        m = copy.deepcopy(v)
        return 0
        """;

    [Fact]
    public async Task WideGraphStaysCharged()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(BuildWideGraph);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(4194304) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public Task RoomyWideGraphRoundTrips() => VerifyWideGraph(128, 33554432);

    [Fact]
    [Trait("Category", "Stress")]
    public Task OriginalScaleRoomyWideGraphRoundTrips() => VerifyWideGraph(20000, 67108864);

    private static async Task VerifyWideGraph(int count, long allowance)
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            import copy
            v = [[i] for i in range(20000)]
            m = copy.deepcopy(v)
            return [len(m), m[19999][0], m is v]
            """.Replace("20000", count.ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("19999", (count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(allowance) };
        var expected = new List<object?> { new BigInteger(count), new BigInteger(count - 1), false };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}