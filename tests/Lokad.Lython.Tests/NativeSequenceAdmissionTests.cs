using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class NativeSequenceAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedRangeConstructionPrecedesLoopEffects(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            using var arm = new Arm(budget);
            var mark = new Mark();
            context.Variables["arm"] = arm;
            context.Variables["mark"] = mark;
            const string source = "def fold(count):\n for item in range(count):mark()\narm()\nfold(3)";
            var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!));
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context);
                else LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);
            });
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            Assert.Same(failure, context.MemoryGovernor.AdmissionFailure);
            Assert.Equal(0, mark.Calls);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);
            Assert.Equal(0, context.State.CurrentRecursionDepth);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task RefusedIterationStoragePreservesSourceAndPrecedesBodyEffects(bool asynchronous, bool tuple)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            object[] values = [BigInteger.One, new BigInteger(2), new BigInteger(3)];
            var source = tuple ? (object)new PyTuple(values, context.MemoryGovernor) : new PyList(values, context.MemoryGovernor);
            using var arm = new Arm(budget);
            var mark = new Mark();
            context.Variables["arm"] = arm;
            context.Variables["mark"] = mark;
            context.Variables["source"] = source;
            const string script = "arm()\nfor item in source:mark()";
            var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(script).Script!));
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context);
                else LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);
            });
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            Assert.Same(failure, context.MemoryGovernor.AdmissionFailure);
            var origin = Assert.IsType<LythonSourceSpan>(failure.Span);
            Assert.Equal("source", script.Substring(origin.Start, origin.Length));
            Assert.Equal(0, mark.Calls);
            Assert.Equal(values, Assert.IsAssignableFrom<IEnumerable<object>>(source));
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Arm(LythonMemoryBudget budget) : LythonRuntime.ICallable, IDisposable
    {
        private ILythonMemoryReservation? _blocker;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Assert.True(budget.TryReserve(budget.LimitBytes!.Value - budget.ReservedBytes, out _blocker));
            return PyNone.Instance;
        }
        public void Dispose() => _blocker?.Dispose();
    }

    private sealed class Mark : LythonRuntime.ICallable
    {
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            return PyNone.Instance;
        }
    }
}
