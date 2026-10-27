using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeCallbackUnwindTests
{
    public static TheoryData<string, string> Boundaries => new()
    {
        { "search-equality", "class K:\n def __eq__(self,other):read();return True\n[K()].index(1)" },
        { "remove-equality", "class K:\n def __eq__(self,other):read();return True\n[K()].remove(1)" },
        { "list-ordering", "class K:\n def __eq__(self,other):read();return True\n[K()]<[1]" },
        { "slice-equality", "class K:\n def __eq__(self,other):read();return True\nslice(K())==slice(1)" },
        { "sorted-truth", "class Flag:\n def __bool__(self):read();return False\nsorted([2,1],reverse=Flag())" },
        { "sort-truth", "class Flag:\n def __bool__(self):read();return False\nitems=[2,1];items.sort(reverse=Flag())" },
        { "list-bound", "class Bound:\n def __index__(self):read();return 0\n[1,2].index(1,Bound())" },
        { "tuple-bound", "class Bound:\n def __index__(self):read();return 0\n(1,2).index(1,Bound())" },
        { "deque-bound", "class Bound:\n def __index__(self):read();return 0\ndeque([1,2]).index(1,Bound())" },
        { "bound-descriptor", "class Bound:\n @property\n def __index__(self):read();return lambda:0\n[1,2].index(1,Bound())" },
        { "list-insert", "class Bound:\n def __index__(self):read();return 0\nitems=[1,2];items.insert(Bound(),9)" },
        { "deque-insert", "class Bound:\n def __index__(self):read();return 0\nitems=deque([1,2]);items.insert(Bound(),9)" },
        { "deque-remove", "class K:\n def __eq__(self,other):read();return True\ndeque([K()]).remove(1)" },
    };

    private const string Prelude = """
        from collections import deque
        def read():
            with open('/cleanup','w') as target:
                target.write('done')
                target.flush()
                with open('/value') as source: source.read()
                try:
                    if fail: storage='x'*8388608
                finally: return 1
        """;

    [Theory, MemberData(nameof(Boundaries))]
    public async Task InFlightCancellationUnwindsBeforeReuse(string name, string boundary)
    {
        var budget=new LythonMemoryBudget(2*1024*1024);
        using var script=new LythonEngine().Compile(Prelude+"\n"+boundary,budget);
        Assert.True(script.IsValid,name);
        var baseline=budget.ReservedBytes;
        var host=new DelayedLythonHost();host.SeedFile("/value","1");
        var started=host.PauseReadUntilCancellation("/value");
        using var cancellation=new CancellationTokenSource();
        var run=script.RunAsync(host,new LythonRunOptions { CancellationToken=cancellation.Token,Globals=new Dictionary<string,object?> { ["fail"]=false } });
        try
        {
            await started.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(run.IsCompleted);
            Assert.True(budget.ReservedBytes>baseline);
            cancellation.Cancel();
            using var result=await run.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled,result.FailureAttribution?.Reason);
        }
        finally
        {
            cancellation.Cancel();
            using var drained=await run.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal("done",host.ReadText("/cleanup"));
        Assert.Equal(baseline,budget.ReservedBytes);
        var fresh=new DelayedLythonHost();fresh.SeedFile("/value","1");
        using(var result=await script.RunAsync(fresh,new LythonRunOptions { Globals=new Dictionary<string,object?> { ["fail"]=false } }))
            Assert.True(result.Success,result.Failure?.Message);
        Assert.Equal(baseline,budget.ReservedBytes);
    }

    [Theory, MemberData(nameof(Boundaries))]
    public async Task RefusalThroughFinallyRemainsTerminal(string name,string boundary)
    {
        var budget=new LythonMemoryBudget(2*1024*1024);
        using var script=new LythonEngine().Compile(Prelude+"\ntry:\n "+boundary.Replace("\n","\n ")+"\nexcept MemoryError:pass\nprint('later')",budget);
        Assert.True(script.IsValid,name);
        var baseline=budget.ReservedBytes;
        foreach(var asynchronous in new[] {false,true})
        {
            var immediate=new MockLythonHost();immediate.SeedFile("/value","1");
            var delayed=new DelayedLythonHost();delayed.SeedFile("/value","1");
            var options=new LythonRunOptions { Globals=new Dictionary<string,object?> { ["fail"]=true } };
            using(var result=asynchronous?await script.RunAsync(delayed,options):script.Run(immediate,options))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance,result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            Assert.Equal("done",asynchronous?delayed.ReadText("/cleanup"):immediate.ReadText("/cleanup"));
            Assert.Equal(baseline,budget.ReservedBytes);
        }
    }
}
