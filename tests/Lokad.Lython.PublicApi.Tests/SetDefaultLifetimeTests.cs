using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SetDefaultLifetimeTests
{
    [Theory]
    [InlineData("hash", false)]
    [InlineData("hash", true)]
    [InlineData("equality", false)]
    [InlineData("equality", true)]
    public async Task CancellationDuringKeyCallbacksFinishesBeforeAllowanceReturns(string phase, bool asynchronous)
    {
        // print writes its value and terminator separately. Signal after one
        // complete write so cancellation cannot race the expected newline.
        var source = CallbackSource("print('working\\n',end='')\nwhile True:pass", phase);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            using var cancellation = new CancellationTokenSource();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var host = new MockLythonHost { OnStandardOutputWrite = () => entered.TrySetResult() };
            var options = new LythonRunOptions { CancellationToken = cancellation.Token };
            var pending = asynchronous ? script.RunAsync(host, options)
                : Task.Factory.StartNew(() => script.Run(host, options), CancellationToken.None,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default);
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
            }
            finally
            {
                await cancellation.CancelAsync();
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Equal("working\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("hash", false)]
    [InlineData("hash", true)]
    [InlineData("equality", false)]
    [InlineData("equality", true)]
    public async Task RefusedKeyCallbackCannotSuppressAdmissionFailure(string phase, bool asynchronous)
    {
        var source = CallbackSource("if fail:\n try:value='x'*2000000\n except MemoryError:print('caught')", phase);
        var budget = new LythonMemoryBudget(512 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var fail in new[] { true, false })
            {
                var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["fail"] = fail } };
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
                {
                    Assert.Equal(!fail, result.Success);
                    if (fail) Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                    else Assert.Equal(new BigInteger(phase == "hash" ? 7 : 3), result.ReturnValue);
                    Assert.Empty(result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturnedDefaultAliasesStayFunded(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile("value=[7]\nd={}\ngot=d.setdefault('x',value)\nreturn [d,got,got is value]", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                var values = Assert.IsType<List<object?>>(result.ReturnValue);
                Assert.Equal(new List<object?> { new BigInteger(7) }, values[1]);
                Assert.Equal(true, values[2]);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static string CallbackSource(string body, string phase)
        => "class Key:\n def __hash__(self):\n" + (phase == "hash" ? Indent(body, 2) : "")
            + "  return 1\n def __eq__(self,other):\n" + (phase == "equality" ? Indent(body, 2) : "")
            + "  return True\nd={}\n" + (phase == "equality" ? "d.setdefault(Key(),3)\n" : "")
            + "return d.setdefault(Key(),7)\n";

    private static string Indent(string body, int count)
        => new string(' ', count) + body.Replace("\n", "\n" + new string(' ', count)) + "\n";
}
