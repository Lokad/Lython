using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TimeModuleTimingTests
{
    [Fact]
    public void MonotonicCountersAndSleepUseOnlyTheOptionalHostCapability()
    {
        var host = new MockLythonHost();
        var timing = host.EnableTiming(1_250_000_000, resolutionNanoseconds: 100);

        var result = new LythonEngine().Run(
            """
import time

before = time.monotonic()
before_ns = time.monotonic_ns()
time.sleep(0.25)
after = time.perf_counter()
after_ns = time.perf_counter_ns()
mono = time.get_clock_info("monotonic")
perf = time.get_clock_info("perf_counter")
wall = time.get_clock_info("time")
return "|".join([
    str(before),
    str(before_ns),
    str(after),
    str(after_ns),
    str(mono.adjustable),
    mono.implementation,
    str(mono.monotonic),
    str(mono.resolution),
    str(perf.resolution),
    str(wall.adjustable),
    str(wall.monotonic),
])
""",
            host);

        Assert.True(result.Success, Describe(result));
        Assert.Equal(
            "1.25|1250000000|1.5|1500000000|False|Lython host monotonic clock|True|1e-07|1e-07|True|False",
            result.ReturnValue);
        Assert.Equal([TimeSpan.FromMilliseconds(250)], timing.Delays);
    }

    [Fact]
    public void MissingTimingCapabilityAndUnsupportedClocksFailExplicitly()
    {
        var result = new LythonEngine().Run(
            """
import time

values = []
for action in [
    lambda: time.monotonic(),
    lambda: time.monotonic_ns(),
    lambda: time.perf_counter(),
    lambda: time.perf_counter_ns(),
    lambda: time.sleep(0),
    lambda: time.get_clock_info("monotonic"),
    lambda: time.get_clock_info("process_time"),
    lambda: time.get_clock_info("unknown"),
    lambda: time.process_time(),
    lambda: time.thread_time_ns(),
    lambda: time.clock_gettime(1),
    lambda: time.clock_settime(1, 0),
]:
    try:
        action()
    except Exception as ex:
        values.append(ex.type)
wall = time.get_clock_info("time")
return "|".join(values) + "|" + str(wall.monotonic)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(
            "RuntimeError|RuntimeError|RuntimeError|RuntimeError|RuntimeError|RuntimeError|NotImplementedError|ValueError|NotImplementedError|NotImplementedError|NotImplementedError|NotImplementedError|False",
            result.ReturnValue);
    }

    [Fact]
    public void SleepValidatesBeforeCallingTheHost()
    {
        var host = new MockLythonHost();
        var timing = host.EnableTiming();
        var result = new LythonEngine().Run(
            """
import time

values = []
for value in ["zero", float("nan"), -1, float("inf")]:
    try:
        time.sleep(value)
    except Exception as ex:
        values.append(ex.type)
time.sleep(-0.0)
return "|".join(values)
""",
            host);

        Assert.True(result.Success, Describe(result));
        Assert.Equal("TypeError|ValueError|ValueError|OverflowError", result.ReturnValue);
        Assert.Equal([TimeSpan.Zero], timing.Delays);
    }

    [Fact]
    public void TimingReadsAndDelaysCountAgainstTheHostCallBudget()
    {
        var host = new MockLythonHost();
        var timing = host.EnableTiming(10);
        var result = new LythonEngine().Run(
            """
import time
time.monotonic_ns()
time.sleep(0.001)
time.perf_counter_ns()
""",
            host,
            new LythonRunOptions { MaxHostCalls = 2 });

        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum host call count exceeded", result.Failure?.Message, StringComparison.Ordinal);
        Assert.Single(timing.Delays);
    }

    [Fact]
    public async Task AsynchronousTimingRequiresRunAsyncAndHasMatchingResults()
    {
        var syncHost = new MockLythonHost();
        syncHost.SetTiming(new YieldingTiming());
        var sync = new LythonEngine().Run("import time\ntime.sleep(0.125)\n", syncHost);

        Assert.False(sync.Success);
        Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
        Assert.Contains("use RunAsync", sync.Failure?.Message, StringComparison.Ordinal);

        var asyncHost = new MockLythonHost();
        asyncHost.SetTiming(new YieldingTiming());
        var asyncResult = await new LythonEngine().RunAsync(
            "import time\ntime.sleep(0.125)\nreturn time.monotonic_ns()\n",
            asyncHost);

        Assert.True(asyncResult.Success, Describe(asyncResult));
        Assert.Equal(new System.Numerics.BigInteger(125_000_000), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SleepPassesRunCancellationToTheHostDelay()
    {
        using var cancellation = new CancellationTokenSource();
        var timing = new BlockingTiming();
        var host = new MockLythonHost();
        host.SetTiming(timing);

        var run = new LythonEngine().RunAsync(
            "import time\ntime.sleep(60)\n",
            host,
            cancellationToken: cancellation.Token);

        await timing.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StaticContractsRecognizeTimingCallsAndRejectWrongShapes()
    {
        var valid = new LythonEngine().Compile(
            """
import time
x = time.monotonic()
xn = time.monotonic_ns()
y = time.perf_counter()
yn = time.perf_counter_ns()
time.sleep(0)
info = time.get_clock_info("time")
time.clock_gettime(0)
time.clock_settime(0, 0)
""");
        var invalid = new LythonEngine().Compile(
            """
import time
time.monotonic(1)
time.monotonic_ns(1)
time.perf_counter(1)
time.perf_counter_ns(1)
time.sleep()
time.sleep(secs=0)
time.get_clock_info()
time.get_clock_info(name="time")
time.clock_gettime()
time.clock_settime(0)
""");

        Assert.True(valid.IsValid, string.Join(" | ", valid.Diagnostics.Select(d => d.Message)));
        Assert.False(invalid.IsValid);
        Assert.Equal(10, invalid.Diagnostics.Count(d => d.Code == "LA3151"));
    }

    private static string Describe(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message));

    private sealed class YieldingTiming : ILythonTiming
    {
        public long MonotonicNanoseconds { get; private set; }

        public long MonotonicResolutionNanoseconds => 100;

        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            MonotonicNanoseconds = checked(MonotonicNanoseconds + duration.Ticks * 100);
        }
    }

    private sealed class BlockingTiming : ILythonTiming
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public long MonotonicNanoseconds => 0;

        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            _ = duration;
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}

