using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class DictWrapperStorageTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RemovalRetainsTheWrapperAndUnrelatedStorage(bool counter, bool pop)
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        governor.Reserve(257, null);
        governor.Commit(257);
        var remaining = CreateThenRemove(governor, counter, pop);
        Assert.Equal(257 + remaining, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long CreateThenRemove(MemoryGovernor governor, bool counter, bool pop)
    {
        var key = PyString.FromString("key");
        if (counter)
        {
            var wrapper = new PyCounter(governor);
            wrapper.SetItem(key, new BigInteger(7));
            Assert.True(pop ? wrapper.TryRemove(key, out _) : wrapper.Remove(key));
            return wrapper.CommittedStorageBytes;
        }
        else
        {
            var wrapper = new PyDefaultDict(PyNone.Instance, governor);
            wrapper.SetItem(key, new BigInteger(7));
            Assert.True(pop ? wrapper.TryRemove(key, out _) : wrapper.Remove(key));
            return wrapper.CommittedStorageBytes;
        }
    }
}
