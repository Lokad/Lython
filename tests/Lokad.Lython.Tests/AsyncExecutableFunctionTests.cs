using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class AsyncExecutableFunctionTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForwardedKeywordDefaultNeedsStagingWhenItsBindingCanChange(bool written)
    {
        var source = """
            def outer(value):
                def make():
                    def call(a=2, *, item=value): return a, item
                    return call
            """;
        if (written) source += "\n    def replace():\n        nonlocal value\n        value=9";
        source += "\n    return make";
        var (code, _, _) = Prepare(source);
        var outer = Assert.Single(code.FunctionBindings()).CodeObject!;
        var make = outer.FunctionBindings().Single(binding => binding.CodeObject!.Name == "make").CodeObject!;
        var definition = Assert.Single(make.Constants.OfType<ExecutableCreateFunction>());
        Assert.Equal(!written, definition.DirectKeywordDefault);
        Assert.Equal(written, make.Instructions.Any(instruction => instruction.OpCode == ExecutableOpCode.StartFunctionDefaults));
        Assert.Equal(written, make.Instructions.Any(instruction => instruction.OpCode == ExecutableOpCode.StoreFunctionDefault));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RefusedNativeDefinitionDoesNotPublishTheCallable(bool asynchronous, bool capturesLocal)
    {
        const string keywordDefault = """
            def outer(value):
                def make():
                    def call(*, item=value): return item
                    record(call)
                    return call
                return make
            record(outer("live"))
            """;
        const string localCapture = """
            def factory(value):
                def read(): return value
                record(read)
                return read
            record(factory)
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var scope = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(scope);
            var (code, context, capture) = Prepare(capturesLocal ? localCapture : keywordDefault,
                new LythonRunOptions { MemoryBudget = hook });
            try
            {
                if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                else LythonRuntime.ExecuteExecutableCodeObject(code, context);
                var make = Assert.IsType<PyExecutableFunction>(Assert.Single(capture.Functions));
                var definition = Assert.Single(make.CodeObject.Instructions, instruction =>
                    instruction.OpCode == ExecutableOpCode.ApplyOperation &&
                    make.CodeObject.Constants[instruction.ConstantIndex] is ExecutableCreateFunction function &&
                    (capturesLocal ? function.CodeObject.ClosureNames.Count == 1 : function.DirectKeywordDefault));
                var baseline = budget.ReservedBytes;
                hook.RefuseGrowth = _ => true;
                CallArgumentValue[] arguments = capturesLocal ? [CallArgumentValue.Positional(BigInteger.One)] : [];
                var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                {
                    if (asynchronous) await make.InvokeAsync(arguments, Span, context);
                    else make.Invoke(arguments, Span, context);
                });
                Assert.Same(definition.Span, failure.Span);
                Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Single(capture.Functions);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            finally
            {
                hook.RefuseGrowth = null;
                context.State.CloseResources();
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefinitionsPreserveExecutableCodeDefaultsAndClosureCells(bool asynchronous)
    {
        var (code, context, capture) = Prepare("def outer(seed):\n    def add(amount=seed,*,scale=2):\n        marker=amount+seed\n        return marker*scale\n    return add\nrecord(outer)\nrecord(outer(3))");
        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        Assert.Equal(2, capture.Functions.Count);
        Assert.All(capture.Functions, f => Assert.IsType<PyExecutableFunction>(f));
        var function = capture.Functions[1];
        Assert.Equal(new BigInteger(12), asynchronous
            ? await function.InvokeAsync([], Span, context) : function.Invoke([], Span, context));
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncBodyKeepsBoundSlotsUntilYieldedCallbackFinishes(bool fail)
    {
        var (code, context, capture) = Prepare("def f(a,b):\n    marker=a+b\n    return tap(marker)\nrecord(f)");
        var tap = new SuspendedTap(fail);
        context.Variables["tap"] = tap;
        await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
        var function = Assert.Single(capture.Functions);
        var pending = function.InvokeAsync([CallArgumentValue.Positional(new BigInteger(7)),
            CallArgumentValue.Positional(new BigInteger(5))], Span, context).AsTask();
        try
        {
            await tap.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.NotNull(tap.Frame?.CurrentExecutableFrame);
            Assert.True(tap.Frame!.TryReadExecutableLocal("a", out var a));
            Assert.Equal(new BigInteger(7), a);
            tap.Release.TrySetResult();
            if (fail) Assert.Equal("ValueError", (await Assert.ThrowsAsync<LythonRuntimeException>(() => pending)).ExceptionType);
            else Assert.Equal(new BigInteger(12), await pending);
        }
        finally
        {
            tap.Release.TrySetResult();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (LythonRuntimeException) when (fail) { }
        }
        Assert.Null(tap.Frame!.CurrentExecutableFrame);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.State.CurrentRecursionDepth);
    }

    [Fact]
    public async Task FailedSuspendingDefaultLookupRefundsUnpublishedVariadicArguments()
    {
        const string source = """
            class Key:
                def __hash__(self): return hash('required')
                def __eq__(self, other): return tap(other)
            def f(*items, required, **options): return required
            f.__kwdefaults__ = {Key(): 0}
            record(f)
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var scope = budget.CreateScope())
        {
            var (code, context, capture) = Prepare(source, new LythonRunOptions { MemoryBudget = scope });
            var tap = new SuspendedTap(fail: true);
            context.Variables["tap"] = tap;
            await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
            var function = Assert.Single(capture.Functions);
            CallArgumentValue[] arguments =
            [CallArgumentValue.Positional(BigInteger.One), CallArgumentValue.Keyword("overflow", BigInteger.One)];
            var pending = function.InvokeAsync(arguments, Span, context).AsTask();
            try
            {
                await tap.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                tap.Release.TrySetResult();
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
                Assert.Equal("ValueError", error.ExceptionType);

                // The first call also warms reusable binding and callback storage.
                var admitted = budget.ReservedBytes;
                error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                    await function.InvokeAsync(arguments, Span, context));
                Assert.Equal("ValueError", error.ExceptionType);
                Assert.Equal(admitted, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally
            {
                tap.Release.TrySetResult();
                try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (LythonRuntimeException) { }
                finally { context.State.CloseResources(); }
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("g=42\ndef g(x=tap(1)):\n    return x")]
    [InlineData("g=42\n@tap\ndef g():\n    return 1")]
    public async Task CancelledDefinitionKeepsThePreviousBinding(string source)
    {
        var (code, context, _) = Prepare(source);
        // Namespace observations survive execution; active slot views do not.
        context.MirrorLocalStores = true;
        var tap = new SuspendedTap(false);
        context.Variables["tap"] = tap;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
        try
        {
            await tap.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var frame = Assert.IsAssignableFrom<LythonRuntime.ExecutionContext>(tap.Frame!.CurrentExecutableFrame);
            Assert.True(frame.TryReadExecutableLocal("g", out var previous));
            Assert.Equal(new BigInteger(42), previous);
            tap.Release.TrySetCanceled();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Equal(new BigInteger(42), context.Variables["g"]);
        }
        finally
        {
            tap.Release.TrySetCanceled();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
        }
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportsPreserveBoundArgumentsAndUnmirroredLocals(bool asynchronous)
    {
        var (code, context, capture) = Prepare("marker=9\nimport math\ndef f(value):\n    preserved=value+1\n    import operator\n    return operator.add(preserved,9)\nrecord(f)");
        Assert.False(code.RequiresLocalVariableMirroring);
        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        var function = Assert.Single(capture.Functions);
        var arguments = new[] { CallArgumentValue.Positional(new BigInteger(2)) };
        Assert.Equal(new BigInteger(12), asynchronous ? await function.InvokeAsync(arguments, Span, context) : function.Invoke(arguments, Span, context));
    }

    private static (ExecutableCodeObject, Context, Capture) Prepare(string source, LythonRunOptions? options = null)
    {
        var frontend = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance);
        Assert.NotNull(frontend.Script);
        var code = ExecutableScript.Compile(frontend.Script).EntryPoint;
        var context = new Context(new MockLythonHost(), options ?? new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var capture = new Capture();
        context.Variables["record"] = capture;
        return (code, context, capture);
    }

    private sealed class Capture : LythonRuntime.ICallable
    {
        internal List<PyFunctionBase> Functions { get; } = [];
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Functions.Add(Assert.IsAssignableFrom<PyFunctionBase>(Assert.Single(arguments).Value));
            return PyNone.Instance;
        }
    }

    private sealed class SuspendedTap(bool fail) : LythonRuntime.ICallable
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Context? Frame;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => throw new InvalidOperationException("Async invocation must not bridge through synchronous guest execution.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Frame = context;
            Started.TrySetResult();
            await Release.Task.ConfigureAwait(false);
            if (fail) throw new LythonRuntimeException("ValueError", "callback failure", span);
            return Assert.Single(arguments).Value;
        }
    }
}
