using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class IntegerBinaryAdmissionTests
{
    public static IEnumerable<object[]> Operations()
    {
        foreach (var op in new[] { BinaryOperatorSyntax.Subtract, BinaryOperatorSyntax.Multiply,
                     BinaryOperatorSyntax.FloorDivide, BinaryOperatorSyntax.Modulo })
        foreach (var asynchronous in new[] { false, true }) yield return [(int)op, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task RefusedMajorArithmeticDoesNotAllocateTheMagnitude(int operation, bool asynchronous)
    {
        var op = (BinaryOperatorSyntax)operation;
        // Warm bookkeeping separately; refusal remains terminal in each command.
        foreach (var measured in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(4 * 1024 * 1024);
            using (var owner = budget.CreateScope())
            {
                var gate = new AdmissionHookMemoryBudget(owner);
                var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = gate });
                var span = new LythonSourceSpan(7, 12, 1, 8);
                var left = LythonRuntime.OwnFreshInteger((BigInteger.One << 524288) + 19, context.MemoryGovernor, span);
                var right = LythonRuntime.OwnFreshInteger((BigInteger.One << 256) + 3, context.MemoryGovernor, span);
                var baseline = budget.ReservedBytes;
                long? beforeRefusal = null;
                var before = GC.GetAllocatedBytesForCurrentThread();
                gate.RefuseGrowth = _ => { beforeRefusal ??= GC.GetAllocatedBytesForCurrentThread() - before; return true; };
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                {
                    if (asynchronous) await LythonRuntime.EvaluateBinaryOperatorAsync(op, left, right, context, span);
                    else LythonRuntime.EvaluateBinaryOperator(op, left, right, context, span);
                });
                Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Equal(span, error.Span);
                Assert.NotNull(beforeRefusal);
                if (measured) Assert.True(beforeRefusal < 32768, $"Allocated {beforeRefusal} before refusing {op}.");
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal((BigInteger.One << 524288) + 19, (BigInteger)left);
                Assert.Equal((BigInteger.One << 256) + 3, (BigInteger)right);
                context.State.CloseResources();
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task AdmittedMagnitudeAndInputAliasesRemainFunded(int operation, bool asynchronous)
    {
        var op = (BinaryOperatorSyntax)operation;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var span = new LythonSourceSpan(7, 12, 1, 8);
            var seed = (BigInteger.One << 65536) + 19;
            var left = LythonRuntime.OwnFreshInteger(seed, context.MemoryGovernor, span);
            var right = LythonRuntime.OwnFreshInteger(op == BinaryOperatorSyntax.Modulo ? seed + 1 : new BigInteger(3), context.MemoryGovernor, span);
            var baseline = budget.ReservedBytes;
            var result = asynchronous
                ? await LythonRuntime.EvaluateBinaryOperatorAsync(op, left, right, context, span)
                : LythonRuntime.EvaluateBinaryOperator(op, left, right, context, span);
            var expected = op switch
            {
                BinaryOperatorSyntax.Subtract => seed - (BigInteger)right,
                BinaryOperatorSyntax.Multiply => seed * (BigInteger)right,
                BinaryOperatorSyntax.FloorDivide => seed / (BigInteger)right,
                _ => seed,
            };
            Assert.Equal(expected, Assert.IsType<BigInteger>(result));
            Assert.Equal(seed, (BigInteger)left);
            Assert.True(budget.ReservedBytes - baseline >= 8192);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            context.State.CloseResources();
            // Resource cleanup must not refund the execution's still-live values.
            Assert.Equal(expected, (BigInteger)result);
            Assert.True(budget.ReservedBytes > baseline);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task CancellationDuringAdmissionRefundsScratchBeforePublication(int operation, bool asynchronous)
    {
        var op = (BinaryOperatorSyntax)operation;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var gate = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = gate, CancellationToken = cancellation.Token });
            var span = new LythonSourceSpan(7, 12, 1, 8);
            var left = LythonRuntime.OwnFreshInteger(BigInteger.One << 65536, context.MemoryGovernor, span);
            var right = LythonRuntime.OwnFreshInteger(new BigInteger(3), context.MemoryGovernor, span);
            var baseline = budget.ReservedBytes;
            gate.AfterGrowth = _ => cancellation.Cancel();
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await LythonRuntime.EvaluateBinaryOperatorAsync(op, left, right, context, span);
                else LythonRuntime.EvaluateBinaryOperator(op, left, right, context, span);
            });
            Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Null(context.MemoryGovernor.AdmissionFailure);
            Assert.Equal(BigInteger.One << 65536, (BigInteger)left);
            context.State.CloseResources();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
