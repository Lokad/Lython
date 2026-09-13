using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG24: a run that exceeds its execution budget reports the denied
// reservation size on the public result; funded runs report zero.
public sealed class MemoryDenialAttributionScenarioTests
{
    [Fact]
    public async Task DeniedReservationIsAttributed()
    {
        var script = new LythonEngine().Compile("return \"x\" * 200000\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 512 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.Equal(200128, sync.DeniedReservationBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.Equal(200128, asyncResult.DeniedReservationBytes);
    }

    [Fact]
    public async Task FundedRunReportsZeroDenial()
    {
        var script = new LythonEngine().Compile("return \"x\" * 200\n");
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(0, sync.DeniedReservationBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(0, asyncResult.DeniedReservationBytes);
    }
}
