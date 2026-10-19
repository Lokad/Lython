using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class CopyHookAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FiniteCopyHookCanRevisitTheSameObject(bool asynchronous)
    {
        const string source = "import copy\nclass Item:\n def __init__(self):self.left=3\n def __deepcopy__(self,memo):\n  self.left-=1\n  return copy.deepcopy(self,memo) if self.left else 7\nanswer=copy.deepcopy(Item())";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        await Execute(code, context, asynchronous);
        Assert.Equal(new BigInteger(7), context.Variables["answer"]);
        Assert.Equal(0, context.State.StructuralTraversal.Depth);
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RecursiveCopyAdmissionUnwindsExistingCountersAndCleanup(bool asynchronous, bool helper)
    {
        var source = "import copy\ncleanup=0\nclass Loop:\n def __deepcopy__(self,memo):\n  global cleanup\n  try:return " +
            (helper ? "self.again(memo)" : "copy.deepcopy(self,memo)") +
            "\n  finally:cleanup+=1\n def again(self,memo):return copy.deepcopy(self,memo)\ncopy.deepcopy(Loop())";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Execute(code, context, asynchronous));
        Assert.Equal(LythonFailureReason.InterpreterStackLimit, failure.Attribution?.Reason);
        Assert.True(failure.Attribution?.Limit is null or LythonRuntime.ExecutionLimits.MaxInterpreterDepth);
        Assert.True(Assert.IsType<BigInteger>(context.Variables["cleanup"]) > 0);
        Assert.Equal(0, context.State.StructuralTraversal.Depth);
        AssertUnwound(context);
        const string recovery = "answer=copy.deepcopy([1,[2]])";
        code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(recovery,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        await Execute(code, context, asynchronous);
        Assert.Equal(0, context.State.StructuralTraversal.Depth);
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static async ValueTask<object?> Execute(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);

    private static void AssertUnwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

}
