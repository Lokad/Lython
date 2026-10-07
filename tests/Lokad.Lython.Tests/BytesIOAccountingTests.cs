using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class BytesIOAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void DeniedGapGrowthPreservesContentAndCursorWithoutReservations()
    {
        var context = Context(512);
        var stream = new LythonRuntime.BytesIOObject(context.Services, Span);
        stream.Write(new PyBytes([255, 0, 1, 2]), Span);
        stream.Seek(1000, 0, Span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var i = 0; i < 100; i++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() => stream.Write(new PyBytes([3]), Span));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(1000, stream.Position);
            Assert.Equal(4, stream.Length);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(new byte[] { 255, 0, 1, 2 }, stream.GetValue(Span).Bytes.ToArray());
    }

    [Theory]
    [InlineData(150)] // Denies the snapshot before allocating its array.
    [InlineData(250)] // Allows the array/owner, then denies registry adoption.
    public void DeniedSnapshotsDoNotConsumeInputOrLeakFreshOwnership(long cap)
    {
        var context = Context(cap);
        var stream = new LythonRuntime.BytesIOObject(context.Services, Span);
        stream.Write(new PyBytes([255, 0, 1, 2]), Span);
        stream.Seek(0, 0, Span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var i = 0; i < 20; i++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() => stream.Read(-1, false, Span));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(0, stream.Position);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Fact]
    public void CloseReleasesBackingAndKeepsIndependentSnapshotsOwned()
    {
        var context = Context(100000);
        var stream = new LythonRuntime.BytesIOObject(context.Services, Span);
        context.Services.State.CallTemporaries.TrackFreshMutable(stream, stream.OwnedBytes, Span);
        stream.Write(new PyBytes([255, 0, 1, 2]), Span);
        var value = stream.GetValue(Span);
        Assert.Same(context.MemoryGovernor, value.OwnerMemoryGovernor);
        stream.Seek(0, 0, Span);
        stream.Write(new PyBytes([3, 4]), Span);
        stream.Truncate(1, Span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var backing = stream.OwnedBytes - LythonRuntime.BytesIOObject.ShellBytes;
        stream.Close();
        Assert.Equal(before - backing, context.MemoryGovernor.CurrentCommittedBytes);
        stream.Close();
        Assert.Equal(before - backing, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(new byte[] { 255, 0, 1, 2 }, value.Bytes.ToArray());
        Assert.Equal(LythonRuntime.BytesIOObject.ShellBytes, stream.OwnedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(stream);
        GC.KeepAlive(value);
    }

    [Fact]
    public void InPlaceOverwriteReusesItsBackingCharge()
    {
        var context = Context(512);
        var stream = new LythonRuntime.BytesIOObject(context.Services, Span);
        stream.Write(new PyBytes([1, 2, 3, 4]), Span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var i = 0; i < 100; i++)
        {
            stream.Seek(0, 0, Span);
            stream.Write(new PyBytes([255, 0]), Span);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(new byte[] { 255, 0, 3, 4 }, stream.GetValue(Span).Bytes.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbandonedStreamAndSnapshotReleaseTheirOwnCurrentCoupons(bool close)
    {
        var context = Context(100000);
        var (stream, value) = Abandon(context, close, out var charge);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        GC.Collect();
        Assert.False(stream.TryGetTarget(out _));
        Assert.False(value.TryGetTarget(out _));
        context.Services.State.CallTemporaries.Sweep(full: true);
        Assert.Equal(before - charge - 2 * ChargeReclamationPool.EntryChargeBytes, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<LythonRuntime.BytesIOObject>, WeakReference<PyBytes>) Abandon(
        LythonRuntime.ExecutionContext context, bool close, out long charge)
    {
        var stream = new LythonRuntime.BytesIOObject(context.Services, Span);
        context.Services.State.CallTemporaries.TrackFreshMutable(stream, stream.OwnedBytes, Span);
        stream.Write(new PyBytes(new byte[200]), Span);
        stream.Write(new PyBytes(new byte[1000]), Span);
        var value = stream.GetValue(Span);
        if (close) stream.Close();
        charge = stream.OwnedBytes + value.CommittedStorageBytes;
        return (new WeakReference<LythonRuntime.BytesIOObject>(stream), new WeakReference<PyBytes>(value));
    }

    private static LythonRuntime.ExecutionContext Context(long cap)
        => new(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = cap });
}
