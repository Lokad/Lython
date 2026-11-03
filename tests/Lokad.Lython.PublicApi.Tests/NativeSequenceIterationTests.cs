using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeSequenceIterationTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task RepeatedNestedLoopsPreserveControlFlowAndGeneratorValues(bool asynchronous, bool tuple)
    {
        var source = "values=" + (tuple ? "(1,2,3)" : "[1,2,3]") + "\n" + """
            seen=[]
            for outer in (0,1,2):
                try:
                    for item in values:
                        if item==2:continue
                        seen.append((outer,item))
                        if outer==1:break
                finally:
                    seen.append(('end',outer))
            print(seen)
            print(list(x+y for x in values for y in (4,5)))
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("[(0, 1), (0, 3), ('end', 0), (1, 1), ('end', 1), (2, 1), (2, 3), ('end', 2)]\n[5, 6, 6, 7, 7, 8]\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task CancellationInterruptsRepeatedNonAllocatingLoops(bool asynchronous, bool tuple)
    {
        var source = "values=" + (tuple ? "(1,2,3)" : "[1,2,3]")
            + "\nprint('started')\nwhile True:\n for item in values:pass";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using var cancellation = new CancellationTokenSource();
        using var entered = new ManualResetEventSlim();
        var host = new MockLythonHost { OnStandardOutputWrite = entered.Set };
        var options = new LythonRunOptions { CancellationToken = cancellation.Token };
        var pending = asynchronous ? Task.Run(() => script.RunAsync(host, options)) : Task.Run(() => script.Run(host, options));
        var observed = false;
        var completedBeforeCancellation = false;
        var controller = new Thread(() =>
        {
            observed = entered.Wait(TimeSpan.FromSeconds(10));
            if (observed) Thread.Sleep(100);
            completedBeforeCancellation = pending.IsCompleted;
            cancellation.Cancel();
        }) { IsBackground = true };
        controller.Start();
        try
        {
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(controller.Join(TimeSpan.FromSeconds(1)));
            Assert.True(observed);
            Assert.False(completedBeforeCancellation);
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Equal("started\n", result.StandardOutput);
        }
        finally
        {
            cancellation.Cancel();
            entered.Set();
            using var drained = await pending;
            Assert.True(controller.Join(TimeSpan.FromSeconds(1)));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
