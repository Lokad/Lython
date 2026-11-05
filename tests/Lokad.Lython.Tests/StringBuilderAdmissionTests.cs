using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;

namespace Lokad.Lython.Tests;

public sealed class StringBuilderAdmissionTests(ITestOutputHelper output)
{
    private static readonly LythonSourceSpan Span = new(2, 3, 4, 5);

    [Fact]
    public void InitialCapacityCancellationReturnsAdmittedAllowanceBeforeAllocation()
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            var baseline = root.ReservedBytes;
            hook.AfterGrowth = _ => cancellation.Cancel();
            var error = Assert.Throws<OperationCanceledException>(() =>
                new GovernedByteBuilder(context.MemoryGovernor, Span, 32, cancellation.Token));
            Assert.Equal(cancellation.Token, error.CancellationToken);
            Assert.Equal(baseline, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
        Assert.True(root.TryReserve(root.LimitBytes!.Value, out var reused));
        reused.Dispose();
    }

    [Fact]
    public void InitialCapacityRefusalRemainsTerminalWhenAdmissionAlsoCancels()
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            var baseline = root.ReservedBytes;
            hook.RefuseGrowth = _ => { cancellation.Cancel(); return true; };
            var error = Assert.Throws<LythonRuntimeException>(() =>
                new GovernedByteBuilder(context.MemoryGovernor, Span, 32, cancellation.Token));
            Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
            Assert.Same(Span, error.Span);
            Assert.True(cancellation.IsCancellationRequested);
            Assert.Equal(baseline, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentAccountedBytes);
            hook.RefuseGrowth = null;
            Assert.Same(error, Assert.Throws<LythonRuntimeException>(() =>
                new GovernedByteBuilder(context.MemoryGovernor, Span, 8, cancellation.Token)));
            Assert.Equal(baseline, root.ReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RefusalPrecedesCopyOrTransferAndKeepsBuilderFunded(bool consume, bool shared)
    {
        foreach (var measured in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(1024 * 1024);
            using var owner = budget.CreateScope();
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
            var governor = context.MemoryGovernor;
            var builder = new GovernedByteBuilder(governor, Span, 65536);
            builder.Append(new byte[65536]);
            var headroom = consume ? PyString.EstimateApproximateBytes(0) - 1 : 8192;
            var padding = 1024 * 1024 - budget.ReservedBytes - headroom;
            ILythonMemoryReservation? blocker = null;
            if (shared) Assert.True(budget.TryReserve(padding, out blocker));
            else { governor.Reserve(padding, Span); governor.Commit(padding); }
            using var hostPadding = blocker;
            var committed = governor.CurrentCommittedBytes;
            var root = budget.ReservedBytes;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var error = Assert.Throws<LythonRuntimeException>(() =>
                { if (consume) builder.ToPyStringAndRelease(); else builder.ToPyString(); });
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (measured) output.WriteLine($"STRING_BUILDER_ADMISSION:{consume}:{shared}:{allocated}");
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance,
                error.Attribution?.Reason);
            Assert.Same(governor.AdmissionFailure, error);
            Assert.Same(Span, error.Span);
            Assert.Equal(committed, governor.CurrentCommittedBytes);
            Assert.Equal(root, budget.ReservedBytes);
            Assert.Equal(65536, builder.Length);
            Assert.Equal(65536, builder.CommittedCapacity);
            Assert.Equal(0, governor.CurrentReservedBytes);
            builder.Release();
            hostPadding?.Dispose();
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
            if (measured) Assert.True(allocated < 32768, $"Refused string conversion allocated {allocated} bytes.");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdmittedStringRemainsFundedAfterBuilderRelease(bool consume)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var builder = new GovernedByteBuilder(context.MemoryGovernor, Span, 65536);
        builder.AppendRepeated((byte)'x', 65536);
        var result = consume ? builder.ToPyStringAndRelease() : builder.ToPyString();
        Assert.Equal(consume ? 0 : 65536, builder.Length);
        builder.Release();
        Assert.Equal(65536, result.Length);
        Assert.Equal((byte)'x', result.Utf8Bytes.Span[0]);
        Assert.Same(context.MemoryGovernor, result.OwnerMemoryGovernor);
        Assert.True(budget.ReservedBytes >= 65536);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void PartialUtf8InputIsCopiedUnderItsOwner()
    {
        var budget = new LythonMemoryBudget(1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        byte[] bytes = [(byte)'x', (byte)'a', (byte)'b', (byte)'y'];
        var value = PyString.FromUtf8(bytes.AsMemory(1, 2), context.MemoryGovernor, Span);
        bytes[1] = (byte)'z';
        Assert.Equal("ab", value.AsString());
        Assert.Same(context.MemoryGovernor, value.OwnerMemoryGovernor);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
