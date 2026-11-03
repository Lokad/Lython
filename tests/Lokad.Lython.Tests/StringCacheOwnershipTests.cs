using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

// The retained offset table stays charged to the string's execution owner.
public sealed class StringCacheOwnershipTests
{
    [Fact]
    public void OffsetTableRemainsFundedByItsExecutionOwner()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        var text = PyString.FromString(new string('\u00e9', 100), governor);
        // Index builds the offset table and commits its own result string.
        _ = text.Index(5);
        // Construction plus the offset table and the indexed result string.
        Assert.Equal(128L + 200L + 32L + 4L * 101L + 130L, governor.CurrentCommittedBytes);
    }

}
