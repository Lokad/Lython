using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringPredicateCallTests
{
    [Theory]
    [InlineData("islower", "λ", true)]
    [InlineData("islower", "Λ", false)]
    [InlineData("islower", "", false)]
    [InlineData("isupper", "Λ", true)]
    [InlineData("isupper", "λ", false)]
    [InlineData("isupper", "", false)]
    [InlineData("istitle", "Λλ", true)]
    [InlineData("istitle", "λλ", false)]
    [InlineData("istitle", "", false)]
    [InlineData("isalpha", "λ𝔄", true)]
    [InlineData("isalpha", "λ1", false)]
    [InlineData("isalpha", "", false)]
    [InlineData("isdigit", "²", true)]
    [InlineData("isdigit", "Ⅳ", false)]
    [InlineData("isdigit", "", false)]
    [InlineData("isalnum", "λ²", true)]
    [InlineData("isalnum", "λ_", false)]
    [InlineData("isalnum", "", false)]
    [InlineData("isascii", "Az\u0000", true)]
    [InlineData("isascii", "é", false)]
    [InlineData("isascii", "", true)]
    [InlineData("isdecimal", "١٢", true)]
    [InlineData("isdecimal", "²", false)]
    [InlineData("isdecimal", "", false)]
    [InlineData("isidentifier", "λ_1", true)]
    [InlineData("isidentifier", "1λ", false)]
    [InlineData("isidentifier", "", false)]
    [InlineData("isnumeric", "Ⅳ", true)]
    [InlineData("isnumeric", "λ", false)]
    [InlineData("isnumeric", "", false)]
    [InlineData("isprintable", "é😀", true)]
    [InlineData("isprintable", "\n", false)]
    [InlineData("isprintable", "", true)]
    [InlineData("isspace", " \t", true)]
    [InlineData("isspace", "x", false)]
    [InlineData("isspace", "", false)]
    public async Task ImmediateExposedExpandedAndDynamicPredicatesFollowPython(string method, string value, bool expected)
    {
        var source = $"first=value.{method}\nsecond=value.{method}\nreturn [value.{method}(),first(),value.{method}(*[]),getattr(value,\"{method}\")(),first is second,first.__self__ is value,first.__name__,first.__qualname__,first.__module__]";
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["value"] = value } };
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new List<object?> { expected, expected, expected, expected, false, true, method, "str." + method, null }, result.ReturnValue);
        }
    }

    [Theory]
    [InlineData("islower")]
    [InlineData("isupper")]
    [InlineData("istitle")]
    [InlineData("isalpha")]
    [InlineData("isdigit")]
    [InlineData("isalnum")]
    [InlineData("isascii")]
    [InlineData("isdecimal")]
    [InlineData("isidentifier")]
    [InlineData("isnumeric")]
    [InlineData("isprintable")]
    [InlineData("isspace")]
    public async Task RejectedArgumentsKeepTheirEffects(string method)
    {
        var source = $"events=[]\ndef argument():\n events.append('arg')\n return 0\ndef check(value):\n try: value.{method}(argument())\n except TypeError: print(events)\n try: value.{method}(flag=argument())\n except TypeError: print(events)\ncheck('λ')";
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("['arg']\n['arg', 'arg']\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task RetainedMethodReceiverKeepsItsFundingAcrossSequentialRuns()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("text='a'*4096\nmethod=text.isalpha\ntext='changed'\nreturn [method.__self__,method()]", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.Equal(new List<object?> { new string('a', 4096), true }, retained.ReturnValue);
                var held = budget.ReservedBytes;
                Assert.True(held > baseline);
                using (var next = script.Run(new MockLythonHost())) Assert.True(next.Success, next.Failure?.Message);
                Assert.Equal(held, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/bind.txt")]
    [InlineData("/call.txt")]
    public async Task CustomPredicatePropertyAndCallStillSuspend(string? pausedPath)
    {
        const string source = "class Value:\n @property\n def isalpha(self):\n  with open('bind.txt','a') as output: output.write('bind')\n  def check():\n   with open('call.txt','a') as output: output.write('call')\n   return 'custom'\n  return check\nprint(Value().isalpha())";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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
                Assert.Same(entered, first);
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
}
