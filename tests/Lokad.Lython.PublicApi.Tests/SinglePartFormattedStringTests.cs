using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SinglePartFormattedStringTests
{
    [Theory]
    [InlineData("value='caf\u00e9 '+str(123456789)\nprint(f'{value}' is value,f'{value!s}' is value,f'{value:}' is value)", "True True True\n")]
    [InlineData("rendered='caf\u00e9 '+str(123456789)\nclass Value:\n def __format__(self,spec):return rendered\nprint(f'{Value()}' is rendered)", "True\n")]
    [InlineData("value='caf\u00e9 '+str(123456789)\nprint(f'{f\"{value}\"}' is value)", "True\n")]
    public async Task SinglePartsPreserveFormattedStringIdentity(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
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
        script.Dispose(); Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task ForwardedResultKeepsItsOwnerAcrossAnotherExecution()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile("return f'{value}'", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var value = new string('\u00e9', 4096);
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["value"] = value } };
        using var first = script.Run(new MockLythonHost(), options);
        using var second = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(first.Success, first.Failure?.Message); Assert.True(second.Success, second.Failure?.Message);
        Assert.Equal(value, first.ReturnValue); Assert.Equal(value, second.ReturnValue);
        Assert.True(budget.ReservedBytes > baseline);
        first.Dispose(); Assert.True(budget.ReservedBytes > baseline);
        Assert.Equal(value, second.ReturnValue);
        second.Dispose(); Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose(); Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FormatterResultStaysFundedThroughIoAndCancellation(bool cancel)
    {
        const string source = "rendered='caf\u00e9 '+str(123456789)\nclass Value:\n def __format__(self,spec):\n  with open('/events.txt','a') as output:output.write('call')\n  return rendered\nprint(f'{Value()}' is rendered)";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/events.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation("/events.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Same(entered, first); Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline); cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(cancel ? "" : "True\n", result.StandardOutput);
            Assert.Equal(cancel ? "before" : "beforecall", host.ReadText("/events.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel(); using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose(); Assert.Equal(0, budget.ReservedBytes);
    }
}
