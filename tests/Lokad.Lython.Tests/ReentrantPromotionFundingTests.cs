using System.Reflection;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class ReentrantPromotionFundingTests
{
    [Fact]
    public void FundedSweepsDoNotTriggerPromotionReliefOrStrandReservations()
    {
        const long cap = 3L * 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        var governor = context.MemoryGovernor;
        var pool = new ChargeReclamationPool(governor);
        var retained = new List<PyString>();
        for (var index = 0; index < 8; index++)
        {
            var value = PyString.FromString("s", governor);
            retained.Add(value);
            pool.TrackString(value);
        }
        pool.Sweep(full: true);
        for (var index = 0; index < 25; index++)
        {
            var value = PyString.FromString("s", governor);
            retained.Add(value);
            pool.TrackString(value);
        }
        var donor = pool;
        var dead = DropDonor(governor, donor);
        // One shared pool contains retained storage and a collected donor.
        // Collected donor charges remain available if exhaustion relief needs
        // them, but sweeping the retained pool must not require any admission.
        governor.ReclamationPool = pool;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(dead.TryGetTarget(out _));
        Assert.Equal(34, pool.Count);
        Assert.Equal(512, ActualBacking(pool));
        var before = governor.CurrentCommittedBytes;
        var pressure = cap - governor.CurrentAccountedBytes - 63;
        governor.Reserve(pressure, null);

        Assert.Equal(257, pool.Sweep());
        var actual = ActualBacking(pool);
        Assert.True(pool.Count == 33 && actual == 512 && pool.CommittedBackingBytes == actual &&
            governor.CurrentReservedBytes == pressure,
            $"entries={pool.Count} (expected 33); actual backing={actual} (expected 512); " +
            $"billed backing={pool.CommittedBackingBytes}; stranded reservation={governor.CurrentReservedBytes - pressure}");
        Assert.Equal(before - 257, governor.CurrentCommittedBytes);
        Assert.All(retained, value => Assert.True(pool.IsTracked(value)));

        // A full sweep also requires no growth, and the donor's dead value
        // releases separately without disturbing the held pressure reservation.
        Assert.Equal(0, pool.Sweep(full: true));
        Assert.Equal(0, donor.Sweep(full: true));
        Assert.Equal(pressure, governor.CurrentReservedBytes);
        governor.ReleaseReserved(pressure);
        pool.Sweep(full: true);
        Assert.Equal(33, pool.Count);
        Assert.Equal(512, ActualBacking(pool));
        Assert.Equal(ActualBacking(pool), pool.CommittedBackingBytes);
        Assert.Equal(before - 257, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.True(governor.PeakAccountedBytes <= cap);
        GC.KeepAlive(retained);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<PyString> DropDonor(MemoryGovernor governor, ChargeReclamationPool pool)
    {
        var value = PyString.FromString("g", governor);
        pool.TrackString(value);
        return new(value);
    }

    private static long ActualBacking(ChargeReclamationPool pool) =>
        8L * ((List<ChargeReclamationPool.ReclamationEntry>)typeof(ChargeReclamationPool)
            .GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pool)!).Capacity;
}
