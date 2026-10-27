using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeCallbackProtocolTests
{
    public sealed record Case(string Name, string Source, string Output, Dictionary<string,string>? Files);
    private static IEnumerable<Case> Read(string file) => JsonSerializer.Deserialize<Case[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory,"Fixtures","NativeCallbacks",file)))!;
    public static IEnumerable<object[]> MutableCases() => Read("cases.json").Where(c => c.Name.StartsWith("list-", StringComparison.Ordinal)).Select(c => new object[] { c.Name, c });
    public static IEnumerable<object[]> ValueCases() => Read("cases.json").Where(c => !c.Name.StartsWith("list-", StringComparison.Ordinal)).Select(c => new object[] { c.Name, c });
    public static IEnumerable<object[]> HostedCases() => Read("hosted.json").Select(c => new object[] { c.Name, c });

    [Theory, MemberData(nameof(MutableCases))]
    public Task MutableCallbacksMatchPython(string name, Case item) => CheckPure(name,item);

    [Theory, MemberData(nameof(ValueCases))]
    public Task ValueProtocolsMatchPython(string name, Case item) => CheckPure(name,item);

    private static async Task CheckPure(string name, Case item)
    {
        Assert.Equal(name,item.Name);
        using var script = new LythonEngine().Compile(item.Source);
        Assert.True(script.IsValid,string.Join("; ",script.Diagnostics.Select(d=>d.Message)));
        var budget = new LythonMemoryBudget(2*1024*1024);
        foreach(var asynchronous in new[] {false,true})
        {
            using(var result = asynchronous ? await script.RunAsync(new MockLythonHost(),new LythonRunOptions { MemoryBudget=budget })
                : script.Run(new MockLythonHost(),new LythonRunOptions { MemoryBudget=budget }))
            {
                Assert.True(result.Success,result.Failure?.Message);
                Assert.Equal(item.Output,result.StandardOutput);
            }
            Assert.Equal(0,budget.ReservedBytes);
        }
    }

    [Theory, MemberData(nameof(HostedCases))]
    public async Task SuspendedCallbacksMatchPython(string name, Case item)
    {
        Assert.Equal(name,item.Name);
        using var script = new LythonEngine().Compile(item.Source);
        Assert.True(script.IsValid,string.Join("; ",script.Diagnostics.Select(d=>d.Message)));
        var budget = new LythonMemoryBudget(2*1024*1024);
        foreach(var mode in new[] {"sync","async","delayed"})
        {
            var immediate = new MockLythonHost("/repo");var delayed = new DelayedLythonHost("/repo");
            foreach(var file in item.Files!) { immediate.SeedFile("/repo/"+file.Key,file.Value);delayed.SeedFile("/repo/"+file.Key,file.Value); }
            var options = new LythonRunOptions { MemoryBudget=budget };
            using(var result = mode=="sync" ? script.Run(immediate,options)
                : await script.RunAsync(mode=="delayed"?delayed:immediate,options))
            {
                Assert.True(result.Success,result.Failure?.Message);
                Assert.Equal(item.Output,result.StandardOutput);
                if(mode=="delayed") Assert.True(delayed.CompletedAsynchronously>0);
            }
            Assert.Equal(0,budget.ReservedBytes);
        }
    }
}
