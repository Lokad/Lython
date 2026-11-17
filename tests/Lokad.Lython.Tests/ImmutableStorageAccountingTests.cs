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
        var governor = new MemoryGovernor(new LythonMemoryBudget(4096));
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
        var governor = new MemoryGovernor(new LythonMemoryBudget(512));
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
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        object first = new BigInteger(1);
        object second = new BigInteger(2);
        IEnumerable<object> Values()
        {
            yield return first;
            // Capacity can already be committed before the iterator resumes.
            // The yielded value must stay funded through either balance.
            var firstPayload = PyTuple.EstimateApproximateBytes(1)
                + RuntimeMemoryEstimates.ScalarStorageBytes;
            Assert.True(governor.CurrentAccountedBytes >= firstPayload);
            Assert.Equal(governor.CurrentAccountedBytes, budget.ReservedBytes);
            yield return second;
        }
        var tuple = new PyTuple(Values(), governor);
        var payload = PyTuple.EstimateApproximateBytes(2)
            + 2 * RuntimeMemoryEstimates.ScalarStorageBytes;
        Assert.InRange(tuple.CommittedStorageBytes, payload,
            payload + 32 + 2 * RuntimeMemoryEstimates.ScalarStorageBytes);
        Assert.Equal(tuple.CommittedStorageBytes, governor.CurrentCommittedBytes);
        Assert.Equal(governor.CurrentAccountedBytes, budget.ReservedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Same(first, tuple[0]);
        Assert.Same(second, tuple[1]);
        GC.KeepAlive(tuple);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void CollectedTupleAndEscapedMagnitudeRemainFundedThroughBatchEnd()
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
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
