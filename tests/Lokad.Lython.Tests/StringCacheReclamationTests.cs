using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// M04: rune-offset tables and cached rune storage belong to the owning
// string coupon: tracked strings release tables and runes with construction
// on drop, and a denied mid-build cache rolls back to the pre-build coupon.
public sealed class StringCacheReclamationTests
{
    [Fact]
    public void OffsetTableJoinsTrackedCoupon()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var text = PyString.FromString(new string('\u00e9', 100), governor);
        pool.TrackString(text);
        // Index builds the offset table and commits its own result string.
        _ = text.Index(5);
        // Construction plus the offset table beside the registry entry.
        Assert.Equal(128L + 200L + 32L + 4L * 101L + 128L + 130L + pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void DroppedStringReleasesTableWithConstruction()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        TrackIndexedString(pool, governor);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(128L + 200L + 32L + 4L * 101L + 128L + 130L + 128L, pool.Sweep());
        Assert.Equal(pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void RunesCacheJoinsTrackedCoupon()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var text = PyString.FromString(new string('\u00e9', 10), governor);
        pool.TrackString(text);
        _ = text.GetRunes();
        // Construction plus array plus one payload per non-ASCII rune.
        var expected = 128L + 20L + 32L + 8L * 10L + 10L * (128L + 2L);
        Assert.Equal(expected + 128L + pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void PartialRuneCacheRollsBack()
    {
        // Construction and array fit, but the sixth rune does not: the build
        // fails with only the construction charge left behind.
        var governor = new MemoryGovernor(2128L + 8032L + 130L * 5L);
        var text = PyString.FromString(new string('\u00e9', 1000), governor);
        var failure = Assert.Throws<LythonRuntimeException>(() => text.GetRunes());
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(2128L, text.CommittedOwnedBytes);
        Assert.Equal(2128L, governor.CurrentCommittedBytes);
        GC.KeepAlive(text);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void TrackIndexedString(ChargeReclamationPool pool, MemoryGovernor governor)
    {
        var text = PyString.FromString(new string('\u00e9', 100), governor);
        pool.TrackString(text);
        // Production tracks Index results through the slice funnel; mirror it so
        // the drop releases exactly what the run owns.
        pool.TrackString(text.Index(5));
    }
}
