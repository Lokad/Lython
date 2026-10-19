using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class DictEntryStorageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BooleanIntegerTwinsShareOneFundedEntry(bool storeBoolean)
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(320));
        var dictionary = new PyDict(governor);
        object key = storeBoolean ? true : new BigInteger(1);
        object lookup = storeBoolean ? new BigInteger(1) : true;
        object original = new BigInteger(2);
        dictionary.SetItem(key, original);
        Assert.Same(original, dictionary.GetItem(lookup));
        dictionary.SetItem(lookup, PyNone.Instance);
        Assert.Single(dictionary);
        Assert.Same(key, dictionary.Keys.Single());
        Assert.Equal(320, governor.CurrentCommittedBytes);
        Assert.True(dictionary.Remove(lookup));
        Assert.Equal(192, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void SaturatedReplacementTransfersItsFundedEntry()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(320));
        var dictionary = new PyDict(governor);
        var key = PyString.FromString("key");
        dictionary.SetItem(key, new BigInteger(1));
        object replacement = new BigInteger(2);
        dictionary.SetItem(key, replacement);
        Assert.Same(replacement, dictionary.GetItem(key));
        dictionary.SetItem(key, PyNone.Instance);
        dictionary.SetItem(key, replacement);
        Assert.Equal(320, governor.CurrentCommittedBytes);
        Assert.Equal(320, governor.PeakAccountedBytes);
        var error = Assert.Throws<LythonRuntimeException>(() => dictionary.SetItem(new BigInteger(3), replacement));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Single(dictionary);
        Assert.Same(replacement, dictionary.GetItem(key));
        Assert.Equal(320, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void CopyRefusalPreservesUnrelatedPinnedStorage()
    {
        var source = new PyDict();
        source.SetItem(new BigInteger(1), new BigInteger(1));
        source.SetItem(new BigInteger(2), new BigInteger(2));
        var governor = new MemoryGovernor(new LythonMemoryBudget(576));
        governor.Reserve(257, null);
        governor.Commit(257);
        var error = Assert.Throws<LythonRuntimeException>(() => new PyDict(source, governor));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(257, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(2, source.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WrapperAttachmentRefusalRefundsItsShellAndCanRetry(bool counter)
    {
        var denied = new MemoryGovernor(new LythonMemoryBudget(128));
        var funded = new MemoryGovernor(new LythonMemoryBudget(null));
        if (counter)
        {
            var wrapper = new PyCounter();
            wrapper.SetItem(new BigInteger(1), PyNone.Instance);
            Assert.Throws<LythonRuntimeException>(() => wrapper.AttachMemoryGovernor(denied));
            Assert.Null(wrapper.OwnerMemoryGovernor);
            Assert.Equal(0, denied.CurrentAccountedBytes);
            wrapper.AttachMemoryGovernor(funded);
            Assert.Equal(wrapper.CommittedStorageBytes, funded.CurrentCommittedBytes);
        }
        else
        {
            var wrapper = new PyDefaultDict(PyNone.Instance);
            wrapper.SetItem(new BigInteger(1), PyNone.Instance);
            Assert.Throws<LythonRuntimeException>(() => wrapper.AttachMemoryGovernor(denied));
            Assert.Null(wrapper.OwnerMemoryGovernor);
            Assert.Equal(0, denied.CurrentAccountedBytes);
            wrapper.AttachMemoryGovernor(funded);
            Assert.Equal(wrapper.CommittedStorageBytes, funded.CurrentCommittedBytes);
        }
        Assert.Equal(0, funded.CurrentReservedBytes);
    }
}
