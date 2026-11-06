using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ClassPatternOwnershipTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WorkingStorageIsAdmittedBeforeGuestEffects(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = hook });
        var mark = new Marker();
        context.Variables["mark"] = mark;
        Execute("class C:__match_args__=('a',);a=1;b=2\nsource=C()\n", context);
        var code = Compile("mark()\nmatch source:\n case C(x,b=y):mark()\n");
        hook.RefuseGrowth = _ => true;
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(() => Run(code, context, asynchronous));
        Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
        Assert.Equal(0, mark.Calls);
        Assert.False(context.Variables.ContainsKey("x"));
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SelectedNameStorageIsAdmittedBeforeAttributeRead(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = hook });
        var mark = new Marker();
        context.Variables["mark"] = mark;
        context.Variables["arm"] = new Arm(hook);
        Execute("class C:\n __match_args__=('a'*1024,)\n def __getattribute__(self,name):mark();return 7\nsource=C()\n", context);
        var code = Compile("arm()\nmatch source:\n case C(value):mark()\n");
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(() => Run(code, context, asynchronous));
        Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
        Assert.Equal(0, mark.Calls);
        Assert.False(context.Variables.ContainsKey("value"));
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task AttributeErrorCannotHideNativeCancellationOrRefusal(bool cancel, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var mark = new Marker();
        context.Variables["mark"] = mark;
        Execute("class C:__match_args__=('a',)\nsource=C()\n", context);
        var getter = new FailingGetter(cancellation, cancel, budget.LimitBytes!.Value);
        Assert.True(Assert.IsType<PyInstance>(context.Variables["source"]).Type.TrySetMember("__getattribute__", getter));
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(() => Run(Compile(
            "match source:\n case C(value,b=other):mark()\n case _:mark()\n"), context, asynchronous));
        if (cancel) Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
        else
        {
            Assert.Same(getter.Refusal, failure);
            Assert.Equal(getter.Origin, failure.Span);
        }
        Assert.Equal(["a"], getter.Fields);
        Assert.Equal(0, mark.Calls);
        Assert.False(context.Variables.ContainsKey("value"));
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ValueEqualityCannotSuppressFirstFailure(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var mark = new Marker();
        context.Variables["mark"] = mark;
        Execute("class V:pass\nclass K:expected=7\nsource=V()\n", context);
        var equality = new RefusingEquality(budget.LimitBytes!.Value);
        Assert.True(Assert.IsType<PyInstance>(context.Variables["source"]).Type.TrySetMember("__eq__", equality));
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(() => Run(Compile(
            "match source:\n case K.expected:mark()\n case _:mark()\n"), context, asynchronous));
        Assert.Same(equality.Refusal, failure);
        Assert.Equal(0, mark.Calls);
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static async Task Run(ExecutableCodeObject code, Context context, bool asynchronous)
    {
        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
    }
    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
    private static void Execute(string source, Context context)
        => LythonRuntime.ExecuteExecutableCodeObject(Compile(source), context, default, null);
    private static void AssertUnwound(Context context)
    {
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(0, context.State.StructuralTraversal.Depth);
        Assert.Null(context.CurrentExecutableFrame);
    }
    private sealed class Marker : LythonRuntime.ICallable
    {
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        { Calls++; return PyNone.Instance; }
    }
    private sealed class Arm(AdmissionHookMemoryBudget hook) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        { hook.RefuseGrowth = _ => true; return PyNone.Instance; }
    }
    private sealed class FailingGetter(CancellationTokenSource cancellation, bool cancel, long requested) : LythonRuntime.ICallable
    {
        internal readonly List<string> Fields = [];
        internal readonly LythonSourceSpan Origin = new(31, 4, 2, 1);
        internal LythonRuntimeException? Refusal;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Fields.Add(Assert.IsType<PyString>(arguments[^1].Value).AsString());
            if (cancel) cancellation.Cancel();
            else
            {
                try { context.MemoryGovernor.Reserve(requested, Origin); }
                catch (LythonRuntimeException error) { Refusal = error; }
            }
            throw new LythonRuntimeException("AttributeError", "not a recoverable mismatch", span);
        }
    }
    private sealed class RefusingEquality(long requested) : LythonRuntime.ICallable
    {
        internal LythonRuntimeException? Refusal;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            try { context.MemoryGovernor.Reserve(requested, span); }
            catch (LythonRuntimeException error) { Refusal = error; }
            return true;
        }
    }
}
