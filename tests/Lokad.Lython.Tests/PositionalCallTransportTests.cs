using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class PositionalCallTransportTests
{
    [Theory]
    [InlineData(1, false, false)]
    [InlineData(2, false, false)]
    [InlineData(3, false, false)]
    [InlineData(1, true, false)]
    [InlineData(2, true, false)]
    [InlineData(3, true, false)]
    [InlineData(1, false, true)]
    [InlineData(2, false, true)]
    [InlineData(3, false, true)]
    [InlineData(1, true, true)]
    [InlineData(2, true, true)]
    [InlineData(3, true, true)]
    public async Task MatchingFrameUsesInvocationOwnedBoundStorage(int width, bool asynchronous, bool keywords)
    {
        var parameters = string.Join(",", Enumerable.Range(0, width).Select(i => "a" + i));
        var arguments = string.Join(",", Enumerable.Range(0, width).Select(i => keywords ? $"a{i}=value" : "value"));
        var frontend = LythonFrontend.Compile($"def f({parameters}):\n    a0=value\n    tap()\n    return a0\nreturn f({arguments})\n");
        var script = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull()));
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var marker = new object();
        var storage = new object[width];
        var tap = new StorageTap(storage);
        context.Variables["value"] = marker;
        context.Variables["tap"] = tap;
        var field = typeof(LythonRuntime).GetField(width == 1 ? "_pooledOneBoundValue" : width == 2 ? "_pooledTwoBoundValues" : "_pooledThreeBoundValues",
            BindingFlags.NonPublic | BindingFlags.Static).RequireNotNull();
        var previous = field.GetValue(null);
        field.SetValue(null, storage);
        try
        {
            var result = asynchronous
                ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(script.EntryPoint, context)
                : LythonRuntime.ExecuteExecutableCodeObject(script.EntryPoint, context, default, [], null);
            Assert.Same(marker, result);
            Assert.True(tap.Invoked);
            Assert.All(storage, value => Assert.Null(value));
            Assert.Null(tap.Frame!.CurrentExecutableFrame);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.Limits.CurrentRecursionDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally { field.SetValue(null, previous); }
    }

    private sealed class StorageTap(object[] storage) : LythonRuntime.ICallable
    {
        internal bool Invoked;
        internal LythonRuntime.ExecutionContext? Frame;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Invoked = true;
            Frame = context;
            var field = typeof(LythonRuntime.ExecutableFrameState).GetField("_locals", BindingFlags.Instance | BindingFlags.NonPublic).RequireNotNull();
            Assert.Same(storage, field.GetValue(context.CurrentExecutableFrame));
            return PyNone.Instance;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BorrowedLocalsRemainOwnedUntilSuspendedInvocationFinishes(bool cancel)
    {
        var frontend = LythonFrontend.Compile("def f(value):\n    tap()\n    return value\nreturn f(value)\n");
        var script = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull()));
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var marker = new object();
        var storage = new object[1];
        var tap = new SuspendedStorageTap(storage);
        context.Variables["value"] = marker;
        context.Variables["tap"] = tap;
        var field = typeof(LythonRuntime).GetField("_pooledOneBoundValue", BindingFlags.NonPublic | BindingFlags.Static).RequireNotNull();
        var previous = field.GetValue(null);
        Task<object?> pending;
        field.SetValue(null, storage);
        try { pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(script.EntryPoint, context).AsTask(); }
        finally { field.SetValue(null, previous); }
        try
        {
            Assert.NotNull(tap.Frame);
            Assert.False(pending.IsCompleted);
            Assert.Same(marker, storage[0]);
            Assert.NotNull(tap.Frame.CurrentExecutableFrame);
            if (cancel)
            {
                tap.Release.TrySetCanceled();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            else
            {
                tap.Release.TrySetResult();
                Assert.Same(marker, await pending.WaitAsync(TimeSpan.FromSeconds(5)));
            }
        }
        finally
        {
            tap.Release.TrySetCanceled();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
        }
        Assert.Null(storage[0]);
        Assert.Null(tap.Frame!.CurrentExecutableFrame);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class SuspendedStorageTap(object[] storage) : LythonRuntime.ICallable
    {
        internal LythonRuntime.ExecutionContext? Frame;
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => throw new InvalidOperationException("Expected async call.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Frame = context;
            var field = typeof(LythonRuntime.ExecutableFrameState).GetField("_locals", BindingFlags.Instance | BindingFlags.NonPublic).RequireNotNull();
            Assert.Same(storage, field.GetValue(context.CurrentExecutableFrame));
            await Release.Task.ConfigureAwait(false);
            return PyNone.Instance;
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task CompiledPositionalCallsDoNotBorrowThePackagedArgumentPool(int width, bool asynchronous)
    {
        var parameters = string.Join(",", Enumerable.Range(0, width).Select(i => "a" + i));
        var arguments = string.Join(",", Enumerable.Repeat("value", width));
        var frontend = LythonFrontend.Compile($"def f({parameters}):\n    return a0\nreturn f({arguments})\n");
        var script = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull()));
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var marker = new object();
        context.Variables["value"] = marker;
        var field = typeof(LythonRuntime).GetField(width == 1 ? "_pooledOneCallArgument" : width == 2 ? "_pooledTwoCallArguments" : "_pooledThreeCallArguments",
            BindingFlags.NonPublic | BindingFlags.Static).RequireNotNull();
        var previous = field.GetValue(null);
        var sentinel = Enumerable.Repeat(CallArgumentValue.Positional(marker), width).ToArray();
        field.SetValue(null, sentinel);
        try
        {
            object? result;
            if (asynchronous)
            {
                var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(script.EntryPoint, context);
                Assert.True(pending.IsCompleted);
                result = await pending;
            }
            else result = LythonRuntime.ExecuteExecutableCodeObject(script.EntryPoint, context, default, [], null);
            Assert.Same(marker, result);
            Assert.Same(sentinel, field.GetValue(null));
            foreach (var argument in sentinel) Assert.Same(marker, argument.Value);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally { field.SetValue(null, previous); }
    }
}
