using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ManagedHashBoundaryTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AsyncPreparationBoundsContinuationNestingAndRecovers(bool cyclic)
    {
        await Task.Run(async () =>
        {
            await Task.Yield();
            var budget = new LythonMemoryBudget(1024 * 1024);
            using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var span = new LythonSourceSpan(31, 4, 2, 1);
            object key = BigInteger.One;
            if (cyclic)
            {
                var items = new object[1];
                key = PyTuple.FromOwnedArray(items);
                items[0] = key;
            }
            else
                for (var i = 0; i <= ExecutionState.MaxInterpreterDepth; i++) key = PyTuple.FromOwnedArray([key]);
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                await PyHashProtocols.GetProtocolHashAsync(key, context, span));
            Assert.Equal("RecursionError", failure.ExceptionType);
            Assert.Equal(span, failure.Span);
            Assert.Equal(0, context.State.StructuralTraversal.Depth);
            Assert.Empty(context.State.StructuralTraversal.ActivePath!);
            var small = PyTuple.FromOwnedArray([BigInteger.One]);
            Assert.Equal(PyHashProtocols.GetProtocolHash(small, context, span), await PyHashProtocols.GetProtocolHashAsync(small, context, span));
            Assert.Null(PyStructuralGuard.AmbientContext);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
        });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ClassificationBoundsDeepOrCyclicInputsAndRecovers(bool cyclic)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var span = new LythonSourceSpan(31, 4, 2, 1);
        object key = BigInteger.One;
        if (cyclic)
        {
            var items = new object[1];
            key = PyTuple.FromOwnedArray(items);
            items[0] = key;
        }
        else
        {
            for (var i = 0; i <= ExecutionState.MaxInterpreterDepth; i++)
                key = PyTuple.FromOwnedArray([key]);
        }
        using (PyStructuralGuard.PushAmbient(context, span))
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => PyHashProtocols.NeedsProtocolKey(key));
            Assert.Equal(LythonFailureReason.InterpreterStackLimit, failure.Attribution?.Reason);
            Assert.Equal(span, failure.Span);
            Assert.Equal(0, context.State.StructuralTraversal.Depth);
            Assert.False(PyHashProtocols.NeedsProtocolKey(PyTuple.FromOwnedArray([BigInteger.One])));
            Assert.Equal(0, context.State.StructuralTraversal.Depth);
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativeHashCannotReturnThroughCaughtRefusalOrCancellation(bool cancel)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("class Key:pass\nkey=Key()\n").Script!)).EntryPoint;
        LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        var key = Assert.IsType<PyInstance>(context.Variables["key"]);
        var native = new NativeHash(cancellation, cancel, budget.LimitBytes!.Value);
        Assert.True(key.Type.TrySetMember("__hash__", native));
        var failure = Assert.Throws<LythonRuntimeException>(() => PyHashProtocols.GetProtocolHash(key, context, native.Origin));
        Assert.Equal(1, native.Calls);
        if (cancel) Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
        else
        {
            Assert.Same(native.Refusal, failure);
            Assert.Equal(native.Origin, failure.Span);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
        }
        Assert.Equal(0, context.State.StructuralTraversal.Depth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class NativeHash(CancellationTokenSource cancellation, bool cancel, long requested) : LythonRuntime.ICallable
    {
        internal int Calls;
        internal LythonRuntimeException? Refusal;
        internal LythonSourceSpan Origin = new(31, 4, 2, 1);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            if (cancel) cancellation.Cancel();
            else
            {
                try { context.MemoryGovernor.Reserve(requested, Origin); }
                catch (LythonRuntimeException error) { Refusal = error; }
            }
            return new BigInteger(17);
        }
    }
}
