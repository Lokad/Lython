using System.Numerics;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class ListConstructionAbortAccountingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(20)]
    public void FailedPrivateSourcePreservesOtherLiveStorage(int knownCount)
    {
        var governor = new MemoryGovernor(4096);
        var live = new PyList([new BigInteger(7)], governor);
        var held = governor.CurrentCommittedBytes;
        var failure = new InvalidOperationException("source failed");
        IEnumerable<object> source = knownCount == 0 ? FailingSource(failure) : new KnownSource(knownCount, failure);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => new PyList(source, governor)));
        Assert.Equal(held, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(new BigInteger(7), live[0]);
        GC.KeepAlive(live);
    }

    [Fact]
    public void DeniedPrivateIncrementalGrowthPreservesOtherLiveStorage()
    {
        var governor = new MemoryGovernor(4096);
        var live = new PyList([new BigInteger(7)], governor);
        var held = governor.CurrentCommittedBytes;
        var values = Enumerable.Range(0, 10000).Select(static value => (object)new BigInteger(value));
        var failure = Assert.Throws<LythonRuntimeException>(() => new PyList(values, governor));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(held, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(new BigInteger(7), live[0]);
        GC.KeepAlive(live);
    }

    private static IEnumerable<object> FailingSource(Exception failure)
    {
        yield return new BigInteger(1);
        throw failure;
    }

    private sealed class KnownSource(int count, Exception failure) : IReadOnlyCollection<object>
    {
        public int Count => count;
        public IEnumerator<object> GetEnumerator() => FailingSource(failure).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
