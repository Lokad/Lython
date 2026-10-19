using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class RawDescriptorGetDispatchTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RawGetterReceivesDescriptorInstanceAndType(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var getter = new RawGetter();
        var descriptorType = new PyType("Descriptor", [], new() { ["__get__"] = getter }, context.MemoryGovernor, Span);
        var descriptor = new PyInstance(descriptorType, context.MemoryGovernor, Span);
        var objectType = Assert.IsType<PyType>(context.State.BuiltinVariables["object"]);
        var type = new PyType("Box", [objectType], new() { ["value"] = descriptor }, context.MemoryGovernor, Span);
        var receiver = new PyInstance(type, context.MemoryGovernor, Span);
        var payload = new PyList([true], context.MemoryGovernor, Span);
        getter.Descriptor = descriptor;
        getter.Receiver = receiver;
        getter.Owner = type;
        getter.Payload = payload;
        context.Variables["box"] = receiver;
        var syntax = LythonFrontend.Compile("return box.value",regexProvider:Utf8RegexProvider.Instance).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        if (asynchronous)
        {
            var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
            try
            {
                Assert.False(pending.IsCompleted);
                Assert.Equal(1, getter.AsyncCalls);
                Assert.NotNull(context.CurrentExecutableFrame);
                getter.Result.SetResult(payload);
                Assert.Same(payload, await pending.WaitAsync(TimeSpan.FromSeconds(10)));
            }
            finally
            {
                getter.Result.TrySetResult(payload);
                await pending.WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
        else Assert.Same(payload, LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
        Assert.Equal(asynchronous ? 0 : 1, getter.SyncCalls);
        Assert.Equal(asynchronous ? 1 : 0, getter.AsyncCalls);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task InvalidRawGetterRejectsBeforeBinding(bool asynchronous, bool classMethod)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var body = new CountingGetter();
        object raw = classMethod ? new PyClassMethod(body)
            : PyProperty.Create(body, PyNone.Instance, PyNone.Instance, PyNone.Instance, context, Span);
        var descriptorType = new PyType("Descriptor", [], new() { ["__get__"] = raw }, context.MemoryGovernor, Span);
        var descriptor = new PyInstance(descriptorType, context.MemoryGovernor, Span);
        var objectType = Assert.IsType<PyType>(context.State.BuiltinVariables["object"]);
        var type = new PyType("Box", [objectType], new() { ["value"] = descriptor }, context.MemoryGovernor, Span);
        context.Variables["box"] = new PyInstance(type, context.MemoryGovernor, Span);
        var syntax = LythonFrontend.Compile("return box.value",regexProvider:Utf8RegexProvider.Instance).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        LythonRuntimeException error;
        if (asynchronous)
            error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context));
        else error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
        Assert.Equal("TypeError", error.ExceptionType);
        Assert.Equal($"'{(classMethod ? "classmethod" : "property")}' object is not callable", error.Message);
        Assert.Equal(0, body.Calls);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class CountingGetter : LythonRuntime.ICallable
    {
        public int Calls { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            return BigInteger.One;
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => ValueTask.FromResult(Invoke(arguments, span, context));
    }

    private sealed class RawGetter : LythonRuntime.ICallable
    {
        public object Descriptor { get; set; } = null!;
        public object Receiver { get; set; } = null!;
        public object Owner { get; set; } = null!;
        public object Payload { get; set; } = null!;
        public int SyncCalls { get; private set; }
        public int AsyncCalls { get; private set; }
        public TaskCompletionSource<object> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private void Check(CallArgumentValue[] arguments)
        {
            Assert.Equal(3, arguments.Length);
            Assert.Same(Descriptor, arguments[0].Value);
            Assert.Same(Receiver, arguments[1].Value);
            Assert.Same(Owner, arguments[2].Value);
        }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Check(arguments);
            SyncCalls++;
            return Payload;
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Check(arguments);
            AsyncCalls++;
            return new(Result.Task);
        }
    }
}
