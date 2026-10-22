using Lokad.Lython.Tests.Harness;
namespace Lokad.Lython.PublicApi.Tests;
public sealed class CopyProtocolExecutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacementUsesClassProtocolsBeforeNativeDefaults(bool asynchronous)
    {
        using var script=new LythonEngine().Compile("""
            from copy import replace
            from dataclasses import dataclass
            class C:
                def __replace__(self,**changes):return 1
            c=C()
            c.__replace__=lambda **changes:2
            print(replace(c))
            class Fake:
                def _replace(self,**changes):return 7
            class Empty:pass
            empty=Empty()
            empty.__replace__=lambda **changes:7
            for value in (Fake(),empty):
                try:replace(value)
                except TypeError:print('unsupported')
            @dataclass
            class Record:
                value:int
                def __replace__(self,**changes):return 7
            print(replace(Record(1),value=2))
            """);
        using var result=asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success,result.Failure?.Message);
        Assert.Equal("1\nunsupported\nunsupported\n7\n",result.StandardOutput);
    }

    private static string Source(string operation)=>"""
        import copy
        def read():
            with open('/copy') as f:return f.read()
        class Descriptor:
            def __get__(self,obj,owner):
                read()
                return lambda value,**kw:read()
        class C:
            def __copy__(self):return read()
            def __deepcopy__(self,memo):return read()
            def __replace__(self,**kw):return read()
        class D:
            __replace__=Descriptor()
        print(
        """+operation+")";

    [Theory]
    [InlineData("copy.copy(C())","value")]
    [InlineData("copy.deepcopy([C()])","['value']")]
    [InlineData("copy.replace(C(),x=1)","value")]
    [InlineData("copy.replace(D(),x=1)","value")]
    public async Task CopyCallbacksAwaitRealIoAndUnwindCancellation(string operation,string expected)
    {
        using var script=new LythonEngine().Compile(Source(operation));
        var budget=new LythonMemoryBudget(262144);
        foreach(var cancel in new[]{false,true})
        {
            var host=new DelayedLythonHost();host.SeedFile("/copy","value");
            var entered=cancel ? host.PauseReadUntilCancellation("/copy") : null;
            using var cancellation=new CancellationTokenSource();
            var pending=script.RunAsync(host,new LythonRunOptions{MemoryBudget=budget,CancellationToken=cancellation.Token});
            try
            {
                if(entered is not null)
                {
                    Assert.Same(entered,await Task.WhenAny(entered,pending).WaitAsync(TimeSpan.FromSeconds(5)));
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes>0);
                    cancellation.Cancel();
                }
                using var result=await pending.WaitAsync(TimeSpan.FromSeconds(5));
                if(cancel)Assert.Equal(LythonFailureReason.Cancelled,result.FailureAttribution?.Reason);
                else
                {
                    Assert.True(result.Success,result.Failure?.Message);
                    Assert.Equal(expected+"\n",result.StandardOutput);
                    Assert.True(host.CompletedAsynchronously>0);
                }
            }
            finally
            {
                cancellation.Cancel();
                using var drained=await pending.WaitAsync(TimeSpan.FromSeconds(5));
            }
            Assert.Equal(0,budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedCopyHookCannotResumeGuestWork(bool asynchronous)
    {
        using var script=new LythonEngine().Compile("""
            import copy
            class C:
                def __deepcopy__(self,memo):
                    try:value=[None]*1000000
                    except MemoryError:pass
                    print('continued')
                    return 7
            print(copy.deepcopy([C()]))
            """);
        var budget=new LythonMemoryBudget(262144);
        using(var result=asynchronous ? await script.RunAsync(new MockLythonHost(),new LythonRunOptions{MemoryBudget=budget})
            : script.Run(new MockLythonHost(),new LythonRunOptions{MemoryBudget=budget}))
        {
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance,result.FailureAttribution?.Reason);
            Assert.Equal("",result.StandardOutput);
        }
        Assert.Equal(0,budget.ReservedBytes);
    }
}
