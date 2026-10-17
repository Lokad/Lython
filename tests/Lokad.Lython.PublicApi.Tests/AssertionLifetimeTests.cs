using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AssertionLifetimeTests
{
    private const string CallbackSource = """
def condition():
 with open('condition.txt','a') as target:target.write('condition')
 return False
def message():
 with open('message.txt','a') as target:target.write('message')
 return 'é'*4096
try:assert condition(),message()
except AssertionError as error:return error.args[0]
""";

    [Theory]
    [InlineData("condition")]
    [InlineData("message")]
    public async Task CancellationDrainsAssertionCallbacksBeforeRefund(string phase)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(CallbackSource, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/condition.txt", "before");
        host.SeedFile("/message.txt", "before");
        var entered = host.PauseWriteUntilCancellation("/" + phase + ".txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
            if (ReferenceEquals(first, pending))
            {
                using var early = await pending;
                Assert.Fail(early.Failure?.Message ?? "Execution finished before its assertion callback suspended.");
            }
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.Equal(phase == "message" ? "beforecondition" : "before", host.ReadText("/condition.txt"));
            Assert.Equal("before", host.ReadText("/message.txt"));
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task EscapingMessageRemainsFundedAcrossSharedExecutions()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(CallbackSource, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new MockLythonHost();
        host.SeedFile("/condition.txt", "before");
        host.SeedFile("/message.txt", "before");
        using (var first = script.Run(host))
        {
            Assert.True(first.Success, first.Failure?.Message);
            var expected = new string('é', 4096);
            Assert.Equal(expected, first.ReturnValue);
            Assert.Equal("beforecondition", host.ReadText("/condition.txt"));
            Assert.Equal("beforemessage", host.ReadText("/message.txt"));
            var held = budget.ReservedBytes;
            Assert.True(held > baseline);
            var delayed = new DelayedLythonHost();
            delayed.SeedFile("/condition.txt", "before");
            delayed.SeedFile("/message.txt", "before");
            using (var second = await script.RunAsync(delayed))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.Equal(expected, second.ReturnValue);
                Assert.True(delayed.CompletedAsynchronously > 0);
                Assert.True(budget.ReservedBytes > held);
                Assert.Equal("beforecondition", delayed.ReadText("/condition.txt"));
                Assert.Equal("beforemessage", delayed.ReadText("/message.txt"));
            }
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("condition")]
    [InlineData("message")]
    [InlineData("diagnostic")]
    public async Task RefusedAssertionStoragePreservesFirstOrigin(string phase)
    {
        const string refuse = """
def refuse():
 print('before')
 try:unused=[0]*8192
 except BaseException:print('caught')
 print('after')
 return False
""";
        var source = refuse + "\n" + (phase switch
        {
            "condition" => "try:assert refuse(),'payload'\nexcept BaseException:print('caught assertion')\nprint('after assertion')",
            "message" => "try:assert False,refuse()\nexcept BaseException:print('caught assertion')\nprint('after assertion')",
            _ => "class Message:\n def __str__(self):refuse();return 'payload'\ntry:assert False,Message()\nfinally:print('cleanup')",
        });
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 64 * 1024 };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.FailureAttribution?.Reason);
                Assert.NotNull(result.Failure?.Span);
                Assert.True(result.DeniedReservationBytes > 0);
                Assert.Equal(phase == "diagnostic" ? "cleanup\nbefore\n" : "before\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task UnhandledMessageRendersAfterCleanupAndBeforeOutputCapture()
    {
        const string source = """
class Message:
 def __str__(self):print('render');return 'payload'
try:assert False,Message()
finally:print('cleanup')
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.False(result.Success);
                Assert.Equal("AssertionError", result.Failure?.ExceptionType);
                Assert.Equal("payload", result.Failure?.Message);
                Assert.Equal("cleanup\nrender\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("None", "None")]
    [InlineData("7", "7")]
    [InlineData("'payload'", "payload")]
    public async Task UnhandledNativeMessagesRemainReadable(string expression, string expected)
    {
        using var script = new LythonEngine().Compile("assert False," + expression);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.False(result.Success);
            Assert.Equal("AssertionError", result.Failure?.ExceptionType);
            Assert.Equal(expected, result.Failure?.Message);
        }
    }

    [Fact]
    public async Task DiagnosticWritersFinishBeforeFailurePublicationAndRefund()
    {
        const string source = """
class Message:
 def __str__(self):
  target=open('diagnostic.txt','w')
  target.write('payload')
  return 'payload'
assert False,Message()
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            using (var result = asynchronous ? await script.RunAsync(delayed!) : script.Run(mock!))
            {
                Assert.False(result.Success);
                Assert.Equal("AssertionError", result.Failure?.ExceptionType);
                Assert.Equal("payload", result.Failure?.Message);
                Assert.Equal("payload", delayed?.ReadText("/diagnostic.txt") ?? mock!.ReadText("/diagnostic.txt"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task AssertionsKeepManagedRecursionProtection()
    {
        using var script = new LythonEngine().Compile("def condition():return condition()\nassert condition()");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxRecursionDepth = 16 };
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.False(result.Success);
            Assert.Contains(result.FailureAttribution?.Reason, new LythonFailureReason?[]
                { LythonFailureReason.RecursionDepthLimit, LythonFailureReason.InterpreterStackLimit });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompleteCsvAssertionsPublishIndependentReport(bool helper)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", helper ? "csv-summary-helpers.py" : "csv-summary.py"));
        source = helper ? source.Replace("    return int(value)", "    assert value,'missing amount'\n    return int(value)")
            : source.Replace("        amount = int(row['amount'])", "        assert row['amount'],'missing amount'\n        amount = int(row['amount'])");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            const string input = "kind,amount,note\nred,3,x\nblue,2,y\nred,-1,z\nblue,5,w\n";
            if (delayed is not null) delayed.SeedFile("/input.csv", input);
            else mock!.SeedFile("/input.csv", input);
            using (var result = asynchronous ? await script.RunAsync(delayed!) : script.Run(mock!))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("3\n", result.StandardOutput);
                Assert.Equal("kind,total\nblue,7\nred,3\n", delayed?.ReadText("/report.csv") ?? mock!.ReadText("/report.csv"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}
