using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class FailureAttributionGuardTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void ArithmeticExhaustionReportsTheActualReservation()
    {
        var governor = new MemoryGovernor(null);
        governor.Reserve(long.MaxValue, Span);
        governor.Commit(long.MaxValue);
        var error = Assert.Throws<LythonRuntimeException>(() => governor.Reserve(1, Span));
        Assert.Equal(new LythonFailureAttribution(LythonFailureReason.ExecutionMemoryLimit, null, long.MaxValue, 1), error.Attribution);
        Assert.Equal(long.MaxValue, governor.CurrentAccountedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void StructuralEntryDistinguishesConfiguredRecursionAndFixedNesting(bool pair, bool interpreter)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxRecursionDepth = 3 });
        if (interpreter) context.Limits.CurrentInterpreterDepth = LythonRuntime.ExecutionLimits.MaxInterpreterDepth;
        else context.Limits.CurrentRecursionDepth = 3;
        var value = new object();
        var error = Assert.Throws<LythonRuntimeException>(() =>
        {
            using var scope = pair ? PyStructuralGuard.EnterPair(value, new object(), Span, context)
                : PyStructuralGuard.EnterSingle(value, Span, context);
        });
        var limit = interpreter ? LythonRuntime.ExecutionLimits.MaxInterpreterDepth : 3;
        Assert.Equal(new LythonFailureAttribution(interpreter ? LythonFailureReason.InterpreterStackLimit : LythonFailureReason.RecursionDepthLimit,
            limit, limit, limit + 1), error.Attribution);
        Assert.Equal(0, context.State.StructuralTraversal.Depth);
    }

    [Fact]
    public void UnrepresentableHostSizeIsOmittedRatherThanWrappedOrSaturated()
    {
        var error = RuntimeErrors.HostRead("controlled denial", 10, BigInteger.One << 90, Span);
        Assert.Equal(new LythonFailureAttribution(LythonFailureReason.HostReadLimit, 10), error.Attribution);
    }
}
