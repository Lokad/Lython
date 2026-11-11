using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ConstructorOwnershipTests
{
    [Theory]
    [InlineData("allocator", false, false)] [InlineData("allocator", false, true)]
    [InlineData("allocator", true, false)] [InlineData("allocator", true, true)]
    [InlineData("initializer", false, false)] [InlineData("initializer", false, true)]
    [InlineData("initializer", true, false)] [InlineData("initializer", true, true)]
    public async Task NativeConstructionCannotSuppressFirstFailure(string route, bool cancel, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, CancellationToken = cancellation.Token });
        Execute("class C:pass\nexisting=C()\n", context);
        var type = Assert.IsType<PyType>(context.Variables["C"]);
        var fault = new Fault(cancellation, cancel, budget.LimitBytes!.Value,
            route == "allocator" ? context.Variables["existing"] : PyNone.Instance);
        Assert.True(type.TrySetMember(route == "allocator" ? "__new__" : "__init__", fault));
        var marker = new Marker();
        context.Variables["mark"] = marker;
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            var code = Compile("result=C(7)\nmark()\n");
            if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        });
        if (cancel) Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
        else
        {
            Assert.Same(fault.Refusal, failure);
            Assert.Equal(fault.Origin, failure.Span);
        }
        Assert.Equal(1, fault.Calls);
        Assert.Equal(0, marker.Calls);
        Assert.False(context.Variables.ContainsKey("result"));
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(context.CurrentExecutableFrame);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DefaultAllocatorRefusesBeforeInitializerMutation(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = hook });
        var marker = new Marker();
        context.Variables["mark"] = marker;
        context.Variables["arm"] = new Arm(hook);
        Execute("class C:\n def __init__(self,value):mark();self.value=value\n", context);
        var code = Compile("arm()\nresult=C(7)\nmark()\n");
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        });
        Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
        Assert.Equal(0, marker.Calls);
        Assert.False(context.Variables.ContainsKey("result"));
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(context.CurrentExecutableFrame);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AdmissionMutationInitializesTheExistingInstanceWithoutRestart(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = hook });
        Execute("namespace=globals()\nclass C:\n def __init__(self,value):self.old=value\ndef replacement(self,*values):self.new=values[0]\n", context);
        var type = Assert.IsType<PyType>(context.Variables["C"]);
        Assert.True(type.TryGetExecutableInitializer(out _));
        Assert.True(type.CanPrepareExecutableConstructor);
        var replacement = context.Variables["replacement"];
        var blocker = new RestartBlocker();
        var code = Compile("result=C(7)\n");
        var changed = false;
        hook.AfterGrowth = bytes =>
        {
            if (bytes != PyInstance.InstanceShellBytes) return;
            hook.AfterGrowth = null;
            Assert.True(type.TrySetMember("__new__", blocker));
            Assert.True(type.TrySetMember("__init__", replacement));
            changed = true;
        };
        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        Assert.True(changed);
        var result = Assert.IsType<PyInstance>(context.Variables["result"]);
        Assert.False(result.TryGetOwnAttribute("old", out _));
        Assert.True(result.TryGetOwnAttribute("new", out var value));
        Assert.Equal(new System.Numerics.BigInteger(7), value);
        Assert.Equal(0, blocker.Calls);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(context.CurrentExecutableFrame);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class RestartBlocker : LythonRuntime.ICallable
    {
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            throw new LythonRuntimeException("AssertionError", "constructor restarted after allocation", span);
        }
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task DynamicInitializerOwnsArgumentsAfterCallerReturnsItsPacket(bool asynchronous, bool keyword)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        Execute("namespace=globals()\nclass C:pass\n", context);
        var type = Assert.IsType<PyType>(context.Variables["C"]);
        var retainer = new PacketRetainer();
        Assert.True(type.TrySetMember("__init__", retainer));
        var code = Compile(keyword ? "result=C(7, named=9)\n" : "result=C(7)\n");
        var before = budget.ReservedBytes;
        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        var packet = Assert.IsType<CallArgumentValue[]>(retainer.Arguments);
        Assert.Equal(keyword ? 2 : 1, packet.Length);
        Assert.True(packet[0].IsPositional);
        Assert.Equal(new System.Numerics.BigInteger(7), packet[0].Value);
        if (keyword)
        {
            Assert.Equal("named", packet[1].KeywordName);
            Assert.Equal(new System.Numerics.BigInteger(9), packet[1].Value);
        }
        Assert.True(budget.ReservedBytes >= before + PyInstance.InstanceShellBytes + 32 + 32 * packet.Length);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(context.CurrentExecutableFrame);
        retainer.Arguments = null;
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class PacketRetainer : IPyBindableCallable
    {
        internal CallArgumentValue[]? Arguments;
        public object Bind(object instance) => this;
        public object Get(object? instance, PyType owner, Context? context, LythonSourceSpan? span) => this;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        { Arguments = arguments; return PyNone.Instance; }
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
    private static void Execute(string source, Context context)
        => LythonRuntime.ExecuteExecutableCodeObject(Compile(source), context, default, null);
    private sealed class Arm(AdmissionHookMemoryBudget hook) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        { hook.RefuseGrowth = _ => true; return PyNone.Instance; }
    }
    private sealed class Marker : LythonRuntime.ICallable
    {
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        { Calls++; return PyNone.Instance; }
    }
    private sealed class Fault(CancellationTokenSource cancellation, bool cancel, long requested, object result) : IPyBindableCallable
    {
        internal int Calls;
        internal LythonRuntimeException? Refusal;
        internal readonly LythonSourceSpan Origin = new(31, 4, 2, 1);
        public object Bind(object instance) => this;
        public object Get(object? instance, PyType owner, Context? context, LythonSourceSpan? span) => this;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            if (cancel) cancellation.Cancel();
            else
            {
                try { context.MemoryGovernor.Reserve(requested, Origin); }
                catch (LythonRuntimeException error) { Refusal = error; }
            }
            return result;
        }
    }
}
