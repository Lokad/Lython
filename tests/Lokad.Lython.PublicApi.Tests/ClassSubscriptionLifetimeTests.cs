using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ClassSubscriptionLifetimeTests
{
    private static string HostedSource(bool descriptor) => """
payload=['é'*4096]
token=[]
def mark(path):
 with open(path,'a') as output:output.write('x')
def read(key):
 mark('item.txt')
 assert key is token
 return payload
""" + "\n" + (descriptor ? """
class Slot:
 def __get__(self,obj,owner):
  mark('get.txt')
  assert obj is None and owner is Box
  return read
class Box:__class_getitem__=Slot()
""" : """
class Box:
 def __class_getitem__(cls,key):
  assert cls is Box
  return read(key)
""") + "\nvalue=Box[token]\nassert value is payload\nreturn value";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DescriptorAndBodyAwaitActualHostWorkWithoutReplay(bool descriptor)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(HostedSource(descriptor), budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var mode in new[] { "sync", "immediate", "delayed" })
            {
                var immediate = new MockLythonHost();
                immediate.SeedFile("/get.txt", ""); immediate.SeedFile("/item.txt", "");
                var delayed = new DelayedLythonHost();
                delayed.SeedFile("/get.txt", ""); delayed.SeedFile("/item.txt", "");
                var options = new LythonRunOptions { MemoryBudget = budget, ResultMemoryBudget = budget };
                using (var result = mode == "sync" ? script.Run(immediate, options)
                    : await script.RunAsync(mode == "delayed" ? delayed : immediate, options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(new string('é', 4096), Assert.Single(Assert.IsType<List<object?>>(result.ReturnValue)));
                    Assert.True(budget.ReservedBytes > baseline);
                    Assert.Equal("x", mode == "delayed" ? delayed.ReadText("/item.txt") : immediate.ReadText("/item.txt"));
                    Assert.Equal(descriptor ? "x" : "", mode == "delayed" ? delayed.ReadText("/get.txt") : immediate.ReadText("/get.txt"));
                    if (mode == "delayed") Assert.True(delayed.CompletedAsynchronously > 0);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, "/item.txt")]
    [InlineData(true, "/get.txt")]
    [InlineData(true, "/item.txt")]
    public async Task CancellationDrainsTheCurrentLookupOrBodyBeforeRefund(bool descriptor, string pause)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(HostedSource(descriptor), budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost(); host.SeedFile("/get.txt", ""); host.SeedFile("/item.txt", "");
            var entered = host.PauseWriteUntilCancellation(pause);
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }, cancellation.Token);
            try
            {
                Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Null(result.ReturnValue);
                Assert.Equal("", host.ReadText(pause));
            }
            finally
            {
                cancellation.Cancel();
                using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusalDuringBindingOrInvocationCannotBeSuppressed(bool descriptor)
    {
        const string body = "print('before')\ntry:unused=[0]*16384\nexcept BaseException:pass\nfinally:return None\n";
        var source = descriptor
            ? "class Slot:\n def __get__(self,obj,owner):\n" + string.Join("\n", body.Split('\n').Select(line => "  " + line))
                + "\nclass Box:__class_getitem__=Slot()\n"
            : "class Box:\n def __class_getitem__(cls,key):\n" + string.Join("\n", body.Split('\n').Select(line => "  " + line)) + "\n";
        source += "try:Box[0]\nexcept BaseException:print('caught')\nprint('after')";
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var budget = new LythonMemoryBudget(64 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            var options = new LythonRunOptions { MemoryBudget = budget };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("before\n", result.StandardOutput);
                Assert.True(result.DeniedReservationBytes > 0);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task EscapingResultsStayFundedAcrossAnotherExecutionAndCodeDisposal()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(HostedSource(true), budget);
        var options = new LythonRunOptions { MemoryBudget = budget, ResultMemoryBudget = budget };
        using var first = script.Run(new MockLythonHost(), options);
        Assert.True(first.Success, first.Failure?.Message);
        using var second = await script.RunAsync(new DelayedLythonHost(), options);
        Assert.True(second.Success, second.Failure?.Message);
        script.Dispose();
        Assert.True(budget.ReservedBytes > 0);
        first.Dispose();
        Assert.True(budget.ReservedBytes > 0);
        Assert.Equal(new string('é', 4096), Assert.Single(Assert.IsType<List<object?>>(second.ReturnValue)));
        second.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
