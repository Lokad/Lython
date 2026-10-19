using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExecutionConsumptionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedRunsReportExistingOutputSizes(bool asynchronous)
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import sys\nprint('é')\nsys.stderr.write('😀')\nreturn 7");
        for (var i = 0; i < 3; i++)
        {
            var options = new LythonRunOptions { };
            var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.True(result.Success, result.Failure?.Message);
            var usage = Assert.IsType<LythonExecutionConsumption>(result.Consumption);
            Assert.Equal(3, usage.StandardOutputBytes);
            Assert.Equal(4, usage.StandardErrorBytes);
        }
    }

    [Theory]
    [InlineData("return 7")]
    [InlineData("raise RuntimeError('later')")]
    [InlineData("raise SystemExit(0)")]
    public async Task TerminalAdmissionDenialKeepsOutputSizes(string ending)
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("print('é')\ntry:\n    x = 'x' * 1000000\nexcept MemoryError:\n    pass\n" + ending);
        foreach (var asynchronous in new[] { false, true })
        {
            var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536)};
            var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.False(result.Success);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.True(result.DeniedReservationBytes > 0);
            Assert.Equal(3, result.Consumption?.StandardOutputBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProjectionFailureReportsRetainedBytesEvenWhenPublicOutputCannotFit(bool asynchronous)
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("print('é')\nreturn [1, 2, 3]");
        var options = new LythonRunOptions { ResultMemoryBudget = new LythonMemoryBudget(0)};
        var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
        Assert.Equal("", result.StandardOutput);
        Assert.Equal(3, result.Consumption?.StandardOutputBytes);
    }

    [Fact]
    public async Task UnadmittedRunsHaveNoConsumption()
    {
        var engine = new LythonEngine(Utf8RegexProvider.Instance);
        foreach (var asynchronous in new[] { false, true })
        {
            async Task<LythonExecutionResult> Run(string source, LythonRunOptions? options = null) => asynchronous
                ? await engine.RunAsync(source, new MockLythonHost(), options ?? new())
                : engine.Run(source, new MockLythonHost(), options ?? new());
            Assert.Null((await Run("x =")).Consumption);
            Assert.Null((await Run("return 7", new() { MemoryBudget = new LythonMemoryBudget(0), Args = ["argument"] })).Consumption);
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
        var call = new LythonEngine(Utf8RegexProvider.Instance).RunAsync("print('é')\nimport time\ntime.sleep(1)", host,
            new LythonRunOptions { CancellationToken = cancellation.Token});
        try { await timing.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { cancellation.Cancel(); }
        var result = await call.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        Assert.Equal(3, result.Consumption?.StandardOutputBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedHostWriteStillReportsAcceptedCapture(bool asynchronous)
    {
        var host = new MockLythonHost { OnStandardOutputWrite = () => throw new InvalidOperationException("controlled failure") };
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("print('é')");
        var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.Equal(LythonFailureReason.HostOperationFailed, result.FailureAttribution?.Reason);
        Assert.Equal(2, result.Consumption?.StandardOutputBytes);
    }

    [Fact]
    public async Task ReadmeCompileOnceExampleWorksWithPolicyInputsAndFiniteLimits()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import json\nvalues = json.loads(payload)\nprint(len(values))\nreturn sum(values)");
        foreach (var (payload, expected, output) in new[] { ("[1,2,3]", "6", "3\n"), ("[4,5]", "9", "2\n") })
        foreach (var asynchronous in new[] { false, true })
        {
            var options = new LythonRunOptions
            {
                Globals = new Dictionary<string, object?> { ["payload"] = payload },
                AllowedBuiltinModules = new HashSet<string> { "json" },
                DisableLocalModuleImports = true,                                 MemoryBudget = new LythonMemoryBudget(8 * 1024 * 1024), ResultMemoryBudget = new LythonMemoryBudget(1024 * 1024),
            };
            var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.ReturnValue?.ToString());
            Assert.Equal(output, result.StandardOutput);
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
