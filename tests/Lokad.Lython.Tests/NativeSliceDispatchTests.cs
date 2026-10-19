using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class NativeSliceDispatchTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var operation in new[] { "read", "assign", "delete", "indices", "length", "replacement", "read-key", "assign-key", "delete-key", "augmented", "augmented-key", "yielding-assign", "lazy-target", "class-assign" })
        foreach (var descriptor in new[] { false, true })
        foreach (var asynchronous in new[] { false, true })
            yield return [operation, descriptor, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ProtocolsUseTheChosenExecutionMode(string operation, bool descriptor, bool asynchronous)
    {
        var hook = descriptor
            ? " @property\n def __index__(self):\n  checkpoint()\n  return lambda:1\n"
            : " def __index__(self): return checkpoint()\n";
        var action = operation switch
        {
            "read" => "values[bound:]",
            "assign" => "values[bound:]=[7]",
            "delete" => "del values[bound:]",
            "indices" => "slice(bound,None,None).indices(3)",
            "length" => "slice(None).indices(bound)",
            "read-key" => "values[slice(bound,None)]",
            "assign-key" => "values[slice(bound,None)]=[7]",
            "delete-key" => "del values[slice(bound,None)]",
            "augmented" => "values[bound:]+=[7]",
            "augmented-key" => "values[slice(bound,None)]+=[7]",
            "yielding-assign" => "def update():\n values[(yield 'bound'):]=[7]\ng=update()\nnext(g)\ntry: g.send(bound)\nexcept StopIteration: pass",
            "lazy-target" => "list(None for values[bound:] in [[7]])",
            "class-assign" => "class C:\n values[bound:]=[7]",
            _ => "values[:]=replacement()"
        };
        var source = "class Bound:\n" + hook + "bound=Bound()\nvalues=[0,1,2]\ndef replacement():\n yield checkpoint()\n" + action;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var checkpoint = new Checkpoint();
        context.Variables["checkpoint"] = checkpoint;
        var syntax = LythonFrontend.Compile(source).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        var calls = operation is "augmented" or "augmented-key" ? 2 : 1;
        Assert.Equal(asynchronous ? 0 : calls, checkpoint.SyncCalls);
        Assert.Equal(asynchronous ? calls : 0, checkpoint.AsyncCalls);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OversizedReplacementFailsBeforeDestinationMutation(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(64 * 1024) });
        var target = new PyList([BigInteger.Zero], context.MemoryGovernor, new(0, 0, 0, 0));
        context.Variables["target"] = target;
        var syntax = LythonFrontend.Compile("def source():\n yield from range(100000)\ntarget[:]=source()").Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        LythonRuntimeException error;
        if (asynchronous)
            error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context));
        else error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
        Assert.Equal(BigInteger.Zero, Assert.Single(target.Iterate()));
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
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
