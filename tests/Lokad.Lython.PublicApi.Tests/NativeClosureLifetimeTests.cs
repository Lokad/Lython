using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeClosureLifetimeTests
{
    [Theory]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(20)]
    public async Task CapturedCallsKeepRealParametersSeparateFromLocalAndCellStorage(int width)
    {
        var parameters = string.Join(',', Enumerable.Range(0, width).Select(i => $"a{i}"));
        var positional = string.Join(',', Enumerable.Range(0, width));
        var named = string.Join(',', Enumerable.Range(0, width).Select(i => $"a{i}={i}"));
        var source = $"def make():\n count=0\n def apply({parameters}):\n  nonlocal count\n  count+=1\n  missing:int\n  return a0+a{width - 1}\n def read():return count\n return apply,read\napply,read=make()\nprint(apply({positional}),read(),apply({named}),read())\n";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal($"{width - 1} 1 {width - 1} 2\n", result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ProtocolFallbackObservesOneCellWriteAndKeepsWithdrawnInputs(bool asynchronous, bool cancel)
    {
        const string source = """
            def make():
                count=0
                def words(value):
                    nonlocal count
                    count+=1
                    return value.lower().split()
                def read():return count
                return words,read
            words,read=make()
            class Text:
                def __init__(self,value):self.value=value
                def lower(self):
                    global payload
                    payload=None
                    print(read())
                    with open('/body') as source:source.read()
                    return self.value
            return words(Text(payload))
            """;
        var budget = new LythonMemoryBudget(512 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var attempt in new[] { cancel, false })
            {
                var mock = new MockLythonHost(); mock.SeedFile("/body", "ready");
                var delayed = new DelayedLythonHost(); delayed.SeedFile("/body", "ready");
                var entered = attempt ? delayed.PauseReadUntilCancellation("/body") : null;
                using var cancellation = new CancellationTokenSource();
                var options = new LythonRunOptions
                {
                    CancellationToken = cancellation.Token,
                    Globals = new Dictionary<string, object?> { ["payload"] = new string('É', 4096) + " A" }
                };
                var pending = asynchronous ? script.RunAsync(delayed, options) : Task.FromResult(script.Run(mock, options));
                try
                {
                    if (entered is not null)
                    {
                        Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
                        Assert.False(pending.IsCompleted);
                        Assert.True(budget.ReservedBytes > baseline + 8192);
                        await cancellation.CancelAsync();
                    }
                    using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.Equal("1\n", result.StandardOutput);
                    Assert.Equal(!attempt, result.Success);
                    if (attempt) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                    else
                    {
                        Assert.Equal(new List<object?> { new string('É', 4096), "A" }, result.ReturnValue);
                        Assert.True(budget.ReservedBytes > baseline + 8192);
                        if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
                    }
                }
                finally
                {
                    await cancellation.CancelAsync();
                    using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                }
                if (asynchronous) Assert.Equal(1, delayed.ClosedFileCount);
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
