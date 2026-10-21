using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CounterCountProtocolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitialCountsCopyAndUpdatesUsePythonOperandOrder(bool asynchronous)
    {
        using var script=new LythonEngine().Compile("""
            from collections import Counter
            c=Counter({'a':'x'})
            print(c['a'])
            c.update({'a':'y'})
            print(c['a'])
            c=Counter(None,a=None,b=object())
            print(c['a'] is None)
            c.update(None)
            c.subtract(None)
            class Count:
                def __init__(self,n): self.n=n
                def __add__(self,other):
                    print('add',self.n,other.n)
                    return Count(self.n+other.n)
                def __sub__(self,other):
                    print('sub',self.n,other.n)
                    return Count(self.n-other.n)
            c=Counter({'a':Count(2)})
            print('copied')
            c.update({'a':Count(3)})
            c.update(a=Count(4))
            c.subtract({'a':Count(1)})
            print(c['a'].n)
            """);
        using var result=asynchronous?await script.RunAsync(new MockLythonHost()):script.Run(new MockLythonHost());
        Assert.True(result.Success,result.Failure?.Message);
        Assert.Equal("x\nyx\nTrue\ncopied\nadd 3 2\nadd 4 5\nsub 9 1\n8\n",result.StandardOutput);
    }

    [Theory]
    [InlineData("c.update({'a':Count(3)})")]
    [InlineData("c.update(a=Count(3))")]
    [InlineData("c.subtract({'a':Count(1)})")]
    public async Task CountArithmeticCanSuspendAndCancellationPreservesCleanup(string operation)
    {
        var budget=new LythonMemoryBudget(1024*1024);
        using var script=new LythonEngine().Compile("""
            from collections import Counter
            class Count:
                def __init__(self,n): self.n=n
                def __add__(self,other):
                    with open('/count') as f: f.read()
                    return Count(self.n+other.n)
                def __sub__(self,other):
                    with open('/count') as f: f.read()
                    return Count(self.n-other.n)
            c=Counter({'a':Count(2)})
            """+"\n"+operation+"\nreturn c['a'].n",budget);
        Assert.True(script.IsValid);
        var baseline=budget.ReservedBytes;
        foreach(var cancel in new[] { false,true })
        {
            var host=new DelayedLythonHost();
            host.SeedFile("/count","data");
            var entered=cancel?host.PauseReadUntilCancellation("/count"):null;
            using var cancellation=new CancellationTokenSource();
            var pending=script.RunAsync(host,cancellationToken:cancellation.Token);
            try
            {
                if(entered is not null)
                {
                    Assert.Same(entered,await Task.WhenAny(entered,pending).WaitAsync(TimeSpan.FromSeconds(5)));
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes>baseline);
                    cancellation.Cancel();
                }
                using var result=await pending.WaitAsync(TimeSpan.FromSeconds(5));
                if(cancel) Assert.Equal(LythonFailureReason.Cancelled,result.FailureAttribution?.Reason);
                else
                {
                    Assert.True(result.Success,result.Failure?.Message);
                    Assert.True(host.CompletedAsynchronously>0);
                }
            }
            finally
            {
                cancellation.Cancel();
                using var drained=await pending.WaitAsync(TimeSpan.FromSeconds(5));
            }
            Assert.Equal(baseline,budget.ReservedBytes);
        }
    }
}
