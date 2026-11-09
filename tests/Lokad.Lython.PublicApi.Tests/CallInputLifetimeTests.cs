using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CallInputLifetimeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PendingCallsKeepArgumentAliasesFundedUntilJoinedCleanup(bool constructor, bool cancel)
    {
        var source = $$"""
            class Holder:
                def __init__(self, first, second, marker):
                    with open('events.txt','a') as target: target.write(marker)
                    self.same = first is second and first == [7]
            def check(first, second, marker):
                with open('events.txt','a') as target: target.write(marker)
                return first is second and first == [7]
            def main():
                value = [7]
                same = {{(constructor ? "Holder(value,value,'called').same" : "check(value,value,'called')")}}
                print(same)
                return value
            return main()
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/events.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation("/events.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "The call completed before its host operation suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using (var result = await pending.WaitAsync(TimeSpan.FromSeconds(10)))
            {
                if (cancel) Assert.False(result.Success);
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(new BigInteger(7), Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue)));
                    Assert.True(budget.ReservedBytes > baseline);
                }
                Assert.Equal(cancel ? "" : "True\n", result.StandardOutput);
                Assert.Equal(cancel ? "before" : "beforecalled", host.ReadText("/events.txt"));
                Assert.True(host.CompletedAsynchronously > 0);
            }
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task KeywordAndPositionalInputsRetainEvaluationOrderAndIdentity()
    {
        const string source = """
            events=[]
            class Target:
                def __getattribute__(self,name):
                    events.append('target')
                    return lambda *args,**kwargs: (args,kwargs)
            def later():
                events.append('argument')
                return 2
            result=Target().method(1,later(),last=3)
            print(events,result)
            def check(*args,last):
                return args[0] is args[1] and args[-1] is last
            value=[]
            print(check(value,value,1,2,3,value,last=value))
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("['target', 'argument'] ((1, 2), {'last': 3})\nTrue\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}
