using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExceptionProtocolTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "class Message:\n def __str__(self):\n  with open('/data') as f:return f.read()\nprint(str(ValueError(Message())))", "payload\n" },
        { "class Message:\n def __repr__(self):\n  with open('/data') as f:return f.read()\nprint(repr(ValueError(Message())))", "ValueError(payload)\n" },
        { "class Message:\n def __repr__(self):\n  with open('/data') as f:return f.read()\nprint(str(ValueError(Message(),7)))", "(payload, 7)\n" },
        { "class Message:\n def __str__(self):\n  with open('/data') as f:return f.read()\nprint(ValueError(Message()).message)", "payload\n" },
        { "class Message:\n def __str__(self):\n  with open('/data') as f:return f.read()\nprint(str(SystemExit(Message())))", "payload\n" },
        { "class Args:\n def __iter__(self):\n  with open('/data') as f:yield f.read()\nerror=ValueError();error.args=Args()\nprint(error.args)", "('payload',)\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ExceptionProtocolsAwaitActualHostWork(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var mock = new MockLythonHost(); mock.SeedFile("/data", "payload");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/data", "payload");
            using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(mock))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
                if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("str")]
    [InlineData("repr")]
    [InlineData("args")]
    public async Task SuspendedExceptionProtocolsCancelAndDrainBeforeReuse(string operation)
    {
        var source = operation == "args"
            ? "class Args:\n def __iter__(self):\n  with open('/data') as f:yield f.read()\nerror=ValueError();error.args=Args()"
            : "class Message:\n def __" + operation + "__(self):\n  with open('/data') as f:return f.read()\n" + operation + "(ValueError(Message()))";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/data", "payload");
        var entered = host.PauseReadUntilCancellation("/data");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
            if (ReferenceEquals(first, pending))
            {
                using var early = await pending;
                Assert.Fail(early.Failure?.Message ?? "Exception protocol completed before its host read.");
            }
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        var ready = new DelayedLythonHost(); ready.SeedFile("/data", "payload");
        using (var reused = await script.RunAsync(ready)) Assert.True(reused.Success, reused.Failure?.Message);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("None", 0)]
    [InlineData("False", 0)]
    [InlineData("True", 1)]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    public async Task SystemExitUsesItsCurrentCode(string value, int expected)
    {
        foreach (var source in new[] { "raise SystemExit(" + value + ")", "error=SystemExit(7);error.code=" + value + "\nraise error" })
        {
            using var script = new LythonEngine().Compile(source);
            Assert.True(script.IsValid);
            foreach (var asynchronous in new[] { false, true })
            {
                using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
                Assert.False(result.Success);
                Assert.Equal("SystemExit", result.Failure?.ExceptionType);
                Assert.Equal(expected, result.ExitCode);
            }
        }
    }
}
