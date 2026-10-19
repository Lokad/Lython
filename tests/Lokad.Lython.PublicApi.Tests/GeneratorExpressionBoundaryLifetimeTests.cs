using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GeneratorExpressionBoundaryLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecutableFilterAwaitsTruthProtocolWithoutReplay(bool cancel)
    {
        const string source = "class Truth:\n def __bool__(self):\n  with open('/events.txt','a') as output:output.write('truth')\n  return True\ng=(i for i in (7,) if Truth())\nprint(list(g))";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/events.txt", "before:");
        var entered = cancel ? host.PauseWriteUntilCancellation("/events.txt") : null;
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
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(cancel ? "" : "[7]\n", result.StandardOutput);
            Assert.Equal(cancel ? "before:" : "before:truth", host.ReadText("/events.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task StoredValuesSurviveSuspensionAndCleanup(bool stop, bool cancel)
    {
        var source = "def run():\n total=0\n def inspect(value):\n  print(total)\n" +
            "  with open('/events.txt','a') as output:output.write(str(value))\n" +
            (stop ? "  raise StopIteration('later')\n g=((total:=i,inspect(i)) for i in (7,))\n" +
                " try:next(g)\n except RuntimeError as error:print(str(error.__cause__),total)\n"
                : "  return False\n g=(i for i in (7,) if (total:=i) and inspect(i))\n print(list(g),total)\n") +
            "run()";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + " " + d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/events.txt", "before:");
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
                    Assert.Fail(early.Failure?.Message ?? "The generator did not suspend.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (cancel)
            {
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Equal("7\n", result.StandardOutput);
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(stop ? "7\nlater 7\n" : "7\n[] 7\n", result.StandardOutput);
            }
            Assert.Equal(cancel ? "before:" : "before:7", host.ReadText("/events.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task FilterOnlyRetainedValuesStayFundedAcrossExecutions()
    {
        const string source = "def run():\n value=None\n g=(i for i in (0,) if (value:=['kept'*1024]) and False)\n list(g)\n return value\nreturn run()";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using var first = script.Run(new MockLythonHost());
        Assert.True(first.Success, first.Failure?.Message);
        var withFirst = budget.ReservedBytes;
        Assert.True(withFirst > baseline);
        using var second = await script.RunAsync(new MockLythonHost());
        Assert.True(second.Success, second.Failure?.Message);
        Assert.True(budget.ReservedBytes > withFirst);
        var expected = string.Concat(Enumerable.Repeat("kept", 1024));
        Assert.Equal(expected, Assert.Single(Assert.IsType<List<object?>>(first.ReturnValue)));
        first.Dispose();
        Assert.True(budget.ReservedBytes > baseline);
        Assert.Equal(expected, Assert.Single(Assert.IsType<List<object?>>(second.ReturnValue)));
        second.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
