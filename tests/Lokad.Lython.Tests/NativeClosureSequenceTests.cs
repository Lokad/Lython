using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class NativeClosureSequenceTests
{
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
                Assert.NotNull(function.CodeObject.NativeBody);
                Assert.Single(function.ClosureCells);
                var span = new LythonSourceSpan(0, 1, 1, 1);
                Assert.False(function.TryExecuteWithoutFrame([], span, out _));
                Assert.Equal(BigInteger.Zero, function.ClosureCells[0].Value);
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

    private sealed class Arm(AdmissionHookMemoryBudget hook) : LythonRuntime.ICallable
    {
        internal bool Refused;
        internal PyExecutableFunction? Function;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Function = Assert.IsType<PyExecutableFunction>(Assert.Single(arguments).Value);
            Assert.NotNull(Function.CodeObject.NativeBody);
            Assert.Contains(Function.CodeObject.NativeBody.Values, value => value.Operation == NativeReturnOperation.StoreClosure);
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
