using System.Diagnostics.CodeAnalysis;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ProjectionCancellationTests
{
    [Theory]
    [InlineData("return [0]*64", 1)]
    [InlineData("return ['value']*64", 1400)]
    [InlineData("return 0", 0)]
    public async Task CancellationDuringProjectionFailsAndReturnsAllowanceAfterDisposal(string source, long cancelAtBytes)
    {
        using var script = new LythonEngine().Compile(source);
        var root = new LythonMemoryBudget(262144);
        foreach (var asynchronous in new[] { false, true })
        {
            using var cancellation = new CancellationTokenSource();
            var resultBudget = new CancellingBudget(root, cancellation, cancelAtBytes);
            var options = new LythonRunOptions { MemoryBudget = root, ResultMemoryBudget = resultBudget, CancellationToken = cancellation.Token };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(cancellation.IsCancellationRequested);
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Null(result.ReturnValue);
                Assert.True(root.ReservedBytes > 0); // Partial storage and failure details remain owned.
                Assert.Equal(result.PeakProjectionMemoryBytes, root.ReservedBytes);
            }
            Assert.Equal(0, root.ReservedBytes);
            using (var recovered = script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = root }))
                Assert.True(recovered.Success, recovered.Failure?.Message);
            Assert.Equal(0, root.ReservedBytes);
        }
    }

    private sealed class CancellingBudget(LythonMemoryBudget root, CancellationTokenSource cancellation, long cancelAtBytes) : ILythonMemoryBudget
    {
        public ILythonMemoryScope CreateScope() => new Owner(this, root.CreateScope());
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation) => root.TryReserve(bytes, out reservation);
        private void Admitted(long bytes) { if (bytes >= cancelAtBytes) cancellation.Cancel(); }
        private sealed class Owner(CancellingBudget probe, ILythonMemoryScope inner) : ILythonMemoryScope
        {
            public ILythonMemoryScope CreateScope() => new Owner(probe, inner.CreateScope());
            public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
            {
                if (!inner.TryReserve(bytes, out var admitted)) { reservation = null; return false; }
                reservation = new Reservation(probe, admitted);
                probe.Admitted(bytes);
                return true;
            }
            public void Dispose() => inner.Dispose();
        }
        private sealed class Reservation(CancellingBudget probe, ILythonMemoryReservation inner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                var accepted = inner.TryResize(bytes);
                if (accepted) probe.Admitted(bytes);
                return accepted;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
