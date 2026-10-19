using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Cancellation deadlines need a quiet scheduler, like the lifetime controls.
[Collection("RuntimeStress")]
public sealed class CooperativeCancellationTests
{
    public static TheoryData<bool, string> PureLoops => new()
    {
        { false, "while True:\n    pass" },
        { true, "while True:\n    pass" },
        { false, "def loop():\n    while True:\n        pass\nloop()" },
        { true, "def loop():\n    while True:\n        pass\nloop()" },
        { false, "while True:\n    try:\n        while True:\n            pass\n    except BaseException:\n        pass" },
        { true, "while True:\n    try:\n        while True:\n            pass\n    except BaseException:\n        pass" },
        { false, "try:\n    while True:\n        pass\nfinally:\n    pass" },
        { true, "try:\n    while True:\n        pass\nfinally:\n    pass" },
        { false, "for i in range(2147483647):\n    pass" },
        { true, "for i in range(2147483647):\n    pass" },
    };

    [Theory]
    [MemberData(nameof(PureLoops))]
    public async Task ActiveCancellationStopsPureLoopsWithNormalLimits(bool asynchronous, string loop)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("print('started')\n" + loop);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Message)));
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = new MockLythonHost { OnStandardOutputWrite = () => entered.TrySetResult(true) };
        const long memoryLimit = 16L * 1024 * 1024;
        var options = new LythonRunOptions
        {
            CancellationToken = cancellation.Token,
            MemoryBudget = new LythonMemoryBudget(memoryLimit),
        };
        // The synchronous host caller must start independently of a busy test
        // thread pool; its queue delay is not guest cancellation latency.
        var pending = asynchronous ? script.RunAsync(host, options)
            : Task.Factory.StartNew(() => script.Run(host, options), CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
        LythonExecutionResult? result = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(25);
            Assert.False(pending.IsCompleted, "The pure loop must still be running when the host cancels it.");
            cancellation.Cancel();
            result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Contains("execution canceled", result.Failure?.Message ?? "", StringComparison.Ordinal);
            Assert.Equal("started\n", result.StandardOutput);
            Assert.True(result.PeakExecutionMemoryBytes <= memoryLimit);
        }
        finally
        {
            cancellation.Cancel();
            // Even a failed startup/latency assertion must drain the active run
            // before releasing its compilation and result ownership.
            result ??= await pending.WaitAsync(TimeSpan.FromSeconds(5));
            result.Dispose();
        }
    }
}
