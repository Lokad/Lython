using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DecimalArithmeticOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void RefusedContextOperationScratchPreservesLiveSignalAliases(int operation)
    {
        var budget = new LythonMemoryBudget(16384);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var policy = context.DecimalContext;
            var flags = policy.Flags;
            var value = new PyDecimal(1.234m);
            Assert.True(owner.TryReserve(budget.LimitBytes!.Value - budget.ReservedBytes - 512, out var pin));
            using (pin)
            {
                var before = budget.ReservedBytes;
                var error = Assert.Throws<LythonRuntimeException>(() => operation switch
                {
                    0 => PyDecimalOps.Quantize(value, -1, PyNone.Instance, policy, context, Span),
                    1 => PyDecimalOps.ToIntegral(value, [], true, context, Span),
                    2 => PyDecimalOps.Normalize(value, [], context, Span),
                    3 => PyDecimalOps.MinMax(value, [new PyDecimal(2m)], "min", context, Span),
                    4 => PyDecimalOps.ScaleB(value, [new System.Numerics.BigInteger(2)], Span, context),
                    5 => PyDecimalOps.RemainderNear(value, [new PyDecimal(1m)], Span, context),
                    6 => PyDecimalOps.LogB(value, [], Span, context),
                    7 => PyDecimalOps.ShiftDigits(value, [System.Numerics.BigInteger.One], false, Span, context),
                    _ => PyDecimalOps.ShiftDigits(value, [System.Numerics.BigInteger.One], true, Span, context),
                });
                Assert.Equal("MemoryError", error.ExceptionType);
                Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
                Assert.Same(flags, policy.Flags);
                Assert.All(flags, pair => Assert.Equal(false, pair.Value));
                Assert.Equal(1.234m, value.Value);
                Assert.Equal(before, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RefusedRemainderLeavesLiveSignalFlagsUnchanged()
    {
        var budget = new LythonMemoryBudget(16384);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var policy = context.DecimalContext;
            policy.TrySetMember("prec", new System.Numerics.BigInteger(1));
            var flags = policy.Flags;
            var inexact = new LythonRuntime.ExceptionTypeValue(LythonRuntime.ModuleException("decimal", "Inexact"));
            Assert.True(owner.TryReserve(budget.LimitBytes!.Value - budget.ReservedBytes - 512, out var pin));
            using (pin)
            {
                var before = budget.ReservedBytes;
                var error = Assert.Throws<LythonRuntimeException>(() => PyDecimalOps.Modulo(new PyDecimal(1.234m), new PyDecimal(1m), Span, "%", context));
                Assert.Equal("MemoryError", error.ExceptionType);
                Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
                Assert.Equal(false, flags.GetItem(inexact));
                Assert.Same(flags, policy.Flags);
                Assert.Equal(before, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RefusedFloatWorkingStorageLeavesSignalAliasesUnchanged()
    {
        var budget = new LythonMemoryBudget(16384);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var policy = context.DecimalContext;
            var flag = new LythonRuntime.ExceptionTypeValue(LythonRuntime.ModuleException("decimal", "FloatOperation"));
            var flags = policy.Flags;
            Assert.True(owner.TryReserve(budget.LimitBytes!.Value - budget.ReservedBytes - 2048, out var pin));
            using (pin)
            {
                var before = budget.ReservedBytes;
                var error = Assert.Throws<LythonRuntimeException>(() => PyDecimalOps.Parse(0.1, Span, context, policy));
                Assert.Equal("MemoryError", error.ExceptionType);
                Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
                Assert.Equal(false, flags.GetItem(flag));
                Assert.Same(flags, policy.Flags);
                Assert.Equal(before, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

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

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void ExactOperationsReturnScratchBeforeTheNextOperation(int operation)
    {
        var budget = new LythonMemoryBudget(16384);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            _ = context.DecimalContext;
            var before = budget.ReservedBytes;
            for (var i = 0; i < 4; i++)
            {
                var result = Assert.IsType<PyDecimal>(operation switch
                {
                    0 => PyDecimalOps.Add(new PyDecimal(1.25m), new PyDecimal(2.75m), Span, "+", context),
                    1 => PyDecimalOps.Quantize(new PyDecimal(4m), 0, PyNone.Instance, context.DecimalContext, context, Span),
                    2 => PyDecimalOps.ToIntegral(new PyDecimal(4.00m), [], true, context, Span),
                    3 => PyDecimalOps.Normalize(new PyDecimal(4m), [], context, Span),
                    4 => PyDecimalOps.MinMax(new PyDecimal(4m), [new PyDecimal(5m)], "min", context, Span),
                    5 => PyDecimalOps.ScaleB(new PyDecimal(0.04m), [new System.Numerics.BigInteger(2)], Span, context),
                    6 => PyDecimalOps.RemainderNear(new PyDecimal(4m), [new PyDecimal(10m)], Span, context),
                    7 => PyDecimalOps.LogB(new PyDecimal(10000m), [], Span, context),
                    8 => PyDecimalOps.ShiftDigits(new PyDecimal(4m), [System.Numerics.BigInteger.Zero], false, Span, context),
                    _ => PyDecimalOps.ShiftDigits(new PyDecimal(4m), [System.Numerics.BigInteger.Zero], true, Span, context),
                });
                Assert.Equal(4m, result.Value);
                Assert.Equal(before, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
