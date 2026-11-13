using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ComposedHelperTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComposedCallsPreserveDefaultsReceiversAndLiveBindings(bool asynchronous)
    {
        const string source = """
            def rounding(amount,rate=2000):
                return (amount*rate+5000)//10000
            def price(amount):
                net=rounding(rate=9000,amount=amount)
                return net+rounding(net)
            class Adjust:
                def apply(self,amount):
                    return rounding(amount)+1
            def invoke(operation,amount):
                return operation(amount)
            def factory():
                count=0
                def add(value):
                    nonlocal count
                    count+=1
                    return value+count
                def calculate(value):
                    return add(value)+add(value)
                return calculate
            calculate=factory()
            first=calculate(10)
            second=calculate(10)
            total=price(1250)
            rounding.__defaults__=(1000,)
            return total,price(1250),invoke(Adjust().apply,1250),first,second
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            for (var pass = 0; pass < 2; pass++)
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    var values = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue);
                    Assert.Equal(["1350", "1238", "126", "23", "27"], values.Select(v => v?.ToString()));
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparedDescendantsKeepAliasesAndOnceOnlyEffectsThroughIo(bool cancel)
    {
        const int payload = 65536;
        const string source = """
            class Marker:
                def __call__(self,text):
                    global record
                    record=None
                    with open('event.txt','a') as target:
                        target.write('once')
                    return text
            marker=Marker()
            def factory():
                count=0
                def leaf(text):
                    nonlocal count
                    count+=1
                    first=text.lower()
                    return first+marker(text)
                def middle(text):
                    nonlocal count
                    count+=10
                    return leaf(text)
                def outer(text):
                    nonlocal count
                    count+=100
                    return middle(text)
                def read():
                    return count
                return outer,read
            outer,read=factory()
            record='A'*65536
            return outer(record),read(),record
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/event.txt", "start|");
        var entered = cancel ? host.PauseWriteUntilCancellation("/event.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        LythonExecutionResult? result = null;
        try
        {
            if (entered is not null)
            {
                await entered.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes >= baseline + 2 * payload);
                cancellation.Cancel();
            }
            result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(!cancel, result.Success);
            if (cancel)
            {
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Equal("start|", host.ReadText("/event.txt"));
            }
            else
            {
                var values = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue);
                Assert.Equal(new string('a', payload) + new string('A', payload), values[0]);
                Assert.Equal("111", values[1]?.ToString());
                Assert.Null(values[2]);
                Assert.Equal("start|once", host.ReadText("/event.txt"));
            }
            Assert.Equal(1, host.ClosedFileCount);
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            result ??= await pending.WaitAsync(TimeSpan.FromSeconds(5));
            result.Dispose();
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
