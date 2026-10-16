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
        for (var remaining = 0; remaining <= 5; remaining++)
        {
            var (context, _, receiver, method, definition) = await Define(width, "        return self\n", asynchronous);
            var instructions = definition.PreparedCode!.Blocks[definition.PreparedCode.EntryBlockIndex].InstructionArray;
            context.Limits.ExecutionStepCount = 1000 - remaining;
            if (remaining == 5)
            {
                Assert.Same(receiver, await Invoke(method, Arguments(width), context, asynchronous));
                Assert.Equal(1000, context.Limits.ExecutionStepCount);
            }
            else
            {
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                    await Invoke(method, Arguments(width), context, asynchronous));
                Assert.Equal("maximum execution step count exceeded (1000)", error.Message);
                Assert.Same(remaining < 3 ? CallSpan : instructions[remaining - 3].Span, error.Span);
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
                Assert.Same(receiver, OwnLocal(frame, "self"));
                var local = Assert.IsType<PyList>(OwnLocal(frame, "local"));
                Assert.Equal(new BigInteger(17), Assert.Single(local));
                Assert.Same(context.MemoryGovernor, local.OwnerMemoryGovernor);
                Assert.Same(type, frame.ImplicitSuperClassCell!.Value);
                Assert.Same(frame.ParentContext!.ClassCell, frame.ImplicitSuperClassCell);
                Assert.Equal("self", frame.FirstPositionalParameterName);
                Assert.True(frame.TryResolveOwnLocal("self", out var liveReceiver));
                Assert.Same(receiver, liveReceiver);
                Assert.Equal(1, frame.Limits.CurrentRecursionDepth);
                for (var i = 0; i < width; i++) Assert.Same(arguments[i].Value, OwnLocal(frame, $"arg{i}"));
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
        Assert.True(snapshots[0].Accounted > 0);
        Assert.Equal((0L, 13L + width, 1L), (snapshots[0].Reserved, snapshots[0].Steps, snapshots[0].Depth));
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
        var pending = method.InvokeAsync([CallArgumentValue.Positional(argument)], CallSpan, context).AsTask();
        try
        {
            await tap.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            var retainedFrame = tap.Frame!;
            Assert.Same(receiver, OwnLocal(retainedFrame, "self"));
            Assert.Same(argument, OwnLocal(retainedFrame, "arg0"));
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
            Assert.Same(argument, OwnLocal(retainedFrame, "arg0"));
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
        finally
        {
            tap.Release.TrySetResult();
            _ = await Xunit.Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuperReadsTheFirstLocalAfterARealAsyncSuspension(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var (context, type, original, method, _) = await Define(1,
            "        self = tap(arg0)\n        return super().__self__\n", true, cancellation.Token);
        var replacement = new PyInstance(type);
        var tap = new SuspendedTap(cancellation.Token);
        context.Variables["tap"] = tap;
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        context.Services.SetCurrentException(active);
        var pending = method.InvokeAsync([CallArgumentValue.Positional(replacement)], CallSpan, context).AsTask();
        try
        {
            await tap.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            var frame = tap.Frame!;
            Assert.NotSame(context, frame);
            Assert.Same(context.Services, frame.Services);
            Assert.Same(type, frame.ImplicitSuperClassCell!.Value);
            Assert.Equal("self", frame.FirstPositionalParameterName);
            Assert.True(frame.TryResolveOwnLocal("self", out var current));
            Assert.Same(original, current);
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
            }
            else
            {
                tap.Release.SetResult();
                Assert.Same(replacement, await pending);
            }
            AssertUnwound(context);
            Assert.Same(active, context.Services.CurrentException);
        }
        finally
        {
            tap.Release.TrySetResult();
            _ = await Xunit.Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectSuperPreloadsPreserveTheActualCallAndLogicalFunding(bool asynchronous)
    {
        var observations = new List<(long Bytes, long Steps)>();
        foreach (var suppressPreload in new[] { false, true })
        {
            // An explicit global declaration suppresses Python's lexical
            // preload. Both calls still resolve the same replacement callable.
            var body = (suppressPreload ? "        global super\n" : "") +
                "        nonlocal __class__\n        __class__=None\n        self=None\n        return super().answer\n";
            var (context, type, receiver, method, _) = await Define(0, body, asynchronous);
            var result = new object();
            receiver.SetAttribute("answer", result);
            var tap = new ReplacementSuper(receiver, frame =>
            {
                Assert.NotSame(context, frame);
                Assert.Same(context.Services, frame.Services);
                Assert.Equal("/class-method.py", frame.SourcePath);
                Assert.True(frame.TryResolveOwnLocal("self", out var value));
                Assert.Same(PyNone.Instance, value);
                Assert.Same(PyNone.Instance, frame.ImplicitSuperClassCell!.Value);
                Assert.Same(frame.ParentContext!.ClassCell, frame.ImplicitSuperClassCell);
                Assert.Equal(1, frame.Limits.CurrentRecursionDepth);
            });
            context.Variables["super"] = tap;
            var before = context.MemoryGovernor.CurrentAccountedBytes;
            context.Limits.ExecutionStepCount = 0;
            Assert.Same(result, await Invoke(method, [], context, asynchronous));
            Assert.Equal(1, tap.Calls);
            Assert.Equal(asynchronous, tap.UsedAsyncEntry);
            observations.Add((context.MemoryGovernor.CurrentAccountedBytes - before,
                context.Limits.ExecutionStepCount));
            AssertUnwound(context);
        }
        Assert.Equal(observations[1].Bytes, observations[0].Bytes);
        Assert.Equal(observations[1].Steps + 1, observations[0].Steps);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectSuperDeletedOperandsFailBeforeTheReplacementCall(bool asynchronous)
    {
        foreach (var removeClass in new[] { false, true })
        {
            var body = (removeClass ? "        nonlocal __class__\n        del __class__\n" : "") +
                "        del self\n        return super().answer\n";
            var (context, _, receiver, method, _) = await Define(0, body, asynchronous);
            var tap = new ReplacementSuper(receiver, _ => throw new InvalidOperationException("Deleted preload reached the callable."));
            context.Variables["super"] = tap;
            var active = new PyException("ValueError", "outer", PyNone.Instance);
            context.Services.SetCurrentException(active);
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Invoke(method, [], context, asynchronous));
            Assert.Equal(removeClass ? "NameError" : "UnboundLocalError", error.ExceptionType);
            Assert.Equal(removeClass
                ? "cannot access free variable '__class__' where it is not associated with a value in enclosing scope"
                : "cannot access local variable 'self' where it is not associated with a value", error.Message);
            Assert.Equal(0, tap.Calls);
            Assert.Same(active, context.Services.CurrentException);
            AssertUnwound(context);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacementSuperKeepsItsAsyncFrameAcrossReentry(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var (context, type, receiver, method, _) = await Define(1,
            "        return super().answer\n    def identity(self,arg0):\n        return self\n", true, cancellation.Token);
        receiver.SetAttribute("answer", receiver);
        var tap = new ReplacementSuper(receiver, _ => { }, cancellation.Token, suspend: true);
        context.Variables["super"] = tap;
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        context.Services.SetCurrentException(active);
        var argument = new object();
        var pending = method.InvokeAsync([CallArgumentValue.Positional(argument)], CallSpan, context).AsTask();
        try
        {
            await tap.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            var frame = tap.Frame!;
            Assert.Same(context.Services, frame.Services);
            Assert.Same(receiver, OwnLocal(frame, "self"));
            Assert.Same(argument, OwnLocal(frame, "arg0"));
            var cell = frame.ImplicitSuperClassCell!;
            Assert.Same(type, cell.Value);
            // Change the actual shared cell while the real async call owns its
            // binder lease, then reenter a neighboring method with no cell use.
            cell.Value = PyNone.Instance;
            Assert.True(type.TryGetOwnMember("identity", out var identity));
            var secondReceiver = new PyInstance(type);
            var second = Assert.IsType<PyBoundMethod>(Assert.IsAssignableFrom<PyFunctionBase>(identity).Bind(secondReceiver));
            Assert.Same(secondReceiver, await second.InvokeAsync([CallArgumentValue.Positional(new object())], CallSpan, context));
            Assert.Same(argument, OwnLocal(frame, "arg0"));
            Assert.Same(cell, frame.ImplicitSuperClassCell);
            Assert.Same(PyNone.Instance, cell.Value);
            Assert.Equal(1, context.Limits.CurrentRecursionDepth);
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
            Assert.Same(active, context.Services.CurrentException);
            AssertUnwound(context);
        }
        finally
        {
            tap.Release.TrySetResult();
            _ = await Xunit.Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EagerComprehensionKeepsTheInvocationAndSeparateCapturedCells(bool asynchronous)
    {
        var (context, type, original, method, _) = await Define(1,
            "        old=lambda:self\n        before(self)\n        values=[tap(self,old(),__class__,arg0) for self in [arg0]]\n        return values,self\n", asynchronous);
        var replacement = new PyInstance(type);
        Context? invocation = null;
        context.Variables["before"] = new RecordingTap(frame => invocation = frame);
        var tap = new RecordingTap(frame =>
        {
            Assert.Same(invocation, frame);
            Assert.Same(context.Services, frame.Services);
            Assert.Same(original, OwnLocal(frame, "self"));
            Assert.True(frame.TryResolveOwnLocal("self", out var current));
            Assert.Same(replacement, current);
            Assert.Same(type, frame.ImplicitSuperClassCell!.Value);
            Assert.Same(replacement, OwnLocal(frame, "arg0"));
        });
        context.Variables["tap"] = tap;
        var result = Assert.IsType<PyTuple>(await Invoke(method, [CallArgumentValue.Positional(replacement)], context, asynchronous));
        Assert.Same(replacement, Assert.Single(Assert.IsType<PyList>(result[0])));
        Assert.Same(original, result[1]);
        Assert.Same(replacement, tap.Arguments![0].Value);
        Assert.Same(original, tap.Arguments[1].Value);
        Assert.Same(type, tap.Arguments[2].Value);
        Assert.Same(replacement, tap.Arguments[3].Value);
        Assert.Same(invocation, invocation!.FunctionClosureContext);
        AssertUnwound(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EagerComprehensionRestoresItsBindingsAfterAsyncReentry(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var (context, type, original, method, _) = await Define(1,
            "        before(self)\n        values=[tap(self,__class__,arg0) for self in [arg0]]\n        after(self)\n        return values\n    def identity(self,arg0):\n        return self\n", true, cancellation.Token);
        var replacement = new PyInstance(type);
        Context? invocation = null;
        context.Variables["before"] = new RecordingTap(frame => invocation = frame);
        var tap = new SuspendedTap(cancellation.Token);
        var after = new RecordingTap(frame =>
        {
            Assert.Same(invocation, frame);
            Assert.True(frame.TryResolveOwnLocal("self", out var restored));
            Assert.Same(original, restored);
        });
        context.Variables["after"] = after;
        context.Variables["tap"] = tap;
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        context.Services.SetCurrentException(active);
        var pending = method.InvokeAsync([CallArgumentValue.Positional(replacement)], CallSpan, context).AsTask();
        try
        {
            await tap.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            var frame = tap.Frame!;
            Assert.Same(invocation, frame);
            Assert.Same(context.Services, frame.Services);
            Assert.True(frame.TryResolveOwnLocal("self", out var current));
            Assert.Same(replacement, current);
            Assert.Same(original, OwnLocal(frame, "self"));
            Assert.NotSame(frame, frame.FunctionClosureContext);
            Assert.Same(replacement, frame.FunctionClosureContext.Variables["self"]);
            Assert.True(type.TryGetOwnMember("identity", out var identity));
            var second = Assert.IsType<PyBoundMethod>(Assert.IsAssignableFrom<PyFunctionBase>(identity).Bind(original));
            Assert.Same(original, await second.InvokeAsync([CallArgumentValue.Positional(new object())], CallSpan, context));
            Assert.Equal(1, context.Limits.CurrentRecursionDepth);
            Assert.Same(replacement, tap.Arguments![0].Value);
            Assert.Same(replacement, OwnLocal(frame, "arg0"));
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
            }
            else
            {
                tap.Release.SetResult();
                Assert.Same(replacement, Assert.Single(Assert.IsType<PyList>(await pending)));
            }
            Assert.Equal(cancel ? 0 : 1, after.Calls);
            Assert.Same(frame, frame.FunctionClosureContext);
            Assert.Same(active, context.Services.CurrentException);
            AssertUnwound(context);
        }
        finally
        {
            tap.Release.TrySetResult();
            _ = await Xunit.Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratorOuterAcquisitionUsesRealAsyncCallsAndIteratorIdentity(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var (context, type, original, method, _) = await Define(1,
            "        before(self,arg0)\n        return (super().__self__ for _ in arg0)\n    def __iter__(self):\n        return tap(self)\n    def __next__(self):\n        return 1\n    def identity(self,arg0):\n        return self\n", true, cancellation.Token);
        var iterator = new PyInstance(type);
        Context? invocation = null;
        context.Variables["before"] = new RecordingTap(frame => invocation = frame);
        var tap = new SuspendedTap(cancellation.Token);
        context.Variables["tap"] = tap;
        var pending = method.InvokeAsync([CallArgumentValue.Positional(iterator)], CallSpan, context).AsTask();
        try
        {
            await tap.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            Assert.Same(iterator, OwnLocal(tap.Frame!, "self"));
            Assert.Null(tap.Frame!.ImplicitSuperClassCell);
            Assert.Same(iterator, OwnLocal(invocation!, "arg0"));
            Assert.Same(original, OwnLocal(invocation!, "self"));
            Assert.Same(context.Services, invocation!.Services);
            Assert.Equal(2, context.Limits.CurrentRecursionDepth);
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
            }
            else
            {
                tap.Release.SetResult();
                var generator = Assert.IsAssignableFrom<IPyAsyncIteratorValue>(await pending);
                var first = await generator.TryMoveNextAsync();
                Assert.True(first.HasValue);
                Assert.Same(iterator, first.Value);
            }
            AssertUnwound(context);
        }
        finally
        {
            tap.Release.TrySetResult();
            _ = await Xunit.Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidGeneratorOuterDoesNotRetainAnIteratorCharge(bool asynchronous)
    {
        var (context, _, _, method, _) = await Define(1,
            "        return (item for item in arg0)\n", asynchronous);
        var before = context.MemoryGovernor.CurrentAccountedBytes;
        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAnyAsync<LythonRuntimeException>(async () =>
                await Invoke(method, [CallArgumentValue.Positional(new BigInteger(37))], context, asynchronous));
            Assert.Equal(before, context.MemoryGovernor.CurrentAccountedBytes);
            AssertUnwound(context);
        }
    }

    private sealed class ReplacementSuper(object result, Action<Context> inspect,
        CancellationToken cancellation = default, bool suspend = false) : LythonRuntime.ICallable
    {
        public int Calls { get; private set; }
        public bool UsedAsyncEntry { get; private set; }
        public Context? Frame { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            Assert.Empty(arguments);
            Frame = context;
            inspect(context);
            return result;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            UsedAsyncEntry = true;
            var value = Invoke(arguments, span, context);
            Started.SetResult();
            if (suspend) await Release.Task.WaitAsync(cancellation);
            else await Task.Yield();
            return value;
        }
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

    private static object OwnLocal(Context frame, string name)
    {
        if (frame.CurrentExecutableFrame?.TryResolveOwnLocal(name, out var value) == true) return value;
        return frame.Variables[name];
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
