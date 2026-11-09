using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class DeferredFunctionOwnershipTests
{
    [Theory]
    [InlineData(false, false, false)] [InlineData(false, true, false)]
    [InlineData(true, false, false)] [InlineData(true, true, false)]
    [InlineData(false, false, true)] [InlineData(false, true, true)]
    [InlineData(true, false, true)] [InlineData(true, true, true)]
    public async Task CapacityAdmissionSeesCurrentFunctionLocalsAndNeverReplaysPriorWrites(bool asynchronous, bool refuse, bool nested)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = hook });
        var definition = "def update(mapping,value):\n value+=3\n mapping['seen']=mapping['seen']+1\n if value<0:return None\n mapping['new']=value\n return value\n";
        if (nested) definition = "def factory():\n" + string.Join('\n', definition.Split('\n').Select(line => " " + line)) + "\n return update\nupdate=factory()\n";
        Execute("namespace=globals()\ntotals={'seen':0,'held':100}\n" + definition, context);
        var totals = Assert.IsType<PyDict>(context.Variables["totals"]);
        var observed = false;
        bool Observe()
        {
            if (PyStructuralGuard.AmbientContext is not { FunctionName: "update" } active) return false;
            Assert.True(active.TryReadExecutableLocal("value", out var value));
            Assert.Equal(new BigInteger(8), value);
            Assert.True(totals.TryGetValue(PyString.FromString("seen"), out var seen));
            Assert.Equal(BigInteger.One, seen);
            observed = true;
            return true;
        }
        if (refuse) hook.RefuseGrowth = _ => Observe();
        else hook.AfterGrowth = _ => { if (Observe()) hook.AfterGrowth = null; };
        var code = Compile("result=update(totals,5)\n");
        if (refuse)
        {
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            });
            Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            Assert.False(context.Variables.ContainsKey("result"));
            Assert.False(totals.ContainsKey(PyString.FromString("new")));
            Assert.Equal(2, totals.Count);
        }
        else
        {
            if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
            else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            Assert.Equal(new BigInteger(8), context.Variables["result"]);
            Assert.True(totals.TryGetValue(PyString.FromString("new"), out var value));
            Assert.Equal(new BigInteger(8), value);
        }
        Assert.True(observed);
        Assert.Equal(0, context.State.CurrentRecursionDepth);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        context.State.CloseResources();
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
    private static void Execute(string source, Context context)
        => LythonRuntime.ExecuteExecutableCodeObject(Compile(source), context, default, null);
}
