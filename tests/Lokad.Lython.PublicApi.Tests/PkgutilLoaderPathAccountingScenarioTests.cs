using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: local-module loaders own their source-path string instead of
/// escaping it. Twenty thousand retained loaders must exceed a 6MB budget in
/// both modes; pre-fix they fit in ~4.6MB with the path invisible.
/// </summary>
public sealed class PkgutilLoaderPathAccountingScenarioTests
{
    private const long LoaderBudgetBytes = 6291456;

    private static MockLythonHost SeededHost()
    {
        var host = new MockLythonHost();
        host.SeedFile("/helper.py", "value = 1\n");
        return host;
    }

    private static LythonRunOptions LocalOptions(long budget)
    {
        return new LythonRunOptions
        {
            MaxExecutionMemoryBytes = budget,
            AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper" },
        };
    }

    [Fact]
    public async Task ManyRetainedLoadersStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import pkgutil
            objs = []
            i = 0
            while i < 20000:
                objs.append(pkgutil.find_loader("helper"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(SeededHost(), LocalOptions(LoaderBudgetBytes));
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= LoaderBudgetBytes);

        var asyncResult = await script.RunAsync(SeededHost(), LocalOptions(LoaderBudgetBytes));
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= LoaderBudgetBytes);
    }

    [Fact]
    public async Task LoaderPathsBehave()
    {
        var script = new LythonEngine().Compile("""
            import importlib.util
            import pkgutil
            loader = pkgutil.find_loader("helper")
            return [loader.name, loader.get_source(), importlib.util.find_spec("helper").origin]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "helper", "value = 1\n", "/helper.py" };
        var sync = script.Run(SeededHost(), LocalOptions(268435456));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(SeededHost(), LocalOptions(268435456));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}