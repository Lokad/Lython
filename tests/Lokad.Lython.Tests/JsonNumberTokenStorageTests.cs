using System.Globalization;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class JsonNumberTokenStorageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultLongKeepsTheIntegerRepresentation(bool asynchronous)
    {
        const string text = "9223372036854775807";
        var (code, context) = Prepare("import json\nreturn json.loads(source)");
        context.Variables["source"] = PyString.FromString(text, context.MemoryGovernor, null);
        Assert.Equal(BigInteger.Parse(text, CultureInfo.InvariantCulture), Assert.IsType<BigInteger>(await Run(code, context, asynchronous)));
        AssertUnwound(context);
    }

    [Theory]
    [InlineData("-0.0", false)]
    [InlineData("-0.0", true)]
    [InlineData("1e9999", false)]
    [InlineData("1e9999", true)]
    public async Task DefaultFloatPreservesSignedZeroAndOverflow(string text, bool asynchronous)
    {
        var (code, context) = Prepare("import json\nreturn json.loads(source)");
        context.Variables["source"] = PyString.FromString(text, context.MemoryGovernor, null);
        var actual = Assert.IsType<double>(await Run(code, context, asynchronous));
        Assert.Equal(BitConverter.DoubleToInt64Bits(double.Parse(text, CultureInfo.InvariantCulture)), BitConverter.DoubleToInt64Bits(actual));
        AssertUnwound(context);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task IntegerMagnitudeAdmissionPrecedesParsing(bool asynchronous, bool deny)
    {
        const long limit = 1024 * 1024;
        var text = new string('9', 64);
        var demand = RuntimeMemoryEstimates.SaturatingMultiply(2, RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(4L * text.Length));
        var (code, context) = Prepare("import json\nreturn json.loads(arm(source))", limit);
        context.Variables["source"] = PyString.FromString(text, context.MemoryGovernor, null);
        using var arm = new AdmissionGate(context.MemoryGovernor, limit, demand, deny);
        context.Variables["arm"] = arm;
        try
        {
            if (deny)
            {
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(code, context, asynchronous));
                Assert.Equal("MemoryError", error.ExceptionType);
                Assert.Equal(demand, context.MemoryGovernor.LastDeniedReservationBytes);
            }
            else Assert.Equal(BigInteger.Parse(text, CultureInfo.InvariantCulture), Assert.IsType<BigInteger>(await Run(code, context, asynchronous)));
            Assert.Equal(1, arm.Calls);
            Assert.True(context.MemoryGovernor.PeakAccountedBytes <= limit);
        }
        finally { arm.Dispose(); }
        AssertUnwound(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LargeIntegerFallbackAdmitsWorkingStorageBeforeDecoding(bool asynchronous)
    {
        const long limit = 1024 * 1024;
        var text = new string('9', 64);
        var magnitudeDemand = 2 * RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(4L * text.Length);
        var workingDemand = 4L * text.Length;
        var (code, context) = Prepare("import json\nreturn json.loads(arm(source))", limit);
        context.Variables["source"] = PyString.FromString(text, context.MemoryGovernor, null);
        using var arm = new AdmissionGate(context.MemoryGovernor, limit, magnitudeDemand + workingDemand, true);
        context.Variables["arm"] = arm;
        try
        {
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(code, context, asynchronous));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(workingDemand, context.MemoryGovernor.LastDeniedReservationBytes);
            Assert.Equal(1, arm.Calls);
            Assert.True(context.MemoryGovernor.PeakAccountedBytes <= limit);
        }
        finally { arm.Dispose(); }
        AssertUnwound(context);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task HookTextAdmissionPrecedesItsCallback(bool asynchronous, bool floating, bool deny)
    {
        const long limit = 1024 * 1024;
        var text = floating ? "1.2300e+04" : "-0";
        var (code, context) = Prepare("import json\nreturn json.loads(arm(source)," + (floating ? "parse_float" : "parse_int") + "=capture)", limit);
        context.Variables["source"] = PyString.FromString(text, context.MemoryGovernor, null);
        var demand = PyString.EstimateApproximateBytes(text.Length);
        using var arm = new AdmissionGate(context.MemoryGovernor, limit, demand, deny);
        var capture = new Capture();
        context.Variables["arm"] = arm;
        context.Variables["capture"] = capture;
        try
        {
            if (deny)
            {
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(code, context, asynchronous));
                Assert.Equal("MemoryError", error.ExceptionType);
                Assert.Equal(demand, context.MemoryGovernor.LastDeniedReservationBytes);
                Assert.Equal(0, capture.Calls);
            }
            else
            {
                var result = Assert.IsType<PyString>(await Run(code, context, asynchronous));
                Assert.Equal(text, result.AsString());
                Assert.Same(capture.Value, result);
                Assert.Equal(1, capture.Calls);
            }
            Assert.Equal(1, arm.Calls);
            Assert.True(context.MemoryGovernor.PeakAccountedBytes <= limit);
        }
        finally { arm.Dispose(); }
        AssertUnwound(context);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task NumericCheckpointsAndArgumentEffectsRemainStable(bool asynchronous, bool floating, bool callback)
    {
        var text = floating ? (callback ? "1.2300e+04" : "-0.0") : (callback ? "-0" : "9223372036854775808");
        var source = "import json\nreturn json.loads(source" + (callback ? "," + (floating ? "parse_float" : "parse_int") + "=capture)" : ")");
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!));
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        string[] effects;
        using (var owner = budget.CreateScope())
        {
            var (context, capture) = Boundary(owner, text);
            var result = await Run(code, context, asynchronous);
            if (callback)
            {
                Assert.Equal(text, Assert.IsType<PyString>(result).AsString());
                Assert.Same(capture.Value, result);
            }
            else if (floating) Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(Assert.IsType<double>(result)));
            else Assert.Equal(BigInteger.Parse(text, CultureInfo.InvariantCulture), Assert.IsType<BigInteger>(result));
            effects = capture.Events.ToArray();
            Assert.Equal(callback ? 1 : 0, capture.Calls);
            AssertUnwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        Assert.Equal(callback ? new[] { "4,0:" + text } : Array.Empty<string>(), effects);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PendingNativeHookKeepsItsArgumentFundedUntilCompletion(bool floating, bool cancel)
    {
        var text = floating ? "1.2300e+04" : "-0";
        var source = "import json\nreturn json.loads(source," + (floating ? "parse_float" : "parse_int") + "=capture)";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!));
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var (context, _) = Boundary(owner, text);
        var capture = new HeldCapture();
        context.Variables["capture"] = capture;
        var pending = Run(code, context, true).AsTask();
        try
        {
            await capture.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.Equal(1, capture.Calls);
            Assert.Equal(text, capture.Value!.AsString());
            Assert.True(capture.Committed >= PyString.EstimateApproximateBytes(text.Length));
            Assert.Equal(capture.Committed, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(capture.Depth, context.Limits.CurrentInterpreterDepth);
            Assert.True(budget.ReservedBytes > 0);
            if (cancel)
            {
                capture.Release.TrySetCanceled();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            else
            {
                capture.Release.TrySetResult();
                Assert.Same(capture.Value, await pending.WaitAsync(TimeSpan.FromSeconds(5)));
            }
        }
        finally
        {
            capture.Release.TrySetCanceled();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
        }
        AssertUnwound(context);
        Assert.True(budget.ReservedBytes > 0); // The execution owner remains live while the capture is retained.
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static (LythonRuntime.ExecutionContext Context, Capture Capture) Boundary(ILythonMemoryBudget owner, string text)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner});
        context.Variables["source"] = PyString.FromString(text, context.MemoryGovernor, null);
        var capture = new Capture();
        context.Variables["capture"] = capture;
        return (context, capture);
    }

    private sealed class HeldCapture : LythonRuntime.ICallable
    {
        public int Calls;
        public PyString? Value;
        public long Committed;
        public long Depth;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => throw new InvalidOperationException("Expected an asynchronous native hook.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            Value = Assert.IsType<PyString>(Assert.Single(arguments).Value);
            Committed = context.MemoryGovernor.CurrentCommittedBytes;
            Depth = context.Limits.CurrentInterpreterDepth;
            Started.TrySetResult();
            await Release.Task.ConfigureAwait(false);
            return Value;
        }
    }

    private static (ExecutableScript, LythonRuntime.ExecutionContext) Prepare(string source, long? limit = null)
        => (ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)), new(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = limit }));

    private static ValueTask<object?> Run(ExecutableScript code, LythonRuntime.ExecutionContext context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
            : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null));

    private static void AssertUnwound(LythonRuntime.ExecutionContext context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Capture : LythonRuntime.ICallable
    {
        public int Calls { get; private set; }
        public object? Value { get; private set; }
        public List<string> Events { get; } = [];
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            Value = Assert.Single(arguments).Value;
            var text = Assert.IsType<PyString>(Value);
            Events.Add($"{context.Limits.CurrentInterpreterDepth},{context.Limits.CurrentRecursionDepth}:{text.AsString()}");
            return Value;
        }
    }

    private sealed class AdmissionGate(MemoryGovernor governor, long limit, long demand, bool deny) : LythonRuntime.ICallable, IDisposable
    {
        private IDisposable? _reservation;
        public int Calls { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            if (deny) _reservation = governor.ReserveTemporary(limit - governor.CurrentAccountedBytes - demand + 1, span);
            return Assert.Single(arguments).Value;
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => ValueTask.FromResult(Invoke(arguments, span, context));
        public void Dispose() { _reservation?.Dispose(); _reservation = null; }
    }
}
