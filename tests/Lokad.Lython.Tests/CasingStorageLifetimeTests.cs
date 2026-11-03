using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CasingStorageLifetimeTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void CancellationDuringOutputAdmissionPreservesInputAndAllowance(int operation)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var input = PyString.FromString("aBcD", context.MemoryGovernor, Span);
            var before = root.ReservedBytes;
            var outputBytes = PyString.EstimateApproximateBytes(4);
            hook.AfterGrowth = bytes => { if (bytes == outputBytes) cancellation.Cancel(); };
            var error = Assert.Throws<LythonRuntimeException>(() => ApplyOrdinary(input, operation, context));
            Assert.True(cancellation.IsCancellationRequested);
            Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.Equal(Span, error.Span);
            Assert.Equal(before, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal("aBcD", input.AsString());
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void RefusedOutputStoragePreservesInputAndAllowance(int operation)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using (var owner = root.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            var input = PyString.FromString("aBcD", context.MemoryGovernor, Span);
            var before = root.ReservedBytes;
            hook.RefuseGrowth = _ => true;
            var error = Assert.Throws<LythonRuntimeException>(() => ApplyOrdinary(input, operation, context));
            Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
            Assert.Equal(Span, error.Span);
            Assert.True(error.Attribution?.Requested > 0);
            Assert.Equal(before, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal("aBcD", input.AsString());
        }
        Assert.Equal(0, root.ReservedBytes);
        Assert.True(root.TryReserve(root.LimitBytes!.Value, out var reused));
        reused.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }

    private static PyString ApplyOrdinary(PyString value, int operation, LythonRuntime.ExecutionContext context)
        => operation switch
        {
            0 => PyStringOps.Lower(value, context, Span),
            1 => PyStringOps.Upper(value, context, Span),
            2 => PyStringOps.Capitalize(value, context, Span),
            3 => PyStringOps.Title(value, context, Span),
            _ => PyStringOps.SwapCase(value, context, Span),
        };

    [Theory]
    [InlineData(0, "abcd")] [InlineData(1, "ABCD")] [InlineData(2, "Abcd")]
    [InlineData(3, "Abcd")] [InlineData(4, "AbCd")]
    public void ResultAndInputRemainIndependentlyFunded(int operation, string expected)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var input = PyString.FromString("aBcD", context.MemoryGovernor, Span);
            var baseline = root.ReservedBytes;
            var result = ApplyOrdinary(input, operation, context);
            Assert.NotSame(input, result);
            Assert.Equal(baseline + result.CommittedOwnedBytes, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(result.Utf8Bytes.Span));
            context.MemoryGovernor.RefundUnpublishedValue(result);
            Assert.Equal(baseline, root.ReservedBytes);
            Assert.Equal("aBcD", input.AsString());
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void AdmissionCancellationPrecedesLargeOutputAllocation()
    {
        var root = new LythonMemoryBudget(2 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var input = PyString.FromString(new string('A', 256 * 1024), context.MemoryGovernor, Span);
            var baseline = root.ReservedBytes;
            hook.AfterGrowth = _ => cancellation.Cancel();
            var before = GC.GetAllocatedBytesForCurrentThread();
            var error = Assert.Throws<LythonRuntimeException>(() => PyStringOps.Lower(input, context, Span));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.True(allocated < 64 * 1024, $"Cancelled casing allocated {allocated} bytes.");
            Assert.Equal(baseline, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationDuringExpandingOutputAdmissionReturnsPrivateStorage(bool upper)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var text = ExpansionInput(upper);
            var input = PyString.FromString(text, context.MemoryGovernor, Span);
            var before = root.ReservedBytes;
            hook.AfterGrowth = _ => cancellation.Cancel();
            var error = Assert.Throws<LythonRuntimeException>(() => Apply(input, upper, context));
            Assert.True(cancellation.IsCancellationRequested);
            Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.Equal(before, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(text, input.AsString());
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExpandingOutputAdmissionIncludesMetadataAndPreservesInput(bool upper)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var text = ExpansionInput(upper);
            var input = PyString.FromString(text, context.MemoryGovernor, Span);
            var charge = PyString.EstimateApproximateBytes(input.Utf8Bytes.Length + (upper ? 4 : 2));
            var available = charge - 1;
            Assert.True(owner.TryReserve(root.LimitBytes!.Value - root.ReservedBytes - available, out var padding));
            using (padding)
            {
                var before = root.ReservedBytes;
                var error = Assert.Throws<LythonRuntimeException>(() => Apply(input, upper, context));
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Equal(charge, error.Attribution?.Requested);
                Assert.Equal(before, root.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(text, input.AsString());
                Assert.Same(error, Assert.Throws<LythonRuntimeException>(() => context.CheckExecution(Span)));
            }
        }
        Assert.Equal(0, root.ReservedBytes);
        Assert.True(root.TryReserve(root.LimitBytes!.Value, out var reused));
        reused.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }

    private static string ExpansionInput(bool upper)
        => new string('A', 4095) + (upper ? "ɐ" : "İ") + new string('b', 4096);

    private static PyString Apply(PyString input, bool upper, LythonRuntime.ExecutionContext context)
        => upper ? PyStringOps.Upper(input, context, Span) : PyStringOps.Lower(input, context, Span);

}
