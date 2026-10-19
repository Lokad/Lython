using System.Numerics;
using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DirectAppendExecutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DifferentReceiversDoNotPackageArgumentsOrConstructBoundMethods(bool asynchronous)
    {
        var (code, context) = Prepare("for items in [left,right]: items.append(argument())\n");
        var left = new PyList();
        var right = new PyList();
        var argument = new ArgumentProbe();
        context.Variables["left"] = left;
        context.Variables["right"] = right;
        context.Variables["argument"] = argument;
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
                await pending;
            }
            else LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);
            Assert.Same(sentinel, pool.GetValue(null));
            Assert.Same(marker, sentinel[0].Value);
            Assert.Equal(2, argument.Calls);
            Assert.Same(argument.Value, Assert.Single(left));
            Assert.Same(argument.Value, Assert.Single(right));
            var caches = (Array)argument.Frame!.GetType().GetField("_memberCaches", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(argument.Frame)!;
            Assert.All(caches.Cast<object?>(), value => Assert.Null(value));
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally { pool.SetValue(null, previous); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedArgumentKeepsTheEvaluatedReceiver(bool cancel)
    {
        var (code, context) = Prepare("return items.append(argument())\n");
        var receiver = new PyList();
        var replacement = new PyList();
        var argument = new SuspendedArgument();
        context.Variables["items"] = receiver;
        context.Variables["argument"] = argument;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context).AsTask();
        try
        {
            Assert.NotNull(argument.Frame);
            Assert.False(pending.IsCompleted);
            var operands = Operands(argument.Frame!);
            Assert.Same(receiver, operands[0]);
            Assert.IsNotAssignableFrom<LythonRuntime.ICallable>(operands[1]);
            context.Variables["items"] = replacement;
            if (cancel)
            {
                argument.Release.TrySetCanceled();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
                Assert.Empty(receiver);
            }
            else
            {
                argument.Release.TrySetResult();
                Assert.Same(PyNone.Instance, await pending.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.Same(argument.Value, Assert.Single(receiver));
                Assert.Empty(Operands(argument.Frame!));
            }
            Assert.Empty(replacement);
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
        var (code, context) = Prepare("class C:\n    @property\n    def append(self): return lookup()\nreturn C().append(argument())\n");
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppendMutatesTheReceiverOnce(bool asynchronous)
    {
        {
            var (code, context) = Prepare("return items.append(value)\n", new LythonRunOptions { });
            var receiver = new PyList();
            context.Variables["items"] = receiver;
            context.Variables["value"] = new BigInteger(7);
                Assert.Same(PyNone.Instance, asynchronous
                    ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
                    : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null));
            Assert.Single(receiver);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedStorageDoesNotMutateTheReceiver(bool asynchronous)
    {
        const long limit = 64 * 1024;
        var (code, context) = Prepare("return items.append(value)\n", new LythonRunOptions { MaxExecutionMemoryBytes = limit });
        var receiver = new PyList(Enumerable.Repeat((object)PyNone.Instance, 8), context.MemoryGovernor);
        var baseline = context.MemoryGovernor.CurrentAccountedBytes;
        context.Variables["items"] = receiver;
        context.Variables["value"] = PyNone.Instance;
        // Occupy the remaining allowance: the ninth element needs replacement
        // backing storage. This checks capacity admission without scalar fees
        // or a tiny-budget threshold; failed growth must preserve the old owner.
        using (context.MemoryGovernor.ReserveTemporary(limit - baseline, null))
        {
            var error = asynchronous
                ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context))
                : Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(8, receiver.Count);
            Assert.All(receiver, value => Assert.Same(PyNone.Instance, value));
        }
        Assert.Equal(baseline, context.MemoryGovernor.CurrentAccountedBytes);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static (ExecutableScript, LythonRuntime.ExecutionContext) Prepare(string source, LythonRunOptions? options = null)
    {
        var frontend = LythonFrontend.Compile(source);
        return (ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull())), new(new MockLythonHost(), options ?? new()));
    }

    private static object[] Operands(object frame)
    {
        var stack = frame.GetType().GetField("_stack", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(frame)!;
        var items = (object[])stack.GetType().GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(stack)!;
        var count = (int)stack.GetType().GetProperty("Count")!.GetValue(stack)!;
        return items[..count];
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

    private sealed class SuspendedArgument : LythonRuntime.ICallable
    {
        internal object Value { get; init; } = new();
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
}
