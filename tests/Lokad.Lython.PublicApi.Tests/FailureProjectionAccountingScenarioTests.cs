using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG23: failure details share the projection budget with the output captures.
/// Oversized messages truncate with an explicit marker and keep their type;
/// when even the truncated minimum overruns, the type survives with empty
/// details instead of masking as ProjectionError.
/// </summary>
public sealed class FailureProjectionAccountingScenarioTests
{
    // Mirrors RuntimeFailureProjection.TruncatedMessageMarker (internal);
    // EndsWith fails loudly if the marker text ever changes.
    private const string TruncatedMarker = "...[truncated to fit the projection budget]";

    private const string RaiseHuge =
        """
        raise MemoryError("x" * 1000000)
        """;

    private const string PrintThenRaise =
        """
        print("y" * 2000)
        raise ValueError("x")
        """;

    [Fact]
    public async Task HugeFailureMessageStaysBounded()
    {
        var script = new LythonEngine().Compile(RaiseHuge);
        Assert.True(script.IsValid);
        // Calibration: pre-fix delivers the full 1MB message with projection
        // peak 0; post-fix truncates inside the 4KB budget and keeps the type.
        var options = new LythonRunOptions { MaxProjectionMemoryBytes = 4096 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.EndsWith(TruncatedMarker, sync.Failure?.Message);
        Assert.True(sync.Failure?.Message.Length < 1000000);
        Assert.True(sync.PeakProjectionMemoryBytes <= 4096);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.EndsWith(TruncatedMarker, asyncResult.Failure?.Message);
        Assert.True(asyncResult.Failure?.Message.Length < 1000000);
        Assert.True(asyncResult.PeakProjectionMemoryBytes <= 4096);
    }

    [Fact]
    public async Task CapturesExhaustedFailureKeepsType()
    {
        var script = new LythonEngine().Compile(PrintThenRaise);
        Assert.True(script.IsValid);
        // Calibration: the 2000-char capture leaves 64 of 4096 bytes, so the
        // one-frame array fits but even the marker-only message does not;
        // pre-fix delivers the full message, post-fix falls back to empty
        // details with the type and the captured output intact.
        var options = new LythonRunOptions { MaxProjectionMemoryBytes = 4096 };
        var expectedOutput = new string('y', 2000) + "\n";
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("ValueError", sync.Failure?.ExceptionType);
        Assert.Equal(string.Empty, sync.Failure?.Message);
        Assert.Empty(sync.Failure?.StackTrace ?? []);
        Assert.Equal(expectedOutput, sync.StandardOutput);
        Assert.True(sync.PeakProjectionMemoryBytes <= 4096);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("ValueError", asyncResult.Failure?.ExceptionType);
        Assert.Equal(string.Empty, asyncResult.Failure?.Message);
        Assert.Empty(asyncResult.Failure?.StackTrace ?? []);
        Assert.Equal(expectedOutput, asyncResult.StandardOutput);
        Assert.True(asyncResult.PeakProjectionMemoryBytes <= 4096);
    }

    [Fact]
    public async Task RoomyFailureMessageStaysExact()
    {
        var script = new LythonEngine().Compile(RaiseHuge);
        Assert.True(script.IsValid);
        var expected = new string('x', 1000000);
        var sync = script.Run(new MockLythonHost());
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.Equal(expected, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.Equal(expected, asyncResult.Failure?.Message);
    }
}
