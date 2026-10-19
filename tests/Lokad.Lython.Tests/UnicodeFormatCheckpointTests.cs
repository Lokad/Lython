using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class UnicodeFormatCheckpointTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SupplementaryPaddingHasCooperativeCheckpoints(bool asynchronous)
    {
        var shortCode = Compile(2);
        var longCode = Compile(4096);
        var shortSteps = await Measure(shortCode, 2);
        var longSteps = await Measure(longCode, 4096);
        Assert.True(longSteps > shortSteps);

        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = Prepare(owner);
        context.Limits.ExecutionStepCount = 10000000 - shortSteps;
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(longCode, context));
        Assert.Equal(LythonFailureReason.ExecutionStepLimit, failure.Attribution?.Reason);
        Check(context);
        owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);

        async Task<long> Measure(ExecutableCodeObject code, int width)
        {
            var controlBudget = new LythonMemoryBudget(1024 * 1024);
            using var controlOwner = controlBudget.CreateScope();
            var control = Prepare(controlOwner);
            var before = control.Limits.ExecutionStepCount;
            var value = await Run(code, control);
            Assert.Equal(width, Assert.IsType<PyString>(value).Length);
            var steps = control.Limits.ExecutionStepCount - before;
            Check(control);
            controlOwner.Dispose(); Assert.Equal(0, controlBudget.ReservedBytes);
            return steps;
        }

        ValueTask<object?> Run(ExecutableCodeObject code, Context context) => asynchronous
            ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : new(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
    }

    private static ExecutableCodeObject Compile(int width) => ExecutableScript.Compile(LoweredScript.Lower(
        LythonFrontend.Compile("return format(value,'🧡>" + width + "')").Script!)).EntryPoint;
    private static Context Prepare(ILythonMemoryBudget owner) => new(new MockLythonHost(), new LythonRunOptions
    { MemoryBudget = owner, MaxExecutionSteps = 10000000, Globals = new Dictionary<string, object?> { ["value"] = "x" } });
    private static void Check(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
