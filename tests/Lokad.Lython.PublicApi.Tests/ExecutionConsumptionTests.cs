using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExecutionConsumptionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RepeatedRunsReportExistingCountersWithoutEnablingFuel(bool asynchronous, bool fuel)
    {
        var script = new LythonEngine().Compile("import sys\nprint('é')\nsys.stderr.write('😀')\nreturn 7");
        for (var i = 0; i < 3; i++)
        {
            var options = new LythonRunOptions { MaxExecutionSteps = fuel ? 1000 : null };
            var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.True(result.Success, result.Failure?.Message);
            var usage = Assert.IsType<LythonExecutionConsumption>(result.Consumption);
            Assert.Equal(3, usage.HostCalls);
            Assert.Equal(3, usage.StandardOutputBytes);
            Assert.Equal(4, usage.StandardErrorBytes);
            if (fuel) Assert.InRange(usage.ExecutionSteps!.Value, 1, 1000);
            else Assert.Null(usage.ExecutionSteps);
        }
    }

    [Theory]
    [InlineData("return 7", true)]
    [InlineData("raise RuntimeError('later')", false)]
    [InlineData("raise SystemExit(0)", false)]
    public async Task CaughtDenialsAndTerminalErrorsKeepCounters(string ending, bool success)
    {
        var script = new LythonEngine().Compile("print('é')\ntry:\n    x = 'x' * 1000000\nexcept MemoryError:\n    pass\n" + ending);
        foreach (var asynchronous in new[] { false, true })
        {
            var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536, MaxExecutionSteps = 1000 };
            var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.Equal(success, result.Success);
            Assert.True(result.DeniedReservationBytes > 0);
            Assert.Equal(3, result.Consumption?.StandardOutputBytes);
            Assert.Equal(2, result.Consumption?.HostCalls);
            Assert.True(result.Consumption?.ExecutionSteps > 0);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProjectionFailureReportsRetainedBytesEvenWhenPublicOutputCannotFit(bool asynchronous)
    {
        var script = new LythonEngine().Compile("print('é')\nreturn [1, 2, 3]");
        var options = new LythonRunOptions { MaxProjectionMemoryBytes = 0 };
        var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.Equal(LythonFailureReason.ProjectionMemoryLimit, result.FailureAttribution?.Reason);
        Assert.Equal("", result.StandardOutput);
        Assert.Equal(3, result.Consumption?.StandardOutputBytes);
    }

    [Fact]
    public async Task LimitTripsAndUnadmittedRunsAreDistinct()
    {
        var engine = new LythonEngine();
        foreach (var asynchronous in new[] { false, true })
        {
            async Task<LythonExecutionResult> Run(string source, LythonRunOptions? options = null) => asynchronous
                ? await engine.RunAsync(source, new MockLythonHost(), options ?? new())
                : engine.Run(source, new MockLythonHost(), options ?? new());
            var steps = await Run("return 7", new() { MaxExecutionSteps = 0 });
            Assert.Equal(LythonFailureReason.ExecutionStepLimit, steps.FailureAttribution?.Reason);
            Assert.Equal(1, steps.Consumption?.ExecutionSteps);
            var host = await Run("open('/missing')", new() { MaxHostCalls = 0 });
            Assert.Equal(LythonFailureReason.HostCallLimit, host.FailureAttribution?.Reason);
            Assert.Equal(1, host.Consumption?.HostCalls);
            Assert.Null((await Run("x =")).Consumption);
            Assert.Null((await Run("return 7", new() { MaxExecutionMemoryBytes = 0, Args = ["argument"] })).Consumption);
        }
        Assert.Null(LythonExecutionResult.Succeeded(1, "output", "", []).Consumption);
    }

    [Fact]
    public async Task CancelledSuspendedHostRunRetainsConsumption()
    {
        var host = new MockLythonHost();
        var timing = new HeldTiming();
        host.SetTiming(timing);
        using var cancellation = new CancellationTokenSource();
        var call = new LythonEngine().RunAsync("print('é')\nimport time\ntime.sleep(1)", host,
            new LythonRunOptions { CancellationToken = cancellation.Token, MaxExecutionSteps = 1000 });
        try { await timing.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { cancellation.Cancel(); }
        var result = await call.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        Assert.Equal(3, result.Consumption?.HostCalls);
        Assert.Equal(3, result.Consumption?.StandardOutputBytes);
        Assert.True(result.Consumption?.ExecutionSteps > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedHostWriteStillReportsAcceptedCaptureAndAttempt(bool asynchronous)
    {
        var host = new MockLythonHost { OnStandardOutputWrite = () => throw new InvalidOperationException("controlled failure") };
        var script = new LythonEngine().Compile("print('é')");
        var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.Equal(LythonFailureReason.HostOperationFailed, result.FailureAttribution?.Reason);
        Assert.Equal(2, result.Consumption?.StandardOutputBytes);
        Assert.Equal(1, result.Consumption?.HostCalls);
    }

    [Fact]
    public async Task ReadmeCompileOnceExampleWorksWithPolicyInputsAndFiniteLimits()
    {
        var script = new LythonEngine().Compile("import json\nvalues = json.loads(payload)\nprint(len(values))\nreturn sum(values)");
        foreach (var (payload, expected, output) in new[] { ("[1,2,3]", "6", "3\n"), ("[4,5]", "9", "2\n") })
        foreach (var asynchronous in new[] { false, true })
        {
            var options = new LythonRunOptions
            {
                Globals = new Dictionary<string, object?> { ["payload"] = payload },
                AllowedBuiltinModules = new HashSet<string> { "json" },
                DisableLocalModuleImports = true,
                MaxExecutionSteps = 100000, MaxHostCalls = 100,
                MaxCollectionSize = 10000, MaxStringLength = 65536,
                MaxHostReadBytes = 0, MaxStandardOutputBytes = 4096, MaxStandardErrorBytes = 4096,
                MaxExecutionMemoryBytes = 8 * 1024 * 1024, MaxProjectionMemoryBytes = 1024 * 1024,
            };
            var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.ReturnValue?.ToString());
            Assert.Equal(output, result.StandardOutput);
            Assert.InRange(result.Consumption!.Value.ExecutionSteps!.Value, 1, 100000);
        }
    }

    private sealed class HeldTiming : ILythonTiming
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public long MonotonicNanoseconds => 0;
        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
