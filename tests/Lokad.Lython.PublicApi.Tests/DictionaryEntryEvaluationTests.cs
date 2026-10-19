using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DictionaryEntryEvaluationTests
{
    [Theory]
    [InlineData("result={mark('key',[]):mark('value',1),**{}}")]
    [InlineData("class Holder:\n    result={mark('key',[]):mark('value',1),**{}}")]
    [InlineData("build=lambda:{mark('key',[]):mark('value',1),**{}}\nbuild()")]
    public async Task OwnValueIsEvaluatedBeforeUnhashableKeyValidation(string body)
    {
        var source = "events=[]\ndef mark(name,value):\n    events.append(name)\n    return value\ntry:\n" +
            string.Join("\n", body.Split('\n').Select(line => "    " + line)) +
            "\nexcept TypeError:\n    print(events)\n";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("['key', 'value']\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoweredValueCanSuspendBeforeKeyValidation(bool cancel)
    {
        const string source = """
def value():
    with open('events.txt','a') as target: target.write('value')
    return 1
try:
    class Holder:
        result={[]:value(),**{}}
except TypeError:
    print('invalid-key')
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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
                await entered.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (cancel)
            {
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
                Assert.Equal("before", host.ReadText("/events.txt"));
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("invalid-key\n", result.StandardOutput);
                Assert.Equal("beforevalue", host.ReadText("/events.txt"));
            }
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("target={[]:fail(),**{}}")]
    [InlineData("class Holder:\n    result={[]:fail(),**{}}")]
    public async Task ValueFailurePrecedesKeyValidationAndAssignment(string body)
    {
        var source = "def fail(): raise ValueError('value')\ntarget={'old':1}\ntry:\n" +
            string.Join("\n", body.Split('\n').Select(line => "    " + line)) +
            "\nexcept ValueError:\n    print(target)";
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("{'old': 1}\n", result.StandardOutput);
        }
    }
}
