using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ComprehensionLambdaLifetimeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("/events.txt")]
    [InlineData("/b.txt")]
    public async Task EscapedBodiesKeepLateBindingsThroughHostSuspension(string? pause)
    {
        const string source = "def read(value):\n with open('/events.txt','a') as out:out.write(value)\n with open(value) as data:return data.read()\ncallbacks=[lambda:read(value) for value in ('/a.txt','/b.txt')]\nprint(callbacks[0](),callbacks[1]())";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/a.txt", "alpha");
        host.SeedFile("/b.txt", "beta");
        host.SeedFile("/events.txt", "before:");
        var entered = pause == "/b.txt" ? host.PauseReadUntilCancellation(pause)
            : pause is not null ? host.PauseWriteUntilCancellation(pause) : null;
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
                    Assert.Fail(early.Failure?.Message ?? "The lambda body did not suspend.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (pause is null)
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("beta beta\n", result.StandardOutput);
            }
            else
            {
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            Assert.Equal(pause is null ? "before:/b.txt/b.txt" : pause == "/b.txt" ? "before:/b.txt" : "before:", host.ReadText("/events.txt"));
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultsSuspendInsideTheExistingBindingLifetime(bool cancel)
    {
        const string source = "def default_value(value):\n with open('/defaults.txt','a') as out:out.write(str(value))\n return value\ncallbacks=[lambda saved=default_value(value):saved for value in range(2)]\nprint([f() for f in callbacks])";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/defaults.txt", "before:");
        var entered = cancel ? host.PauseWriteUntilCancellation("/defaults.txt") : null;
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
                    Assert.Fail(early.Failure?.Message ?? "The default did not suspend.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (cancel)
            {
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("[0, 1]\n", result.StandardOutput);
            }
            Assert.Equal(cancel ? "before:" : "before:01", host.ReadText("/defaults.txt"));
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturnedAliasesStayFundedAfterBindingsAndFunctionsEnd(bool firstAsync)
    {
        const string source = "def make(value):\n return [lambda:value for value in ('a'*8192,value)]\ncallbacks=make('held'*4096)\nsaved=callbacks[0]()\ncallbacks.clear()\nreturn saved";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using var first = firstAsync ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        using var second = firstAsync ? script.Run(new MockLythonHost()) : await script.RunAsync(new MockLythonHost());
        Assert.True(first.Success, first.Failure?.Message);
        Assert.True(second.Success, second.Failure?.Message);
        Assert.Equal(string.Concat(Enumerable.Repeat("held", 4096)), first.ReturnValue);
        Assert.Equal(first.ReturnValue, second.ReturnValue);
        first.Dispose();
        Assert.True(budget.ReservedBytes > baseline);
        second.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
