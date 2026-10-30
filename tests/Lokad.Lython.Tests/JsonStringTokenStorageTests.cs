using System.Text;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class JsonStringTokenStorageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidUtf8StillRejectsBeforeProducingAString(bool asynchronous)
    {
        var (code, context) = Prepare("import json\nreturn json.loads(source)");
        context.Variables["source"] = PyString.FromOwnedUtf8([(byte)'"', 255, (byte)'"'], context.MemoryGovernor, null);
        var error = asynchronous
            ? await Assert.ThrowsAnyAsync<Exception>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context))
            : Assert.ThrowsAny<Exception>(() => LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null));
        Assert.Contains("UTF", error.Message, StringComparison.OrdinalIgnoreCase);
        AssertUnwound(context);
    }

    [Theory]
    [InlineData("\"alpha\"", "alpha", false)]
    [InlineData("\"alpha\"", "alpha", true)]
    [InlineData("\"café😀\"", "café😀", false)]
    [InlineData("\"café😀\"", "café😀", true)]
    [InlineData("\"\"", "", false)]
    [InlineData("\"\"", "", true)]
    [InlineData("\"a\\nb\"", "a\nb", false)]
    [InlineData("\"a\\nb\"", "a\nb", true)]
    public async Task DecodedStorageDoesNotBorrowTheInputArray(string source, string expected, bool asynchronous)
    {
        var (code, context) = Prepare("import json\nreturn json.loads(source)");
        var bytes = Encoding.UTF8.GetBytes(source);
        context.Variables["source"] = PyString.FromOwnedUtf8(bytes, context.MemoryGovernor, null);
        var decoded = Assert.IsType<PyString>(asynchronous
            ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null));
        if (bytes.Length > 2) bytes[1] = (byte)'z';
        Assert.Equal(expected, decoded.AsString());
        if (expected.Length == 0) Assert.Same(PyString.Empty, decoded);
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
    public async Task OutputStorageIsAdmittedBesideTokenScratch(bool asynchronous, bool deny, bool escaped)
    {
        const long limit = 1024 * 1024;
        const int length = 4096;
        var (code, context) = Prepare("import json\nreturn json.loads(arm(source))", new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) });
        var document = PyString.FromString("\"" + new string('a', length - (escaped ? 1 : 0))
            + (escaped ? @"\u0061" : "") + "\"", context.MemoryGovernor, null);
        context.Variables["source"] = document;
        using var arm = new AdmissionGate(context.MemoryGovernor, limit, length, document.Utf8Bytes.Length, deny);
        context.Variables["arm"] = arm;
        try
        {
            if (deny)
            {
                var error = asynchronous
                    ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context))
                    : Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null));
                Assert.Equal("MemoryError", error.ExceptionType);
                Assert.Equal(PyString.EstimateApproximateBytes(length), context.MemoryGovernor.LastDeniedReservationBytes);
            }
            else
            {
                var result = asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
                    : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);
                Assert.Equal(new string('a', length), Assert.IsType<PyString>(result).AsString());
            }
            Assert.Equal(1, arm.Calls);
            Assert.True(context.MemoryGovernor.PeakAccountedBytes <= limit);
        }
        finally { arm.Dispose(); }
        AssertUnwound(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LargerEscapedTokensKeepAnIndependentFundedCopy(bool asynchronous)
    {
        var (code, context) = Prepare("import json\nreturn json.loads(source)");
        var source = "\"" + string.Concat(Enumerable.Repeat(@"\ud83d\ude00", 22)) + "\"";
        var bytes = Encoding.UTF8.GetBytes(source);
        context.Variables["source"] = PyString.FromOwnedUtf8(bytes, context.MemoryGovernor, null);
        var before = context.MemoryGovernor.CurrentAccountedBytes;
        var decoded = Assert.IsType<PyString>(asynchronous
            ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null));
        bytes[1] = (byte)'x';
        Assert.Equal(string.Concat(Enumerable.Repeat("😀", 22)), decoded.AsString());
        Assert.True(context.MemoryGovernor.CurrentAccountedBytes > before);
        AssertUnwound(context);
    }

    private static (ExecutableScript, LythonRuntime.ExecutionContext) Prepare(string source, LythonRunOptions? options = null)
        => (ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)), new(new MockLythonHost(), options ?? new()));

    private static void AssertUnwound(LythonRuntime.ExecutionContext context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class AdmissionGate(MemoryGovernor governor, long limit, int length, int tokenLength, bool deny) : LythonRuntime.ICallable, IDisposable
    {
        private IDisposable? _reservation;
        public int Calls { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            if (deny)
            {
                var scratch = 4L * tokenLength;
                var result = PyString.EstimateApproximateBytes(length);
                _reservation = governor.ReserveTemporary(limit - governor.CurrentAccountedBytes - scratch - result + 1, span);
            }
            return Assert.Single(arguments).Value;
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => ValueTask.FromResult(Invoke(arguments, span, context));
        public void Dispose() { _reservation?.Dispose(); _reservation = null; }
    }
}
