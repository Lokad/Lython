using System.Reflection;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ReentrantPromotionFundingTests
{
    [Fact]
    public void ReliefCannotPublishAnUnderfundedPromotionOrStrandItsReservation()
    {
        const long cap = 3L * 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        var governor = context.MemoryGovernor;
        var owner = new object();
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
        context.State.RegisterPool(owner, pool);
        var donorOwner = new object();
        var donor = new ChargeReclamationPool(governor);
        var dead = DropDonor(governor, donor);
        // Registrations enumerate newest first, so donor relief precedes the
        // active pool's nested sweep. Both owners stay live, like an active CSV
        // source and the input cursor registered on its first pull.
        context.State.RegisterPool(donorOwner, donor);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(dead.TryGetTarget(out _));
        Assert.Equal(33, pool.Count);
        Assert.Equal(320, ActualBacking(pool));
        var before = governor.CurrentCommittedBytes;
        var pressure = cap - governor.CurrentAccountedBytes - 63;
        governor.Reserve(pressure, null);

        var failure = Assert.Throws<LythonRuntimeException>(() => pool.Sweep());

        Assert.Equal("MemoryError", failure.ExceptionType);
        var actual = ActualBacking(pool);
        Assert.True(pool.Count == 33 && actual == 512 && pool.CommittedBackingBytes == actual &&
            governor.CurrentReservedBytes == pressure,
            $"entries={pool.Count} (expected 33); actual backing={actual} (expected 512); " +
            $"billed backing={pool.CommittedBackingBytes}; stranded reservation={governor.CurrentReservedBytes - pressure}");
        Assert.Equal(before - 257 + 192, governor.CurrentCommittedBytes);
        Assert.All(retained, value => Assert.True(pool.IsTracked(value)));

        // Funded retry must publish exactly once and reconcile actual capacity.
        governor.ReleaseReserved(pressure);
        pool.Sweep(full: true);
        Assert.Equal(33, pool.Count);
        Assert.Equal(768, ActualBacking(pool));
        Assert.Equal(ActualBacking(pool), pool.CommittedBackingBytes);
        Assert.Equal(before - 257 + 192 + 256, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.True(governor.PeakAccountedBytes <= cap);
        GC.KeepAlive(owner);
        GC.KeepAlive(donorOwner);
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
        new[] { "_young", "_old" }.Sum(name => 8L *
            ((List<ChargeReclamationPool.ReclamationEntry>)typeof(ChargeReclamationPool)
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pool)!).Capacity);
}
