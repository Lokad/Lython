using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExecutableCallLifetimeTests
{
    private const string Source = """
def descend(n,payload):
 if n==0:
  with open('call.txt','a') as target:target.write(payload)
  return payload
 return descend(n-1,payload)
return descend(32,'é'*4096)
""";

    [Fact]
    public async Task SuspendedCallersDrainBeforeCancellationRefund()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(Source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/call.txt", "before");
        var entered = host.PauseWriteUntilCancellation("/call.txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Equal("before", host.ReadText("/call.txt"));
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task EscapingResultsStayFundedAcrossAnotherExecutionAndCodeDisposal()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(Source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/call.txt", "");
        using (var retained = await script.RunAsync(host))
        {
            Assert.True(retained.Success, retained.Failure?.Message);
            Assert.Equal(new string('é', 4096), retained.ReturnValue);
            Assert.True(host.CompletedAsynchronously > 0);
            var held = budget.ReservedBytes;
            Assert.True(held > baseline);
            var secondHost = new MockLythonHost(); secondHost.SeedFile("/call.txt", "");
            using (var second = script.Run(secondHost))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.Equal(retained.ReturnValue, second.ReturnValue);
            }
            Assert.Equal(held, budget.ReservedBytes);
            script.Dispose();
            Assert.True(budget.ReservedBytes > 0);
            Assert.Equal(new string('é', 4096), retained.ReturnValue);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedDescendantCannotResumeAnyGuestCaller(bool asynchronous)
    {
        const string source = """
def leaf():
 print('before')
 try:unused=[0]*8192
 except BaseException:pass
 return 'value'
def descend(n):
 if n:return descend(n-1)
 return leaf()
try:print(descend(16))
except BaseException:print('caught')
print('after')
""";
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(64 * 1024);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("before\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
