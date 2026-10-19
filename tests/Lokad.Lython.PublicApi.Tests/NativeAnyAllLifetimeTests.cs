using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeAnyAllLifetimeTests
{
    [Fact]
    public async Task CompleteBinaryScanPublishesIndependentJsonGolden()
    {
        const string source = """
import json
with open('input.bin','rb') as source:payload=source.read()
with open('report.json','w',newline='') as target:
 json.dump({'present':any(payload),'nonzero':all(payload),'size':len(payload)},target,sort_keys=True)
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
            byte[] input = [1, 2, 0, 4];
            if (delayed is not null) delayed.SeedBytes("/input.bin", input);
            else mock!.SeedBytes("/input.bin", input);
            using (var result = asynchronous ? await script.RunAsync(delayed!) : script.Run(mock!))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("written\n", result.StandardOutput);
                Assert.Equal("{\"nonzero\": false, \"present\": true, \"size\": 4}", delayed?.ReadText("/report.json") ?? mock!.ReadText("/report.json"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("any", null)]
    [InlineData("any", "first")]
    [InlineData("any", "second")]
    [InlineData("all", null)]
    [InlineData("all", "first")]
    [InlineData("all", "second")]
    public async Task GuestIteratorAwaitsAndDrainsWithoutConsumingLaterItems(string operation, string? pause)
    {
        var source = "payload='held'*1024\nclass Values:\n def __iter__(self):\n  with open('first.txt','a') as target:target.write('first')\n  yield "
            + (operation == "any" ? "False" : "True")
            + "\n  with open('second.txt','a') as target:target.write('second')\n  yield " + (operation == "any" ? "True" : "False")
            + "\n  with open('later.txt','a') as target:target.write('later')\nprint(" + operation + "(Values()))\nreturn payload";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/first.txt", "before");
        host.SeedFile("/second.txt", "before");
        host.SeedFile("/later.txt", "before");
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
                    Assert.Fail(early.Failure?.Message ?? "Iterator pull did not suspend.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(pause is null, result.Success);
            Assert.Equal(pause is null ? (operation == "any" ? "True\n" : "False\n") : "", result.StandardOutput);
            Assert.Equal("before", host.ReadText("/later.txt"));
            Assert.Equal(pause == "first" ? "before" : "beforefirst", host.ReadText("/first.txt"));
            Assert.Equal(pause is null ? "beforesecond" : "before", host.ReadText("/second.txt"));
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeBytesRemainFundedWhileAnotherExecutionUsesTheRoot(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("payload=b'\\x01'*4096\nassert any(payload)\nassert all(payload)\nreturn payload", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using var held = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(held.Success, held.Failure?.Message);
        var expected = Enumerable.Repeat((byte)1, 4096).ToArray();
        Assert.Equal(expected, Assert.IsType<byte[]>(held.ReturnValue));
        var retained = budget.ReservedBytes;
        Assert.True(retained > baseline);
        using (var second = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(second.Success, second.Failure?.Message);
            Assert.Equal(expected, Assert.IsType<byte[]>(second.ReturnValue));
            Assert.True(budget.ReservedBytes > retained);
            Assert.Equal(expected, Assert.IsType<byte[]>(held.ReturnValue));
        }
        Assert.Equal(retained, budget.ReservedBytes);
        script.Dispose();
        Assert.True(budget.ReservedBytes > 0);
        Assert.Equal(expected, Assert.IsType<byte[]>(held.ReturnValue));
        held.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
