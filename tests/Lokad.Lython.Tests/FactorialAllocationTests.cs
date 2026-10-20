using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class FactorialAllocationTests
{
    // Invoke the operation on this thread. Public Run schedules a worker, whose
    // allocation cannot be measured with a caller-thread counter.
    private static readonly Func<object[], LythonSourceSpan, Context, object> Factorial = typeof(LythonRuntime)
        .GetNestedType("MathModule", BindingFlags.NonPublic)!
        .GetMethod("Factorial", BindingFlags.NonPublic | BindingFlags.Static)!
        .CreateDelegate<Func<object[], LythonSourceSpan, Context, object>>();

    [Fact]
    public void DenialAvoidsArithmeticAllocationWithFundedPositiveControl()
    {
        var budget = new LythonMemoryBudget();
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            _ = Factorial([new BigInteger(12)], new(0, 0, 0, 0), context);
            object[] input = [new BigInteger(4000)];
            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = Factorial(input, new(0, 0, 0, 0), context);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(Assert.IsType<BigInteger>(result) > BigInteger.One);
            Assert.True(allocated > 1_000_000, $"Positive control allocated only {allocated} bytes.");
        }
        Assert.Equal(0, budget.ReservedBytes);

        budget = new LythonMemoryBudget(65536);
        using (var warmOwner = budget.CreateScope())
        {
            var warmContext = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = warmOwner });
            Assert.Throws<LythonRuntimeException>(() => Factorial([new BigInteger(20000)], new(0, 0, 0, 0), warmContext));
        }
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            object[] input = [new BigInteger(20000)];
            var committed = context.MemoryGovernor.CurrentCommittedBytes;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var error = Assert.Throws<LythonRuntimeException>(() => Factorial(input, new(0, 0, 0, 0), context));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
            Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.True(allocated < 65536, $"Refused factorial allocated {allocated} bytes.");
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
