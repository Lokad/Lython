using System.Collections;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExceptionArgumentLifetimeTests
{
    private static string DiagnosticSource(string type)
    {
        var method = type == "KeyError" ? "__repr__" : "__str__";
        return "import csv\nclass Message:\n def " + method + "(self):\n  target=open('diagnostic.txt','w')\n  target.write('payload')\n  print('render')\n  return 'payload'\nerror=" + type +
            "(Message())\nprint('constructed')\ntry:raise error\nfinally:print('cleanup')";
    }

    [Theory]
    [InlineData("ValueError")]
    [InlineData("KeyError")]
    [InlineData("csv.Error")]
    public async Task UnhandledArgumentsRenderAfterCleanupAndAwaitWriterPublication(string type)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(DiagnosticSource(type), budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            using (var result = asynchronous ? await script.RunAsync(delayed!) : script.Run(mock!))
            {
                Assert.False(result.Success);
                Assert.Equal(type == "csv.Error" ? "Error" : type, result.Failure?.ExceptionType);
                Assert.Equal("payload", result.Failure?.Message);
                Assert.Equal("constructed\ncleanup\nrender\n", result.StandardOutput);
                Assert.Equal("payload", delayed?.ReadText("/diagnostic.txt") ?? mock!.ReadText("/diagnostic.txt"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task CancelledDiagnosticPublicationDrainsBeforeRefund()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(DiagnosticSource("ValueError"), budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/diagnostic.txt", "before");
        var entered = host.PauseWriteUntilCancellation("/diagnostic.txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
            if (ReferenceEquals(first, pending))
            {
                using var early = await pending;
                Assert.Fail(early.Failure?.Message ?? "Failure publication completed before its write suspended.");
            }
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal("ValueError", result.Failure?.ExceptionType);
            Assert.Equal("before", host.ReadText("/diagnostic.txt"));
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("str")]
    [InlineData("message")]
    [InlineData("diagnostic")]
    public async Task ArgumentRenderingRefusalPreservesFirstResourceOrigin(string phase)
    {
        const string common = """
class Message:
 def __str__(self):
  print('before render')
  try:unused=[0]*8192
  except BaseException:print('caught')
  return 'payload'
error=ValueError(Message())
print('constructed')
""";
        var source = common + "\n" + (phase == "diagnostic" ? "raise error" :
            "try:unused=" + (phase == "str" ? "str(error)" : "error.message") + "\nexcept BaseException:print('caught render')\nprint('after')");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(64 * 1024) };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("constructed\nbefore render\n", result.StandardOutput);
                Assert.True(result.DeniedReservationBytes > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task RetainedMutableArgumentsStayFundedAcrossSharedExecutions()
    {
        const string source = "message=['é'*4096]\nerror=ValueError(message)\nreturn error.args[0]";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var first = script.Run(new MockLythonHost()))
        {
            Assert.True(first.Success, first.Failure?.Message);
            var expected = new string('é', 4096);
            Assert.Equal(expected, Assert.Single(Assert.IsAssignableFrom<IList>(first.ReturnValue).Cast<object>()));
            var held = budget.ReservedBytes;
            Assert.True(held > baseline);
            using (var second = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.Equal(expected, Assert.Single(Assert.IsAssignableFrom<IList>(second.ReturnValue).Cast<object>()));
                Assert.True(budget.ReservedBytes > held);
            }
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
