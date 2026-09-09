using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG22: host-provided argv payload is guest-retained through sys.argv, so its
/// strings and backing array charge the execution budget at construction.
/// </summary>
public sealed class ArgvAccountingScenarioTests
{
    [Fact]
    public async Task LargeArgvPayloadStaysCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import sys
            return len(sys.argv)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions
        {
            Args = Enumerable.Repeat(new string('x', 1024), 2000).ToList(),
            MaxExecutionMemoryBytes = 65536,
        };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task SmallArgvStillProjects()
    {
        var script = new LythonEngine().Compile(
            """
            import sys
            return sys.argv[1]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { Args = ["prog", "b"] };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("b", sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("b", asyncResult.ReturnValue);
    }
}