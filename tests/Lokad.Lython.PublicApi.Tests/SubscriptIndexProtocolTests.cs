using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SubscriptIndexProtocolTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "class Index: pass\nkey=Index()\nkey.__index__=lambda:0\ntry: print([10,20][key])\nexcept TypeError: print('type-only')", "type-only\n" },
        { "class Index:\n __index__=None\ntry: print([10,20][Index()])\nexcept TypeError: print('not-callable')", "not-callable\n" },
        { "class Index:\n def __index__(self): return 'bad'\ntry: print([10,20][Index()])\nexcept TypeError: print('non-int')", "non-int\n" },
        { "events=[]\nclass Index:\n def __index__(self):\n  events.append('index')\n  raise ValueError('bad')\nvalue='before'\ntry: value=[10,20][Index()]\nexcept ValueError: print(value,events)", "before ['index']\n" },
        { "events=[]\nclass Index:\n @property\n def __index__(self):\n  events.append('bind')\n  return lambda:1\nprint([10,20][Index()],events)", "20 ['bind']\n" },
        { "class Index:\n def __index__(self): return 1<<100\nprint(range(1<<101)[Index()]==1<<100)\ntry: print([10,20][Index()])\nexcept IndexError: print('out-of-range')", "True\nout-of-range\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task TypeLevelHooksAndFailureOrderMatchPython(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("[10,20]", "20\n", false)]
    [InlineData("[10,20]", "20\n", true)]
    [InlineData("range(3)", "2\n", false)]
    [InlineData("range(3)", "2\n", true)]
    public async Task IndexCallbacksSuspendAndCancelWithoutReplay(string target, string expected, bool cancel)
    {
        var source = "class Index:\n def __index__(self):\n  with open('events.txt','a') as output: output.write('index')\n  return -1\nprint(" + target + "[Index()])";
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
                    Assert.Fail(early.Failure?.Message ?? "Execution completed before its index callback suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (cancel) Assert.False(result.Success);
            else Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(cancel ? "" : expected, result.StandardOutput);
            Assert.Equal(cancel ? "before" : "beforeindex", host.ReadText("/events.txt"));
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
    [InlineData("[10,20]", "20\n", false)]
    [InlineData("[10,20]", "20\n", true)]
    [InlineData("(10,20)", "20\n", false)]
    [InlineData("(10,20)", "20\n", true)]
    [InlineData("b'ab'", "98\n", false)]
    [InlineData("b'ab'", "98\n", true)]
    [InlineData("'ab'", "b\n", false)]
    [InlineData("'ab'", "b\n", true)]
    [InlineData("range(3)", "2\n", false)]
    [InlineData("range(3)", "2\n", true)]
    public async Task SequenceReadsUseIndexProtocol(string target, string expected, bool asynchronous)
    {
        var source = "class Index:\n def __index__(self): return -1\nprint(" + target + "[Index()])";
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DictionaryKeysDoNotInvokeIndexProtocol(bool asynchronous)
    {
        const string source = "events=[]\nclass Key:\n def __index__(self):\n  events.append('index')\n  return 1\nkey=Key()\nvalues={key:'kept'}\nprint(values[key],events)";
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("kept []\n", result.StandardOutput);
    }
}
