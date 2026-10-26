using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FunctionWrapperProtocolTests
{
    public sealed record Case(string Name,string Source,string Output);
    private static IEnumerable<Case> Read(string file) => JsonSerializer.Deserialize<Case[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","FunctionWrappers",file)))!;
    public static IEnumerable<object[]> Cases() => Read("cases.json").Select(c=>new object[]{c.Name,c});
    public static IEnumerable<object[]> HostedCases() => Read("hosted.json").Select(c=>new object[]{c.Name,c});
    [Theory,MemberData(nameof(Cases))]
    public async Task SemanticsMatchIndependentPythonGoldens(string name,Case item)
    {
        Assert.Equal(name,item.Name);
        using var script=new LythonEngine().Compile(item.Source);
        Assert.True(script.IsValid);
        var budget=new LythonMemoryBudget(4*1024*1024);
        foreach(var asynchronous in new[]{false,true})
        {
            using(var result=asynchronous?await script.RunAsync(new MockLythonHost(),new LythonRunOptions{MemoryBudget=budget})
                :script.Run(new MockLythonHost(),new LythonRunOptions{MemoryBudget=budget}))
            {
                Assert.True(result.Success,result.Failure?.Message);
                Assert.Equal(item.Output,result.StandardOutput);
            }
            Assert.Equal(0,budget.ReservedBytes);
        }
    }
    [Theory, MemberData(nameof(HostedCases))]
    public async Task HostedCallbacksMatchPython(string name, Case item)
    {
        Assert.Equal(name,item.Name);
        using var script = new LythonEngine().Compile(item.Source);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(4*1024*1024);
        foreach (var mode in new[] { "sync","async","delayed" })
        {
            var immediate = new MockLythonHost(); immediate.SeedFile("/value","1");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/value","1");
            using (var result = mode == "sync" ? script.Run(immediate,new LythonRunOptions { MemoryBudget=budget })
                : await script.RunAsync(mode=="delayed" ? delayed : immediate,new LythonRunOptions { MemoryBudget=budget }))
            {
                Assert.True(result.Success,result.Failure?.Message);
                Assert.Equal(item.Output,result.StandardOutput);
                if (mode=="delayed") Assert.True(delayed.CompletedAsynchronously>0);
            }
            Assert.Equal(0,budget.ReservedBytes);
        }
    }

}
