using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class StringIOAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void DeniedGrowthLeavesCursorContentHistoryAndReservationsIntact()
    {
        var context = Context(512);
        var stream = new LythonRuntime.StringIOObject(context.Services, Span, null);
        stream.Write(PyString.FromString("a😀bc"), Span);
        stream.Seek(200, 0, Span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var i = 0; i < 100; i++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() => stream.Write(PyString.FromString("\r\nx"), Span));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(200, stream.Position);
            Assert.Equal(4, stream.Length);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.True(stream.TryGetMember("newlines", out var newlines));
        Assert.Same(PyNone.Instance, newlines);
        Assert.Equal("a😀bc", stream.GetValue(Span).AsString());
    }

    [Fact]
    public void ReadDenialDoesNotConsumeCharactersAndReleasesScratch()
    {
        var context = Context(300);
        var stream = new LythonRuntime.StringIOObject(context.Services, Span, "\n");
        stream.Write(PyString.FromString("a😀bc"), Span);
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
    public void CloseResnapshotsItsCouponWhileSnapshotsRemainOwned()
    {
        var context = Context(100000);
        var stream = new LythonRuntime.StringIOObject(context.Services, Span, "\n");
        context.Services.State.CallTemporaries.TrackFreshMutable(stream, stream.OwnedBytes, Span);
        stream.Write(PyString.FromString("é😀abc"), Span);
        var value = stream.GetValue(Span);
        Assert.Same(context.MemoryGovernor, value.OwnerMemoryGovernor);
        stream.Seek(0, 0, Span);
        stream.Write(PyString.FromString("XY"), Span);
        stream.Truncate(1, Span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var backing = stream.OwnedBytes - LythonRuntime.StringIOObject.ShellBytes;
        stream.Close();
        Assert.Equal(before - backing, context.MemoryGovernor.CurrentCommittedBytes);
        stream.Close();
        Assert.Equal(before - backing, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal("é😀abc", value.AsString());
        Assert.Equal(LythonRuntime.StringIOObject.ShellBytes, stream.OwnedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(stream);
        GC.KeepAlive(value);
    }

    [Fact]
    public void AbandonedStreamReclaimsCurrentBackingAfterGrowthAndClose()
    {
        foreach (var close in new[] { false, true })
        {
            var context = Context(100000);
            var weak = Abandon(context, close, out var charge);
            var before = context.MemoryGovernor.CurrentCommittedBytes;
            GC.Collect();
            Assert.False(weak.TryGetTarget(out _));
            context.Services.State.CallTemporaries.Sweep(full: true);
            Assert.Equal(before - charge - ChargeReclamationPool.EntryChargeBytes, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<LythonRuntime.StringIOObject> Abandon(LythonRuntime.ExecutionContext context, bool close, out long charge)
    {
        var stream = new LythonRuntime.StringIOObject(context.Services, Span, "\n");
        context.Services.State.CallTemporaries.TrackFreshMutable(stream, stream.OwnedBytes, Span);
        stream.Write(PyString.FromString(new string('x', 200)), Span);
        stream.Write(PyString.FromString(new string('y', 1000)), Span);
        if (close) stream.Close();
        charge = stream.OwnedBytes;
        return new WeakReference<LythonRuntime.StringIOObject>(stream);
    }

    private static LythonRuntime.ExecutionContext Context(long cap)
        => new(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = cap });
}
