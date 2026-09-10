using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG22: a local module retains one dictionary slot per exported variable for
/// the run, and later attribute additions grow the same table, so both paths
/// own their slots while keys and values stay aliased to existing owners.
/// </summary>
public sealed class ImportExportedEntryAccountingScenarioTests
{
    private const int GlobalCount = 20000;
    private const int AddCount = 1500;

    // 20k entries own 640000B on top of a ~210000B import; the 1500 later
    // additions own 48000B on top of a sub-kilobyte import.
    private const long GlobalsBudgetBytes = 400000;
    private const long AddsBudgetBytes = 32000;

    private static MockLythonHost SeededBigHost(out HashSet<string> allowed)
    {
        var host = new MockLythonHost();
        var content = new StringBuilder();
        for (var i = 0; i < GlobalCount; i++)
        {
            content.Append("v").Append(i).Append(" = 1\n");
        }

        host.SeedFile("/big.py", content.ToString());
        allowed = new HashSet<string>(StringComparer.Ordinal) { "big" };
        return host;
    }

    private static string AddStatements()
    {
        var source = new StringBuilder("import helper\n");
        for (var i = 0; i < AddCount; i++)
        {
            source.Append("helper.a").Append(i).Append(" = 1\n");
        }

        return source.Append("return helper.a1499\n").ToString();
    }

    [Fact]
    public async Task ManyRetainedGlobalsStayCharged()
    {
        var host = SeededBigHost(out var allowed);
        var script = new LythonEngine().Compile("import big\nreturn big.v19999\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = GlobalsBudgetBytes, AllowedLocalModules = allowed };
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= GlobalsBudgetBytes);

        var host2 = SeededBigHost(out var allowed2);
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = GlobalsBudgetBytes, AllowedLocalModules = allowed2 });
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= GlobalsBudgetBytes);
    }

    [Fact]
    public async Task RoomyManyGlobalsSucceed()
    {
        var host = SeededBigHost(out var allowed);
        var script = new LythonEngine().Compile("import big\nreturn big.v19999\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576, AllowedLocalModules = allowed };
        var expected = new BigInteger(1);
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = SeededBigHost(out var allowed2);
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = 1048576, AllowedLocalModules = allowed2 });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ManyModuleAttributeAddsStayCharged()
    {
        var source = AddStatements();
        var host = new MockLythonHost();
        host.SeedFile("/helper.py", "value = 1\n");
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "helper" };
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = AddsBudgetBytes, AllowedLocalModules = allowed };
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= AddsBudgetBytes);

        var host2 = new MockLythonHost();
        host2.SeedFile("/helper.py", "value = 1\n");
        var allowed2 = new HashSet<string>(StringComparer.Ordinal) { "helper" };
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { MaxExecutionMemoryBytes = AddsBudgetBytes, AllowedLocalModules = allowed2 });
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= AddsBudgetBytes);
    }

    [Fact]
    public async Task FewModuleAttributeAddsBehave()
    {
        var script = new LythonEngine().Compile("""
            import helper
            helper.a1 = 1
            helper.a2 = 2
            helper.a1 = 3
            return [helper.a1, helper.a2, helper.value]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(3), new BigInteger(2), new BigInteger(1) };
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "helper" };
        var host = new MockLythonHost();
        host.SeedFile("/helper.py", "value = 1\n");
        var sync = script.Run(host, new LythonRunOptions { AllowedLocalModules = allowed });
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var allowed2 = new HashSet<string>(StringComparer.Ordinal) { "helper" };
        var host2 = new MockLythonHost();
        host2.SeedFile("/helper.py", "value = 1\n");
        var asyncResult = await script.RunAsync(host2, new LythonRunOptions { AllowedLocalModules = allowed2 });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}