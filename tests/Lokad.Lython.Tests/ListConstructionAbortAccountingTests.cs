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

    [Theory]
    [InlineData(9)]
    [InlineData(30)]
    public void FailedPrivatePopulationAfterPromotionRefundsCurrentStorage(int count)
    {
        var governor = new MemoryGovernor(4096);
        var live = new PyList([new BigInteger(7)], governor);
        var held = governor.CurrentCommittedBytes;
        var failure = new InvalidOperationException("late source failure");
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => new PyList(LateFailure(count, failure), governor)));
        Assert.Equal(held, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(new BigInteger(7), live[0]);
        GC.KeepAlive(live);
    }

    [Fact]
    public void UnderestimatedCountStillPreflightsGrowth()
    {
        var governor = new MemoryGovernor(700);
        var source = new GrowingSource();
        var failure = Assert.Throws<LythonRuntimeException>(() => new PyList(source, governor));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.InRange(source.Pulls, 9, 99);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(20)]
    public void CompletedPrivateConstructionChargesStorageAndRetainsIdentity(int count)
    {
        var governor = new MemoryGovernor(4096);
        var value = new object();
        var values = Enumerable.Repeat(value, count).ToArray();
        var list = new PyList(values, governor);
        Assert.Equal(count, list.Count);
        Assert.Equal(list.CommittedStorageBytes, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        foreach (var item in list) Assert.Same(value, item);
        list.Add(value);
        Assert.Same(value, list[count]);
        Assert.Equal(list.CommittedStorageBytes, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void FailedLiveExtensionKeepsItsSuccessfulPrefixCharged()
    {
        var governor = new MemoryGovernor(4096);
        var list = new PyList([new BigInteger(7)], governor);
        var failure = new LythonRuntimeException("ValueError", "source failed", null);
        Assert.Same(failure, Assert.Throws<LythonRuntimeException>(() => list.AddRange(FailingSource(failure))));
        Assert.Equal(2, list.Count);
        Assert.Equal(new BigInteger(7), list[0]);
        Assert.Equal(new BigInteger(1), list[1]);
        Assert.Equal(list.CommittedStorageBytes, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    [Fact]
    public void FailedScalarAdoptionAfterPopulationStillRefundsStorage()
    {
        var governor = new MemoryGovernor(192);
        var failure = Assert.Throws<LythonRuntimeException>(() => new PyList([new BigInteger(1)], governor));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }

    private static IEnumerable<object> LateFailure(int count, Exception failure)
    {
        for (var i = 0; i < count; i++) yield return new BigInteger(i);
        throw failure;
    }

    private sealed class GrowingSource : IReadOnlyCollection<object>
    {
        public int Count => 2;
        public int Pulls;
        public IEnumerator<object> GetEnumerator()
        {
            for (var i = 0; i < 100; i++)
            {
                Pulls++;
                yield return new BigInteger(i);
            }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
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
