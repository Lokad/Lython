using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FailureAttributionTests
{
    public static IEnumerable<object[]> LimitCases()
    {
        var cases = new[]
        {
            ("def f():\n    return f()\nf()", LythonFailureReason.RecursionDepthLimit, 3L),
            ("def f():\n    return f()\nf()", LythonFailureReason.InterpreterStackLimit, 512L),
            ("return [1, 2, 3]", LythonFailureReason.CollectionSizeLimit, 2L),
            ("return 'abc'", LythonFailureReason.StringLengthLimit, 2L),
            ("with open('/input') as f:\n    return f.read()", LythonFailureReason.HostReadLimit, 2L),
            ("print('abc')", LythonFailureReason.StandardOutputLimit, 2L),
            ("import sys\nsys.stderr.write('abc')", LythonFailureReason.StandardErrorLimit, 2L),
            ("return 1", LythonFailureReason.ExecutionMemoryLimit, 0L),
            ("return [1, 2, 3]", LythonFailureReason.ProjectionMemoryLimit, 0L),
        };
        foreach (var (source, reason, limit) in cases)
        foreach (var asynchronous in new[] { false, true }) yield return [source, reason, limit, asynchronous];
    }

    [Theory]
    [MemberData(nameof(LimitCases))]
    public async Task RealLimitsHaveTypedQuantities(string source, LythonFailureReason reason, long limit, bool asynchronous)
    {
        var host = new MockLythonHost();
        host.SeedFile("/empty", "");
        host.SeedFile("/input", "abc");
        var timing = new YieldTiming();
        host.SetTiming(timing);
        if (asynchronous && reason != LythonFailureReason.ExecutionMemoryLimit)
            source = "import time\ntime.sleep(0)\n" + source;
        var options = Options(reason, limit);
        var script = new LythonEngine().Compile(source);
        var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        Assert.False(result.Success);
        var attribution = Assert.IsType<LythonFailureAttribution>(result.FailureAttribution);
        Assert.Equal(reason, attribution.Reason);
        // Managed stack admission may trip before the fixed depth bound.
        if (reason != LythonFailureReason.InterpreterStackLimit || attribution.Limit is not null)
            Assert.Equal(limit, attribution.Limit);
        if (reason is LythonFailureReason.CollectionSizeLimit or LythonFailureReason.StringLengthLimit or LythonFailureReason.HostReadLimit)
            Assert.Equal(3, attribution.Observed);
        if (reason is LythonFailureReason.ExecutionMemoryLimit or LythonFailureReason.ProjectionMemoryLimit)
        {
            Assert.Equal(0, attribution.Observed);
            Assert.True(attribution.Requested > 0);
        }
        if (asynchronous && reason != LythonFailureReason.ExecutionMemoryLimit)
            Assert.True(timing.CompletedAsynchronously > 0, "The denial must occur after genuine suspension.");
    }

    [Theory]
    [InlineData("raise", false)]
    [InlineData("raise e", false)]
    [InlineData("e.args = ('changed',)\n    raise e", false)]
    [InlineData("e.Attribution = 'cancelled'\n    raise e", false)]
    [InlineData("raise ImportError(str(e))", true)]
    [InlineData("raise RuntimeError('execution canceled')", true)]
    public async Task ProvenanceFollowsActualExceptionsAndCannotBeForged(string ending, bool lookalike)
    {
        var source = "try:\n    import json\nexcept ImportError as e:\n    " + ending;
        var script = new LythonEngine().Compile(source);
        foreach (var asynchronous in new[] { false, true })
        {
            var result = asynchronous
                ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { AllowedBuiltinModules = new HashSet<string>() })
                : script.Run(new MockLythonHost(), new LythonRunOptions { AllowedBuiltinModules = new HashSet<string>() });
            Assert.False(result.Success);
            Assert.Equal(lookalike ? LythonFailureReason.GuestException : LythonFailureReason.ModulePolicyDenied,
                result.FailureAttribution?.Reason);
        }
    }

    [Theory]
    [InlineData("return 7")]
    [InlineData("raise RuntimeError('later')")]
    public async Task MemoryDenialRetainsItsOriginalTerminalAttribution(string ending)
    {
        var source = "try:\n    x = 'x' * 1000000\nexcept MemoryError:\n    pass\n" + ending;
        var script = new LythonEngine().Compile(source);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        foreach (var asynchronous in new[] { false, true })
        {
            var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.False(result.Success);
            Assert.True(result.DeniedReservationBytes > 0);
            Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.FailureAttribution?.Reason);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(128)]
    public async Task FailedDiagnosticProjectionPreservesOriginalAttribution(long projectionBudget)
    {
        var script = new LythonEngine().Compile("import json");
        var options = new LythonRunOptions
        {
            AllowedBuiltinModules = new HashSet<string>(),
            MaxProjectionMemoryBytes = projectionBudget,
        };
        foreach (var asynchronous in new[] { false, true })
        {
            var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.Equal("ImportError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.ModulePolicyDenied, result.FailureAttribution?.Reason);
            Assert.True(result.PeakProjectionMemoryBytes <= projectionBudget);
        }
    }

    [Fact]
    public async Task CompilationHostPreflightRuntimeCapabilityAndProjectionRejectionAreDistinct()
    {
        var engine = new LythonEngine();
        foreach (var asynchronous in new[] { false, true })
        {
            async Task<LythonExecutionResult> Run(string source) => asynchronous
                ? await engine.RunAsync(source, new MockLythonHost()) : engine.Run(source, new MockLythonHost());
            Assert.Equal(LythonFailureReason.Compilation, (await Run("x =")).FailureAttribution?.Reason);
            Assert.Equal(LythonFailureReason.HostCapabilityUnavailable, (await Run("import subprocess")).FailureAttribution?.Reason);
            Assert.Equal(LythonFailureReason.HostCapabilityUnavailable, (await Run("import importlib\nimportlib.import_module('subprocess')")).FailureAttribution?.Reason);
            Assert.Equal(LythonFailureReason.HostCapabilityUnavailable, (await Run("import time\ntime.sleep(0)")).FailureAttribution?.Reason);
            Assert.Equal(LythonFailureReason.ProjectionRejected, (await Run("x = []\nx.append(x)\nreturn x")).FailureAttribution?.Reason);
            var exit = await Run("raise SystemExit(0)");
            Assert.False(exit.Success);
            Assert.Equal(0, exit.ExitCode);
            Assert.Equal(LythonFailureReason.GuestException, exit.FailureAttribution?.Reason);
        }
    }

    [Fact]
    public async Task ActualSuspendedHostCancellationHasTypedOrigin()
    {
        var host = new MockLythonHost();
        var timing = new HeldTiming();
        host.SetTiming(timing);
        using var cancellation = new CancellationTokenSource();
        var call = new LythonEngine().RunAsync("import time\ntime.sleep(1)", host,
            new LythonRunOptions { CancellationToken = cancellation.Token });
        try { await timing.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { cancellation.Cancel(); }
        var result = await call.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        var cancelledBeforeEntry = new LythonEngine().Run("return 1", new MockLythonHost(),
            new LythonRunOptions { CancellationToken = cancellation.Token });
        Assert.Equal(LythonFailureReason.Cancelled, cancelledBeforeEntry.FailureAttribution?.Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostCallbackFailureHasTypedOrigin(bool asynchronous)
    {
        var host = new MockLythonHost { OnStandardOutputWrite = () => throw new InvalidOperationException("controlled fault") };
        var script = new LythonEngine().Compile("print('test')");
        var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.False(result.Success);
        Assert.Equal(LythonFailureReason.HostOperationFailed, result.FailureAttribution?.Reason);
    }

    private static LythonRunOptions Options(LythonFailureReason reason, long limit) => reason switch
    {
        LythonFailureReason.RecursionDepthLimit => new() { MaxRecursionDepth = (int)limit },
        LythonFailureReason.InterpreterStackLimit => new() { MaxRecursionDepth = 1024 },
        LythonFailureReason.CollectionSizeLimit => new() { MaxCollectionSize = (int)limit },
        LythonFailureReason.StringLengthLimit => new() { MaxStringLength = (int)limit },
        LythonFailureReason.HostReadLimit => new() { MaxHostReadBytes = limit },
        LythonFailureReason.StandardOutputLimit => new() { MaxStandardOutputBytes = limit },
        LythonFailureReason.StandardErrorLimit => new() { MaxStandardErrorBytes = limit },
        LythonFailureReason.ExecutionMemoryLimit => new() { MaxExecutionMemoryBytes = limit },
        LythonFailureReason.ProjectionMemoryLimit => new() { MaxProjectionMemoryBytes = limit },
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    private sealed class YieldTiming : ILythonTiming
    {
        public long MonotonicNanoseconds => 0;
        public int CompletedAsynchronously { get; private set; }
        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            CompletedAsynchronously++;
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
