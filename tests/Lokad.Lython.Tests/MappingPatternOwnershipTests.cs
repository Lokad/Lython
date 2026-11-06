using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class MappingPatternOwnershipTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ConstantPatternStorageIsAdmittedBeforeExecution(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = hook });
        var marker = new Marker();
        context.Variables["mark"] = marker;
        Execute("source={1:10}\n", context);
        var code = Compile("mark()\nmatch source:\n case {1:value}:mark()\n");
        hook.RefuseGrowth = _ => true;
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        });
        Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
        Assert.Equal(0, marker.Calls);
        Assert.False(context.Variables.ContainsKey("value"));
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CapturePreflightPrecedesKeyGetterAndBody(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = hook });
        var marker = new Marker();
        context.Variables["mark"] = marker;
        context.Variables["arm"] = new Arm(hook);
        Execute("namespace=globals()\nsource={1:10}\nclass D:\n def __get__(self,obj,owner):mark();return 1\nclass Keys:item=D()\n", context);
        const string source = "arm()\nmatch source:\n case {Keys.item:value}:mark()\n";
        var code = Compile(source);
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        });
        Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
        var span = Assert.IsType<LythonSourceSpan>(failure.Span);
        Assert.Equal("{Keys.item:value}", source.Substring(span.Start, span.Length));
        Assert.Equal(0, marker.Calls);
        Assert.Equal(1, Assert.IsType<PyDict>(context.Variables["source"]).Count);
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativeHashCannotHideFirstFailure(bool cancel)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
        Execute("namespace=globals()\nclass Key:\n def __hash__(self):return 1\nkey=Key()\nclass Keys:item=key\nsource={key:10}\n", context);
        var key = Assert.IsType<PyInstance>(context.Variables["key"]);
        var fault = new Fault(cancellation, cancel, budget.LimitBytes!.Value);
        var hash = new NativeHash(fault);
        Assert.True(key.Type.TrySetMember("__hash__", hash));
        var failure = Assert.Throws<LythonRuntimeException>(() => Execute("match source:\n case {Keys.item:value}:pass\n", context));
        AssertFailure(failure, fault, cancel);
        Assert.Equal(1, hash.Calls);
        Assert.Equal(1, Assert.IsType<PyDict>(context.Variables["source"]).Count);
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task FieldReadCannotReturnAfterNativeFailure(bool cancel, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
        Execute("namespace=globals()\nfrom dataclasses import dataclass\n@dataclass(frozen=True)\nclass Row:\n a:int\n b:int\nkey=Row(1,2)\n", context);
        var key = Assert.IsType<PyInstance>(context.Variables["key"]);
        var fault = new Fault(cancellation, cancel, budget.LimitBytes!.Value);
        var getter = new NativeGetter(key, fault);
        Assert.True(key.Type.TrySetMember("__getattribute__", getter));
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            if (asynchronous) await PyDataclass.ReadFieldAsync(key, "a", context, fault.Origin);
            else PyDataclass.ReadField(key, "a", context, fault.Origin);
        });
        AssertFailure(failure, fault, cancel);
        Assert.Equal(["a"], getter.Fields);
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("{1:value}")]
    [InlineData("{3:value}")]
    [InlineData("{1:99}")]
    public void RepeatedMatchesReturnPrivateWorkingStorage(string pattern)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var observation = new Balances();
        context.Variables["observe"] = observation;
        Execute("namespace=globals()\nsource={1:10,2:20}\nfor i in range(4):\n match source:\n  case " + pattern + ":pass\n observe()\n", context);
        Assert.Equal(4, observation.Accounted.Count);
        Assert.All(observation.Accounted.Skip(1), value => Assert.True(value <= observation.Accounted[0],
            $"Private pattern storage accumulated: {string.Join(',', observation.Accounted)}"));
        Assert.All(observation.Reserved, value => Assert.Equal(0, value));
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CapturedLargeAliasSurvivesClearAndSuspendedBody()
    {
        const string source = "namespace=globals()\nsource={1:1<<65536,2:20}\nclass D:\n def __get__(self,obj,owner):source.clear();return 1<<65536\nclass Keys:a=1;b=2;expected=D()\nmatch source:\n case {Keys.a:Keys.expected as captured,Keys.b:other}:\n  observe()\n  with open('/hold','w') as stream:stream.write('held')\n";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var host = new DelayedLythonHost();
        var entered = host.PauseWriteUntilCancellation("/hold");
        var context = new Context(host, new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var observation = new Balances();
        context.Variables["observe"] = observation;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(Compile(source), context).AsTask();
        Exception? failure;
        try
        {
            Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(pending.IsCompleted);
            Assert.Equal(0, Assert.IsType<PyDict>(context.Variables["source"]).Count);
            Assert.Equal(BigInteger.One << 65536, Assert.IsType<BigInteger>(context.Variables["captured"]));
            Assert.Equal(new BigInteger(20), context.Variables["other"]);
            Assert.Single(observation.Accounted);
            Assert.True(observation.Accounted[0] >= RuntimeMemoryEstimates.EstimateBigIntegerBytes(BigInteger.One << 65536));
            Assert.Equal(0, Assert.Single(observation.Reserved));
        }
        finally
        {
            cancellation.Cancel();
            failure = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        Assert.Equal(LythonFailureReason.Cancelled, Assert.IsType<LythonRuntimeException>(failure).Attribution?.Reason);
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static void AssertUnwound(Context context)
    {
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(0, context.State.StructuralTraversal.Depth);
        Assert.Null(context.CurrentExecutableFrame);
    }

    private static void AssertFailure(LythonRuntimeException failure, Fault fault, bool cancel)
    {
        if (cancel) Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
        else { Assert.Same(fault.Refusal, failure); Assert.Equal(fault.Origin, failure.Span); }
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
    private static void Execute(string source, Context context)
        => LythonRuntime.ExecuteExecutableCodeObject(Compile(source), context, default, null);

    private sealed class Marker : LythonRuntime.ICallable
    {
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context) { Calls++; return PyNone.Instance; }
    }
    private sealed class Arm(AdmissionHookMemoryBudget hook) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context) { hook.RefuseGrowth = _ => true; return PyNone.Instance; }
    }
    private sealed class Balances : LythonRuntime.ICallable
    {
        internal readonly List<long> Accounted = [], Reserved = [];
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        { Accounted.Add(context.MemoryGovernor.CurrentAccountedBytes); Reserved.Add(context.MemoryGovernor.CurrentReservedBytes); return PyNone.Instance; }
    }
    private sealed class Fault(CancellationTokenSource cancellation, bool cancel, long requested)
    {
        internal LythonRuntimeException? Refusal;
        internal readonly LythonSourceSpan Origin = new(31, 4, 2, 1);
        internal void Trigger(Context context)
        {
            if (cancel) cancellation.Cancel();
            else { try { context.MemoryGovernor.Reserve(requested, Origin); } catch (LythonRuntimeException error) { Refusal = error; } }
        }
    }
    private sealed class NativeHash(Fault fault) : LythonRuntime.ICallable
    {
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context) { Calls++; fault.Trigger(context); return BigInteger.One; }
    }
    private sealed class NativeGetter(PyInstance target, Fault fault) : LythonRuntime.ICallable
    {
        internal readonly List<string> Fields = [];
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            var name = Assert.IsType<PyString>(arguments[^1].Value).ToString();
            if (name is "a" or "b") { Fields.Add(name); if (Fields.Count == 1) fault.Trigger(context); }
            return target.TryGetOwnAttribute(name, out var value) ? value : PyNone.Instance;
        }
    }
}
