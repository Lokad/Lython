using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ActiveExecutionCallerTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResumedGeneratorIncludesItsDynamicCallerAndWithdrawsTheLinkAfterYield(bool asynchronous)
    {
        var context = Context();
        var probe = new Probe();
        context.Variables["probe"] = probe;
        context.Variables["next"] = new Advance();
        var code = Compile("def values():\n    probe()\n    yield None\ndef caller():\n    saved = 'x' * 16000\n    cursor = values()\n    next(cursor)\ncaller()\n");
        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null);
        Assert.True(probe.Checked);
        Assert.Null(Active(context.State));
        Assert.Null(Caller(probe.Frame!));
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task FailedFunctionAndGeneratorRestoreTheExactPublishedCaller(bool asynchronous, bool cancellation)
    {
        var context = Context();
        var probe = new FailingProbe(cancellation);
        context.Variables["ValueError"] = context.State.BuiltinVariables["ValueError"];
        context.Variables["probe"] = probe;
        context.Variables["next"] = new Advance();
        var code = Compile("def values():\n    probe()\n    yield None\ndef caller():\n    next(values())\ntry:\n    caller()\nexcept ValueError:\n    pass\n");
        async Task Execute()
        {
            if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context);
            else LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null);
        }
        if (cancellation) await Assert.ThrowsAnyAsync<OperationCanceledException>(Execute);
        else await Execute();
        Assert.NotNull(probe.Frame);
        Assert.Null(Caller(probe.Frame!));
        Assert.Null(Active(context.State));
        Assert.Null(context.CurrentExecutableFrame);
    }

    [Fact]
    public void FunctionAdmissionFailurePublishesNoCallerLink()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxRecursionDepth = 0 });
        var previous = new LythonRuntime.ExecutableFrameState(Compile("pass\n").EntryPoint, [], null, null);
        context.EnterExecutableSlots(previous);
        var child = new LythonRuntime.ExecutionContext(context);
        Assert.Throws<LythonRuntimeException>(() => child.EnterFunctionCall(Span));
        Assert.Same(previous, Active(context.State));
        Assert.Null(Caller(child));
        context.LeaveExecutableSlots(null);
        Assert.Null(Active(context.State));
    }

    [Fact]
    public void FailedInterpreterCreationKeepsTheAlreadyPublishedCaller()
    {
        var context = Context();
        var previous = new LythonRuntime.ExecutableFrameState(Compile("pass\n").EntryPoint, [], null, null);
        context.EnterExecutableSlots(previous);
        var code = Compile("pass\n").EntryPoint;
        var bound = new BoundCallArguments(new object[] { PyNone.Instance }, new ArgumentPresence(1));
        // Controlled invalid binding reaches actual interpreter construction,
        // before the new frame can be published.
        Assert.Throws<IndexOutOfRangeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code, context, bound, new[] { int.MaxValue }, null));
        Assert.Same(previous, Active(context.State));
        context.LeaveExecutableSlots(null);
        Assert.Null(Active(context.State));
        Assert.Null(Caller(previous));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SimpleReturnRestoresTheActualCallingFrame(bool asynchronous)
    {
        var context = Context();
        var check = new RootEcho();
        context.Variables["check"] = check;
        var code = Compile("def increment(value):\n    return value + 1\ncheck(increment(2))\n");
        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null);
        Assert.True(check.Checked);
        Assert.Null(Active(context.State));
    }

    private static object? Active(ExecutionState state) => state.GetType()
        .GetProperty("ActiveExecutionRoot", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(state);

    private static object? Caller(object frame) => frame.GetType()
        .GetProperty("ActiveCaller", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(frame);

    private static ExecutableScript Compile(string source)
    {
        var parsed = LythonFrontend.Compile(source);
        Assert.Empty(parsed.Diagnostics);
        return ExecutableScript.Compile(LoweredScript.Lower(parsed.Script!));
    }

    private static LythonRuntime.ExecutionContext Context()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        context.Variables.Clear();
        return context;
    }

    private sealed class Advance : LythonRuntime.ICallable, IMemoryStorageEdges
    {
        public void VisitStorageChildren(MemoryStorageWalk visitor) { }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var caller = Active(context.State);
            var generator = Assert.IsType<LythonRuntime.PyGenerator>(Assert.Single(arguments).Value);
            Assert.True(generator.TryMoveNext(out var value));
            Assert.Null(Caller(generatorFrame(generator)));
            Assert.Same(caller, Active(context.State));
            return value;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var caller = Active(context.State);
            var generator = Assert.IsType<LythonRuntime.PyGenerator>(Assert.Single(arguments).Value);
            var result = await generator.TryMoveNextAsync();
            Assert.True(result.HasValue);
            Assert.Null(Caller(generatorFrame(generator)));
            Assert.Same(caller, Active(context.State));
            return result.Value;
        }
        private static object generatorFrame(object generator) => generator.GetType().GetField("_interpreter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(generator)!;
    }

    private sealed class RootEcho : LythonRuntime.ICallable, IMemoryStorageEdges
    {
        internal bool Checked;
        public void VisitStorageChildren(MemoryStorageWalk visitor) { }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Assert.Same(context.CurrentExecutableFrame, Active(context.State));
            Checked = true;
            return PyNone.Instance;
        }
    }

    private sealed class Probe : LythonRuntime.ICallable, IMemoryStorageEdges
    {
        internal bool Checked;
        internal object? Frame;
        public void VisitStorageChildren(MemoryStorageWalk visitor) { }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Frame = Active(context.State);
            Assert.Same(context.CurrentExecutableFrame, Frame);
            Assert.NotNull(Frame);
            using var walk = new MemoryStorageWalk(context.MemoryGovernor, 2048);
            Assert.True(walk.TryTraverse([Frame]));
            Assert.Contains(walk.Visited.ToArray(), value => value is PyString { Utf8Bytes.Length: 16000 });
            Checked = true;
            return PyNone.Instance;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            await Task.Yield();
            return Invoke(arguments, span, context);
        }
    }

    private sealed class FailingProbe(bool cancellation) : LythonRuntime.ICallable, IMemoryStorageEdges
    {
        internal object? Frame;
        public void VisitStorageChildren(MemoryStorageWalk visitor) { }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Frame = Active(context.State);
            Assert.Same(context.CurrentExecutableFrame, Frame);
            if (cancellation) throw new OperationCanceledException();
            throw new LythonRuntimeException("ValueError", "controlled dynamic call failure", span);
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            await Task.Yield();
            return Invoke(arguments, span, context);
        }
    }
}
