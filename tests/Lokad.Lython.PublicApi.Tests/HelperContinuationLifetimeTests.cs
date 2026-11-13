using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class HelperContinuationLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeFormattedRecordKeepsWithdrawnInputThroughProtocolIo(bool cancel)
    {
        const int payload = 65536;
        const string source = """
            events=[]
            class Value:
                def __format__(self,spec):
                    global record
                    record=None
                    events.append('format')
                    with open('event.txt','a') as target:
                        target.write('once')
                    return 'β'
            def render(record):
                return f"{record['prefix']}|{record['value']}|{record['prefix']}"
            record={'prefix':'A'*65536,'value':Value()}
            result=render(record)
            return result,events,record
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
                Assert.Equal(new string('A', payload) + "|β|" + new string('A', payload), values[0]);
                Assert.Equal("format", Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<object?>>(values[1])));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialFormattingKeepsValuesThroughProtocolIoAndCleanup(bool cancel)
    {
        const string source = """
            events=[]
            class Value:
                def __format__(self,spec):
                    events.append('format')
                    with open('event.txt','a') as target:
                        target.write('once')
                    return 'β'
            def render(value):
                first='A'*4096
                return f'{first}:{value}:{first}'
            return render(Value()),events
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
                Assert.True(budget.ReservedBytes > baseline);
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
                Assert.Equal(new string('A', 4096) + ":β:" + new string('A', 4096), values[0]);
                Assert.Equal("format", Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<object?>>(values[1])));
                Assert.Equal("start|once", host.ReadText("/event.txt"));
                Assert.True(budget.ReservedBytes > baseline);
            }
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
