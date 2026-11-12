using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class OrderedFunctionContinuationTests
{
    private const string Source = """
        class Note:
            def __eq__(self,other):
                global payload
                assert self.peek()==1
                payload=None
                with open('/gate') as source:source.read()
                return False
        def run():
            count=0
            def peek():return count
            def validate(record):
                nonlocal count
                count+=1
                amount=int(record['amount'])
                if amount<0:return None
                if record['note']=='skip':return None
                return record['kind'],amount,len(record['retained'])
            note=Note()
            note.peek=peek
            result=validate({'kind':'k','amount':'123','note':note,'retained':payload})
            print(count,result[0],result[1],result[2])
        run()
        """;

    private static LythonRunOptions Options(LythonMemoryBudget budget, CancellationToken token = default)
        => new()
        {
            MemoryBudget = budget,
            CancellationToken = token,
            Globals = new Dictionary<string, object?> { ["payload"] = new string('x', 65536) },
        };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateProtocolKeepsInitializersAndRetainedInputs(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(Source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new MockLythonHost();
            host.SeedFile("/gate", "ready");
            using (var result = asynchronous ? await script.RunAsync(host, Options(budget)) : script.Run(host, Options(budget)))
            {
                Assert.True(result.Success, result.Failure?.Message);
                // Independent CPython output. The protocol also checks the local
                // initializer's effect before any real host operation can begin.
                Assert.Equal("1 k 123 65536\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task LateProtocolJoinsCancellationBeforeReturningInputFunding()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(Source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/gate", "ready");
            var readEntered = host.PauseReadUntilCancellation("/gate");
            var closeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var closeReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            host.BeforeFileClose = async () =>
            {
                closeEntered.TrySetResult();
                await closeReleased.Task;
            };
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, Options(budget, cancellation.Token));
            try
            {
                await readEntered.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes >= baseline + 65536);
                await cancellation.CancelAsync();
                await closeEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes >= baseline + 65536);
                closeReleased.TrySetResult();
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            }
            finally
            {
                await cancellation.CancelAsync();
                closeReleased.TrySetResult();
                using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            var reused = new MockLythonHost();
            reused.SeedFile("/gate", "ready");
            using (var result = await script.RunAsync(reused, Options(budget)))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("1 k 123 65536\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
