using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG20: user-supplied memo dicts back the same CLR mirror map as internal
/// memos, so they hold the same transient scratch per entry (the user dict
/// itself keeps its own durable ownership). Without it, identical work peaks
/// lower through a user memo than through the internal one.
/// </summary>
public sealed class CopyExternalMemoAccountingScenarioTests
{
    private const string BuildExternalMemoCopy =
        """
        import copy
        src = [[i] for i in range(20000)]
        memo = {}
        dst = copy.deepcopy(src, memo)
        return [len(dst), len(memo) > 19000]
        """;

    [Fact]
    public async Task ExternalMemoMirrorStaysCharged()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(BuildExternalMemoCopy);
        Assert.True(script.IsValid);
        // Calibration: pre-fix peak is 9534720, so this fits; the 128B-per-entry
        // mirror backstop (+2560000) pushes the post-fix peak past 12MB.
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(11000000) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public Task RoomyExternalMemoCopySucceeds() => VerifyExternalMemoCopy(128, 33554432);

    [Fact]
    [Trait("Category", "Stress")]
    public Task OriginalScaleRoomyExternalMemoCopySucceeds() => VerifyExternalMemoCopy(20000, 67108864);

    private static async Task VerifyExternalMemoCopy(int count, long allowance)
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(BuildExternalMemoCopy.Replace("20000", count.ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("19000", (count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(allowance) };
        var expected = new List<object?> { new BigInteger(count), true };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ExternalMemoSharesCopies()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            import copy
            shared = [1]
            src = [shared, shared]
            memo = {}
            dst = copy.deepcopy(src, memo)
            return [dst[0] is dst[1], dst[0] == [1], len(memo) > 0]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
