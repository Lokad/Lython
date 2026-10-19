using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using System.Numerics;

namespace Lokad.Lython.Tests;

public sealed class ExecutableAugmentedSliceTests
{
    [Theory]
    [InlineData("def run(items,value):\n items[:]+=value", false)]
    [InlineData("def run(items,start,stop,step,value):\n items[start:stop:step]+=value", false)]
    [InlineData("def run(items,value):\n items[:]+=(yield value)", true)]
    [InlineData("def run(items,start,stop,step,value):\n (yield items)[(yield start):(yield stop):(yield step)]+=(yield value)", true)]
    public void SliceUpdatesUseFrameOperandsWithoutFallbacks(string source, bool generator)
    {
        var syntax = LythonFrontend.Compile(source).Script!;
        var code = Assert.Single(ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint.Functions).CodeObject!;
        Assert.Empty(code.StatementFallbacks);
        Assert.Empty(code.ExpressionFallbacks);
        Assert.Equal(generator, code.RequiresLocalVariableMirroring);
        Assert.Single(code.Constants.OfType<ExecutableStoreSlice>());
        Assert.Single(code.Constants.OfType<ExecutableReadRetainedSlice>());
    }

    [Theory]
    [InlineData("def run(items,value):\n (yield items)[:]=value", false)]
    [InlineData("def run(items,value):\n items[(yield 1):]=value", false)]
    [InlineData("def run(items,value):\n (yield items).value=value", true)]
    public void YieldingOrdinaryStoresUseSharedOperations(string source, bool member)
    {
        var syntax = LythonFrontend.Compile(source).Script!;
        var code = Assert.Single(ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint.Functions).CodeObject!;
        Assert.Empty(code.StatementFallbacks);
        Assert.Empty(code.ExpressionFallbacks);
        if (member) Assert.Single(code.Constants.OfType<ExecutableStoreMember>());
        else Assert.Single(code.Constants.OfType<ExecutableStoreSlice>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadRhsAndStoreUseTheChosenExecutionMode(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var checkpoint = new Checkpoint();
        context.Variables["checkpoint"] = checkpoint;
        var syntax = LythonFrontend.Compile("class Key:\n def __init__(self,value): self.value=value\n def __index__(self): return checkpoint(self.value)\nitems=[1,2,3]\nitems[Key(0):Key(1):Key(1)]+=checkpoint()").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        Assert.Equal(asynchronous ? 0 : 7, checkpoint.SyncCalls);
        Assert.Equal(asynchronous ? 7 : 0, checkpoint.AsyncCalls);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DeniedGrowthKeepsTheOriginalTargetUnchanged(bool asynchronous, bool deny)
    {
        const long limit = 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) });
        using var rhs = new CapacityGate(context.MemoryGovernor, limit, deny);
        context.Variables["rhs"] = rhs;
        var syntax = LythonFrontend.Compile("items=[0,1,2]\nitems[1:2]+=rhs([0]*1024,items)").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        try
        {
            if (deny)
            {
                LythonRuntimeException error;
                if (asynchronous)
                    error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context));
                else error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
                Assert.Equal("MemoryError", error.ExceptionType);
            }
            else if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            Assert.Equal(1, rhs.Calls);
            Assert.Equal(deny ? 3 : 1027, rhs.Items!.Count);
            Assert.Equal(BigInteger.Zero, rhs.Items[0]);
            Assert.Equal(BigInteger.One, rhs.Items[1]);
            Assert.Equal(deny ? new BigInteger(2) : BigInteger.Zero, rhs.Items[2]);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.True(context.MemoryGovernor.PeakAccountedBytes <= limit);
        }
        finally { rhs.Dispose(); }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class CapacityGate(MemoryGovernor governor, long limit, bool deny) : LythonRuntime.ICallable, IDisposable
    {
        private IDisposable? _reservation;
        public int Calls { get; private set; }
        public PyList? Items { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            Items = Assert.IsType<PyList>(arguments[1].Value);
            if (deny) _reservation = governor.ReserveTemporary(limit - governor.CurrentAccountedBytes - 32, span);
            return arguments[0].Value;
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => ValueTask.FromResult(Invoke(arguments, span, context));
        public void Dispose() { _reservation?.Dispose(); _reservation = null; }
    }

    private sealed class Checkpoint : LythonRuntime.ICallable
    {
        public int SyncCalls { get; private set; }
        public int AsyncCalls { get; private set; }
        private static object Value(CallArgumentValue[] arguments)
            => arguments.Length == 0 ? new PyList([new BigInteger(4)]) : arguments[0].Value;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            SyncCalls++;
            return Value(arguments);
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            AsyncCalls++;
            await Task.Yield();
            return Value(arguments);
        }
    }
}
