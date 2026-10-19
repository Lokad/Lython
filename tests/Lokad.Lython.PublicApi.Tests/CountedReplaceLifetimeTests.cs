using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CountedReplaceLifetimeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("old")]
    [InlineData("new")]
    [InlineData("count")]
    public async Task ArgumentsAwaitOrDrainBeforeReplacementAndRefund(string? phase)
    {
        const string source = """
def old():
 with open('old.txt','a') as target:target.write('old')
 return 'a'
def replacement():
 with open('new.txt','a') as target:target.write('new')
 return 'bb'
def limit():
 with open('count.txt','a') as target:target.write('count')
 return 1
print('a😀a'.replace(old(),replacement(),limit()))
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        var phases = new[] { "old", "new", "count" };
        foreach (var name in phases) host.SeedFile("/" + name + ".txt", "before");
        using var cancellation = new CancellationTokenSource();
        var entered = phase is null ? null : host.PauseWriteUntilCancellation("/" + phase + ".txt");
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Replacement completed before its argument suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(phase is null, result.Success);
            Assert.Equal(phase is null ? "bb😀a\n" : "", result.StandardOutput);
            var completed = phase is null ? phases.Length : Array.IndexOf(phases, phase);
            for (var i = 0; i < phases.Length; i++)
                Assert.Equal(i < completed ? "before" + phases[i] : "before", host.ReadText("/" + phases[i] + ".txt"));
            if (phase is not null) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("bind")]
    [InlineData("call")]
    public async Task GuestDescriptorBindingAndCallStillAwait(string? phase)
    {
        const string source = """
class Text:
 @property
 def replace(self):
  with open('bind.txt','a') as target:target.write('bind')
  def convert(old,new,count):
   with open('call.txt','a') as target:target.write('call')
   return 'custom'
  return convert
print(Text().replace('a','b',1))
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/bind.txt", "before");
        host.SeedFile("/call.txt", "before");
        using var cancellation = new CancellationTokenSource();
        var entered = phase is null ? null : host.PauseWriteUntilCancellation("/" + phase + ".txt");
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Guest replacement completed before suspension.");
                }
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(phase is null, result.Success);
            Assert.Equal(phase is null ? "custom\n" : "", result.StandardOutput);
            Assert.Equal(phase == "bind" ? "before" : "beforebind", host.ReadText("/bind.txt"));
            Assert.Equal(phase is null ? "beforecall" : "before", host.ReadText("/call.txt"));
            if (phase is not null) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacementAdmissionFailureIsTerminal(bool exposed)
    {
        var source = "text='a'*4096\nreplacement='b'*4096\n" + (exposed ? "method=text.replace\n" : "") +
            "print('ready')\ntry:result=" + (exposed ? "method('a',replacement,4096)" : "text.replace('a',replacement,4096)") +
            "\nexcept BaseException:print('caught')\nprint('after')";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(128 * 1024) };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("ready\n", result.StandardOutput);
                Assert.True(result.DeniedReservationBytes > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StoredMethodsAndCountedResultsKeepSharedOwners(bool exposed)
    {
        var source = "text='a'*4096\n" + (exposed ? "method=text.replace\ntext='changed'\nreturn method('a','b',4096)" : "return text.replace('a','b',4096)");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var first = script.Run(new MockLythonHost()))
        {
            Assert.True(first.Success, first.Failure?.Message);
            Assert.Equal(new string('b', 4096), first.ReturnValue);
            var held = budget.ReservedBytes;
            Assert.True(held > baseline);
            using (var second = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.Equal(new string('b', 4096), second.ReturnValue);
                Assert.True(budget.ReservedBytes > held);
            }
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task CompleteTextReportPublishesIndependentOutput()
    {
        const string source = """
def edit(line):return line.replace('old','new',1)
with open('input.txt') as source:
 with open('report.txt','w',newline='') as target:
  for line in source:target.write(edit(line))
print('written')
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            const string input = "old-old\nplain\né-old-old\n";
            if (delayed is not null) delayed.SeedFile("/input.txt", input);
            else mock!.SeedFile("/input.txt", input);
            using (var result = asynchronous ? await script.RunAsync(delayed!) : script.Run(mock!))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("written\n", result.StandardOutput);
                Assert.Equal("new-old\nplain\né-new-old\n", delayed?.ReadText("/report.txt") ?? mock!.ReadText("/report.txt"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task CountProtocolKeepsManagedRecursionProtection()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("class Count:\n def __index__(self):return self.__index__()\nprint('a'.replace('a','b',Count()))");
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
}
