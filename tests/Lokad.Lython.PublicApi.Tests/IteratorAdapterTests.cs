using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IteratorAdapterTests
{
    public sealed record Case(string Name, string Source, string Output);
    private static IEnumerable<Case> Read(string file) => JsonSerializer.Deserialize<Case[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","IteratorAdapters",file)))!;
    private static bool Preparation(Case item) => item.Name.StartsWith("map-") || item.Name.StartsWith("chain-") || item.Name.StartsWith("empty-unused-callback-map(");
    public static IEnumerable<object[]> PreparationCases() => Read("cases.json").Where(Preparation).Select(c => new object[] { c.Name,c });
    public static IEnumerable<object[]> OperationCases() => Read("cases.json").Where(c => !Preparation(c)).Select(c => new object[] { c.Name,c });
    public static IEnumerable<object[]> HostedCases() => Read("hosted.json").Select(c => new object[] { c.Name,c });

    [Theory, MemberData(nameof(PreparationCases))]
    public Task PreparationMatchesPython(string name, Case item) => Compare(name,item);
    [Theory, MemberData(nameof(OperationCases))]
    public Task OperationsMatchPython(string name, Case item) => Compare(name,item);
    private static async Task Compare(string name, Case item)
    {
        Assert.Equal(name,item.Name);
        using var script = new LythonEngine().Compile(item.Source);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(4*1024*1024);
        foreach (var asynchronous in new[] { false,true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget=budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget=budget }))
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

    [Theory]
    [InlineData("map(lambda *args:0,*sources)")]
    [InlineData("itertools.chain(*sources)")]
    public async Task ConstructorAdmissionPrecedesSourceEffects(string expression)
    {
        using var script = new LythonEngine().Compile("import itertools,time\nclass Source:\n def __iter__(self):\n  print('source effect')\n  return iter([1])\nsources=[Source()]*1024\ntime.time()\ntry:\n value="+expression+"\nexcept MemoryError:\n pass\nprint('late')");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false,true })
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 10000);
            using (var result = asynchronous ? await script.RunAsync(host,new LythonRunOptions { MemoryBudget=budget })
                : script.Run(host,new LythonRunOptions { MemoryBudget=budget }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance,result.FailureAttribution?.Reason);
                Assert.DoesNotContain("source effect",result.StandardOutput);
                Assert.DoesNotContain("late",result.StandardOutput);
            }
            Assert.Equal(0,budget.Root.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("map(lambda x:x,Source())")]
    [InlineData("itertools.chain(Source())")]
    [InlineData("itertools.chain.from_iterable([Source()])")]
    public async Task SuspendedAcquisitionCancelsAndReturnsAllowance(string expression)
    {
        using var script = new LythonEngine().Compile("import itertools\nclass Source:\n def __iter__(self):\n  with open('pause') as file:n=int(file.read())\n  return iter([n])\nprint(list("+expression+"))");
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();host.SeedFile("/pause","1");
        var reached = host.PauseReadUntilCancellation("/pause");
        var budget = new LythonMemoryBudget(4*1024*1024);
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host,new LythonRunOptions { MemoryBudget=budget,CancellationToken=cancellation.Token });
        await reached.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        using (var result = await pending)
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled,result.FailureAttribution?.Reason);
        }
        Assert.Equal(0,budget.ReservedBytes);
        var next = new DelayedLythonHost();next.SeedFile("/pause","1");
        using (var result = await script.RunAsync(next,new LythonRunOptions { MemoryBudget=budget }))
        {
            Assert.True(result.Success,result.Failure?.Message);
            Assert.Equal("[1]\n",result.StandardOutput);
            Assert.True(next.CompletedAsynchronously>0);
        }
        Assert.Equal(0,budget.ReservedBytes);
    }
}
