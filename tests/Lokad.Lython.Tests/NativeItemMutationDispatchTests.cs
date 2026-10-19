using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class NativeItemMutationDispatchTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var operation in new[] { "set", "delete", "augmented", "immutable-set", "immutable-delete" })
        foreach (var deque in new[] { false, true })
        foreach (var descriptor in new[] { false, true })
        foreach (var asynchronous in new[] { false, true })
            if (!deque || !operation.StartsWith("immutable", StringComparison.Ordinal))
                yield return [operation, deque, descriptor, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task IndexBindingAndCallUseTheSelectedMode(string operation, bool deque, bool descriptor, bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var checkpoint = new Checkpoint();
        context.Variables["checkpoint"] = checkpoint;
        var hook = descriptor ? " @property\n def __index__(self):\n  checkpoint()\n  return lambda:1\n"
            : " def __index__(self): return checkpoint()\n";
        var immutable = operation.StartsWith("immutable", StringComparison.Ordinal);
        var action = operation switch { "set" or "immutable-set" => "values[key]=7", "delete" or "immutable-delete" => "del values[key]", _ => "values[key]+=7" };
        var source = "class Key:\n" + hook + "key=Key()\n" + (immutable ? "values=(0,1,2)\n" : deque ? "from collections import deque\nvalues=deque([0,1,2])\n" : "values=[0,1,2]\n") + action;
        var syntax = LythonFrontend.Compile(source).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        if (immutable)
        {
            LythonRuntimeException error;
            if (asynchronous)
                error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context));
            else error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
            Assert.Equal("TypeError", error.ExceptionType);
        }
        else if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        var calls = operation == "augmented" ? 2 : 1;
        Assert.Equal(asynchronous ? 0 : calls, checkpoint.SyncCalls);
        Assert.Equal(asynchronous ? calls : 0, checkpoint.AsyncCalls);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ConvertedIndexPreservesCallbackAdmissionBeforeMutation(bool asynchronous, bool denied)
    {
        const long limit = 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var target = new PyList([BigInteger.Zero], context.MemoryGovernor, span);
        using var fund = new AllocationGate(context.MemoryGovernor, limit, denied);
        var ready = new Checkpoint();
        context.Variables["target"] = target;
        context.Variables["fund"] = fund;
        context.Variables["ready"] = ready;
        var syntax = LythonFrontend.Compile("class Key:\n def __index__(self):\n  fund()\n  scratch=[0]*1024\n  ready()\n  return 0\nkey=Key()\ntarget[key]=1").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        try
        {
            if (denied)
            {
                LythonRuntimeException error;
                if (asynchronous)
                    error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context));
                else error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
                Assert.Equal("MemoryError", error.ExceptionType);
            }
            else if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            Assert.Equal(1, fund.Calls);
            Assert.Equal(denied || asynchronous ? 0 : 1, ready.SyncCalls);
            Assert.Equal(!denied && asynchronous ? 1 : 0, ready.AsyncCalls);
            Assert.Equal(denied ? BigInteger.Zero : BigInteger.One, Assert.Single(target.Iterate()));
            Assert.Null(context.CurrentExecutableFrame);
            Assert.True(context.MemoryGovernor.PeakAccountedBytes <= limit);
        }
        finally { fund.Dispose(); }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class AllocationGate(MemoryGovernor governor, long limit, bool denied) : LythonRuntime.ICallable, IDisposable
    {
        private IDisposable? _reservation;
        public int Calls { get; private set; }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            if (denied) _reservation = governor.ReserveTemporary(limit - governor.CurrentAccountedBytes - 32, span);
            return PyNone.Instance;
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
