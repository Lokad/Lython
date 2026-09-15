using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// I01: a denied projection reservation must not inflate the reported peak:
// PeakProjectionMemoryBytes reflects accepted charges only, whether the
// overrun comes from the return value or follows captured output.
public sealed class ProjectionPeakAlignmentScenarioTests
{
    [Fact]
    public async Task ReturnValueOverrunKeepsPeakAtAcceptedCharges()
    {
        var script = new LythonEngine().Compile("return \"x\" * 1000\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxProjectionMemoryBytes = 100 };

        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("ProjectionError", sync.Failure?.ExceptionType);
        Assert.Equal(0, sync.PeakProjectionMemoryBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("ProjectionError", asyncResult.Failure?.ExceptionType);
        Assert.Equal(0, asyncResult.PeakProjectionMemoryBytes);
    }

    [Fact]
    public async Task CapturesThenOverrunKeepsCapturePeak()
    {
        var script = new LythonEngine().Compile(
            """
            print("y" * 100)
            return "x" * 10000
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxProjectionMemoryBytes = 4096 };
        var expectedOutput = new string('y', 100) + "\n";

        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("ProjectionError", sync.Failure?.ExceptionType);
        Assert.Equal(expectedOutput, sync.StandardOutput);
        Assert.Equal(32L + (2L * 101), sync.PeakProjectionMemoryBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("ProjectionError", asyncResult.Failure?.ExceptionType);
        Assert.Equal(expectedOutput, asyncResult.StandardOutput);
        Assert.Equal(32L + (2L * 101), asyncResult.PeakProjectionMemoryBytes);
    }

    [Fact]
    public async Task TinyBudgetStillProjectsFreeValues()
    {
        var script = new LythonEngine().Compile("return 0\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxProjectionMemoryBytes = 0 };

        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(0), sync.ReturnValue);
        Assert.Equal(0, sync.PeakProjectionMemoryBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(0), asyncResult.ReturnValue);
        Assert.Equal(0, asyncResult.PeakProjectionMemoryBytes);
    }

    [Fact]
    public async Task TinyBudgetDeniesFirstCharge()
    {
        var script = new LythonEngine().Compile("return \"x\"\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxProjectionMemoryBytes = 0 };

        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("ProjectionError", sync.Failure?.ExceptionType);
        Assert.Equal(0, sync.PeakProjectionMemoryBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("ProjectionError", asyncResult.Failure?.ExceptionType);
        Assert.Equal(0, asyncResult.PeakProjectionMemoryBytes);
    }
}