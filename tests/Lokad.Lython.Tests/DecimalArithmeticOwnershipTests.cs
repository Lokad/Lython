using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DecimalArithmeticOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void RefusedArithmeticScratchDoesNotPublishACounterUpdate()
    {
        var budget = new LythonMemoryBudget(16384);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            _ = context.DecimalContext;
            var key = PyString.FromString("value");
            var old = new PyDecimal(1.234m);
            var counter = new PyCounter(context.MemoryGovernor, Span);
            counter.SetItem(key, old);
            var incoming = new PyDict(context.MemoryGovernor, Span);
            incoming.SetItem(key, new PyDecimal(0.006m));
            Assert.True(LythonRuntime.CounterMembers.TryGetMember(counter, "update", out var update));
            Assert.True(owner.TryReserve(budget.LimitBytes!.Value - budget.ReservedBytes - 32, out var pin));
            using (pin)
            {
                var before = budget.ReservedBytes;
                var error = Assert.Throws<LythonRuntimeException>(() => ((LythonRuntime.ICallable)update).Invoke([CallArgumentValue.Positional(incoming)], Span, context));
                Assert.Equal("MemoryError", error.ExceptionType);
                Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
                Assert.True(counter.TryGetValue(key, out var stored));
                Assert.Same(old, stored);
                Assert.Equal(before, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ExactArithmeticReturnsScratchBeforeTheNextOperation()
    {
        var budget = new LythonMemoryBudget(16384);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            _ = context.DecimalContext;
            var before = budget.ReservedBytes;
            for (var i = 0; i < 4; i++)
            {
                var result = Assert.IsType<PyDecimal>(PyDecimalOps.Add(new PyDecimal(1.25m), new PyDecimal(2.75m), Span, "+", context));
                Assert.Equal(4m, result.Value);
                Assert.Equal(before, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
