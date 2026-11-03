using System.Numerics;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class DictStorageOriginalAccountingTests
{
    [Fact]
    public void RefusedEntryAdmissionDoesNotRetainAnUninsertedKey()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(256));
        var dictionary = new PyDict(governor);
        var before = governor.CurrentCommittedBytes;
        var error = Assert.Throws<LythonRuntimeException>(() =>
            dictionary.SetItem(new BigInteger(1), new BigInteger(2)));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Empty(dictionary);
        Assert.Equal(before, governor.CurrentCommittedBytes);
        Assert.Equal(before, dictionary.CommittedStorageBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovingAnEqualDistinctKeyKeepsItsReusableCapacityFunded(bool pop)
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        var dictionary = new PyDict(governor);
        dictionary.SetItem(new BigInteger(17), PyNone.Instance);
        var fundedEntry = governor.CurrentCommittedBytes;
        if (pop)
        {
            Assert.True(dictionary.TryRemove(new BigInteger(17), out var value));
            Assert.Same(PyNone.Instance, value);
        }
        else Assert.True(dictionary.Remove(new BigInteger(17)));
        Assert.Empty(dictionary);
        // Capacity and its reusable pair headroom remain owned after removal.
        Assert.Equal(fundedEntry, governor.CurrentCommittedBytes);
        Assert.Equal(governor.CurrentCommittedBytes, dictionary.CommittedStorageBytes);
    }

    [Fact]
    public void FirstAttachmentAdmitsExistingStorageBeforePublishingOwnership()
    {
        var dictionary = new PyDict();
        dictionary.SetItem(new BigInteger(1), new BigInteger(2));
        var values = dictionary.Values;
        var denied = new MemoryGovernor(new LythonMemoryBudget(1));
        var error = Assert.Throws<LythonRuntimeException>(() => dictionary.AttachMemoryGovernor(denied));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Null(dictionary.OwnerMemoryGovernor);
        Assert.Equal(0, denied.CurrentAccountedBytes);
        Assert.Equal(new BigInteger(2), dictionary.GetItem(BigInteger.One));
        var funded = new MemoryGovernor(new LythonMemoryBudget(4096));
        dictionary.AttachMemoryGovernor(funded);
        Assert.Same(funded, dictionary.OwnerMemoryGovernor);
        Assert.True(dictionary.CommittedStorageBytes > 0);
        Assert.Equal(dictionary.CommittedStorageBytes, funded.CurrentCommittedBytes);
        Assert.Equal(new BigInteger(2), dictionary.GetItem(BigInteger.One));
        dictionary.SetItem(BigInteger.One, new BigInteger(3));
        Assert.Equal(new BigInteger(3), Assert.Single(values));
    }
}
