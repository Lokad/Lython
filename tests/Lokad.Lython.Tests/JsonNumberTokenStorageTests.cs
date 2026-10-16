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
        using var arm = new AdmissionGate(context.MemoryGovernor, limit, text.Length, demand, deny);
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
        using var arm = new AdmissionGate(context.MemoryGovernor, limit, text.Length, demand, deny);
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

    private static (ExecutableScript, LythonRuntime.ExecutionContext) Prepare(string source, long? limit = null)
        => (ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)), new(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = limit }));

    private static ValueTask<object?> Run(ExecutableScript code, LythonRuntime.ExecutionContext context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
            : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null));

    private static void AssertUnwound(LythonRuntime.ExecutionContext context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Capture : LythonRuntime.ICallable
    {
        public int Calls { get; private set; }
        public object? Value { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            return Value = Assert.Single(arguments).Value;
        }
    }

    private sealed class AdmissionGate(MemoryGovernor governor, long limit, int length, long demand, bool deny) : LythonRuntime.ICallable, IDisposable
    {
        private IDisposable? _reservation;
        public int Calls { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            if (deny) _reservation = governor.ReserveTemporary(limit - governor.CurrentAccountedBytes - 4L * length - demand + 1, span);
            return Assert.Single(arguments).Value;
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => ValueTask.FromResult(Invoke(arguments, span, context));
        public void Dispose() { _reservation?.Dispose(); _reservation = null; }
    }
}
