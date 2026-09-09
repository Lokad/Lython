using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG22: each newly registered module retains a registry slot plus handle
/// wrapper, name strings, and exported-table infrastructure for the run.
/// Hundreds of tiny local modules isolate registry growth from module
/// contents; re-imports hit the registry and pay nothing.
/// </summary>
public sealed class ImportRegistryAccountingScenarioTests
{
    private const int ModuleCount = 500;

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
    public async Task ManyRetainedImportsStayCharged()
    {
        var host = SeededHost(out var allowed, out var source);
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        // Calibration: pre-fix peak is 69000, so this fits; the 512B-per-module
        // backstop (+256000) pushes the post-fix peak past 300000.
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 130000, AllowedLocalModules = allowed };
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var host2 = SeededHost(out var allowed2, out _);
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = 130000, AllowedLocalModules = allowed2 });
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task RoomyManyImportsSucceed()
    {
        var host = SeededHost(out var allowed, out var source);
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576, AllowedLocalModules = allowed };
        var expected = new BigInteger(1);
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = SeededHost(out var allowed2, out _);
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = 1048576, AllowedLocalModules = allowed2 });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ReimportKeepsIdentity()
    {
        var host = new MockLythonHost();
        host.SeedFile("/helper.py", "value = 1\n");
        var script = new LythonEngine().Compile(
            """
            import helper
            import helper as again
            helper.value = 2
            return [helper.value, again.value, helper is again]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(2), new BigInteger(2), true };
        var options = new LythonRunOptions { AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper" } };
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedFile("/helper.py", "value = 1\n");
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper" } });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
