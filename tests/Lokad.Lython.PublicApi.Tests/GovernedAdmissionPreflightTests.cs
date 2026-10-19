using System.Diagnostics.CodeAnalysis;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GovernedAdmissionPreflightTests
{
    [Theory]
    [InlineData("return 1 << 6000000\n")]
    [InlineData("return 'x' * 750000\n")]
    public void SharedRefusalPrecedesTheLargePayloadAllocation(string source)
    {
        var engine = new LythonEngine(Utf8RegexProvider.Instance);
        using var warm = engine.Run("pass\n", new MockLythonHost());
        var script = engine.Compile(source);
        var budget = new RecordingBudget();
        using var result = script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget });
        Assert.False(result.Success);
        Assert.True(budget.LargeRequest >= 750000);
        Assert.True(budget.AllocatedAtRefusal < 262144,
            $"Large admission saw {budget.AllocatedAtRefusal} allocated bytes since execution ownership began.");
        result.Dispose(); // The returned failure diagnostics also have a result owner.
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    private sealed class RecordingBudget : ILythonMemoryBudget
    {
        internal readonly LythonMemoryBudget Root = new(32768);
        internal long LargeRequest;
        internal long AllocatedAtRefusal;
        private int _scopes;
        public ILythonMemoryScope CreateScope()
            => new Owner(this, Root.CreateScope(), Interlocked.Increment(ref _scopes) == 1);
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
            => Root.TryReserve(bytes, out reservation);

        private sealed class Owner(RecordingBudget probe, ILythonMemoryScope inner, bool execution) : ILythonMemoryScope
        {
            private long _initial;
            public ILythonMemoryScope CreateScope() => new Owner(probe, inner.CreateScope(), execution);
            public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
            {
                if (execution) _initial = GC.GetAllocatedBytesForCurrentThread();
                if (!inner.TryReserve(bytes, out var admitted)) { reservation = null; return false; }
                reservation = new Admission(probe, admitted, _initial, execution);
                return true;
            }
            public void Dispose() => inner.Dispose();
        }

        private sealed class Admission(RecordingBudget probe, ILythonMemoryReservation inner, long initial, bool execution) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                var accepted = inner.TryResize(bytes);
                if (execution && !accepted && bytes >= 750000)
                {
                    probe.LargeRequest = bytes;
                    probe.AllocatedAtRefusal = GC.GetAllocatedBytesForCurrentThread() - initial;
                }
                return accepted;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
