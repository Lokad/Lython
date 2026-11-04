using System.Globalization;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class SimpleFunctionExecutionBoundaryTests
{
    private static readonly LythonSourceSpan CallSpan = new(100, 6, 20, 3);

    [Fact]
    public void FormattedHelperErrorsReleaseWorkingStorageAndPreserveTheCallerException()
    {
        foreach (var driver in Enum.GetValues<Driver>())
        {
            var prefix = new string('x', 8192);
            var (function, context, _) = Function("def f(value):\n    return f'" + prefix + "{value:bad}'\n", new());
            var active = new PyException("RuntimeError", "outer", PyNone.Instance);
            context.State.SetCurrentException(active);
            var baseline = context.MemoryGovernor.CurrentAccountedBytes;
            for (var pass = 0; pass < 3; pass++)
            {
                var error = Assert.Throws<LythonRuntimeException>(() => Invoke(function,
                    [CallArgumentValue.Positional(new BigInteger(7))], context, driver));
                Assert.Equal("ValueError", error.ExceptionType);
                Assert.Same(active, error.PythonContext);
                Assert.Same(active, context.State.CurrentException);
                Assert.Equal("/callee.py", error.SourcePath);
                Assert.Equal("f", Assert.Single(error.Frames).FunctionName);
                Assert.Equal(baseline, context.MemoryGovernor.CurrentAccountedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FormattedHelperUnwindsGrowthFailureWithoutRefundingItsInput(bool cancel)
    {
        foreach (var driver in Enum.GetValues<Driver>())
        {
            var budget = new LythonMemoryBudget(2 * 1024 * 1024);
            using var cancellation = new CancellationTokenSource();
            using (var owner = budget.CreateScope())
            {
                var hook = new AdmissionHookMemoryBudget(owner);
                var prefix = new string('x', 8192);
                var (function, context, _) = Function("def f(value):\n    return f'" + prefix + "{value:>40000s}'\n",
                    new() { MemoryBudget = hook, CancellationToken = cancellation.Token });
                var input = PyString.FromString("kept", context.MemoryGovernor, CallSpan);
                var baseline = context.MemoryGovernor.CurrentAccountedBytes;
                var reached = false;
                if (cancel) hook.AfterGrowth = bytes => { if (bytes >= 32768) { reached = true; cancellation.Cancel(); } };
                else hook.RefuseGrowth = bytes => { if (bytes < 32768) return false; reached = true; return true; };
                Exception error = cancel
                    ? Assert.ThrowsAny<OperationCanceledException>(() => Invoke(function,
                        [CallArgumentValue.Positional(input)], context, driver))
                    : Assert.Throws<LythonRuntimeException>(() => Invoke(function,
                        [CallArgumentValue.Positional(input)], context, driver));
                Assert.True(reached);
                if (!cancel)
                {
                    Assert.Equal(LythonFailureReason.SharedMemoryAllowance, ((LythonRuntimeException)error).Attribution?.Reason);
                    Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
                }
                Assert.Equal("kept", input.AsString());
                Assert.Equal(baseline, context.MemoryGovernor.CurrentAccountedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                context.State.CloseResources();
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public void OrdinaryFunctionDriversReturnTheSameResult()
    {
        foreach (var driver in Enum.GetValues<Driver>())
        {
            var (function, context, _) = Function("def f(value):\n    return value + 3\n", new LythonRunOptions());
            Assert.Equal(new BigInteger(8), Invoke(function, [CallArgumentValue.Positional(new BigInteger(5))], context, driver));
            Assert.Equal(0, context.State.CurrentRecursionDepth);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);
            Assert.Null(context.CurrentExecutableFrame);
        }
    }

    [Theory]
    [InlineData("return value + 3", 8)]
    [InlineData("intermediate = value + 3\n    return intermediate + 4", 12)]
    public void NativeArithmeticReturnPreservesForwarding(string body, int expected)
    {
        foreach (var driver in Enum.GetValues<Driver>())
        {
            var (function, context, code) = Function("def f(value):\n    " + body + "\n", new LythonRunOptions());
            Assert.NotNull(code.NativeReturn);
            Assert.Equal(new BigInteger(expected), Invoke(function,
                [CallArgumentValue.Positional(new BigInteger(5))], context, driver));
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);
        }
    }

    [Fact]
    public void FreshResultDenialKeepsSpanAndActiveException()
    {
        foreach (var driver in Enum.GetValues<Driver>())
        {
            var source = "def f(value, right):\n    return value + right\n";
            var (function, context, code) = Function(source, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(4096) });
            var active = new PyException("ValueError", "outer", PyNone.Instance);
            context.State.SetCurrentException(active);
            var governor = context.MemoryGovernor;
            var large = BigInteger.One << 40000;
            CallArgumentValue[] arguments = [CallArgumentValue.Positional(large), CallArgumentValue.Positional(BigInteger.One)];
            var error = Assert.Throws<LythonRuntimeException>(() => Invoke(function, arguments, context, driver));
            Assert.Equal("MemoryError", error.ExceptionType);
            var addition = Assert.Single(code.Instructions,
                instruction => instruction.OpCode == ExecutableOpCode.Binary);
            Assert.Same(addition.Span, error.Span);
            Assert.True(governor.LastDeniedReservationBytes >= RuntimeMemoryEstimates.EstimateBigIntegerBytes(large + 1));
            Assert.Equal(governor.LastDeniedReservationBytes, error.Attribution?.Requested);
            Assert.Same(active, error.PythonContext);
            Assert.Same(active, context.State.CurrentException);
            Assert.Equal("/callee.py", error.SourcePath);
            Assert.Equal("f", Assert.Single(error.Frames).FunctionName);
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.Equal(0, context.State.CurrentRecursionDepth);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);

        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void PureReturnKeepsIdentityAndCallerException(int shape)
    {
        foreach (var driver in Enum.GetValues<Driver>())
        {
            var body = shape == 0 ? "return value" : shape == 1 ? "return 17" : "return";
            var (function, context, code) = Function($"def f(value):\n    {body}\n", new LythonRunOptions { });
            var argument = new PyList([new BigInteger(5)], context.MemoryGovernor, CallSpan);
            var active = new PyException("ValueError", "outer", PyNone.Instance);
            context.State.SetCurrentException(active);
            var result = Invoke(function, [CallArgumentValue.Positional(argument)], context, driver);
            Assert.Same(shape == 0 ? argument : shape == 1 ? code.Constants[0] : PyNone.Instance, result);
            Assert.Same(active, context.State.CurrentException);
            Assert.Equal(0, context.State.CurrentRecursionDepth);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);

        }
    }

    [Fact]
    public void CancellationPreventsInvocation()
    {
        foreach (var driver in Enum.GetValues<Driver>())
        {
            using var cancellation = new CancellationTokenSource();
            var (function, context, _) = Function("def f(value):\n    return value + 3\n", new LythonRunOptions
            {
            CancellationToken = cancellation.Token
            });
            cancellation.Cancel();
            var error = Assert.Throws<LythonRuntimeException>(() => Invoke(function, [CallArgumentValue.Positional(BigInteger.One)], context, driver));
            Assert.Equal("execution canceled", error.Message);
            Assert.Same(CallSpan, error.Span);
            Assert.Equal(0, context.State.CurrentRecursionDepth);

        }
    }

    [Theory]
    [InlineData("def f(value):\n    return value + 3\nreturn f(True)\n", "4")]
    [InlineData("def f(value):\n    return value + 3\nreturn f(1.5)\n", "4.5")]
    [InlineData("class V:\n    def __add__(self, other):\n        return 91\ndef f(value):\n    return value + 3\nreturn f(V())\n", "91")]
    [InlineData("class V:\n    def __radd__(self, other):\n        return 83\ndef f(value):\n    return 3 + value\nreturn f(V())\n", "83")]
    [InlineData("def make():\n    offset = 2\n    def f(value):\n        return value + offset\n    return f\nreturn make()(5)\n", "7")]
    [InlineData("def f(value):\n    return locals()['value']\nreturn f(9)\n", "9")]
    [InlineData("def f(value):\n    try:\n        return value + 3\n    finally:\n        value = 999\nreturn f(2)\n", "5")]
    public void RichOperandsAndObservableFramesKeepPythonBehavior(string source, string expected)
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(source, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message ?? string.Join("|", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(expected, Convert.ToString(result.ReturnValue, CultureInfo.InvariantCulture));
    }

    private enum Driver { General, Direct, GeneralAsync, DirectAsync }

    private static object Invoke(PyExecutableFunction function, CallArgumentValue[] arguments,
        LythonRuntime.ExecutionContext context, Driver driver)
    {
        Assert.True(function.SupportsDirectPositional);
        var values = arguments.Select(argument => (object?)argument.Value).ToArray();
        return driver switch
        {
            Driver.General => function.Invoke(arguments, CallSpan, context),
            Driver.Direct => function.InvokeFromOperands(values, CallSpan, context),
            Driver.GeneralAsync => function.InvokeAsync(arguments, CallSpan, context).GetAwaiter().GetResult(),
            _ => function.InvokeFromOperandsAsync(values, CallSpan, context).GetAwaiter().GetResult(),
        };
    }

    [Fact]
    public void NativeConversionRefusalKeepsBorrowedInputAndOperationOrigin()
    {
        foreach (var driver in Enum.GetValues<Driver>())
        {
            const long capacity = 1024 * 1024;
            var budget = new LythonMemoryBudget(capacity);
            using (var inputOwner = budget.CreateScope())
            {
                var inputGovernor = new MemoryGovernor(inputOwner);
                var input = Lokad.Lython.Runtime.Text.PyString.FromString(new string('9', 50000), inputGovernor,
                    new LythonSourceSpan(99, 1, 1, 1));
                var inputCharge = budget.ReservedBytes;
                using (var executionOwner = budget.CreateScope())
                {
                    var (function, context, code) = Function("def f(value):\n    return int(value)\n",
                        new LythonRunOptions { MemoryBudget = executionOwner });
                    using (var occupied = budget.CreateScope())
                    {
                        Assert.True(occupied.TryReserve(capacity - budget.ReservedBytes - 4096, out _));
                        var active = new PyException("RuntimeError", "outer", PyNone.Instance);
                        context.State.SetCurrentException(active);
                        var error = Assert.Throws<LythonRuntimeException>(() =>
                            Invoke(function, [CallArgumentValue.Positional(input)], context, driver));
                        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                        var operation = Assert.Single(code.CallSites);
                        Assert.Same(operation.CallSpan, error.Span);
                        Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                        Assert.Same(active, error.PythonContext);
                        Assert.Same(active, context.State.CurrentException);
                        Assert.Equal(0, context.State.CurrentInterpreterDepth);
                        Assert.Equal(0, context.State.CurrentRecursionDepth);
                        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                        Assert.Equal(50000, input.Length);
                    }
                    context.State.CloseResources();
                    function = null!;
                    context = null!;
                    code = null!;
                }
                Assert.Equal(inputCharge, budget.ReservedBytes);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public void CompoundReturnRefusalAfterTextWorkKeepsOriginAndInputOwner()
    {
        foreach (var driver in Enum.GetValues<Driver>())
        {
            const long capacity = 2 * 1024 * 1024;
            var budget = new LythonMemoryBudget(capacity);
            using (var inputOwner = budget.CreateScope())
            {
                var input = Lokad.Lython.Runtime.Text.PyString.FromString(" " + new string('9', 50000) + " ",
                    new MemoryGovernor(inputOwner), CallSpan);
                var inputCharge = budget.ReservedBytes;
                using (var executionOwner = budget.CreateScope())
                {
                    var (function, context, code) = Function("def f(value):\n    return int(value.strip()) + 1\n",
                        new LythonRunOptions { MemoryBudget = executionOwner });
                    using (var pressure = budget.CreateScope())
                    {
                        Assert.True(pressure.TryReserve(capacity - budget.ReservedBytes - 128 * 1024, out _));
                        var active = new PyException("RuntimeError", "outer", PyNone.Instance);
                        context.State.SetCurrentException(active);
                        var error = Assert.Throws<LythonRuntimeException>(() =>
                            Invoke(function, [CallArgumentValue.Positional(input)], context, driver));
                        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                        Assert.Same(code.CallSites.MaxBy(site => site.CallSpan.Length)!.CallSpan, error.Span);
                        Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                        Assert.Same(active, error.PythonContext);
                        Assert.Same(active, context.State.CurrentException);
                        Assert.Equal(0, context.State.CurrentInterpreterDepth);
                        Assert.Equal(0, context.State.CurrentRecursionDepth);
                        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                        Assert.Equal(50002, input.Length);
                    }
                    context.State.CloseResources();
                }
                // A governed source's fresh slices conservatively share its
                // owner; ending the execution cannot refund that source owner.
                Assert.True(budget.ReservedBytes >= inputCharge);
                Assert.Equal(50002, input.Length);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    private static (PyExecutableFunction Function, LythonRuntime.ExecutionContext Context, ExecutableCodeObject Code) Function(string source, LythonRunOptions options)
    {
        var frontend = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var executable = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull()));
        var binding = Assert.Single(executable.EntryPoint.Functions);
        var code = binding.CodeObject.RequireNotNull();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options,regexProvider:Utf8RegexProvider.Instance);
        var closure = LythonRuntime.ExecutionContext.CreateModule(context, "/callee.py", "callee");
        var function = new PyExecutableFunction("f", binding.Function.Parameters, code, closure, [], [], code.ScopeFacts);
        return (function, context, code);
    }
}
