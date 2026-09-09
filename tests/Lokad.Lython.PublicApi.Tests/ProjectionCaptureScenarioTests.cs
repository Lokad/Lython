using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG23: captured standard output shares the projection budget with the return
/// value, so large output cannot escape projection accounting.
/// </summary>
public sealed class ProjectionCaptureScenarioTests
{
    [Fact]
    public async Task LargeStandardOutputCountsAgainstProjectionBudget()
    {
        var script = new LythonEngine().Compile(
            """
            i = 0
            while i < 2000:
                print("0123456789")
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxProjectionMemoryBytes = 4096 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("ProjectionError", sync.Failure?.ExceptionType);
        Assert.Contains("projection memory budget exceeded", sync.Failure?.Message, StringComparison.Ordinal);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("ProjectionError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("projection memory budget exceeded", asyncResult.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SmallOutputAndReturnStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            print("hi")
            return [1, 2]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("hi\n", sync.StandardOutput);
        Assert.Equal(2, Assert.IsType<List<object?>>(sync.ReturnValue).Count);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("hi\n", asyncResult.StandardOutput);
        Assert.Equal(2, Assert.IsType<List<object?>>(asyncResult.ReturnValue).Count);
    }
}