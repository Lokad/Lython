using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class DictWrapperReclamationOriginalTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RemovalRefreshesTheWrapperWithoutReleasingUnrelatedStorage(bool counter, bool pop)
    {
        var governor = new MemoryGovernor(null);
        governor.Reserve(257, null);
        governor.Commit(257);
        var pool = new ChargeReclamationPool(governor);
        var remaining = CreateThenRemove(pool, governor, counter, pop);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(remaining + ChargeReclamationPool.EntryChargeBytes, pool.Sweep());
        Assert.Equal(257 + pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long CreateThenRemove(ChargeReclamationPool pool, MemoryGovernor governor, bool counter, bool pop)
    {
        var key = PyString.FromString("key");
        if (counter)
        {
            var wrapper = new PyCounter(governor);
            wrapper.SetItem(key, new BigInteger(7));
            pool.TrackFreshMutable(wrapper, wrapper.CommittedStorageBytes);
            Assert.True(pop ? wrapper.TryRemove(key, out _) : wrapper.Remove(key));
            return wrapper.CommittedStorageBytes;
        }
        else
        {
            var wrapper = new PyDefaultDict(PyNone.Instance, governor);
            wrapper.SetItem(key, new BigInteger(7));
            pool.TrackFreshMutable(wrapper, wrapper.CommittedStorageBytes);
            Assert.True(pop ? wrapper.TryRemove(key, out _) : wrapper.Remove(key));
            return wrapper.CommittedStorageBytes;
        }
    }
}
