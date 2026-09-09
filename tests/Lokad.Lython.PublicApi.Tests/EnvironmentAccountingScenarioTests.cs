using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG22: the host-provided environment copy is guest-visible through
/// os.environ, so its table charges the execution budget at construction.
/// Keys and values stay host-owned references.
/// </summary>
public sealed class EnvironmentAccountingScenarioTests
{
    [Fact]
    public async Task LargeEnvironmentTableStaysCharged()
    {
        var script = new LythonEngine().Compile("import os\nreturn 0\n");
        Assert.True(script.IsValid);
        var environment = Enumerable.Range(0, 20000).ToDictionary(static i => "K" + i, static i => "v");
        var options = new LythonRunOptions
        {
            Environment = environment,
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
    public async Task SmallEnvironmentStillProjects()
    {
        var script = new LythonEngine().Compile("import os\nreturn os.getenv(\"K\")\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions
        {
            Environment = new Dictionary<string, string> { ["K"] = "V" },
        };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("V", sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("V", asyncResult.ReturnValue);
    }
}