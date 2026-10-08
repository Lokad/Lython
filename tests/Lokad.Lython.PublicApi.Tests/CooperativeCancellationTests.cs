using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

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
        var script = new LythonEngine().Compile("print('started')\n" + loop);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Message)));
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = new MockLythonHost { OnStandardOutputWrite = () => entered.TrySetResult(true) };
        const long memoryLimit = 16L * 1024 * 1024;
        var options = new LythonRunOptions
        {
            CancellationToken = cancellation.Token,
            MaxExecutionMemoryBytes = memoryLimit,
        };
        var pending = asynchronous ? script.RunAsync(host, options) : Task.Run(() => script.Run(host, options));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(25);
            Assert.False(pending.IsCompleted, "The pure loop must still be running when the host cancels it.");
            cancellation.Cancel();
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Contains("execution canceled", result.Failure?.Message ?? "", StringComparison.Ordinal);
            Assert.Equal("started\n", result.StandardOutput);
            Assert.True(result.PeakExecutionMemoryBytes <= memoryLimit);
        }
        finally
        {
            cancellation.Cancel();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitFuelRemainsEnforcedWhenDefaultLimitsAreDisabled(bool asynchronous)
    {
        var script = new LythonEngine().Compile("while True:\n    pass");
        var options = new LythonRunOptions { MaxExecutionSteps = 50, DisableDefaultLimits = true };
        var pending = asynchronous ? script.RunAsync(new MockLythonHost(), options)
            : Task.Run(() => script.Run(new MockLythonHost(), options));
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum execution step count exceeded", result.Failure?.Message ?? "", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitFuelObservesPassOnlyForIterations(bool asynchronous)
    {
        var script = new LythonEngine().Compile("for i in range(1000):\n    pass\nreturn 42");
        var options = new LythonRunOptions { MaxExecutionSteps = 50 };
        var pending = asynchronous ? script.RunAsync(new MockLythonHost(), options)
            : Task.Run(() => script.Run(new MockLythonHost(), options));
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum execution step count exceeded", result.Failure?.Message ?? "", StringComparison.Ordinal);
    }
}
