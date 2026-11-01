using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DictionaryCopyLifetimeTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    [Fact]
    public void UnknownLengthCopyAdmitsGrowthBeforeDrainingInputAndRefundsFailure()
    {
        var root = new LythonMemoryBudget(2048);
        var produced = 0;
        var disposed = false;
        IEnumerable<KeyValuePair<object, object>> Pairs()
        {
            try
            {
                for (var index = 0; index < 64; index++)
                {
                    produced++;
                    yield return new(new BigInteger(index), PyNone.Instance);
                }
            }
            finally { disposed = true; }
        }

        using (var owner = root.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var failure = Assert.Throws<LythonRuntimeException>(() => PyDictStorage.Create(Pairs(), governor, Span));
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            Assert.InRange(produced, 1, 63);
            Assert.True(disposed);
            Assert.Equal(0, governor.CurrentAccountedBytes);
            Assert.Equal(0, root.ReservedBytes);
        }
        using (var owner = root.CreateScope())
            Assert.Equal(0, PyDictStorage.Create(0, new MemoryGovernor(owner), Span).Count);
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void CancellationDuringManagedCopyStopsTheInputAndReturnsPrivateStorage()
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        var produced = 0;
        var disposed = false;
        IEnumerable<KeyValuePair<object, object>> Pairs()
        {
            try
            {
                for (var index = 0; index < 16384; index++)
                {
                    if (++produced == 10) cancellation.Cancel();
                    yield return new(new BigInteger(index), PyNone.Instance);
                }
            }
            finally { disposed = true; }
        }

        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var baseline = root.ReservedBytes;
            using var ambient = PyStructuralGuard.PushAmbient(context, Span);
            var failure = Assert.Throws<LythonRuntimeException>(() =>
                PyDictStorage.Create(Pairs(), context.MemoryGovernor, Span));
            Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
            Assert.InRange(produced, 10, 138);
            Assert.True(disposed);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(baseline, root.ReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }
}
