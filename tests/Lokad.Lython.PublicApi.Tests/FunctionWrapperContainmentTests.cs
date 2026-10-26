using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FunctionWrapperContainmentTests
{
    public static IEnumerable<object[]> Kinds() => new[] { "dispatch","method","recursive","metadata-getter","metadata-names",
        "metadata-update","metadata-setter","typed","cached-property","partial-descriptor","method-descriptor","dispatch-class" }.Select(x=>new object[]{x});
    private static string Indent(string source,int depth) => string.Join('\n',source.Split('\n').Select(x=>new string(' ',depth)+x));
    private static string Program(string kind,bool refuse)
    {
        var result=kind switch { "recursive" or "metadata-getter" => "'ok'", "typed"=>"False", "metadata-names" or "metadata-update" or "metadata-setter"=>"None",
            "partial-descriptor" or "method-descriptor"=>"lambda *args:7", "dispatch-class"=>"int", _=>"1" };
        var body=refuse?"try:\n payload='x'*1000000\nfinally:\n return "+result
            :"with open('pause') as file:\n file.read()\nreturn "+result;
        string definition,operation;
        switch(kind)
        {
            case "dispatch":definition="@functools.singledispatch\ndef f(value):\n"+Indent(body,1);operation="f(2)";break;
            case "method":definition="class C:\n @functools.singledispatchmethod\n def f(self,value):\n"+Indent(body,2);operation="C().f(2)";break;
            case "recursive":definition="@functools.recursive_repr()\ndef f(value):\n"+Indent(body,1);operation="f(2)";break;
            case "metadata-getter":definition="class Wrapped:\n @property\n def tag(self):\n"+Indent(body,2)+"\ndef wrapper():pass";operation="functools.update_wrapper(wrapper,Wrapped(),assigned=('tag',),updated=())";break;
            case "metadata-names":
                var names=refuse?body+"\nyield '__name__'":"with open('pause') as file:\n file.read()\nyield '__name__'";
                definition="class Names:\n def __iter__(self):\n"+Indent(names,2)+"\ndef wrapped():pass\ndef wrapper():pass";
                operation="functools.update_wrapper(wrapper,wrapped,assigned=Names(),updated=())";break;
            case "metadata-update":definition="class Target:\n def update(self,source):\n"+Indent(body,2)+"\ndef wrapper():pass\ndef wrapped():pass\nwrapper.extra=Target()\nwrapped.extra={}";operation="functools.update_wrapper(wrapper,wrapped,assigned=(),updated=('extra',))";break;
            case "metadata-setter":definition="class Target:\n def __setattr__(self,name,value):\n"+Indent(body,2)+"\ndef wrapped():pass";operation="functools.update_wrapper(Target(),wrapped,assigned=('__name__',),updated=())";break;
            case "typed":definition="class Flag:\n def __bool__(self):\n"+Indent(body,2);operation="functools.lru_cache(typed=Flag())(lambda x:x)";break;
            case "cached-property":definition="class C:\n @functools.cached_property\n def value(self):\n"+Indent(body,2);operation="C().value";break;
            case "dispatch-class":definition="class C:\n @property\n def __class__(self):\n"+Indent(body,2)+"\n@functools.singledispatch\ndef f(value):return 0\nf.register(int,lambda value:1)";operation="f(C())";break;
            default:
                definition="class Descriptor:\n def __get__(self,instance,owner):\n"+Indent(body,2);
                if(kind=="partial-descriptor") { definition+="\nclass C:g=functools.partialmethod(Descriptor(),2)";operation="C().g(3)"; }
                else { definition+="\nclass C:\n @functools.singledispatchmethod\n def f(self,value):return 0\nC.f.register(int,Descriptor())";operation="C().f(2)"; }
                break;
        }
        return "import functools\n"+definition+"\n"+operation+"\nprint('late')";
    }

    [Theory,MemberData(nameof(Kinds))]
    public async Task SuspendedWrapperCallbacksCancelAndAllowBudgetReuse(string kind)
    {
        using var script=new LythonEngine().Compile(Program(kind,false));
        Assert.True(script.IsValid);
        var host=new DelayedLythonHost();host.SeedFile("/pause","1");
        var reached=host.PauseReadUntilCancellation("/pause");
        using var cancellation=new CancellationTokenSource();
        var budget=new LythonMemoryBudget(2*1024*1024);
        var pending=script.RunAsync(host,new LythonRunOptions{MemoryBudget=budget,CancellationToken=cancellation.Token});
        await reached.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(pending.IsCompleted);
        Assert.True(budget.ReservedBytes>0);
        cancellation.Cancel();
        using(var result=await pending)
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled,result.FailureAttribution?.Reason);
            Assert.DoesNotContain("late",result.StandardOutput);
        }
        Assert.Equal(0,budget.ReservedBytes);
        var next=new DelayedLythonHost();next.SeedFile("/pause","1");
        using(var result=await script.RunAsync(next,new LythonRunOptions{MemoryBudget=budget}))
        {
            Assert.True(result.Success,result.Failure?.Message);
            Assert.True(next.CompletedAsynchronously>0);
            Assert.Contains("late",result.StandardOutput);
        }
        Assert.Equal(0,budget.ReservedBytes);
    }

    [Theory,MemberData(nameof(Kinds))]
    public async Task RefusedWrapperCallbacksRemainTerminalDespiteFinallyReturns(string kind)
    {
        using var script=new LythonEngine().Compile(Program(kind,true));
        Assert.True(script.IsValid);
        var budget=new LythonMemoryBudget(256*1024);
        foreach(var asynchronous in new[]{false,true})
        {
            using(var result=asynchronous?await script.RunAsync(new MockLythonHost(),new LythonRunOptions{MemoryBudget=budget})
                :script.Run(new MockLythonHost(),new LythonRunOptions{MemoryBudget=budget}))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance,result.FailureAttribution?.Reason);
                Assert.True(result.DeniedReservationBytes>0);
                Assert.DoesNotContain("late",result.StandardOutput);
            }
            Assert.Equal(0,budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task RetainedWrapperResultsShareAllowanceUntilBothOwnersFinish()
    {
        using var script=new LythonEngine().Compile("import functools\n@functools.lru_cache()\ndef f(x):return 'x'*8192+str(x)\n@functools.singledispatch\ndef dispatch(x):return f(x)\nview=dispatch.registry\nclass C:\n @functools.cached_property\n def value(self):return dispatch(2)\nx=C()\nreturn [dispatch(1),x.value,f(1),view[object](2)]");
        Assert.True(script.IsValid);
        var budget=new LythonMemoryBudget(2*1024*1024);
        var options=new LythonRunOptions{MemoryBudget=budget};
        using var first=script.Run(new MockLythonHost(),options);
        Assert.True(first.Success,first.Failure?.Message);
        var one=budget.ReservedBytes;
        Assert.True(one>0);
        using var second=await script.RunAsync(new MockLythonHost(),options);
        Assert.True(second.Success,second.Failure?.Message);
        Assert.True(budget.ReservedBytes>one);
        first.Dispose();
        Assert.True(budget.ReservedBytes>0);
        var values=Assert.IsType<List<object?>>(second.ReturnValue);
        Assert.Equal(new string('x',8192)+"1",values[0]);
        Assert.Equal(new string('x',8192)+"2",values[1]);
        Assert.Equal(values[0],values[2]);Assert.Equal(values[1],values[3]);
        second.Dispose();
        Assert.Equal(0,budget.ReservedBytes);
    }
}
