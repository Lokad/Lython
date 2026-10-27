using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DecimalPowerOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);
    private static readonly LythonRuntime.ExceptionTypeValue Inexact = new(LythonRuntime.ModuleException("decimal", "Inexact"));

    [Fact]
    public void UnfundedExactPowerRefusesBeforeGrowingOrChangingSignals()
    {
        var root = new LythonMemoryBudget(65536);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var policy = context.DecimalContext;
            var value = new PyDecimal(1.000001m);
            var before = root.ReservedBytes;
            var error = Assert.Throws<LythonRuntimeException>(() => PyDecimalOps.Power(value, new BigInteger(1000000), Span, "**", context));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
            Assert.Equal(1.000001m, value.Value);
            Assert.Equal(false, policy.Flags.GetItem(Inexact));
            Assert.Equal(before, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PowerObservesCancellationAfterWorkingStorageAdmission(bool modular)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using (var owner = root.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var budget = new CancelOnGrowth(owner, cancellation);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
            var policy = context.DecimalContext;
            var before = root.ReservedBytes;
            var error = Assert.Throws<LythonRuntimeException>(() => modular
                ? PyDecimalOps.PowerMod(BigInteger.One << 100000, new PyDecimal(1000m), new BigInteger(7), Span, context)
                : PyDecimalOps.Power(new PyDecimal(1.01m), new BigInteger(1000), Span, "**", context));
            Assert.True(budget.Triggered);
            Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.Null(context.MemoryGovernor.AdmissionFailure);
            Assert.Equal(false, policy.Flags.GetItem(Inexact));
            Assert.Equal(before, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void CompletedPowersReturnWorkingAllowanceBeforeReuse()
    {
        var root = new LythonMemoryBudget(65536);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            _ = context.DecimalContext;
            var before = root.ReservedBytes;
            for (var i = 0; i < 4; i++)
            {
                var result = Assert.IsType<PyDecimal>(PyDecimalOps.Power(new PyDecimal(1.25m), new BigInteger(2), Span, "**", context));
                Assert.Equal(1.5625m, result.Value);
                Assert.Equal(before, root.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    private sealed class CancelOnGrowth(ILythonMemoryBudget inner, CancellationTokenSource cancellation) : ILythonMemoryBudget
    {
        public bool Triggered { get; private set; }
        public ILythonMemoryScope CreateScope() => inner.CreateScope();
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!inner.TryReserve(bytes, out var actual)) { reservation = null; return false; }
            reservation = new Admission(this, actual);
            return true;
        }
        private void Grew(long previous, long bytes)
        {
            if (!Triggered && bytes - previous >= 8192) { Triggered = true; cancellation.Cancel(); }
        }
        private sealed class Admission(CancelOnGrowth owner, ILythonMemoryReservation inner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                var previous = inner.Bytes;
                if (!inner.TryResize(bytes)) return false;
                owner.Grew(previous, bytes);
                return true;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
