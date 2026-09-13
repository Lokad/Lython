using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG11: functions defined in an imported module retain that module's scope,
// which is per-module retained state rather than the shared run root, so the
// closure-retention walk owns it like any other defining context (builtin
// aliases stay owned by the run). Pre-fix, a 40000-member module retained
// through its single def peaks near 1.9MB; owning the scope trips the budget.
public sealed class ImportedModuleScopeAccountingScenarioTests
{
    private static MockLythonHost SeededHost(out HashSet<string> allowed)
    {
        var host = new MockLythonHost();
        var body = "";
        for (var k = 0; k < 40000; k++) { body += "v" + k + " = " + k + "\n"; }
        body += "def f():\n    return 1\n";
        host.SeedFile("/big.py", body);
        allowed = new HashSet<string>(StringComparer.Ordinal) { "big" };
        return host;
    }

    [Fact]
    public async Task ManyModuleMembersRetainedViaDefTrip()
    {
        var script = new LythonEngine().Compile("import big\nreturn 0\n");
        Assert.True(script.IsValid);
        var host = SeededHost(out var allowed);
        var sync = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = 2621440, AllowedLocalModules = allowed });
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncHost = SeededHost(out var allowed2);
        var asyncResult = await script.RunAsync(asyncHost, new LythonRunOptions { MaxExecutionMemoryBytes = 2621440, AllowedLocalModules = allowed2 });
        Assert.False(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ImportedDefStillWorks()
    {
        var host = new MockLythonHost();
        host.SeedFile("/helper.py", "def f(a, b=2):\n    return a + b\n");
        var script = new LythonEngine().Compile("import helper\nreturn helper.f(1)\n");
        Assert.True(script.IsValid);
        var expected = new BigInteger(3);
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/helper.py", "def f(a, b=2):\n    return a + b\n");
        var sync = script.Run(syncHost, new LythonRunOptions { AllowedLocalModules = new HashSet<string> { "helper" } });
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncHost = new MockLythonHost();
        asyncHost.SeedFile("/helper.py", "def f(a, b=2):\n    return a + b\n");
        var asyncResult = await script.RunAsync(asyncHost, new LythonRunOptions { AllowedLocalModules = new HashSet<string> { "helper" } });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
