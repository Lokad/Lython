using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class StructuralWorkBudgetTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreshExecutionsDoNotSharePeriodicWorkProgress(bool ambientFallback)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        // A thread-owned counter must cross a checkpoint across these contexts;
        // no individual execution has performed enough work to reach one.
        for (var execution = 0; execution < 64; execution++)
        {
            using (var owner = budget.CreateScope())
            {
                var context = new Context(new MockLythonHost(), new LythonRunOptions
                    { MemoryBudget = owner, MaxExecutionSteps = 0 });
                using (PyStructuralGuard.PushAmbient(context, null))
                    NoteWork(ambientFallback);
                Assert.Equal(0, context.Limits.ExecutionStepCount);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }

        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner, MaxExecutionSteps = 0 });
            using (PyStructuralGuard.PushAmbient(context, null))
            {
                for (var work = 0; work < 63; work++) NoteWork(ambientFallback);
                Assert.Equal(0, context.Limits.ExecutionStepCount);
                var error = Assert.Throws<LythonRuntimeException>(() => NoteWork(ambientFallback));
                Assert.Equal(LythonFailureReason.ExecutionStepLimit, error.Attribution?.Reason);
                Assert.Equal(0, error.Attribution?.Limit);
                Assert.Equal(1, context.Limits.ExecutionStepCount);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static void NoteWork(bool ambientFallback)
    {
        if (ambientFallback) PyStructuralGuard.NoteWork(null, null);
        else PyStructuralGuard.NoteWork();
    }
}
