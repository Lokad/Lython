using System.Globalization;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class SimpleFunctionExecutionBoundaryTests
{
    private static readonly LythonSourceSpan CallSpan = new(100, 6, 20, 3);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void EachEntryAndInstructionKeepsItsFuelBoundary(int fuel)
    {
        var (function, context, code) = Function("def f(value):\n    return value + 3\n", new LythonRunOptions { MaxExecutionSteps = fuel });
        if (fuel == 7)
        {
            Assert.Equal(new BigInteger(8), function.Invoke([CallArgumentValue.Positional(new BigInteger(5))], CallSpan, context));
            Assert.Equal(7, context.Limits.ExecutionStepCount);
        }
        else
        {
            var error = Assert.Throws<LythonRuntimeException>(() => function.Invoke([CallArgumentValue.Positional(new BigInteger(5))], CallSpan, context));
            Assert.Equal("RuntimeError", error.ExceptionType);
            Assert.Equal($"maximum execution step count exceeded ({fuel})", error.Message);
            var expected = fuel == 1 ? CallSpan : fuel == 2 ? null : code.Blocks[code.EntryBlockIndex].InstructionArray[fuel - 3].Span;
            Assert.Same(expected, error.Span);
            Assert.Equal(fuel + 1, context.Limits.ExecutionStepCount);
            if (fuel >= 2) Assert.Equal("/callee.py", error.SourcePath);
        }
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Null(context.CurrentExecutableFrame);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConstantAndFreshResultDenialsKeepSpanAndActiveException(bool resultDenied)
    {
        var source = resultDenied ? "def f(value, right):\n    return value + right\n" : "def f(value):\n    return value + 3\n";
        var (function, context, code) = Function(source, new LythonRunOptions { MaxExecutionSteps = 1000, MaxExecutionMemoryBytes = 4096 });
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        context.Services.SetCurrentException(active);
        var governor = context.MemoryGovernor;
        if (!resultDenied)
        {
            var pressure = 4096 - governor.CurrentAccountedBytes - 32;
            governor.Reserve(pressure, CallSpan);
            governor.Commit(pressure);
        }
        var large = BigInteger.One << 40000;
        CallArgumentValue[] arguments = resultDenied
            ? [CallArgumentValue.Positional(large), CallArgumentValue.Positional(BigInteger.One)]
            : [CallArgumentValue.Positional(BigInteger.One)];
        var error = Assert.Throws<LythonRuntimeException>(() => function.Invoke(arguments, CallSpan, context));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Same(code.Blocks[code.EntryBlockIndex].InstructionArray[resultDenied ? 2 : 1].Span, error.Span);
        Assert.Equal(resultDenied ? RuntimeMemoryEstimates.EstimateBigIntegerBytes(large + 1) : 33, governor.LastDeniedReservationBytes);
        Assert.Same(active, error.PythonContext);
        Assert.Same(active, context.Services.CurrentException);
        Assert.Equal("/callee.py", error.SourcePath);
        Assert.Equal("f", Assert.Single(error.Frames).FunctionName);
        Assert.Equal(resultDenied ? 6 : 5, context.Limits.ExecutionStepCount);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void PureReturnKeepsIdentityAndCallerException(int shape)
    {
        var body = shape == 0 ? "return value" : shape == 1 ? "return 17" : "return";
        var (function, context, code) = Function($"def f(value):\n    {body}\n", new LythonRunOptions { MaxExecutionSteps = 1000 });
        var argument = new PyList([new BigInteger(5)], context.MemoryGovernor, CallSpan);
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        context.Services.SetCurrentException(active);
        var result = function.Invoke([CallArgumentValue.Positional(argument)], CallSpan, context);
        Assert.Same(shape == 0 ? argument : shape == 1 ? code.Constants[0] : PyNone.Instance, result);
        Assert.Same(active, context.Services.CurrentException);
        Assert.Equal(shape == 2 ? 4 : 5, context.Limits.ExecutionStepCount);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FuelStillPrecedesCancellationAtInvocation(bool fuelDenied)
    {
        using var cancellation = new CancellationTokenSource();
        var (function, context, _) = Function("def f(value):\n    return value + 3\n", new LythonRunOptions
        {
            MaxExecutionSteps = fuelDenied ? 1 : 1000, CancellationToken = cancellation.Token
        });
        if (fuelDenied) context.Limits.ExecutionStepCount = 1;
        cancellation.Cancel();
        var error = Assert.Throws<LythonRuntimeException>(() => function.Invoke([CallArgumentValue.Positional(BigInteger.One)], CallSpan, context));
        Assert.Equal(fuelDenied ? "maximum execution step count exceeded (1)" : "execution canceled", error.Message);
        Assert.Same(CallSpan, error.Span);
        Assert.Equal(fuelDenied ? 2 : 1, context.Limits.ExecutionStepCount);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
    }

    [Theory]
    [InlineData("def f(value):\n    return value + 3\nreturn f(True)\n", "4")]
    [InlineData("def f(value):\n    return value + 3\nreturn f(1.5)\n", "4.5")]
    [InlineData("class V(int):\n    def __add__(self, other):\n        return 91\ndef f(value):\n    return value + 3\nreturn f(V(2))\n", "91")]
    [InlineData("class V:\n    def __radd__(self, other):\n        return 83\ndef f(value):\n    return 3 + value\nreturn f(V())\n", "83")]
    [InlineData("def make():\n    offset = 2\n    def f(value):\n        return value + offset\n    return f\nreturn make()(5)\n", "7")]
    [InlineData("def f(value):\n    return locals()['value']\nreturn f(9)\n", "9")]
    [InlineData("def f(value):\n    try:\n        return value + 3\n    finally:\n        value = 999\nreturn f(2)\n", "5")]
    public void RichOperandsAndObservableFramesKeepPythonBehavior(string source, string expected)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message ?? string.Join("|", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(expected, Convert.ToString(result.ReturnValue, CultureInfo.InvariantCulture));
    }

    private static (PyExecutableFunction Function, LythonRuntime.ExecutionContext Context, ExecutableCodeObject Code) Function(string source, LythonRunOptions options)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var executable = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull()));
        var binding = Assert.Single(executable.EntryPoint.Functions);
        var code = binding.CodeObject.RequireNotNull();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options);
        var closure = LythonRuntime.ExecutionContext.CreateModule(context, "/callee.py", "callee");
        var function = new PyExecutableFunction("f", binding.Function.Parameters, code, closure, [], [], code.ScopeFacts);
        return (function, context, code);
    }
}
