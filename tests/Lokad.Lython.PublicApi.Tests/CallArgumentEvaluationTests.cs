using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CallArgumentEvaluationTests
{
    [Theory]
    [InlineData("result=target(*mark('star',[]),key=mark('keyword',1))")]
    [InlineData("class Holder:\n    result=target(*mark('star',[]),key=mark('keyword',1))")]
    [InlineData("build=lambda:target(*mark('star',[]),key=mark('keyword',1))\nbuild()")]
    public async Task NonCallableTargetsDoNotSuppressArgumentEffects(string body)
    {
        var source = "events=[]\ndef mark(name,value):\n    events.append(name)\n    return value\ntry:\n" +
            string.Join("\n", body.Split('\n').Select(line => "    " + line)) +
            "\nexcept TypeError:\n    print(events)\n";
        await CheckBoth(source, "['star', 'keyword']\n");
    }

    [Theory]
    [InlineData("def fail():\n    events.append('value')\n    raise ValueError('sentinel')\ntry: target(*fail())\nexcept ValueError: print('value',events)\nexcept TypeError: print('type',events)", "value ['value']\n")]
    [InlineData("def mark(name,value):\n    events.append(name)\n    return value\ntry: target(*mark('star',3),key=mark('keyword',1))\nexcept TypeError: print(events)", "['star', 'keyword']\n")]
    [InlineData("def mark(name,value):\n    events.append(name)\n    return value\ntry: target(mark('positional',1),*mark('star',[]))\nexcept TypeError: print(events)", "['positional', 'star']\n")]
    public async Task ArgumentFailuresAndSoleStarTimingWinOverTargetRejection(string body, string expected)
        => await CheckBoth("events=[]\n" + body, expected);

    private static async Task CheckBoth(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["target"] = 0 } };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoweredArgumentsCanSuspendAndCancelBeforeTargetRejection(bool cancel)
    {
        const string source = """
def values():
    with open('events.txt','a') as output: output.write('argument')
    return []
try:
    class Holder:
        result=target(*values())
except TypeError:
    print('invalid-target')
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["target"] = 0 } };
        var host = new DelayedLythonHost();
        host.SeedFile("/events.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation("/events.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, options, cancellation.Token);
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
                Assert.Equal("invalid-target\n", result.StandardOutput);
                Assert.Equal("beforeargument", host.ReadText("/events.txt"));
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
}
