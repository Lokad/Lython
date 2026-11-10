using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ReceiverFunctionLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapingMethodArgumentStaysFundedAcrossAnotherExecution(bool exactPositional)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile($$"""
            class Reader:
                def keep(self,value{{(exactPositional ? "" : ",*,unused=None")}}):return value
            keep=Reader().keep
            return keep('x'*8192)
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var first = script.Run(new MockLythonHost()))
        {
            Assert.True(first.Success, first.Failure?.Message);
            var retained = budget.ReservedBytes;
            Assert.True(retained > baseline);
            using (var second = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.Equal(new string('x', 8192), second.ReturnValue);
                Assert.Equal(new string('x', 8192), first.ReturnValue);
                Assert.True(budget.ReservedBytes > retained);
            }
            Assert.Equal(retained, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DynamicMethodOperandAwaitsCancellationAndReusesBudget(bool asynchronousRerun)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("""
            class Value:
                def __add__(self,suffix):
                    with open('/gate.txt') as source:return source.read()+suffix
            class Reader:
                def convert(self,value):return value+'!'
            convert=Reader().convert
            return convert(Value())
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/gate.txt", "kept");
        using var cancellation = new CancellationTokenSource();
        var entered = host.PauseReadUntilCancellation("/gate.txt");
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        LythonExecutionResult? result = null;
        try
        {
            await entered.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Contains("execution canceled", result.Failure?.Message ?? "", StringComparison.Ordinal);
        }
        finally
        {
            cancellation.Cancel();
            result ??= await pending.WaitAsync(TimeSpan.FromSeconds(5));
            result.Dispose();
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        var immediate = new MockLythonHost();
        immediate.SeedFile("/gate.txt", "kept");
        using (var completed = asynchronousRerun ? await script.RunAsync(immediate) : script.Run(immediate))
        {
            Assert.True(completed.Success, completed.Failure?.Message);
            Assert.Equal("kept!", completed.ReturnValue);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task MethodAdmissionRefusalRemainsTerminal(bool asynchronous, bool finallyReturn)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        var source = "class Reader:\n def convert(self,value):return value.lower()\nconvert=Reader().convert\n" +
            "value='A'*65536\nprint('ready')\ntry:\n result=convert(value)\nexcept MemoryError:\n print('caught')\n" +
            (finallyReturn ? "finally:\n return 'suppressed'\n" : "") + "print('after')\nreturn result";
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        ILythonMemoryReservation? padding = null;
        var host = new MockLythonHost
        {
            OnStandardOutputWrite = () =>
            {
                if (padding is null) Assert.True(budget.TryReserve(1024 * 1024 - budget.ReservedBytes - 4096, out padding));
            }
        };
        try
        {
            using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.True(result.FailureAttribution?.Requested >= 65536);
            Assert.Equal("ready\n", result.StandardOutput);
            Assert.NotNull(padding);
        }
        finally { padding?.Dispose(); }
        Assert.Equal(baseline, budget.ReservedBytes);
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(finallyReturn ? "suppressed" : new string('a', 65536), result.ReturnValue);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
