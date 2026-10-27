using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ArgparseContractTests
{
    public static IEnumerable<object[]> HostedCases()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ArgparseContracts", "hosted.json"));
        foreach (var row in JsonSerializer.Deserialize<JsonElement[]>(text)!)
            yield return [row.GetProperty("name").GetString()!, row.GetProperty("source").GetString()!, row.GetProperty("output").GetString()!, row.GetProperty("error").GetString()!];
    }

    [Theory, MemberData(nameof(HostedCases))]
    public async Task HostProtocolsActuallySuspend(string name, string source, string output, string error)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var mode in new[] { "sync", "immediate", "delayed" })
        {
            var immediate = new MockLythonHost("/repo"); immediate.SeedFile("/repo/input.txt", "ok");
            var delayed = new DelayedLythonHost("/repo"); delayed.SeedFile("/repo/input.txt", "ok");
            using (var result = mode == "sync" ? script.Run(immediate, new LythonRunOptions { MemoryBudget = budget })
                       : await script.RunAsync(mode == "delayed" ? delayed : immediate, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(output, result.StandardOutput);
                Assert.Equal(error, result.StandardError);
                if (mode == "delayed") Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    public static IEnumerable<object[]> Cases()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ArgparseContracts", "cases.json"));
        foreach (var row in JsonSerializer.Deserialize<JsonElement[]>(text)!)
            yield return [row.GetProperty("name").GetString()!, row.GetProperty("source").GetString()!,
                row.GetProperty("success").GetBoolean(), row.GetProperty("output").GetString()!,
                row.GetProperty("error").GetString()!, row.GetProperty("failure").GetString()!];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ContractsMatchIndependentCpython(string name, string source, bool success, string output, string error, string? failure)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.Equal(success, result.Success);
                    Assert.Equal(output, result.StandardOutput);
                    Assert.Equal(error, result.StandardError);
                    Assert.Equal(failure, result.Failure?.ExceptionType);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task UnsupportedExtensionsFailExplicitlyBeforeChangingLayout()
    {
        using var script = new LythonEngine().Compile("""
            import argparse
            events=[]
            def formatter(*args,**kwargs):
                events.append('called')
                raise RuntimeError('factory')
            try:argparse.ArgumentParser(formatter_class=formatter)
            except NotImplementedError:print('formatter')
            p=argparse.ArgumentParser(add_help=False)
            a=p.add_argument('--x')
            for field,value in [('nargs',2),('option_strings',[])]:
                try:setattr(a,field,value)
                except NotImplementedError:print(field)
            print(events,a.nargs,p.parse_args(['--x','yes']).x)
            """);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("formatter\nnargs\noption_strings\n[] None yes\n", result.StandardOutput);
        }
    }
}
