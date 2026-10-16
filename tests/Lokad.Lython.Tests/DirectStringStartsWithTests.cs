using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DirectStringStartsWithTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryCallsLeaveTheArgumentPoolUntouched(bool asynchronous)
    {
        var (code, context) = Prepare("return text.startswith(prefix)");
        context.Variables["text"] = PyString.FromString("alpha", context.MemoryGovernor, null);
        context.Variables["prefix"] = PyString.FromString("a", context.MemoryGovernor, null);
        var pool = typeof(LythonRuntime).GetField("_pooledOneCallArgument", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = pool.GetValue(null);
        var marker = new object();
        var sentinel = new[] { CallArgumentValue.Positional(marker) };
        pool.SetValue(null, sentinel);
        try
        {
            if (asynchronous)
            {
                var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context);
                Assert.True(pending.IsCompleted);
                Assert.Equal(true, await pending);
            }
            else Assert.Equal(true, LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null));
            Assert.Same(sentinel, pool.GetValue(null));
            Assert.Same(marker, sentinel[0].Value);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally { pool.SetValue(null, previous); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DistinctStringsDoNotConstructBoundMethodCaches(bool asynchronous)
    {
        var (code, context) = Prepare("for text in [left,right]:\n    result=text.startswith(prefix)\n    record()\n");
        var left = PyString.FromString("alpha", context.MemoryGovernor, null);
        var right = PyString.FromString(" café😀 ", context.MemoryGovernor, null);
        var record = new Record();
        context.Variables["left"] = left;
        context.Variables["right"] = right;
        context.Variables["prefix"] = PyString.FromString("a", context.MemoryGovernor, null);
        context.Variables["record"] = record;
        Assert.Equal(1, Assert.Single(code.EntryPoint.CallSites, site => site.HasReceiver).ArgumentCount);
        if (asynchronous)
        {
            var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context);
            Assert.True(pending.IsCompleted);
            await pending;
        }
        else LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null);
        Assert.Equal(2, record.Values.Count);
        Assert.True(Assert.IsType<bool>(record.Values[0]));
        Assert.False(Assert.IsType<bool>(record.Values[1]));
        var caches = (Array)record.Frame!.GetType().GetField("_memberCaches", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(record.Frame)!;
        Assert.All(caches.Cast<object?>(), value => Assert.Null(value));
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardFuelAndSourceSpansMatchTheOrdinaryCallBoundary(bool asynchronous)
    {
        for (var fuel = 1; fuel <= 9; fuel++)
        {
            var (code, context) = Prepare("return text.startswith(prefix)\n", new LythonRunOptions { MaxExecutionSteps = fuel });
            var text = PyString.FromString("word", context.MemoryGovernor, null);
            context.Variables["text"] = text;
            context.Variables["prefix"] = PyString.FromString("w", context.MemoryGovernor, null);
            if (fuel == 9)
                Assert.Equal(true, asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
                    : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null));
            else
            {
                var error = asynchronous
                    ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context))
                    : Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null));
                Assert.Equal("RuntimeError", error.ExceptionType);
                Assert.Equal(fuel + 1, context.Limits.ExecutionStepCount);
                var instructions = code.EntryPoint.Blocks[code.EntryPoint.EntryBlockIndex].InstructionArray;
                Assert.Same(instructions[fuel <= 4 ? fuel - 1 : fuel == 8 ? 4 : 3].Span, error.Span);
            }
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedArgumentKeepsTheEvaluatedReceiver(bool cancel)
    {
        var (code, context) = Prepare("return text.startswith(argument())\n");
        var receiver = PyString.FromString("alpha", context.MemoryGovernor, null);
        var replacement = PyString.FromString("other", context.MemoryGovernor, null);
        var argument = new SuspendedArgument();
        context.Variables["text"] = receiver;
        context.Variables["argument"] = argument;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context).AsTask();
        try
        {
            Assert.NotNull(argument.Frame);
            Assert.False(pending.IsCompleted);
            var operands = Operands(argument.Frame!);
            Assert.Same(receiver, operands[0]);
            Assert.IsNotAssignableFrom<LythonRuntime.ICallable>(operands[1]);
            context.Variables["text"] = replacement;
            if (cancel)
            {
                argument.Release.TrySetCanceled();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

            }
            else
            {
                argument.Release.TrySetResult();
                Assert.Equal(true, await pending.WaitAsync(TimeSpan.FromSeconds(5)));

                Assert.Empty(Operands(argument.Frame!));
            }
            Assert.Equal(1, argument.Calls);
        }
        finally
        {
            argument.Release.TrySetCanceled();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
        }
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FallbackLookupCanSuspendBeforeArguments(bool cancel)
    {
        var (code, context) = Prepare("class C:\n    @property\n    def startswith(self): return lookup()\nreturn C().startswith(argument())\n");
        var method = new ArgumentProbe();
        var argument = new ArgumentProbe();
        var lookup = new SuspendedArgument { Value = method };
        context.Variables["lookup"] = lookup;
        context.Variables["argument"] = argument;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context).AsTask();
        try
        {
            await lookup.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // Class getters may retain the lowered route; the enclosing
            // executable invocation remains active while lookup suspends.
            Assert.NotNull(context.CurrentExecutableFrame);
            Assert.Equal(1, lookup.Calls);
            Assert.False(pending.IsCompleted);
            Assert.Equal(0, argument.Calls);
            if (cancel)
            {
                lookup.Release.TrySetCanceled();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
                Assert.Equal(0, argument.Calls);
                Assert.Equal(0, method.Calls);
            }
            else
            {
                lookup.Release.TrySetResult();
                Assert.Same(method.Value, await pending.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.Equal(1, argument.Calls);
                Assert.Equal(1, method.Calls);
                Assert.Same(argument.Value, method.LastArgument);
            }
        }
        finally
        {
            lookup.Release.TrySetCanceled();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
        }
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static (ExecutableScript, LythonRuntime.ExecutionContext) Prepare(string source, LythonRunOptions? options = null)
    {
        var frontend = LythonFrontend.Compile(source);
        return (ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull())), new(new MockLythonHost(), options ?? new()));
    }

    private sealed class Record : LythonRuntime.ICallable
    {
        internal List<object> Values { get; } = [];
        internal LythonRuntime.ExecutableFrameState? Frame;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Frame = context.CurrentExecutableFrame;
            Assert.True(Frame!.TryResolveOwnLocal("result", out var value));
            Values.Add(value);
            return PyNone.Instance;
        }
    }
    private static object[] Operands(object frame)
    {
        var stack = frame.GetType().GetField("_stack", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(frame)!;
        var items = (object[])stack.GetType().GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(stack)!;
        var count = (int)stack.GetType().GetProperty("Count")!.GetValue(stack)!;
        return items[..count];
    }

    private sealed class SuspendedArgument : LythonRuntime.ICallable
    {
        internal object Value { get; init; } = PyString.FromString("a");
        internal LythonRuntime.ExecutableFrameState? Frame;
        internal int Calls;
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => throw new InvalidOperationException("Expected asynchronous call.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            Frame = context.CurrentExecutableFrame;
            Started.TrySetResult();
            await Release.Task.ConfigureAwait(false);
            return Value;
        }
    }
    private sealed class ArgumentProbe : LythonRuntime.ICallable
    {
        internal int Calls;
        internal object Value { get; } = new();
        internal object? LastArgument;
        internal LythonRuntime.ExecutableFrameState? Frame;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            if (arguments.Length != 0) LastArgument = Assert.Single(arguments).Value;
            Frame = context.CurrentExecutableFrame;
            return Value;
        }
    }


}
