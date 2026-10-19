using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutableAugmentedMemberTests
{
    [Fact]
    public void MemberUpdatesUseExistingInstructionsWithoutMirroring()
    {
        var syntax = LythonFrontend.Compile("def run(receiver,value):\n receiver().total+=value").Script!;
        var code = Assert.Single(ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint.Functions).CodeObject!;
        Assert.Empty(code.StatementFallbacks);
        Assert.Empty(code.ExpressionFallbacks);
        Assert.False(code.RequiresLocalVariableMirroring);
        Assert.Single(code.Constants.OfType<ExecutableStoreMember>());
        Assert.Single(code.Blocks.SelectMany(b => b.Instructions), i => i.OpCode == ExecutableOpCode.LoadMember);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedAttributeGrowthAfterRhsDoesNotStore(bool asynchronous)
    {
        const long limit = 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) });
        using var rhs = new DenyCapacity(context.MemoryGovernor, limit);
        context.Variables["rhs"] = rhs;
        var syntax = LythonFrontend.Compile("class Box:\n def __getattr__(self,name): return 1\nbox=Box()\nbox.value+=rhs()").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        try
        {
            LythonRuntimeException error;
            if (asynchronous)
                error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context));
            else error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(1, rhs.Calls);
            Assert.Equal(0, Assert.IsType<PyInstance>(context.Variables["box"]).OwnAttributeCount);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.True(context.MemoryGovernor.PeakAccountedBytes <= limit);
        }
        finally { rhs.Dispose(); }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetterRhsAndSetterUseTheChosenExecutionMode(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var checkpoint = new Checkpoint();
        context.Variables["checkpoint"] = checkpoint;
        var syntax = LythonFrontend.Compile("class Box:\n @property\n def value(self): return checkpoint()\n @value.setter\n def value(self,value): checkpoint()\nbox=Box()\nbox.value+=checkpoint()").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        Assert.Equal(asynchronous ? 0 : 3, checkpoint.SyncCalls);
        Assert.Equal(asynchronous ? 3 : 0, checkpoint.AsyncCalls);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class DenyCapacity(MemoryGovernor governor, long limit) : LythonRuntime.ICallable, IDisposable
    {
        private IDisposable? _reservation;
        public int Calls { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            _reservation = governor.ReserveTemporary(limit - governor.CurrentAccountedBytes - 32, span);
            return BigInteger.Zero;
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => ValueTask.FromResult(Invoke(arguments, span, context));
        public void Dispose() { _reservation?.Dispose(); _reservation = null; }
    }

    private sealed class Checkpoint : LythonRuntime.ICallable
    {
        public int SyncCalls { get; private set; }
        public int AsyncCalls { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            SyncCalls++;
            return BigInteger.One;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            AsyncCalls++;
            await Task.Yield();
            return BigInteger.One;
        }
    }
}
