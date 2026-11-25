using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DirectGlobalOperandTests
{
    [Theory]
    [InlineData("left=1\nright=2\ndef read():return left + right\ndel left\ndel right\nread()\n", "left +", "left")]
    [InlineData("class Left:\n def __add__(self,value):raise AssertionError('must not run')\nleft=Left()\nright=2\ndef read():return left + right\ndel right\nread()\n", "right\ndel", "right")]
    [InlineData("data=[1]\nindex=0\ndef read():return data[index]\ndel data\ndel index\nread()\n", "data[index]", "data")]
    public async Task MissingGlobalsKeepOperandOrderAndOriginalSpan(string source, string location, string name)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.False(result.Success);
                    Assert.Equal("NameError", result.Failure?.ExceptionType);
                    Assert.Contains(name, result.Failure!.Message);
                    var span = Assert.IsType<LythonSourceSpan>(result.Failure.Span);
                    Assert.Equal(source.IndexOf(location, StringComparison.Ordinal), span.Start);
                    Assert.Equal(name.Length, span.Length);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task ArithmeticFailurePrecedesLaterMissingGlobal()
    {
        const string source = "value=1\ndef read():return 1 // 0 + value\ndel value\nread()\n";
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.False(result.Success);
            Assert.Equal("ZeroDivisionError", result.Failure?.ExceptionType);
        }
    }

    [Fact]
    public async Task ReadsBeforeCallsKeepTheirSnapshots()
    {
        const string source = """
            marker=['original']
            data=[marker]
            events=[]
            def swap():
                global data
                events.append('swap')
                data=[['changed']]
                return 0
            def read():return data[swap()]
            assert read() is marker
            def original(value):
                events.append('original')
                return value
            def replacement(value):assert False
            target=original
            def change():
                global target
                target=replacement
                return marker
            def invoke():return target(change())
            assert invoke() is marker
            assert target is replacement
            assert events==['swap','original']
            return 'ok'
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("ok", result.ReturnValue);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingIndexProtocolKeepsItsEvaluatedGlobalsAndFunding(bool cancel)
    {
        const string source = """
            marker=['original']
            data=[marker]
            class Index:
                def __index__(self):
                    global data,key
                    with open('input.txt') as source:assert source.read()=='ready'
                    data=[['changed']]
                    key=99
                    return 0
            key=Index()
            def read():return data[key]
            observed=read()
            assert observed is marker
            return observed
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var immediate = new MockLythonHost(); immediate.SeedFile("/input.txt", "ready");
            using (var first = script.Run(immediate))
            {
                Assert.True(first.Success, first.Failure?.Message);
                var retained = budget.ReservedBytes;
                Assert.True(retained > baseline);
                var delayed = new DelayedLythonHost(); delayed.SeedFile("/input.txt", "ready");
                using var cancellation = new CancellationTokenSource();
                var entered = cancel ? delayed.PauseReadUntilCancellation("/input.txt") : null;
                var pending = script.RunAsync(delayed, cancellationToken: cancellation.Token);
                try
                {
                    if (entered is not null)
                    {
                        await entered.WaitAsync(TimeSpan.FromSeconds(10));
                        Assert.False(pending.IsCompleted);
                        Assert.True(budget.ReservedBytes > retained);
                        cancellation.Cancel();
                    }
                    using var second = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.Equal(!cancel, second.Success);
                    if (cancel) Assert.Equal(LythonFailureReason.Cancelled, second.FailureAttribution?.Reason);
                    else
                    {
                        Assert.True(delayed.CompletedAsynchronously > 0);
                        Assert.Equal(new object[] { "original" }, Assert.IsAssignableFrom<IReadOnlyList<object>>(second.ReturnValue));
                    }
                }
                finally
                {
                    cancellation.Cancel();
                    using var joined = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                }
                Assert.Equal(new object[] { "original" }, Assert.IsAssignableFrom<IReadOnlyList<object>>(first.ReturnValue));
                Assert.Equal(retained, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task ArithmeticArgumentKeepsResolvedTargetThroughProtocolIo()
    {
        const string source = """
            def increment(value): return value + 1
            def replacement(value): return value + 10
            target = increment

            class Operand:
                def __add__(self, other):
                    global target
                    target = replacement
                    with open('input.txt') as source:
                        return int(source.read())

            def invoke(value): return target(value + 1)
            print(invoke(2), invoke(Operand()), invoke(2))
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new MockLythonHost(); host.SeedFile("/input.txt", "41");
            using (var result = script.Run(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("4 42 13\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/input.txt", "41");
            using (var result = await script.RunAsync(delayed))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("4 42 13\n", result.StandardOutput);
                Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
