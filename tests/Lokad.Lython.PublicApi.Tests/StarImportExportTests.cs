using Lokad.Lython.Tests.Harness;
namespace Lokad.Lython.PublicApi.Tests;
public sealed class StarImportExportTests
{
    [Fact]
    public async Task SuspendedExportIndexCancelsAndReleasesItsOwner()
    {
        var host=new DelayedLythonHost();
        host.SeedFile("/selected.py","""
            class Names:
                def __getitem__(self,index):
                    with open('/names') as f:return f.read()
            __all__=Names()
            """);
        host.SeedFile("/names","wanted");
        var entered=host.PauseReadUntilCancellation("/names");
        using var script=new LythonEngine().Compile("from selected import *\nprint('continued')");
        var budget=new LythonMemoryBudget(262144);
        using var cancellation=new CancellationTokenSource();
        var pending=script.RunAsync(host,new LythonRunOptions{MemoryBudget=budget,
            AllowedLocalModules=new HashSet<string>{"selected"},CancellationToken=cancellation.Token});
        try
        {
            Assert.Same(entered,await Task.WhenAny(entered,pending).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes>0);
            cancellation.Cancel();
            using var result=await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(LythonFailureReason.Cancelled,result.FailureAttribution?.Reason);
            Assert.Equal("",result.StandardOutput);
        }
        finally
        {
            cancellation.Cancel();
            using var drained=await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(0,budget.ReservedBytes);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitExportsPreserveOrderPrivateNamesAndBindingsBeforeFailure(bool asynchronous)
    {
        ILythonHost host;
        const string module="__all__=['_wanted','missing']\n_wanted=7\nunwanted=2";
        if(asynchronous){var h=new DelayedLythonHost();h.SeedFile("/selected.py",module);host=h;}
        else{var h=new MockLythonHost();h.SeedFile("/selected.py",module);host=h;}
        using var script=new LythonEngine().Compile("""
            try:from selected import *
            except AttributeError:print('missing')
            print(_wanted,'unwanted' in globals())
            """);
        var options=new LythonRunOptions{AllowedLocalModules=new HashSet<string>{"selected"}};
        using var result=asynchronous ? await script.RunAsync(host,options) : script.Run(host,options);
        Assert.True(result.Success,result.Failure?.Message);
        Assert.Equal("missing\n7 False\n",result.StandardOutput);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExportsUseIndexedSequenceProtocolRatherThanIteration(bool asynchronous)
    {
        ILythonHost host;
        const string module="""
            class Names:
                def __iter__(self):raise AssertionError('iteration')
                def __getitem__(self,index):
                    with open('/names') as f:names=f.read().split()
                    return names[index]
            __all__=Names()
            _wanted=7
            """;
        if(asynchronous){var h=new DelayedLythonHost();h.SeedFile("/selected.py",module);h.SeedFile("/names","_wanted");host=h;}
        else{var h=new MockLythonHost();h.SeedFile("/selected.py",module);h.SeedFile("/names","_wanted");host=h;}
        using var script=new LythonEngine().Compile("from selected import *\nprint(_wanted)");
        var options=new LythonRunOptions{AllowedLocalModules=new HashSet<string>{"selected"}};
        using var result=asynchronous ? await script.RunAsync(host,options) : script.Run(host,options);
        Assert.True(result.Success,result.Failure?.Message);
        Assert.Equal("7\n",result.StandardOutput);
        if(host is DelayedLythonHost delayed)Assert.True(delayed.CompletedAsynchronously>0);
    }
}
