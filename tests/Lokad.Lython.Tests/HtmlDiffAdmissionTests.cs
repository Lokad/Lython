using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class HtmlDiffAdmissionTests
{
    [Fact]
    public void CancellationDuringEscapingRefundsWorkingStorage()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var controlled = new CancelOnGrowth(owner, cancellation);
        var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = controlled, CancellationToken = cancellation.Token });
        var caption = PyString.FromString(new string('<', 16000), context.MemoryGovernor, null);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        controlled.Threshold = before + 1024;
        var error = Assert.Throws<LythonRuntimeException>(() => Render(caption, context, false));
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.InRange(context.MemoryGovernor.CurrentCommittedBytes - before, 0, 4096);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefusalPrecedesProportionalEscapingAndRefundsBuilder(bool file)
    {
        var budget = new LythonMemoryBudget(65536);
        // Warm the renderer and refusal path outside the measured operation.
        for (var pass = 0; pass < 2; pass++)
        {
            using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var caption = PyString.FromString(new string('<', 16000), context.MemoryGovernor, null);
            var before = context.MemoryGovernor.CurrentCommittedBytes;
            Assert.True(budget.TryReserve(65536 - budget.ReservedBytes - 1024, out var padding));
            using (padding)
            {
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var error = Assert.Throws<LythonRuntimeException>(() => Render(caption, context, file));
                var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                // Empty sequence/matcher metadata stays batch-owned; the large
                // HTML backing must have been refunded without touching caption.
                Assert.InRange(context.MemoryGovernor.CurrentCommittedBytes - before, 0, 4096);
                Assert.Equal(16000, caption.Length);
                if (pass == 1) Assert.True(allocated < 65536, $"Denied rendering allocated {allocated} bytes.");
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static object Render(PyString caption, Context context, bool file)
    {
        var renderer = new LythonRuntime.DifflibHtmlDiffObject(8, null);
        Assert.True(renderer.TryGetMember(file ? "make_file" : "make_table", out var member));
        return Assert.IsAssignableFrom<LythonRuntime.ICallable>(member).Invoke(
            [CallArgumentValue.Positional(Array.Empty<object>()), CallArgumentValue.Positional(Array.Empty<object>()),
             CallArgumentValue.Positional(caption)], new(0, 0, 0, 0), context);
    }

    private sealed class CancelOnGrowth(ILythonMemoryBudget inner, CancellationTokenSource cancellation) : ILythonMemoryBudget
    {
        internal long Threshold = long.MaxValue;
        private void OnResize(long bytes) { if (bytes >= Threshold) cancellation.Cancel(); }
        public ILythonMemoryScope CreateScope() => inner.CreateScope();
        public bool TryReserve(long bytes, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!inner.TryReserve(bytes, out var value)) { reservation = null; return false; }
            reservation = new Reservation(value, this);
            return true;
        }
        private sealed class Reservation(ILythonMemoryReservation inner, CancelOnGrowth owner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                if (!inner.TryResize(bytes)) return false;
                owner.OnResize(bytes);
                return true;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
