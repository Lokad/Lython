using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DirectFloatTextLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArgumentIoKeepsCapturedCallableAndDrainsBeforeRefund(bool cancel)
    {
        const string source = """
def value():
 global float
 text=' '*4096+'1.25'
 with open('argument.txt','a') as target:target.write('value')
 float=lambda value:9.0
 return text
print(float(value()),float('2.5'))
""";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/argument.txt", "before");
        using var cancellation = new CancellationTokenSource();
        var entered = cancel ? host.PauseWriteUntilCancellation("/argument.txt") : null;
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Float argument did not suspend.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "" : "1.25 9.0\n", result.StandardOutput);
            Assert.Equal(cancel ? "before" : "beforevalue", host.ReadText("/argument.txt"));
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
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
    public async Task CompleteCsvConversionPublishesIndependentJsonGolden()
    {
        const string source = """
import csv,json
def amount(row):return float(row['amount'])
with open('input.csv',newline='') as source:
 values=[amount(row) for row in csv.DictReader(source)]
with open('report.json','w',newline='') as target:
 json.dump({'count':len(values),'total':sum(values)},target,sort_keys=True)
print('written')
""";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            const string input = "name,amount\nfirst,1.25\nsecond,2.50\nthird,-0.75\n";
            if (delayed is not null) delayed.SeedFile("/input.csv", input);
            else mock!.SeedFile("/input.csv", input);
            using (var result = asynchronous ? await script.RunAsync(delayed!) : script.Run(mock!))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("written\n", result.StandardOutput);
                Assert.Equal("{\"count\": 3, \"total\": 3.0}", delayed?.ReadText("/report.json") ?? mock!.ReadText("/report.json"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParsedInputRemainsFundedWhileAnotherExecutionUsesTheRoot(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("text=' '*4096+'1.25'\nassert float(text)==1.25\nreturn text", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using var held = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(held.Success, held.Failure?.Message);
        var expected = new string(' ', 4096) + "1.25";
        Assert.Equal(expected, held.ReturnValue);
        var retained = budget.ReservedBytes;
        Assert.True(retained > baseline);
        using (var second = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(second.Success, second.Failure?.Message);
            Assert.Equal(expected, second.ReturnValue);
            Assert.True(budget.ReservedBytes > retained);
            Assert.Equal(expected, held.ReturnValue);
        }
        Assert.Equal(retained, budget.ReservedBytes);
        script.Dispose();
        Assert.True(budget.ReservedBytes > 0);
        Assert.Equal(expected, held.ReturnValue);
        held.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
