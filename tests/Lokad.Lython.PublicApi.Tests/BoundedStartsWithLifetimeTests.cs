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
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("class Bound:\n def __index__(self):return self.__index__()\nprint('abc'.startswith('a',Bound()))");
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
    [InlineData("early", 1, "needle result\n")]
    [InlineData("late", 8, "needle result\n")]
    [InlineData("missing", 8, "last unterminated")]
    [InlineData("missing-terminated", 8, "last terminated\n")]
    [InlineData("rebound", 1, "special!\n")]
    public async Task ProtectedPrefixSearchKeepsTheLastLineAndCloses(string shape, int expectedCount, string expectedLast)
    {
        const string source = """
def wanted(line):
    return line.startswith('needle')
def alternate(line):
    return line.endswith('!\n')
if rebound:
    wanted = alternate
scanned = 0
match = ''
with open('input.txt') as source:
    for line in source:
        scanned += 1
        if wanted(line):
            match = line
            break
print(scanned, line == match)
with open('report.txt', 'w', newline='') as output:
    output.write(line)
""";
        var prefix = "short café\r\nsecond line\rthird line\n" + new string('x', 9000) + "\n"
            + "after boundary\nnext discarded\nlast discarded\n";
        var input = shape switch
        {
            "early" => "needle result\n" + prefix,
            "late" => prefix + "needle result\r\nignored\n",
            "missing" => prefix + "last unterminated",
            "missing-terminated" => prefix + "last terminated\r\n",
            _ => "special!\r\n" + prefix
        };
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["rebound"] = shape == "rebound" } };
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var immediate = asynchronous ? null : new MockLythonHost();
            if (delayed is not null) delayed.SeedFile("/input.txt", input);
            else immediate!.SeedFile("/input.txt", input);
            using (var result = asynchronous ? await script.RunAsync(delayed!, options) : script.Run(immediate!, options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal($"{expectedCount} {(shape.StartsWith("missing", StringComparison.Ordinal) ? "False" : "True")}\n", result.StandardOutput);
                Assert.Equal(expectedLast, delayed?.ReadText("/report.txt") ?? immediate!.ReadText("/report.txt"));
            }
            Assert.Equal(2, delayed?.ClosedFileCount ?? immediate!.ClosedFileCount);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task CountingLoopUsesItsGlobalIncrement()
    {
        const string source = """
STEP = 2
def search(lines):
    def wanted(line):
        return line.startswith('needle')
    scanned = 0
    for line in lines:
        scanned += STEP
        if wanted(line):
            break
    return scanned
print(search(['no','needle']))
""";
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
        using var sync = script.Run(new MockLythonHost());
        using var asynchronous = await script.RunAsync(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
        Assert.Equal("4\n", sync.StandardOutput);
        Assert.Equal(sync.StandardOutput, asynchronous.StandardOutput);
    }
}
