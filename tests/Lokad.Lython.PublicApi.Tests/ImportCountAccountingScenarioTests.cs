using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG22: the imported-module registry observes the shared collection count,
/// so the aggregate number of registered modules honors MaxCollectionSize
/// instead of growing without bound.
/// </summary>
public sealed class ImportCountAccountingScenarioTests
{
    private const int ModuleCount = 20;

    private static MockLythonHost SeededHost(out HashSet<string> allowed, out string source)
    {
        var host = new MockLythonHost();
        allowed = new HashSet<string>(StringComparer.Ordinal);
        source = "";
        for (var i = 0; i < ModuleCount; i++)
        {
            host.SeedFile("/m" + i + ".py", "value = 1\n");
            allowed.Add("m" + i);
            source += "import m" + i + "\n";
        }

        source += "return m0.value\n";
        return host;
    }

    [Fact]
    public async Task ManyImportsRespectCollectionCount()
    {
        var host = SeededHost(out var allowed, out var source);
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxCollectionSize = 10, AllowedLocalModules = allowed };
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
        Assert.Contains("maximum collection size exceeded", sync.Failure?.Message, StringComparison.Ordinal);

        var host2 = SeededHost(out var allowed2, out _);
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxCollectionSize = 10, AllowedLocalModules = allowed2 });
        Assert.False(asyncResult.Success);
        Assert.Equal("RuntimeError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("maximum collection size exceeded", asyncResult.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RoomyManyImportsSucceed()
    {
        var host = SeededHost(out var allowed, out var source);
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var expected = new BigInteger(1);
        var sync = script.Run(host, new LythonRunOptions { AllowedLocalModules = allowed });
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = SeededHost(out var allowed2, out _);
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { AllowedLocalModules = allowed2 });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
