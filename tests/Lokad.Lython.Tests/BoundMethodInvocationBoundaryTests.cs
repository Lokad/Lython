using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class BoundMethodInvocationBoundaryTests
{
    private static readonly LythonSourceSpan Span = new(100, 6, 20, 3);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ReceiverCallsPreserveResultsAndCleanup(int width)
    {
        {
            var (function, context, code) = Function(width, new LythonRunOptions { });
            var receiver = new object();
            var method = new PyBoundMethod(receiver, function);
                Assert.Same(receiver, method.Invoke(Arguments(width), Span, context));
            Assert.Equal(0, context.Limits.CurrentRecursionDepth);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Null(context.CurrentExecutableFrame);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ReceiverCallsKeepCancellation(int width)
    {
        using var cancellation = new CancellationTokenSource();
        var (function, context, _) = Function(width, new LythonRunOptions
        {
            CancellationToken = cancellation.Token
        });
        cancellation.Cancel();
        var error = Assert.Throws<LythonRuntimeException>(() => new PyBoundMethod(new object(), function).Invoke(Arguments(width), Span, context));
        Assert.Equal("execution canceled", error.Message);
        Assert.Same(Span, error.Span);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ReceiverPreparationKeepsLogicalFundingAndActiveException(int width)
    {
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(8192) };
        var (boundFunction, boundContext, _) = Function(width, options);
        var (directFunction, directContext, _) = Function(width, options);
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        boundContext.Services.SetCurrentException(active);
        directContext.Services.SetCurrentException(active);
        var receiver = new object();
        var arguments = Arguments(width);
        var prefixed = new[] { CallArgumentValue.Positional(receiver) }.Concat(arguments).ToArray();
        var beforeBound = boundContext.MemoryGovernor.CurrentAccountedBytes;
        var beforeDirect = directContext.MemoryGovernor.CurrentAccountedBytes;
        Assert.Same(receiver, new PyBoundMethod(receiver, boundFunction).Invoke(arguments, Span, boundContext));
        Assert.Same(receiver, directFunction.Invoke(prefixed, Span, directContext));
        Assert.Equal(directContext.MemoryGovernor.CurrentAccountedBytes - beforeDirect, boundContext.MemoryGovernor.CurrentAccountedBytes - beforeBound);
        Assert.Equal(0, boundContext.MemoryGovernor.CurrentReservedBytes);
        Assert.Same(active, boundContext.Services.CurrentException);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FrameAdmissionDenialKeepsSpanFundingAndRollback(int width)
    {
        var (function, context, _) = Function(width, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(8192), MaxRecursionDepth = 0 });
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        context.Services.SetCurrentException(active);
        var governor = context.MemoryGovernor;
        var before = governor.CurrentAccountedBytes;
        var receiver = new object();
        var arguments = Arguments(width);
        var direct = Assert.Throws<LythonRuntimeException>(() => function.Invoke(new[] { CallArgumentValue.Positional(receiver) }.Concat(arguments).ToArray(), Span, context));
        var denied = governor.LastDeniedReservationBytes;
        var bound = Assert.Throws<LythonRuntimeException>(() => new PyBoundMethod(receiver, function).Invoke(arguments, Span, context));
        Assert.Equal("RecursionError", bound.ExceptionType);
        Assert.Equal(direct.Message, bound.Message);
        Assert.Same(direct.Span, bound.Span);
        Assert.Equal(denied, governor.LastDeniedReservationBytes);
        Assert.Equal(before, governor.CurrentAccountedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Same(active, context.Services.CurrentException);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneralWrappedCallablesKeepFreshRetainableArrays(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { },regexProvider:Utf8RegexProvider.Instance);
        var receiver = new object();
        var callable = new RetainingCallable();
        var method = new PyBoundMethod(receiver, callable);
        for (var width = 0; width <= 3; width++)
        {
            var arguments = Arguments(width);
            if (width == 2) arguments[1] = CallArgumentValue.Keyword("right", arguments[1].Value);
            var result = asynchronous ? await method.InvokeAsync(arguments, Span, context) : method.Invoke(arguments, Span, context);
            var first = Assert.IsType<CallArgumentValue[]>(result);
            Assert.NotSame(arguments, first);
            Assert.Same(receiver, first[0].Value);
            Assert.Equal(arguments, first.Skip(1));
            var saved = first.ToArray();
            var next = asynchronous ? await method.InvokeAsync(Arguments(width), Span, context) : method.Invoke(Arguments(width), Span, context);
            Assert.NotSame(first, next);
            Assert.Equal(saved, first);
        }
        Assert.All(callable.Calls, call => { Assert.Same(context, call.Context); Assert.Same(Span, call.Span); });
    }

    private sealed class RetainingCallable : LythonRuntime.ICallable
    {
        internal List<(CallArgumentValue[] Arguments, LythonSourceSpan Span, LythonRuntime.ExecutionContext Context)> Calls { get; } = [];
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls.Add((arguments, span, context));
            return arguments;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            await Task.Yield();
            return Invoke(arguments, span, context);
        }
    }

    private static CallArgumentValue[] Arguments(int width)
        => Enumerable.Range(0, width).Select(_ => CallArgumentValue.Positional(new object())).ToArray();

    private static (PyExecutableFunction Function, LythonRuntime.ExecutionContext Context, ExecutableCodeObject Code) Function(int width, LythonRunOptions options)
    {
        var parameters = string.Concat(Enumerable.Range(0, width).Select(i => $", a{i}"));
        var frontend = LythonFrontend.Compile($"def f(self{parameters}):\n    return self\n",regexProvider:Utf8RegexProvider.Instance);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var executable = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull()));
        var binding = Assert.Single(executable.EntryPoint.Functions);
        var code = binding.CodeObject.RequireNotNull();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options,regexProvider:Utf8RegexProvider.Instance);
        var closure = LythonRuntime.ExecutionContext.CreateModule(context, "/callee.py", "callee");
        return (new PyExecutableFunction("f", binding.Function.Parameters, code, closure, [], [], code.ScopeFacts), context, code);
    }
}
