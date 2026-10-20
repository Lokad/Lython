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
    public void RemovingAnEqualDistinctKeyReleasesItsRetainedCharge(bool pop)
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        var dictionary = new PyDict(governor);
        var before = governor.CurrentCommittedBytes;
        dictionary.SetItem(new BigInteger(17), PyNone.Instance);
        if (pop)
        {
            Assert.True(dictionary.TryRemove(new BigInteger(17), out var value));
            Assert.Same(PyNone.Instance, value);
        }
        else Assert.True(dictionary.Remove(new BigInteger(17)));
        Assert.Empty(dictionary);
        Assert.Equal(before, governor.CurrentCommittedBytes);
        Assert.Equal(before, dictionary.CommittedStorageBytes);
    }

    [Fact]
    public void FirstAttachmentAdmitsExistingStorageBeforePublishingOwnership()
    {
        var dictionary = new PyDict();
        dictionary.SetItem(new BigInteger(1), new BigInteger(2));
        var denied = new MemoryGovernor(new LythonMemoryBudget(1));
        var error = Assert.Throws<LythonRuntimeException>(() => dictionary.AttachMemoryGovernor(denied));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Null(dictionary.OwnerMemoryGovernor);
        Assert.Equal(0, denied.CurrentAccountedBytes);
        var funded = new MemoryGovernor(new LythonMemoryBudget(1024));
        dictionary.AttachMemoryGovernor(funded);
        Assert.Same(funded, dictionary.OwnerMemoryGovernor);
        Assert.True(dictionary.CommittedStorageBytes > 0);
        Assert.Equal(dictionary.CommittedStorageBytes, funded.CurrentCommittedBytes);
    }
}
