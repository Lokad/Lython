using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DiscoveryProtocolTests
{
    // Independent CPython 3.13 goldens; no Python installation or runtime
    // implementation access is required to exercise the shipped boundary.
    public sealed record Case(string Name, string Source, string Output, Dictionary<string, string>? Files);
    private static IEnumerable<object[]> Read(string file) => JsonSerializer.Deserialize<Case[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Discovery", file)))!.Select(c => new object[] { c.Name, c });
    public static IEnumerable<object[]> Cases() => Read("cases.json");
    public static IEnumerable<object[]> HostedCases() => Read("hosted.json");

    [Theory, MemberData(nameof(Cases))]
    public async Task PureSemanticsMatchPython(string name, Case item)
    {
        Assert.Equal(name, item.Name);
        using var script = new LythonEngine().Compile(item.Source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(item.Output, result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory, MemberData(nameof(HostedCases))]
    public async Task HostedDiscoveryMatchesPython(string name, Case item)
    {
        Assert.Equal(name, item.Name);
        using var script = new LythonEngine().Compile(item.Source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        foreach (var mode in new[] { "sync", "async", "delayed" })
        {
            var immediate = new MockLythonHost("/repo");
            var delayed = new DelayedLythonHost("/repo");
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in item.Files!)
            {
                immediate.SeedFile("/repo/" + file.Key, file.Value);
                delayed.SeedFile("/repo/" + file.Key, file.Value);
                allowed.Add("/repo/" + file.Key);
                allowed.Add(file.Key);
                allowed.Add(file.Key.Replace("/__init__.py", "").Replace(".py", "").Replace('/', '.'));
            }
            var options = new LythonRunOptions { MemoryBudget = budget, SourcePath = "/repo/main.py", AllowedLocalModules = allowed };
            using (var result = mode == "sync" ? script.Run(immediate, options)
                : await script.RunAsync(mode == "delayed" ? delayed : immediate, options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(item.Output, result.StandardOutput);
                if (mode == "delayed") Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task ModuleInfoDefinitionsAndDefaultsAreExecutionOwned()
    {
        using var script = new LythonEngine().Compile("""
            import pkgutil, sys, importlib.util
            info = pkgutil.ModuleInfo([], [], [])
            assert isinstance(info, tuple)
            assert info._fields is pkgutil.ModuleInfo._fields
            assert not pkgutil.ModuleInfo._field_defaults
            pkgutil.ModuleInfo._field_defaults['name'] = info
            assert importlib.util.find_spec('sys') is sys.__spec__
            assert pkgutil.get_loader(sys) is sys.__loader__
            return 1
            """);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var budget = new LythonMemoryBudget(256 * 1024);
        foreach (var asynchronous in new[] { false, true, false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })) Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
