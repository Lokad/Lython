using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class StringIOAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BorrowedInputRefusesCopyAndMaterializationBeforeChangingItsCursor(bool write)
    {
        var root = new LythonMemoryBudget(65536);
        using (var sourceOwner = root.CreateScope())
        using (var streamOwner = root.CreateScope())
        {
            var sourceGovernor = new MemoryGovernor(sourceOwner);
            var text = PyString.FromString(string.Concat(Enumerable.Repeat("😀", 2048)), sourceGovernor, Span);
            var context = Context(0, streamOwner);
            var stream = LythonRuntime.StringIOObject.FromBorrowedText(text, context.Services, Span);
            Assert.True(root.TryReserve(root.LimitBytes!.Value - root.ReservedBytes - 512, out var hold));
            using (hold)
            {
                var before = root.ReservedBytes;
                var error = Assert.Throws<LythonRuntimeException>(() =>
                {
                    if (write) stream.Write(PyString.FromString("x"), Span);
                    else stream.Read(1024, false, Span);
                });
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Equal(0, stream.Position);
                Assert.Equal(2048, stream.Length);
                // Inspect retained state without resuming guest work after refusal.
                Assert.Same(text, typeof(LythonRuntime.StringIOObject).GetField("_borrowedText",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(stream));
                Assert.Equal(before, root.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            stream.Close();
            streamOwner.Dispose();
            Assert.True(sourceGovernor.CurrentCommittedBytes > 0);
            Assert.Equal(sourceGovernor.CurrentCommittedBytes, root.ReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

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
        // Inspect existing storage: a new guest snapshot is forbidden after refusal.
        var buffer = (int[])typeof(LythonRuntime.StringIOObject).GetField("_buffer",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(stream)!;
        Assert.Equal(new[] { 97, 0x1F600, 98, 99 }, buffer[..stream.Length]);
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
    public void CloseReleasesOnlyItsBackingWhileSnapshotsRemainOwned()
    {
        var context = Context(100000);
        var stream = new LythonRuntime.StringIOObject(context.Services, Span, "\n");
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
    public void CollectedStreamStaysBatchOwnedUntilCleanup()
    {
        foreach (var close in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(100000);
            using var owner = budget.CreateScope();
            var context = Context(100000, owner);
            var weak = Abandon(context, close, out var charge);
            var before = context.MemoryGovernor.CurrentCommittedBytes;
            GC.Collect();
            Assert.False(weak.TryGetTarget(out _));
            Assert.Equal(before, budget.ReservedBytes);
            Assert.True(before >= charge);
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<LythonRuntime.StringIOObject> Abandon(LythonRuntime.ExecutionContext context, bool close, out long charge)
    {
        var stream = new LythonRuntime.StringIOObject(context.Services, Span, "\n");
        stream.Write(PyString.FromString(new string('x', 200)), Span);
        stream.Write(PyString.FromString(new string('y', 1000)), Span);
        if (close) stream.Close();
        charge = stream.OwnedBytes;
        return new WeakReference<LythonRuntime.StringIOObject>(stream);
    }

    private static LythonRuntime.ExecutionContext Context(long cap, ILythonMemoryBudget? budget = null)
        => new(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget ?? new LythonMemoryBudget(cap) },regexProvider:Utf8RegexProvider.Instance);
}
