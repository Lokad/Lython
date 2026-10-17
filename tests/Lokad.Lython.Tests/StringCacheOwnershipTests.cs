using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Retained caches stay charged; a denied partial build refunds unpublished storage.
[Collection("AllocationSensitive")]
public sealed class StringCacheOwnershipTests
{
    [Fact]
    public void OffsetTableRemainsFundedByItsExecutionOwner()
    {
        var governor = new MemoryGovernor(null);
        var text = PyString.FromString(new string('\u00e9', 100), governor);
        // Index builds the offset table and commits its own result string.
        _ = text.Index(5);
        // Construction plus the offset table and the indexed result string.
        Assert.Equal(128L + 200L + 32L + 4L * 101L + 130L, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void RuneCacheRemainsFundedByItsExecutionOwner()
    {
        var governor = new MemoryGovernor(null);
        var text = PyString.FromString(new string('\u00e9', 10), governor);
        _ = text.GetRunes();
        // Construction plus array plus one payload per non-ASCII rune.
        var expected = 128L + 20L + 32L + 8L * 10L + 10L * (128L + 2L);
        Assert.Equal(expected, governor.CurrentCommittedBytes);
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

}
