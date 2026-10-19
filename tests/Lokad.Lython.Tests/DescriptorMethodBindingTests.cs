using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DescriptorMethodBindingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData("__set__", "box.value=payload", 2)]
    [InlineData("__delete__", "del box.value", 1)]
    public async Task AsyncSetterAndDeleterSlotBindingNeverUsesItsSyncEntry(string slot, string source, int arity)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var getter = new AsyncGetter();
        var property = PyProperty.Create(getter, PyNone.Instance, PyNone.Instance, PyNone.Instance, context, Span);
        var descriptorType = new PyType("Descriptor", [], new() { [slot] = property }, context.MemoryGovernor, Span);
        var descriptor = new PyInstance(descriptorType, context.MemoryGovernor, Span);
        var objectType = Assert.IsType<PyType>(context.State.BuiltinVariables["object"]);
        var type = new PyType("Box", [objectType], new() { ["value"] = descriptor }, context.MemoryGovernor, Span);
        var receiver = new PyInstance(type, context.MemoryGovernor, Span);
        var payload = new PyList([true], context.MemoryGovernor, Span);
        var method = new AsyncMethod(receiver, payload, arity);
        context.Variables["box"] = receiver;
        context.Variables["payload"] = payload;
        var syntax = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
        try
        {
            Assert.False(pending.IsCompleted);
            Assert.Equal(1, getter.Calls);
            Assert.NotNull(context.CurrentExecutableFrame);
            getter.Result.SetResult(method);
            await pending;
            Assert.Equal(1, method.Calls);
        }
        finally
        {
            getter.Result.TrySetResult(method);
            await pending;
        }
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class AsyncGetter : LythonRuntime.ICallable
    {
        public int Calls { get; private set; }
        public TaskCompletionSource<object> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => throw new InvalidOperationException("Synchronous descriptor binding is forbidden in this controlled callback.");
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            return new(Result.Task);
        }
    }

    private sealed class AsyncMethod(PyInstance receiver, PyList payload, int arity) : LythonRuntime.ICallable
    {
        public int Calls { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => throw new InvalidOperationException("Synchronous descriptor invocation is forbidden in this controlled callback.");
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Assert.Equal(arity, arguments.Length);
            Assert.Same(receiver, arguments[0].Value);
            if (arguments.Length == 2) Assert.True(ReferenceEquals(arguments[1].Value, payload) || ReferenceEquals(arguments[1].Value, receiver.Type));
            Calls++;
            return ValueTask.FromResult<object>(payload);
        }
    }
}
