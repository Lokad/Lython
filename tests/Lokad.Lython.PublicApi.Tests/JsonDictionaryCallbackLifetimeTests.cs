using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonDictionaryCallbackLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    public async Task DictionaryDefaultKeepsOwnersThroughHostIoAndCleanup(bool cancel, bool expanded = false, bool wide = false)
    {
        const string source = """
import json
def replacement(value):
 payload='a'*4096
 with open('callback.txt','a') as target:target.write('value')
 return payload
return json.dumps({'value':{1}},default=replacement)
""";
        var separator = wide ? ":" : ": ";
        var expected = "{\"value\"" + separator + "\"" + new string('a', 4096) + "\"}";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        var scriptSource = wide
            ? source.Replace("default=replacement", "default=replacement,ensure_ascii=False,check_circular=True,separators=(',', ':')", StringComparison.Ordinal)
            : expanded
            ? source.Replace("default=replacement", "**{'default': replacement}", StringComparison.Ordinal)
            : source;
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(scriptSource, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/callback.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation("/callback.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Dictionary callback did not suspend.");
                }
                Assert.False(pending.IsCompleted);
                var suspended = budget.ReservedBytes;
                Assert.True(suspended > baseline);
                var otherHost = new DelayedLythonHost(); otherHost.SeedFile("/callback.txt", "before");
                using (var held = await script.RunAsync(otherHost).WaitAsync(TimeSpan.FromSeconds(5)))
                {
                    Assert.True(held.Success, held.Failure?.Message);
                    Assert.Equal(expected, held.ReturnValue);
                    Assert.True(budget.ReservedBytes > suspended);
                    Assert.False(pending.IsCompleted);
                    cancellation.Cancel();
                    using (var cancelled = await pending.WaitAsync(TimeSpan.FromSeconds(5)))
                    {
                        Assert.False(cancelled.Success);
                        Assert.Equal(LythonFailureReason.Cancelled, cancelled.FailureAttribution?.Reason);
                    }
                    Assert.True(budget.ReservedBytes > baseline);
                    Assert.Equal(expected, held.ReturnValue);
                    Assert.Equal("beforevalue", otherHost.ReadText("/callback.txt"));
                }
            }
            if (cancel) Assert.Equal("before", host.ReadText("/callback.txt"));
            else
            {
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("beforevalue", host.ReadText("/callback.txt"));
                Assert.Equal(expected, result.ReturnValue);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose(); Assert.Equal(0, budget.ReservedBytes);
    }
}
