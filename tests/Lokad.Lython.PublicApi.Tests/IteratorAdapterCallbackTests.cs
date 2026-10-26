using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IteratorAdapterCallbackTests
{
    public static IEnumerable<object[]> Kinds() => new[] { "filter","filter-result","compress","groupby","sentinel","accumulate" }.Select(x=>new object[]{x});
    private static string Program(string kind,bool refuse)
    {
        var body=refuse?"try:\n payload='x'*1000000\nfinally:\n return RESULT":"with open('pause') as file:\n value=int(file.read())\nreturn RESULT";
        var result=kind=="accumulate"?(refuse?"Value(self.n+other.n)":"Value(self.n+other.n+value)"):"True";
        var declaration=kind switch
        {
            "groupby" or "sentinel"=>"class Key:\n def __eq__(self,other):\n",
            "accumulate"=>"class Value:\n def __init__(self,n):self.n=n\n def __add__(self,other):\n",
            _=>"class Flag:\n def __bool__(self):\n"
        };
        var operation=kind switch
        {
            "filter"=>"print(len(list(filter(None,[Flag()]))))",
            "filter-result"=>"print(len(list(filter(lambda x:Flag(),[1]))))",
            "compress"=>"print(list(itertools.compress([7],[Flag()])))",
            "groupby"=>"print([len(list(group)) for key,group in itertools.groupby([Key(),Key()])])",
            "sentinel"=>"print(next(iter(lambda:1,Key()),None))",
            _=>"print([x.n for x in itertools.accumulate([Value(1),Value(2)])])"
        };
        return "import itertools\n"+declaration+string.Join('\n',body.Replace("RESULT",result).Split('\n').Select(x=>"  "+x))+"\n"+operation+"\nprint('late')";
    }

    [Theory,MemberData(nameof(Kinds))]
    public async Task InFlightCallbacksCancelAndReturnTheirAllowance(string kind)
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
    public async Task CallbackRefusalRemainsTerminalThroughFinallyReturns(string kind)
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
                Assert.Equal("MemoryError",result.Failure?.ExceptionType);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance,result.FailureAttribution?.Reason);
                Assert.True(result.DeniedReservationBytes>0);
                Assert.DoesNotContain("late",result.StandardOutput);
            }
            Assert.Equal(0,budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task SharedExecutionsKeepCapturedSourcesAndResultsFunded()
    {
        using var script=new LythonEngine().Compile("def make():\n text='x'*8192\n return map(lambda n:text+str(n),[1,2])\nreturn list(make())");
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
        second.Dispose();
        Assert.Equal(0,budget.ReservedBytes);
    }
}
