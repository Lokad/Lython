using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeTruthLifetimeTests
{
    [Fact]
    public async Task CompleteCsvFilterPublishesIndependentJsonGolden()
    {
        const string source = """
import csv,json
def accepted(row):return all((row['status']=='ok',float(row['amount'])>0))
with open('input.csv',newline='') as source:
 count=sum(1 for row in csv.DictReader(source) if accepted(row))
with open('report.json','w',newline='') as target:json.dump({'accepted':count},target,sort_keys=True)
print('written')
""";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            const string input = "status,amount\nok,1.5\nbad,2\nok,3\nok,0\n";
            if (delayed is not null) delayed.SeedFile("/input.csv", input);
            else mock!.SeedFile("/input.csv", input);
            using (var result = asynchronous ? await script.RunAsync(delayed!) : script.Run(mock!))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("written\n", result.StandardOutput);
                Assert.Equal("{\"accepted\": 2}", delayed?.ReadText("/report.json") ?? mock!.ReadText("/report.json"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("any", null)]
    [InlineData("any", "bind")]
    [InlineData("any", "call")]
    [InlineData("all", null)]
    [InlineData("all", "bind")]
    [InlineData("all", "call")]
    public async Task AggregateTruthAwaitsAllUsersBeforeRefund(string operation, string? pause)
    {
        var source = "payload='held'*1024\nclass Value:\n @property\n def __bool__(self):\n  with open('bind.txt','a') as target:target.write('bind')\n  def answer():\n   with open('call.txt','a') as target:target.write('call')\n   return True\n  return answer\nassert "
            + operation + "([" + (operation == "any" ? "False" : "True") + ",Value()])\nreturn payload";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/bind.txt", "before");
        host.SeedFile("/call.txt", "before");
        var entered = pause is null ? null : host.PauseWriteUntilCancellation("/" + pause + ".txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Truth callback did not suspend.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(pause is null, result.Success);
            Assert.Empty(result.StandardOutput);
            Assert.Equal(pause == "bind" ? "before" : "beforebind", host.ReadText("/bind.txt"));
            Assert.Equal(pause is null ? "beforecall" : "before", host.ReadText("/call.txt"));
            if (pause is null)
            {
                Assert.Equal(string.Concat(Enumerable.Repeat("held", 1024)), result.ReturnValue);
                Assert.True(budget.ReservedBytes > baseline);
                Assert.True(host.CompletedAsynchronously > 0);
            }
            else Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("bool", false)]
    [InlineData("bool", true)]
    [InlineData("any", false)]
    [InlineData("any", true)]
    [InlineData("all", false)]
    [InlineData("all", true)]
    public async Task RefusedTruthAdmissionIsTerminalWithoutRefundingOtherResults(string operation, bool asynchronous)
    {
        var argument = operation == "bool" ? "Value()" : "[Value()]";
        var source = "class Value:\n def __bool__(self):\n  with open('effect.txt','a') as target:target.write('before')\n  storage='x'*2000000\n  return True\ntry: result=" + operation
            + "(" + argument + ")\nexcept MemoryError:print('recovered')\nprint('after')";
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        using var retainedScript = new LythonEngine().Compile("return 'held'*1024", budget);
        Assert.True(script.IsValid);
        using var held = retainedScript.Run(new MockLythonHost());
        Assert.True(held.Success, held.Failure?.Message);
        var baseline = budget.ReservedBytes;
        var delayed = new DelayedLythonHost();
        var mock = new MockLythonHost();
        delayed.SeedFile("/effect.txt", "");
        mock.SeedFile("/effect.txt", "");
        using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(mock))
        {
            Assert.False(result.Success);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.Equal("before", asynchronous ? delayed.ReadText("/effect.txt") : mock.ReadText("/effect.txt"));
            Assert.Equal(string.Concat(Enumerable.Repeat("held", 1024)), held.ReturnValue);
            if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        retainedScript.Dispose();
        Assert.True(budget.ReservedBytes > 0);
        held.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
