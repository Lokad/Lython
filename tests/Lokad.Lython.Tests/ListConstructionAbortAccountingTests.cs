using System.Numerics;
using Lokad.Lython.Runtime;
using Xunit.Abstractions;

namespace Lokad.Lython.Tests;

public sealed class ListConstructionAbortAccountingTests(ITestOutputHelper output)
{
    [Fact]
    public void GrowthFundsReplacementWhileOldStorageRemainsOwned()
    {
        var budget = new LythonMemoryBudget(PyListStorage.EstimateCapacityBytes(32));
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        var alias = new object();
        var storage = new ArrayPyListStorage(16, governor, null);
        storage.Add(alias);
        var held = budget.ReservedBytes;
        var committed = governor.CurrentCommittedBytes;
        var failure = Assert.Throws<LythonRuntimeException>(() => storage.EnsureCapacity(17, governor, null));
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance,
            failure.Attribution?.Reason);
        Assert.Same(governor.AdmissionFailure, failure);
        Assert.Equal(held, budget.ReservedBytes);
        Assert.Equal(committed, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Same(alias, Assert.Single(storage));
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefusalPrecedesBackingAllocationAndPreservesLiveAliases(bool grow)
    {
        foreach (var measured in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(4096);
            using var owner = budget.CreateScope();
            var governor = new MemoryGovernor(owner);
            var alias = new object();
            var live = new PyList([alias], governor);
            var storage = new ArrayPyListStorage(16, governor, null);
            storage.Add(alias);
            var committed = governor.CurrentCommittedBytes;
            var held = budget.ReservedBytes;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var error = Assert.Throws<LythonRuntimeException>(() =>
            {
                if (grow) storage.EnsureCapacity(8192, governor, null);
                else _ = new ArrayPyListStorage(8192, governor, null);
            });
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (measured) output.WriteLine($"LIST_STORAGE_ADMISSION:{grow}:{allocated}");
            Assert.Same(governor.AdmissionFailure, error);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance,
                error.Attribution?.Reason);
            Assert.Equal(committed, governor.CurrentCommittedBytes);
            Assert.Equal(held, budget.ReservedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.Same(alias, Assert.Single(live));
            Assert.Same(alias, Assert.Single(storage));
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
            if (measured) Assert.True(allocated < 16384, $"Refused list backing allocated {allocated} bytes.");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(20)]
    public void FailedPrivateSourcePreservesOtherLiveStorage(int knownCount)
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(4096));
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
        var governor = new MemoryGovernor(new LythonMemoryBudget(4096));
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
        var governor = new MemoryGovernor(new LythonMemoryBudget());
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
        var governor = new MemoryGovernor(new LythonMemoryBudget(PyListStorage.EstimateCapacityBytes(8) + PyListStorage.EstimateCapacityBytes(9) - 1));
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
        var governor = new MemoryGovernor(new LythonMemoryBudget());
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
        var governor = new MemoryGovernor(new LythonMemoryBudget(4096));
        var list = new PyList([new BigInteger(7)], governor);
        var failure = new LythonRuntimeException("ValueError", "source failed", null);
        Assert.Same(failure, Assert.Throws<LythonRuntimeException>(() => list.AddRange(FailingSource(failure))));
        Assert.Equal(2, list.Count);
        Assert.Equal(new BigInteger(7), list[0]);
        Assert.Equal(new BigInteger(1), list[1]);
        Assert.Equal(list.CommittedStorageBytes, governor.CurrentCommittedBytes);
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
