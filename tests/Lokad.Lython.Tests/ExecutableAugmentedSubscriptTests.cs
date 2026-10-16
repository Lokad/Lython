using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutableAugmentedSubscriptTests
{
    [Theory]
    [InlineData("def run(items,key,value):\n items[key]+=value", false)]
    [InlineData("def run(items,key,value):\n items[(yield key)]+=value", true)]
    [InlineData("def run(items,key,value):\n (yield items)[key]+=(yield value)", true)]
    public void UpdatesRetainOperandsWithoutPreparedReads(string source, bool generator)
    {
        var syntax = LythonFrontend.Compile(source).Script!;
        var code = Assert.Single(ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint.Functions).CodeObject!;
        Assert.Empty(code.StatementFallbacks);
        Assert.Empty(code.ExpressionFallbacks);
        Assert.Equal(generator, code.RequiresLocalVariableMirroring);
        Assert.Single(code.Constants.OfType<ExecutableStoreSubscript>());
        Assert.Single(code.Constants.OfType<ExecutableReadRetainedSubscript>());
        Assert.Empty(code.Constants.OfType<ExecutableReadPreparedTarget>());
        Assert.Empty(code.Constants.OfType<ExecutableStorePreparedTarget>());
    }

    [Fact]
    public void YieldingOrdinaryStoreSharesTheSubscriptStore()
    {
        var syntax = LythonFrontend.Compile("def run(items,key,value):\n (yield items)[(yield key)]=value").Script!;
        var code = Assert.Single(ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint.Functions).CodeObject!;
        Assert.Empty(code.StatementFallbacks);
        Assert.Empty(code.ExpressionFallbacks);
        Assert.Single(code.Constants.OfType<ExecutableStoreSubscript>());
        Assert.Empty(code.Constants.OfType<ExecutableStorePreparedTarget>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetterRhsAndSetterUseTheChosenExecutionMode(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var checkpoint = new Checkpoint();
        context.Variables["checkpoint"] = checkpoint;
        var syntax = LythonFrontend.Compile("class Box:\n def __getitem__(self,key): return checkpoint(key)\n def __setitem__(self,key,value): checkpoint(key,value)\nbox=Box()\nkey=[]\nbox[key]+=checkpoint()").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, [], null);
        Assert.Equal(asynchronous ? 0 : 3, checkpoint.SyncCalls);
        Assert.Equal(asynchronous ? 3 : 0, checkpoint.AsyncCalls);
        Assert.Same(context.Variables["key"], checkpoint.Key);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task InplaceGrowthRequiresAdmissionBeforeAliasMutation(bool asynchronous, bool deny)
    {
        const long limit = 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = limit });
        using var rhs = new CapacityGate(context.MemoryGovernor, limit, deny);
        context.Variables["rhs"] = rhs;
        var syntax = LythonFrontend.Compile("items=[[1]]\nitems[0]+=rhs([0]*1024,items)").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        try
        {
            if (deny)
            {
                LythonRuntimeException error;
                if (asynchronous)
                    error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context));
                else error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code, context, default, [], null));
                Assert.Equal("MemoryError", error.ExceptionType);
            }
            else if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, [], null);
            Assert.Equal(1, rhs.Calls);
            var alias = Assert.IsType<PyList>(rhs.Alias);
            Assert.Equal(deny ? 1 : 1025, alias.Count);
            Assert.Same(alias, rhs.Items![0]);
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
        public object? Alias { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            Items = Assert.IsType<PyList>(arguments[1].Value);
            Alias = Items[0];
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
        public object? Key { get; private set; }
        private object Check(CallArgumentValue[] arguments)
        {
            if (arguments.Length == 1) Key = arguments[0].Value;
            else if (arguments.Length == 2)
            {
                Assert.Same(Key, arguments[0].Value);
                Assert.Equal(new BigInteger(2), arguments[1].Value);
            }
            return BigInteger.One;
        }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            SyncCalls++;
            return Check(arguments);
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            AsyncCalls++;
            await Task.Yield();
            return Check(arguments);
        }
    }
}
