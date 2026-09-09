using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG23: deeply nested return values surface a ProjectionError instead of
/// exhausting the host process stack.
/// </summary>
public sealed class ProjectionGraphScenarioTests
{
    [Fact]
    public async Task DeepReturnValueFailsProjection()
    {
        var script = new LythonEngine().Compile(
            """
            v = 1
            i = 0
            while i < 600:
                v = [v]
                i = i + 1
            return v
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.False(sync.Success);
        Assert.Equal("ProjectionError", sync.Failure?.ExceptionType);
        Assert.Contains("maximum projection depth", sync.Failure?.Message, StringComparison.Ordinal);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.False(asyncResult.Success);
        Assert.Equal("ProjectionError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("maximum projection depth", asyncResult.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShallowReturnValueStillProjects()
    {
        var script = new LythonEngine().Compile(
            """
            return [1, {"name": "lokad"}]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var list = Assert.IsType<List<object?>>(sync.ReturnValue);
        Assert.Equal(2, list.Count);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(2, Assert.IsType<List<object?>>(asyncResult.ReturnValue).Count);
    }
}