using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class SubscriptSetterBindingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);
    [Fact]
    public async Task AsyncStoreAwaitsDescriptorWithoutUsingItsSyncEntry()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var setter = new Setter();
        var getter = new AsyncGetter();
        var property = PyProperty.Create(getter, PyNone.Instance, PyNone.Instance,
            PyNone.Instance, context, Span);
        var type = new PyType("Box", [], new() { ["__setitem__"] = property }, context.MemoryGovernor, default);
        var receiver = new PyInstance(type, context.MemoryGovernor, default);
        var payload = new PyList([true], context.MemoryGovernor, default);
        context.Variables["box"] = receiver;
        context.Variables["payload"] = payload;
        var syntax = LythonFrontend.Compile("box[0]=payload").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
        try
        {
            Assert.False(pending.IsCompleted);
            Assert.Equal(1, getter.AsyncCalls);
            Assert.NotNull(context.CurrentExecutableFrame);
            getter.Result.SetResult(setter);
            await pending;
            Assert.Equal(1, setter.Calls);
            Assert.Same(payload, setter.Value);
        }
        finally
        {
            getter.Result.TrySetResult(setter);
            await pending;
        }
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class AsyncGetter : LythonRuntime.ICallable
    {
        public int AsyncCalls { get; private set; }
        public TaskCompletionSource<object> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => throw new InvalidOperationException("Synchronous descriptor entry is forbidden in this controlled callback.");
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            AsyncCalls++;
            return new(Result.Task);
        }
    }

    private sealed class Setter : LythonRuntime.ICallable
    {
        public int Calls { get; private set; }
        public object? Value { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => throw new InvalidOperationException("Synchronous setter entry is forbidden in this controlled callback.");
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Assert.Equal(2, arguments.Length);
            Calls++;
            Value = arguments[1].Value;
            return ValueTask.FromResult<object>(PyNone.Instance);
        }
    }
}
