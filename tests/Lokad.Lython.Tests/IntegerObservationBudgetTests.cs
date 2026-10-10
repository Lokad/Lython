using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class IntegerObservationBudgetTests
{
    [Theory]
    [InlineData("0", 33)]
    [InlineData("1", 33)]
    [InlineData("-1", 33)]
    [InlineData("127", 33)]
    [InlineData("128", 34)]
    [InlineData("-128", 33)]
    [InlineData("-129", 34)]
    [InlineData("9223372036854775807", 40)]
    [InlineData("9223372036854775808", 41)]
    [InlineData("-9223372036854775808", 40)]
    [InlineData("-9223372036854775809", 41)]
    [InlineData("18446744073709551616", 41)]
    public void ObservationUsesSignedPayloadSizeWithoutRetainingCharge(string text, long expectedBytes)
    {
        const long priorCommitment = 16;
        object value = BigInteger.Parse(text, CultureInfo.InvariantCulture);
        var span = new LythonSourceSpan(10, 4, 2, 3);

        foreach (var shortfall in new[] { 0, 1 })
        {
            var state = new ExecutionState(new MockLythonHost(), new LythonRunOptions
            {
                MaxExecutionMemoryBytes = priorCommitment + expectedBytes - shortfall
            });
            var services = new ExecutionServices(state);
            state.MemoryGovernor.Reserve(priorCommitment, null);
            state.MemoryGovernor.Commit(priorCommitment);

            if (shortfall == 0)
            {
                services.ObserveValue(value, span);
                Assert.Equal(0, state.MemoryGovernor.LastDeniedReservationBytes);
            }
            else
            {
                var error = Assert.Throws<LythonRuntimeException>(() => services.ObserveValue(value, span));
                Assert.Equal("MemoryError", error.ExceptionType);
                Assert.Same(span, error.Span);
                Assert.Equal(expectedBytes, state.MemoryGovernor.LastDeniedReservationBytes);
            }

            Assert.Equal(priorCommitment, state.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, state.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(priorCommitment, state.MemoryGovernor.CurrentAccountedBytes);
        }
        GC.KeepAlive(value);
    }

    [Fact]
    public void AlreadyOwnedHeapIntegerStillNeedsObservationPreflight()
    {
        const long budget = 4096;
        const long expectedBytes = 41; // 2**64 has nine signed payload bytes.
        var state = new ExecutionState(new MockLythonHost(), new LythonRunOptions
        {
            MaxExecutionMemoryBytes = budget
        });
        var services = new ExecutionServices(state);
        var governor = state.MemoryGovernor;
        var span = new LythonSourceSpan(20, 5, 3, 4);
        object value = BigInteger.One << 64;
        Assert.Same(value, LythonRuntime.OwnFreshInteger(value, governor, state.CallTemporaries, span));
        // Fund survivor promotion before pinning the remaining budget. Denial
        // relief may sweep the pool, whose bookkeeping has its own charges.
        Assert.Equal(0, state.CallTemporaries.Sweep(full: true));

        var padding = budget - governor.CurrentAccountedBytes - expectedBytes;
        governor.Reserve(padding, null);
        governor.Commit(padding);
        var committed = governor.CurrentCommittedBytes;
        services.ObserveValue(value, span);
        Assert.Equal(committed, governor.CurrentCommittedBytes);

        governor.Reserve(1, null);
        governor.Commit(1);
        var error = Assert.Throws<LythonRuntimeException>(() => services.ObserveValue(value, span));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Same(span, error.Span);
        Assert.Equal(expectedBytes, governor.LastDeniedReservationBytes);
        Assert.Equal(committed + 1, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(value);
    }
}
