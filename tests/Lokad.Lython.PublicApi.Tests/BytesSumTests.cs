using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BytesSumTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "print(sum(b''),sum(b'\\x00\\x01\\x7f\\x80\\xff'),sum(b'\\xff'*300,5))", "0 511 76505\n" },
        { "print(sum(b'',True) is True,type(sum(b'\\x01',True)) is int,sum(b'\\xff',False))", "True True 255\n" },
        { "print(sum(b'\\x01\\xff',2**63-1),sum(b'\\xff',-(2**63)),sum(b'\\xff',2**64-1))", "9223372036854776063 -9223372036854775553 18446744073709551870\n" },
        { "start=1<<1024\nresult=sum(bytes(range(256)),start)\nprint(result-start,sum(b'',start) is start,start==1<<1024)", "32640 True True\n" },
        { "print(sum(b'\\xff',-256),sum(b'\\xff',-255),sum(b'\\xff',-254))", "-1 0 1\n" },
        { "marker=[]\nprint(sum(b'',marker) is marker,sum(b'',None) is None)", "True True\n" },
        { "for start in ['x',b'x']:\n    try: sum(b'',start)\n    except TypeError: print('start')", "start\nstart\n" },
        { "class Total:\n    def __init__(self,value): self.value=value\n    def __add__(self,item):\n        print(item)\n        return Total(self.value+item+sum(b'\\x01'))\nprint(sum(b'\\x01\\x02',Total(5)).value)", "1\n2\n10\n" },
        { "class Data:\n    def __iter__(self): return iter(b'\\x01\\x02')\nprint(sum(Data()))", "3\n" },
        { "import decimal\nprint(sum(b'\\x01\\x02',0.5),sum(b'\\x01\\x02',1+2j),sum(b'\\x01\\x02',decimal.Decimal('0.5')))", "3.5 (4+2j) 3.5\n" },
        { "print(sum(b'\\x01',start=4),sum(*[b'\\x01'],**{'start':-1}))\ntry: sum(iterable=b'\\x01')\nexcept TypeError: print('positional')", "5 0\npositional\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ExactBytesAndDynamicFallbacksPreservePythonResults(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task RetainedMagnitudeResultsRemainChargedAcrossAnotherExecution()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("return sum(b'\\xff'*4096, start)", budget);
        var baseline = budget.ReservedBytes;
        var start = BigInteger.One << 200000;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["start"] = start } };
        using var first = script.Run(new MockLythonHost(), options);
        Assert.True(first.Success, first.Failure?.Message);
        Assert.Equal(start + 255 * 4096, first.ReturnValue);
        Assert.True(budget.ReservedBytes > baseline);
        using var second = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(second.Success, second.Failure?.Message);
        Assert.Equal(start + 255 * 4096, second.ReturnValue);
        first.Dispose();
        Assert.True(budget.ReservedBytes > baseline);
        second.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DynamicStartStillAwaitsHostIoAndCancellation(bool cancel)
    {
        const string source = """
class Total:
    def __init__(self,value): self.value=value
    def __add__(self,item):
        with open('events.txt','a') as target: target.write(str(item)+'\n')
        return Total(self.value+item)
return sum(b'\x01\x02',Total(5)).value
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/events.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation("/events.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                await entered.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                Assert.Equal("before", host.ReadText("/events.txt"));
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (cancel)
            {
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Equal("before", host.ReadText("/events.txt"));
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new BigInteger(8), result.ReturnValue);
                Assert.Equal("before1\n2\n", host.ReadText("/events.txt"));
            }
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
