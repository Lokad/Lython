using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ShortTextStorageTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    [Theory]
    [InlineData("a")]
    [InlineData("é")]
    [InlineData("😀")]
    [InlineData("α😀β")]
    public void ConcurrentMemoryBorrowsAreStableAndRemainFunded(string content)
    {
        var root = new LythonMemoryBudget(4096);
        using (var owner = root.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var text = PyString.CopyFromUtf8(Encoding.UTF8.GetBytes(content), governor, Span);
            var admitted = root.ReservedBytes;
            var borrows = new ReadOnlyMemory<byte>[16];
            Parallel.For(0, borrows.Length, index => borrows[index] = text.Utf8Bytes);
            foreach (var bytes in borrows)
            {
                Assert.Equal(borrows[0], bytes);
                Assert.Equal(content, Encoding.UTF8.GetString(bytes.Span));
            }
            Assert.Equal(content, Encoding.UTF8.GetString(text.Utf8Span));
            Assert.Equal(admitted, root.ReservedBytes);
            Assert.Equal(admitted, text.CommittedOwnedBytes);
            Assert.True(((IPyOwnershipSnapshot)text).TrySnapshotOwnership(out var retained));
            Assert.Equal(admitted, retained);
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedCopyAdmissionPreservesInputAndWorkingAllowance(bool refuse)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var input = Encoding.UTF8.GetBytes("α😀β");
            var before = root.ReservedBytes;
            using var ambient = PyStructuralGuard.PushAmbient(context, Span);
            if (refuse) hook.RefuseGrowth = _ => true;
            else hook.AfterGrowth = _ => cancellation.Cancel();
            var error = Assert.Throws<LythonRuntimeException>(() => PyString.CopyFromUtf8(input, context.MemoryGovernor, Span));
            Assert.Equal(refuse ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled,
                error.Attribution?.Reason);
            if (refuse) Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
            Assert.Equal(Span, error.Span);
            Assert.Equal(before, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal("α😀β", Encoding.UTF8.GetString(input));
        }
        Assert.Equal(0, root.ReservedBytes);
        Assert.True(root.TryReserve(root.LimitBytes!.Value, out var reused));
        reused.Dispose();
    }

    [Theory]
    [InlineData("µ", "Μ")]
    [InlineData("ß", "SS")]
    public void CasingHeadroomRemainsFundedUntilTheResultIsRelinquished(string content, string expected)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var input = PyString.FromString(content, context.MemoryGovernor, Span);
            var before = root.ReservedBytes;
            var result = PyStringOps.Upper(input, context, Span);
            Assert.Equal(expected, result.AsString());
            Assert.Equal(before + result.CommittedOwnedBytes, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            _ = result.Utf8Bytes;
            Assert.Equal(before + result.CommittedOwnedBytes, root.ReservedBytes);
            context.MemoryGovernor.RefundUnpublishedValue(result);
            Assert.Equal(before, root.ReservedBytes);
            Assert.Equal(content, input.AsString());
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void PrefundedMemoryBorrowRemainsAvailableToCleanupAfterRefusal()
    {
        var root = new LythonMemoryBudget(4096);
        using (var owner = root.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var governor = new MemoryGovernor(hook);
            var text = PyString.CopyFromUtf8("value"u8, governor, Span);
            var before = root.ReservedBytes;
            hook.RefuseGrowth = _ => true;
            var failure = Assert.Throws<LythonRuntimeException>(() => governor.Reserve(1, Span));
            var memory = text.Utf8Bytes;
            Assert.Equal("value", Encoding.UTF8.GetString(memory.Span));
            Assert.Same(failure, governor.AdmissionFailure);
            Assert.Equal("value", Encoding.UTF8.GetString(text.Utf8Span));
            Assert.Equal(before, root.ReservedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }
}
