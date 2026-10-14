using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class ImmutableStorageAccountingTests
{
    [Fact]
    public void FundingTransferCannotCommitOrDisposeAnotherReservationsBytes()
    {
        var governor = new MemoryGovernor(4096);
        using var other = governor.ReserveTemporary(17, null);
        var funding = governor.ReserveTemporary(20, null);
        Assert.Throws<InvalidOperationException>(() => funding.Commit(21));
        Assert.Throws<InvalidOperationException>(() => funding.Commit(-1));
        Assert.Equal(37, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        funding.Commit(11);
        Assert.Equal(26, governor.CurrentReservedBytes);
        Assert.Equal(11, governor.CurrentCommittedBytes);
        funding.Dispose();
        funding.Dispose();
        Assert.Equal(17, governor.CurrentReservedBytes);
        Assert.Equal(11, governor.CurrentCommittedBytes);
        Assert.Throws<ObjectDisposedException>(() => funding.Commit(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void UnregisteredLargeMagnitudeCannotFitSmallScalarAllowance(int construction)
    {
        object large = BigInteger.One << 8192;
        var governor = new MemoryGovernor(512);
        using var other = governor.ReserveTemporary(17, null);
        object[] items = [large, large];
        var failure = Assert.Throws<LythonRuntimeException>(() => construction switch
        {
            0 => new PyTuple(items, governor),
            1 => new PyTuple(items.Select(static value => value), governor),
            _ => PyTuple.FromOwnedArray(items, governor, null),
        });
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(17, governor.CurrentReservedBytes);
        Assert.True(governor.PeakAccountedBytes <= 512);
        GC.KeepAlive(large);
    }

    [Fact]
    public void MaterializingNumericSlotsFundsThemBeforeAdvancingSource()
    {
        var governor = new MemoryGovernor(4096);
        IEnumerable<object> Values()
        {
            yield return new BigInteger(1);
            Assert.True(governor.CurrentReservedBytes >= 160);
            Assert.Equal(0, governor.CurrentCommittedBytes);
            yield return new BigInteger(2);
        }
        var tuple = new PyTuple(Values(), governor);
        Assert.Equal(192, tuple.CommittedStorageBytes);
        Assert.Equal(192, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(tuple);
    }

    [Fact]
    public void CollectedTupleAndEscapedMagnitudeRemainFundedThroughBatchEnd()
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(4096, owner);
        var (dropped, escaped, originalCharge) = MakeEscapedMagnitude(governor);
        var funded = budget.ReservedBytes;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(dropped.TryGetTarget(out _));
        Assert.Equal(funded, budget.ReservedBytes);
        Assert.True(funded > originalCharge);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(escaped);
        escaped = null!;
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<PyTuple>, object, long) MakeEscapedMagnitude(MemoryGovernor governor)
    {
        object value = BigInteger.One << 100;
        LythonRuntime.OwnFreshInteger(value, governor, null);
        var before = governor.CurrentCommittedBytes;
        var tuple = new PyTuple(new[] { value, value }, governor);
        return (new WeakReference<PyTuple>(tuple), tuple[0], before);
    }
}
