using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

// Exercise functions produced by real class definitions, rather than manually
// constructing a function with a module closure. These boundaries must survive
// a future change to the representation of class-method bodies.
public sealed class ClassMethodExecutionBoundaryTests(ITestOutputHelper output)
{
    private static readonly LythonSourceSpan CallSpan = new(100, 6, 20, 3);

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task ClassFactoryMethodsKeepFuelBoundaries(int width, bool asynchronous)
    {
        for (var remaining = 0; remaining <= 3; remaining++)
        {
            var (context, _, receiver, method, definition) = await Define(width, "        return self\n", asynchronous);
            var expression = Assert.IsType<LoweredReturnStatement>(Assert.Single(definition.Body)).Expression!;
            context.Limits.ExecutionStepCount = 1000 - remaining;
            if (remaining == 3)
            {
                Assert.Same(receiver, await Invoke(method, Arguments(width), context, asynchronous));
                Assert.Equal(1000, context.Limits.ExecutionStepCount);
            }
            else
            {
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                    await Invoke(method, Arguments(width), context, asynchronous));
                Assert.Equal("maximum execution step count exceeded (1000)", error.Message);
                Assert.Same(remaining < 2 ? CallSpan : expression.Span, error.Span);
                Assert.Equal(remaining < 2 ? null : "/class-method.py", error.SourcePath);
                Assert.Equal(1001, context.Limits.ExecutionStepCount);
            }
            AssertUnwound(context);
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task ClassFactoryMethodsCheckFuelBeforeCancellation(int width, bool asynchronous)
    {
        foreach (var fuelDenied in new[] { false, true })
        {
            using var cancellation = new CancellationTokenSource();
            var (context, _, _, method, _) = await Define(width, "        return self\n", asynchronous, cancellation.Token);
            context.Limits.ExecutionStepCount = fuelDenied ? 1000 : 0;
            cancellation.Cancel();
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                await Invoke(method, Arguments(width), context, asynchronous));
            Assert.Equal(fuelDenied ? "maximum execution step count exceeded (1000)" : "execution canceled", error.Message);
            Assert.Same(CallSpan, error.Span);
            Assert.Equal(fuelDenied ? 1001 : 1, context.Limits.ExecutionStepCount);
            AssertUnwound(context);
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task ClassFactoryMethodsRollBackRejectedAdmission(int width, bool asynchronous)
    {
        var (context, _, _, method, _) = await Define(width, "        return self\n", asynchronous);
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        context.Services.SetCurrentException(active);
        var before = context.MemoryGovernor.CurrentAccountedBytes;
        // The real class and receiver already exist. Deny only this invocation.
        context.Limits.CurrentRecursionDepth = context.Limits.MaxRecursionDepth!.Value;
        try
        {
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                await Invoke(method, Arguments(width), context, asynchronous));
            Assert.Equal("RecursionError", error.ExceptionType);
            Assert.Same(CallSpan, error.Span);
            Assert.Equal(before, context.MemoryGovernor.CurrentAccountedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Same(active, context.Services.CurrentException);
            Assert.Equal(context.Limits.MaxRecursionDepth.Value, context.Limits.CurrentRecursionDepth);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        }
        finally { context.Limits.CurrentRecursionDepth = 0; }
        Assert.Same(context.Variables["receiver"], await Invoke(method, Arguments(width), context, asynchronous));
        AssertUnwound(context);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ClassCellsArgumentsAndLogicalFundingAgreeAcrossModes(int width)
    {
        var snapshots = new List<(long Accounted, long Reserved, long Steps, long Depth)>();
        foreach (var asynchronous in new[] { false, true })
        {
            var suffix = string.Concat(Enumerable.Range(0, width).Select(i => $", arg{i}"));
            var body = $"        local = [17]\n        return tap(self, __class__, local{suffix})\n";
            var (context, type, receiver, method, _) = await Define(width, body, asynchronous);
            var active = new PyException("ValueError", "outer", PyNone.Instance);
            context.Services.SetCurrentException(active);
            var arguments = Arguments(width);
            var tap = new RecordingTap(frame =>
            {
                Assert.NotSame(context, frame);
                Assert.Same(context.Services, frame.Services);
                Assert.Equal("/class-method.py", frame.SourcePath);
                Assert.Same(active, frame.Services.CurrentException);
                Assert.Same(receiver, frame.Variables["self"]);
                var local = Assert.IsType<PyList>(frame.Variables["local"]);
                Assert.Equal(new BigInteger(17), Assert.Single(local));
                Assert.Same(context.MemoryGovernor, local.OwnerMemoryGovernor);
                Assert.Same(type, frame.ImplicitSuperClassCell!.Value);
                Assert.Same(frame.ParentContext!.ClassCell, frame.ImplicitSuperClassCell);
                Assert.Same(receiver, frame.ImplicitSuperReceiver);
                Assert.Equal(1, frame.Limits.CurrentRecursionDepth);
                for (var i = 0; i < width; i++) Assert.Same(arguments[i].Value, frame.Variables[$"arg{i}"]);
            });
            context.Variables["tap"] = tap;
            var before = context.MemoryGovernor.CurrentAccountedBytes;
            context.Limits.ExecutionStepCount = 0;
            Assert.Same(receiver, await Invoke(method, arguments, context, asynchronous));
            Assert.Equal(1, tap.Calls);
            Assert.Equal(asynchronous, tap.UsedAsyncEntry);
            Assert.Same(receiver, tap.Arguments![0].Value);
            Assert.Same(type, tap.Arguments[1].Value);
            Assert.IsType<PyList>(tap.Arguments[2].Value);
            for (var i = 0; i < width; i++) Assert.Same(arguments[i].Value, tap.Arguments[3 + i].Value);
            snapshots.Add((tap.Accounted - before, tap.Reserved, tap.Steps, tap.Depth));
            output.WriteLine($"width={width}, async={asynchronous}, snapshot={snapshots[^1]}");
            Assert.Same(active, context.Services.CurrentException);
            AssertUnwound(context);
        }
        Assert.Equal(snapshots[0], snapshots[1]);
        // Pinned from the unchanged production run before a body prototype:
        // retained list funding, no outstanding reservation, exact checkpoints,
        // and the call-expression interpreter depth at the observation site.
        Assert.Equal((384L, 0L, 13L + width, 1L), snapshots[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealAsyncBodyKeepsItsFrameAndArgumentsAcrossReentry(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var (context, type, receiver, method, _) = await Define(1,
            "        return tap(self, __class__, arg0)\n    def identity(self, arg0):\n        return self\n", true, cancellation.Token);
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        context.Services.SetCurrentException(active);
        var tap = new SuspendedTap(cancellation.Token);
        context.Variables["tap"] = tap;
        var argument = new object();
        var pending = method.InvokeAsync([CallArgumentValue.Positional(argument)], CallSpan, context);
        await tap.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(pending.IsCompleted);
        var retainedFrame = tap.Frame!;
        Assert.Same(receiver, retainedFrame.Variables["self"]);
        Assert.Same(argument, retainedFrame.Variables["arg0"]);
        Assert.Same(type, retainedFrame.ImplicitSuperClassCell!.Value);
        Assert.Same(context.Services, retainedFrame.Services);

        // Run another real factory-produced method while the first frame owns
        // its binder lease. The first call's references must remain untouched.
        var secondReceiver = Assert.IsType<PyInstance>(type.Invoke([], CallSpan, context));
        Assert.True(type.TryGetOwnMember("identity", out var identity));
        var secondMethod = Assert.IsType<PyBoundMethod>(Assert.IsAssignableFrom<PyFunctionBase>(identity).Bind(secondReceiver));
        Assert.Same(secondReceiver, await secondMethod.InvokeAsync([CallArgumentValue.Positional(new object())], CallSpan, context));
        Assert.Equal(1, context.Limits.CurrentRecursionDepth);
        Assert.Same(argument, tap.Arguments![2].Value);
        Assert.Same(argument, retainedFrame.Variables["arg0"]);
        if (cancel)
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        }
        else
        {
            tap.Release.SetResult();
            Assert.Same(receiver, await pending);
        }
        AssertUnwound(context);
        Assert.Same(active, context.Services.CurrentException);
        Assert.Equal(0, retainedFrame.Limits.CurrentRecursionDepth);
    }

    private sealed class RecordingTap(Action<Context> inspect) : LythonRuntime.ICallable
    {
        public int Calls { get; private set; }
        public bool UsedAsyncEntry { get; private set; }
        public CallArgumentValue[]? Arguments { get; private set; }
        public long Accounted { get; private set; }
        public long Reserved { get; private set; }
        public long Steps { get; private set; }
        public long Depth { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            inspect(context);
            Arguments = arguments.ToArray();
            Accounted = context.MemoryGovernor.CurrentAccountedBytes;
            Reserved = context.MemoryGovernor.CurrentReservedBytes;
            Steps = context.Limits.ExecutionStepCount;
            Depth = context.Limits.CurrentInterpreterDepth;
            return arguments[0].Value;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            UsedAsyncEntry = true;
            await Task.Yield();
            return Invoke(arguments, span, context);
        }
    }

    private sealed class SuspendedTap(CancellationToken cancellation) : LythonRuntime.ICallable
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Context? Frame { get; private set; }
        public CallArgumentValue[]? Arguments { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => throw new InvalidOperationException("The async method body used the synchronous callable entry.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Frame = context;
            Arguments = arguments;
            Started.SetResult();
            await Release.Task.WaitAsync(cancellation);
            return arguments[0].Value;
        }
    }

    private async Task<(Context Context, PyType Type, PyInstance Receiver, PyBoundMethod Method, LoweredFunctionDefinitionStatement Definition)>
        Define(int width, string body, bool asynchronous, CancellationToken cancellation = default)
    {
        var suffix = string.Concat(Enumerable.Range(0, width).Select(i => $", arg{i}"));
        var frontend = LythonFrontend.Compile($"class C:\n    def f(self{suffix}):\n{body}\nreceiver = C()\n");
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var lowered = LoweredScript.Lower(frontend.Script.RequireNotNull());
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        {
            MaxExecutionSteps = 1000, MaxExecutionMemoryBytes = 1024 * 1024,
            SourcePath = "/class-method.py", CancellationToken = cancellation
        });
        if (asynchronous) await LythonRuntime.ExecuteStatementsAsync(lowered.Statements, context);
        else LythonRuntime.ExecuteExecutableCodeObject(ExecutableScript.Compile(lowered).EntryPoint, context, default, [], null);
        var type = Assert.IsType<PyType>(context.Variables["C"]);
        Assert.True(type.TryGetOwnMember("f", out var value));
        var function = Assert.IsAssignableFrom<PyFunctionBase>(value);
        output.WriteLine($"class factory: async={asynchronous}, callable={function.GetType().Name}, width={width}");
        Assert.Same(type, function.OwnerType);
        var receiver = Assert.IsType<PyInstance>(context.Variables["receiver"]);
        var method = Assert.IsType<PyBoundMethod>(function.Bind(receiver));
        var definition = Assert.Single(Assert.IsType<LoweredClassDefinitionStatement>(lowered.Statements[0]).Body
            .OfType<LoweredFunctionDefinitionStatement>(), definition => definition.Syntax.DeclaredName == "f");
        AssertUnwound(context);
        return (context, type, receiver, method, definition);
    }

    private static CallArgumentValue[] Arguments(int width)
        => Enumerable.Range(0, width).Select(_ => CallArgumentValue.Positional(new object())).ToArray();
    private static ValueTask<object> Invoke(PyBoundMethod method, CallArgumentValue[] arguments, Context context, bool asynchronous)
        => asynchronous ? method.InvokeAsync(arguments, CallSpan, context) : ValueTask.FromResult(method.Invoke(arguments, CallSpan, context));
    private static void AssertUnwound(Context context)
    {
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(context.CurrentExecutableFrame);
    }
}
