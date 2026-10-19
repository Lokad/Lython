using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GovernedImportAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalCompilationUsesTheSharedAllowanceBeforeModuleEffects(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        var host = new MockLythonHost();
        host.SeedFile("/marker", "unchanged");
        host.SeedFile("/helper.py", string.Concat(Enumerable.Repeat("pass\n", 20000)) +
            "with open('/marker', 'w') as f:\n f.write('changed')\nvalue = 3\n");
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import helper\nreturn helper.value\n", budget);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { AllowedLocalModules = new HashSet<string> { "helper" } };
        using var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.Equal("unchanged", host.ReadText("/marker"));
        result.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
