using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MirroredFunctionLifetimeTests
{
    private const string Source = """
        payload=['é'*4096]
        def make(value,/,*extra,offset=3,**named):
            class Snapshot:
                stored=(value,extra,offset,named)
            def read():return Snapshot.stored
            global payload
            payload=None
            try:
                with open('/function') as source:source.read()
                if fail_stage=='/function':unused='z'*2000000
            except BaseException:print('suppressed-function')
            finally:
                if fail_stage=='/function':return None
            return read
        class Holder:
            def __init__(self,read,*,marker=7):
                class Captured:
                    saved=read()
                try:
                    with open('/initializer') as source:source.read()
                    if fail_stage=='/initializer':unused='z'*2000000
                except BaseException:print('suppressed-initializer')
                finally:
                    if fail_stage=='/initializer':return None
                self.value=Captured.saved
                self.marker=marker
        read=make(payload,9,offset=4,label='held')
        item=Holder(read=read)
        # Reuse working storage before reading the escaped class/closure again.
        other=make(['other'],11)
        print(len(read()[0][0]),read()[1:],item.marker,other()[1:])
        return [read()[0],item.value[0]]
        """;

    [Theory]
    [InlineData(false, "success", "/function")]
    [InlineData(true, "success", "/initializer")]
    [InlineData(false, "refusal", "/function")]
    [InlineData(true, "refusal", "/function")]
    [InlineData(false, "refusal", "/initializer")]
    [InlineData(true, "refusal", "/initializer")]
    [InlineData(true, "cancel", "/function")]
    [InlineData(true, "cancel", "/initializer")]
    public async Task MirroredCallsKeepEscapingValuesAndDrainTheirUsers(bool asynchronous, string outcome, string path)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using (var script = new LythonEngine().Compile(Source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics));
            var baseline = budget.ReservedBytes;
            // A second execution also proves the first one's idle storage and
            // failed admission do not survive cleanup into the next command.
            foreach (var attempt in new[] { outcome, "success" })
            {
                var mock = new MockLythonHost();
                var delayed = new DelayedLythonHost();
                foreach (var file in new[] { "/function", "/initializer" })
                {
                    mock.SeedFile(file, "ready");
                    delayed.SeedFile(file, "ready");
                }
                using var cancellation = new CancellationTokenSource();
                var entered = attempt == "cancel" ? delayed.PauseReadUntilCancellation(path) : null;
                var options = new LythonRunOptions
                {
                    CancellationToken = cancellation.Token,
                    Globals = new Dictionary<string, object?> { ["fail_stage"] = attempt == "refusal" ? path : "" }
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
                    Assert.Equal(attempt == "success", result.Success);
                    if (attempt == "success")
                    {
                        Assert.Equal("4096 ((9,), 4, {'label': 'held'}) 7 ((11,), 3, {})\n", result.StandardOutput);
                        var values = Assert.IsType<List<object?>>(result.ReturnValue);
                        Assert.Equal(2, values.Count);
                        foreach (var value in values)
                            Assert.Equal(new string('é', 4096), Assert.Single(Assert.IsType<List<object?>>(value)));
                        Assert.True(budget.ReservedBytes > baseline + 8192);
                        if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
                    }
                    else
                    {
                        Assert.Equal(attempt == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                            result.FailureAttribution?.Reason);
                        Assert.Empty(result.StandardOutput);
                    }
                }
                finally
                {
                    await cancellation.CancelAsync();
                    using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
