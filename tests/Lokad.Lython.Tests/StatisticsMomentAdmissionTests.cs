using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class StatisticsMomentAdmissionTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData("variance", false), InlineData("stdev", false)]
    [InlineData("variance", true), InlineData("stdev", true)]
    public void WideWorkingArithmeticIsAdmittedBeforeAllocation(string member, bool cancel)
    {
        var root = new LythonMemoryBudget(cancel ? 1048576 : 65536);
        using (var owner = root.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var budget = new CancelOnMajorGrowth(owner, cancellation);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
            {
                MemoryBudget = budget, CancellationToken = cancellation.Token,
            });
            Assert.True(LythonRuntime.StatisticsModule.Instance.TryGetMember(member, out var value));
            var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(value);
            _ = callable.Invoke([CallArgumentValue.Positional(new PyList([BigInteger.One, new BigInteger(3)]))], Span, context);
            var wide = BigInteger.One << 100000;
            var input = new PyList([wide, wide + 1]);
            var arguments = new[] { CallArgumentValue.Positional(input) };
            var before = root.ReservedBytes;
            budget.Armed = cancel;
            var allocation = GC.GetAllocatedBytesForCurrentThread();
            var error = Assert.Throws<LythonRuntimeException>(() => callable.Invoke(arguments, Span, context));
#if !DEBUG
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - allocation < wide.GetByteCount());
#endif
            Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
            if (cancel) { Assert.True(budget.Triggered); Assert.Null(context.MemoryGovernor.AdmissionFailure); }
            else Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
            Assert.Equal(wide, input[0]);
            Assert.Equal(wide + 1, input[1]);
            Assert.Equal(before, root.ReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    private sealed class CancelOnMajorGrowth(ILythonMemoryBudget inner, CancellationTokenSource cancellation) : ILythonMemoryBudget
    {
        public bool Armed { get; set; }
        public bool Triggered { get; private set; }
        private void Cancel() { Triggered = true; cancellation.Cancel(); }
        public ILythonMemoryScope CreateScope() => inner.CreateScope();
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!inner.TryReserve(bytes, out var actual)) { reservation = null; return false; }
            reservation = new Admission(this, actual); return true;
        }
        private sealed class Admission(CancelOnMajorGrowth owner, ILythonMemoryReservation inner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                var previous = inner.Bytes;
                if (!inner.TryResize(bytes)) return false;
                if (owner.Armed && bytes - previous >= 8192) owner.Cancel();
                return true;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
