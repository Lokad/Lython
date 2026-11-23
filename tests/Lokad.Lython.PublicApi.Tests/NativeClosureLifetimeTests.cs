using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeClosureLifetimeTests
{
    [Fact]
    public async Task BinaryAssignmentPreservesClosureAliasesAcrossASuspendingOperator()
    {
        const string source = """
            def calculate():
                current = 4
                def read(): return current
                class Operand:
                    def __mul__(self, scale):
                        with open('/body') as stream:
                            stream.read()
                        print(read())
                        return 2**70
                def write(value):
                    nonlocal current
                    current = value * 3 + 11
                    return current
                first = write(Operand())
                return first is read(), first, write(7), read()
            return calculate()
            """;
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/body", "ready");
        using (var result = await script.RunAsync(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("4\n", result.StandardOutput);
            Assert.Equal(new object?[] { true, (BigInteger.One << 70) + 11, new BigInteger(32), new BigInteger(32) },
                Assert.IsAssignableFrom<IEnumerable<object?>>(result.ReturnValue));
        }
        Assert.Equal(1, host.ClosedFileCount);
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FactoryCapturesSurvivePromotionAndAwaitedCleanup(bool cancel)
    {
        const string source = """
            def observe(callback):
                with open('/body') as stream:
                    stream.read()
                callback(3)
            def make(seed):
                saved = [seed]
                def append(value):
                    saved.append(value)
                def reader():
                    return lambda: saved
                read = reader()
                observe(append)
                return [read() is saved, read()]
            return make(7)
            """;
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join(", ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/body", "ready");
        var entered = cancel ? host.PauseReadUntilCancellation("/body") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        try
        {
            if (entered is not null)
            {
                Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                await cancellation.CancelAsync();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(!cancel, result.Success);
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else Assert.Equal(new List<object?> { true, new List<object?> { new BigInteger(7), new BigInteger(3) } },
                result.ReturnValue);
        }
        finally
        {
            await cancellation.CancelAsync();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(1, host.ClosedFileCount);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FactoryKeepsTheSameBindingAcrossSuspensionAndRebinding(bool cancel)
    {
        const string source = """
            def pause():
                with open('/body') as stream:
                    stream.read()
            def factory(value):
                def read(): return value
                value += 1
                before = read()
                pause()
                value += 2
                return before, read
            before, read = factory(7)
            print(before, read())
            """;
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join(", ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/body", "ready");
        var entered = cancel ? host.PauseReadUntilCancellation("/body") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                await cancellation.CancelAsync();
            }

            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            if (cancel)
            {
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("8 10\n", result.StandardOutput);
            }
            Assert.Equal(1, host.ClosedFileCount);
        }
        finally
        {
            await cancellation.CancelAsync();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task RefusedCaptureEndsExecutionBeforeTheClosureCanEscape()
    {
        const string source = """
            import time
            def factory(value):
                def read(): return value
                return read
            try:
                time.time()
                read = factory(7)
                time.time()
            except MemoryError:
                time.time()
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join(", ", script.Diagnostics));
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 0);
            var options = new LythonRunOptions { MemoryBudget = budget };
            using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
            {
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.True(budget.AtGate > 0);
                Assert.Equal(0, host.AtSecondGate);
                Assert.Equal(3, result.Failure?.Span?.Line);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(20)]
    public async Task CapturedCallsKeepRealParametersSeparateFromLocalAndCellStorage(int width)
    {
        var parameters = string.Join(',', Enumerable.Range(0, width).Select(i => $"a{i}"));
        var positional = string.Join(',', Enumerable.Range(0, width));
        var named = string.Join(',', Enumerable.Range(0, width).Select(i => $"a{i}={i}"));
        var source = $"def make():\n count=0\n def apply({parameters}):\n  nonlocal count\n  count+=1\n  missing:int\n  return a0+a{width - 1}\n def read():return count\n return apply,read\napply,read=make()\nprint(apply({positional}),read(),apply({named}),read())\n";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal($"{width - 1} 1 {width - 1} 2\n", result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ProtocolFallbackObservesOneCellWriteAndKeepsWithdrawnInputs(bool asynchronous, bool cancel)
    {
        const string source = """
            def make():
                count=0
                def words(value):
                    nonlocal count
                    count+=1
                    return value.lower().split()
                def read():return count
                return words,read
            words,read=make()
            class Text:
                def __init__(self,value):self.value=value
                def lower(self):
                    global payload
                    payload=None
                    print(read())
                    with open('/body') as source:source.read()
                    return self.value
            return words(Text(payload))
            """;
        var budget = new LythonMemoryBudget(512 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var attempt in new[] { cancel, false })
            {
                var mock = new MockLythonHost(); mock.SeedFile("/body", "ready");
                var delayed = new DelayedLythonHost(); delayed.SeedFile("/body", "ready");
                var entered = attempt ? delayed.PauseReadUntilCancellation("/body") : null;
                using var cancellation = new CancellationTokenSource();
                var options = new LythonRunOptions
                {
                    CancellationToken = cancellation.Token,
                    Globals = new Dictionary<string, object?> { ["payload"] = new string('É', 4096) + " A" }
                };
                var pending = asynchronous ? script.RunAsync(delayed, options) : Task.FromResult(script.Run(mock, options));
                try
                {
                    if (entered is not null)
                    {
                        Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
                        Assert.False(pending.IsCompleted);
                        Assert.True(budget.ReservedBytes > baseline + 8192);
                        await cancellation.CancelAsync();
                    }
                    using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.Equal("1\n", result.StandardOutput);
                    Assert.Equal(!attempt, result.Success);
                    if (attempt) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                    else
                    {
                        Assert.Equal(new List<object?> { new string('É', 4096), "A" }, result.ReturnValue);
                        Assert.True(budget.ReservedBytes > baseline + 8192);
                        if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
                    }
                }
                finally
                {
                    await cancellation.CancelAsync();
                    using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                }
                if (asynchronous) Assert.Equal(1, delayed.ClosedFileCount);
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
