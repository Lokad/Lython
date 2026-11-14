using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class NativeClosureSequenceTests
{
    [Theory]
    [InlineData(false, false, false)] [InlineData(true, false, false)]
    [InlineData(false, true, false)] [InlineData(true, true, false)]
    [InlineData(false, false, true)] [InlineData(true, false, true)]
    [InlineData(false, true, true)] [InlineData(true, true, true)]
    public async Task AdmissionRebindingCallerLocalsKeepsAlreadyBoundParameters(bool asynchronous, bool keywords, bool guarded)
    {
        var source = """
            def make():
                count=0
                def append(value,suffix):
                    nonlocal count
                    count+=1
                    return value.lower()+suffix
                def read():return count
                return append,read
            append,read=make()
            suffix='-old'
            arm(append)
            result=CALL
            assert suffix=='-new'
            assert read()==1
            return result
            """.Replace("CALL", keywords ? "append(suffix=suffix,value=payload)" : "append(payload,suffix)");
        if (guarded)
            source = source.Replace("return value.lower()+suffix",
                "if not value: return value\n        return value.replace('A',suffix)");
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            context.Variables["payload"] = PyString.FromString(new string('A', 4096), context.MemoryGovernor, null);
            var changed = PyString.FromString("-new", context.MemoryGovernor, null);
            var armed = new RebindCaller(hook, changed);
            context.Variables["arm"] = armed;
            var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!));
            try
            {
                var result = asynchronous
                    ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
                    : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);
                Assert.True(armed.Rebound);
                Assert.Equal(guarded ? string.Concat(Enumerable.Repeat("-old", 4096)) : new string('a', 4096) + "-old",
                    Assert.IsType<PyString>(result).AsString());
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { hook.AfterGrowth = null; context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void IncomingCellCannotSatisfyAnAbsentParameterOrPublishAnEarlyStore()
    {
        const string source = """
            def make():
                count=0
                def take(value):
                    nonlocal count
                    count+=1
                    return value
                return take
            return make()
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!));
            try
            {
                var function = Assert.IsType<PyExecutableFunction>(LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null));
                Assert.True(function.CodeObject.CanRunNativeInstructions);
                Assert.Single(function.ClosureCells);
                var span = new LythonSourceSpan(0, 1, 1, 1);
                var failure = Assert.Throws<LythonRuntimeException>(() => function.Invoke([], span, context));
                Assert.Equal("TypeError", failure.ExceptionType);
                Assert.Equal(BigInteger.Zero, function.ClosureCells[0].Value);
                var retained = PyString.FromString("kept", context.MemoryGovernor, span);
                Assert.Same(retained, function.Invoke([CallArgumentValue.Positional(retained)], span, context));
                Assert.Equal(BigInteger.One, function.ClosureCells[0].Value);
                Assert.Equal("kept", retained.AsString());
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusalAfterNativeCellWritePreservesFirstOriginWithoutReplaying(bool asynchronous)
    {
        const string source = """
            def make():
                count=0
                def words(value):
                    nonlocal count
                    count+=1
                    return value.lower().split()
                return words
            words=make()
            arm(words)
            try:
                words(payload)
            except MemoryError:print('suppressed')
            finally:return 'suppressed-finally'
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            var payload = PyString.FromString(new string('A', 8192), context.MemoryGovernor, null);
            context.Variables["payload"] = payload;
            var frontend = LythonFrontend.Compile(source);
            var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!));
            var armed = new Arm(hook);
            context.Variables["arm"] = armed;
            try
            {
                var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                {
                    if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context);
                    else LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);
                });
                Assert.True(armed.Refused);
                Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Equal(new BigInteger(1), Assert.Single(armed.Function!.ClosureCells).Value);
                var splitSpan = Assert.Single(armed.Function.CodeObject.CallSites.Where(site => site.CasedSplit is not null)
                    .Select(site => site.CasedSplit!.SplitCallSpan).Distinct());
                Assert.Same(splitSpan, failure.Span);
                Assert.Equal(8192, payload.ByteLength);
                Assert.All(payload.Utf8Span.ToArray(), value => Assert.Equal((byte)'A', value));
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally
            {
                hook.RefuseGrowth = null;
                context.State.CloseResources();
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task LateCallbackRefusalKeepsEarlierEffectsAndFirstOrigin(bool asynchronous, bool mapped)
    {
        var source = """
            def make():
                count=0
                def apply(value):
                    nonlocal count
                    count+=1
                    return callback(value)
                return apply
            apply=make()
            arm(apply)
            try:
                CALL
            except MemoryError:print('suppressed')
            finally:return 'suppressed-finally'
            """.Replace("CALL", mapped ? "list(map(apply,[payload]))" : "apply(payload)");
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            var payload = PyString.FromString(new string('A', 4096), context.MemoryGovernor, null);
            context.Variables["payload"] = payload;
            var callback = new RefusingCallback();
            context.Variables["callback"] = callback;
            var arm = new ArmLateFrame(hook);
            context.Variables["arm"] = arm;
            var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!));
            try
            {
                var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                {
                    if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context);
                    else LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);
                });
                Assert.True(arm.Refused);
                Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Equal(BigInteger.One, Assert.Single(arm.Function!.ClosureCells).Value);
                Assert.Equal(1, callback.Calls);
                Assert.Equal(new string('A', 4096), payload.AsString());
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { hook.RefuseGrowth = null; context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class RefusingCallback : LythonRuntime.ICallable
    {
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            context.MemoryGovernor.Reserve(8192, span);
            return arguments[0].Value!;
        }
    }

    private sealed class ArmLateFrame(AdmissionHookMemoryBudget hook) : LythonRuntime.ICallable
    {
        internal bool Refused;
        internal PyExecutableFunction? Function;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Function = Assert.IsType<PyExecutableFunction>(Assert.Single(arguments).Value);
            hook.RefuseGrowth = _ =>
            {
                // Allow argument/iterator setup. The local write is an observable
                // effect; the late callback's explicit admission must refuse.
                if (Assert.Single(Function.ClosureCells).Value is not BigInteger count || count == BigInteger.Zero) return false;
                Assert.Equal(BigInteger.One, count);
                return Refused = true;
            };
            return PyNone.Instance;
        }
    }

    private sealed class RebindCaller(AdmissionHookMemoryBudget hook, PyString replacement) : LythonRuntime.ICallable
    {
        internal bool Rebound;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var function = Assert.IsType<PyExecutableFunction>(Assert.Single(arguments).Value);
            var caller = context.CurrentExecutableFrame!;
            hook.AfterGrowth = _ =>
            {
                // Rebind during output construction, after the cell effect. No
                // particular allocation size, ordinal or checkpoint is required.
                if (Assert.Single(function.ClosureCells).Value is not BigInteger count || count != BigInteger.One) return;
                hook.AfterGrowth = null;
                Assert.True(caller.TryStoreLocalOrClosure("suffix", replacement));
                Rebound = true;
            };
            return PyNone.Instance;
        }
    }

    private sealed class Arm(AdmissionHookMemoryBudget hook) : LythonRuntime.ICallable
    {
        internal bool Refused;
        internal PyExecutableFunction? Function;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Function = Assert.IsType<PyExecutableFunction>(Assert.Single(arguments).Value);
            Assert.True(Function.CodeObject.CanRunNativeInstructions);
            hook.RefuseGrowth = _ =>
            {
                // The native counter store precedes output admission, including
                // when the latter refuses. No rebinding or body replay is allowed.
                Assert.Equal(new BigInteger(1), Assert.Single(Function.ClosureCells).Value);
                Refused = true;
                return true;
            };
            return PyNone.Instance;
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => new(Invoke(arguments, span, context));
    }
}
