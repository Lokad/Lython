using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BoundedStartsWithLifetimeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("prefix")]
    [InlineData("start")]
    [InlineData("end")]
    public async Task ArgumentsAwaitOrDrainBeforeMatchingAndRefund(string? phase)
    {
        const string source = """
text='aé😀z'*1024
def value(name,result):
 global text
 with open(name+'.txt','a') as target:target.write(name)
 text='changed'
 return result
print(text.startswith(value('prefix','é'),value('start',1),value('end',3)),text)
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        var phases = new[] { "prefix", "start", "end" };
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
                    Assert.Fail(early.Failure?.Message ?? "Matching completed before its argument suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(phase is null, result.Success);
            Assert.Equal(phase is null ? "True changed\n" : "", result.StandardOutput);
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

    [Fact]
    public async Task CompleteFileSearchPublishesOnlyTheFirstMatch()
    {
        const string source = """
def wanted(line):return line.startswith('id:',4,7)
selected=[]
with open('input.txt') as source:
 for line in source:
  if wanted(line):
   selected.append(line)
   break
with open('report.txt','w',newline='') as target:target.write(''.join(selected))
print('written')
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            const string input = "row tag:0\nrow id:é😀\nrow id:later\n";
            if (delayed is not null) delayed.SeedFile("/input.txt", input);
            else mock!.SeedFile("/input.txt", input);
            using (var result = asynchronous ? await script.RunAsync(delayed!) : script.Run(mock!))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("written\n", result.StandardOutput);
                Assert.Equal("row id:é😀\n", delayed?.ReadText("/report.txt") ?? mock!.ReadText("/report.txt"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task BoundProtocolKeepsManagedRecursionProtection()
    {
        using var script = new LythonEngine().Compile("class Bound:\n def __index__(self):return self.__index__()\nprint('abc'.startswith('a',Bound()))");
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
