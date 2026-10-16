using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringLowerCallTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedMethodsAndResultsKeepTheirOwners(bool exposed)
    {
        var source = "text='A'*4096\n" + (exposed
            ? "method=text.lower\ntext='changed'\nreturn method()"
            : "return text.lower()");
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.Equal(new string('a', 4096), Assert.IsType<string>(retained.ReturnValue));
                var held = budget.ReservedBytes;
                using (var next = script.Run(new MockLythonHost())) Assert.True(next.Success, next.Failure?.Message);
                Assert.Equal(held, budget.ReservedBytes);
                Assert.True(held > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedLowerDoesNotPublishTheResult(bool exposed)
    {
        var source = "value='before'\ntext='A'*32768\n" + (exposed ? "method=text.lower\n" : "") +
            "print('ready')\ntry: value=" + (exposed ? "method()" : "text.lower()") +
            "\nexcept MemoryError: print(value,len(text))";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 80 * 1024 };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("ready\nbefore 32768\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/bind.txt")]
    [InlineData("/call.txt")]
    public async Task CustomBindingAndCallsStillSuspendAndCancel(string? pausedPath)
    {
        const string source = "class Text:\n @property\n def lower(self):\n  with open('bind.txt','a') as output: output.write('bind')\n  def convert():\n   with open('call.txt','a') as output: output.write('call')\n   return 'custom'\n  return convert\nprint(Text().lower())";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/bind.txt", "before");
        host.SeedFile("/call.txt", "before");
        var entered = pausedPath is null ? null : host.PauseWriteUntilCancellation(pausedPath);
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
                    Assert.Fail(early.Failure?.Message ?? "Execution completed before its custom lower operation suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (pausedPath is null) Assert.True(result.Success, result.Failure?.Message);
            else Assert.False(result.Success);
            Assert.Equal(pausedPath is null ? "custom\n" : "", result.StandardOutput);
            Assert.Equal(pausedPath == "/bind.txt" ? "before" : "beforebind", host.ReadText("/bind.txt"));
            Assert.Equal(pausedPath is null ? "beforecall" : "before", host.ReadText("/call.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    public static TheoryData<string, string> Programs => new()
    {
        { "for text in ('','AbC','İΣ','ΟΣ','Straße','𐐀'):\n print(text.lower())", "\nabc\ni̇ς\nος\nstraße\n𐐨\n" },
        { "text='AbC'\nfirst=text.lower\nsecond=text.lower\nprint(first is second,first.__self__ is text,first.__name__,first.__qualname__,first.__module__)\ntext='CHANGED'\nprint(first(),second())", "False True lower str.lower None\nabc abc\n" },
        { "events=[]\ndef argument():\n events.append('arg')\n return 0\ndef call(value):\n try: value.lower(argument())\n except TypeError: print(events)\ncall('AbC')", "['arg']\n" },
        { "class Value:\n def lower(self): return 'custom'\nprint(Value().lower())", "custom\n" },
        { "events=[]\ndef arguments():\n events.append('args')\n return []\ntext='AbC'\nprint(text.lower(*arguments()),events)", "abc ['args']\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ImmediateExposedAndDynamicCallsMatchPython(string source, string expected)
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
}
