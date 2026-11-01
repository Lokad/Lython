using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CasingStorageLifetimeTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void CancellationDuringFinalOutputFundingRefundsUnpublishedCasing(int operation)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var hook = new AdmissionHook(owner);
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
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    [InlineData(3, false)] [InlineData(3, true)]
    [InlineData(4, false)] [InlineData(4, true)]
    public void RefusedWorkingOrResultStoragePreservesInputAndAllowance(int operation, bool resultStorage)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using (var owner = root.CreateScope())
        {
            var hook = new AdmissionHook(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            var input = PyString.FromString("aBcD", context.MemoryGovernor, Span);
            var before = root.ReservedBytes;
            var outputBytes = PyString.EstimateApproximateBytes(4);
            hook.RefuseGrowth = bytes => !resultStorage || bytes == outputBytes;
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
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationDuringExpansionGrowthStopsNativeCasingAndReturnsScratch(bool upper)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var hook = new AdmissionHook(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var text = ExpansionInput(upper);
            var input = PyString.FromString(text, context.MemoryGovernor, Span);
            var before = root.ReservedBytes;
            hook.AfterGrowth = bytes => { if (bytes >= 2L * input.Utf8Bytes.Length) cancellation.Cancel(); };
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
    [InlineData(false, "initial")]
    [InlineData(true, "initial")]
    [InlineData(false, "growth")]
    [InlineData(true, "growth")]
    [InlineData(false, "final")]
    [InlineData(true, "final")]
    public void RefusedCapacityAndCopyOverlapKeepInputAndReturnPrivateStorage(bool upper, string stage)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var text = ExpansionInput(upper);
            var input = PyString.FromString(text, context.MemoryGovernor, Span);
            var length = input.Utf8Bytes.Length;
            var available = stage switch
            {
                "initial" => length - 1,
                "growth" => 3L * length - 1,
                _ => 2L * length + PyString.EstimateApproximateBytes(length + 1) - 1
            };
            Assert.True(owner.TryReserve(root.LimitBytes!.Value - root.ReservedBytes - available, out var padding));
            using (padding)
            {
                var before = root.ReservedBytes;
                var error = Assert.Throws<LythonRuntimeException>(() => Apply(input, upper, context));
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Equal(stage switch
                {
                    "initial" => length,
                    "growth" => 2L * length,
                    _ => PyString.EstimateApproximateBytes(length + 1)
                }, error.Attribution?.Requested);
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

    private sealed class AdmissionHook(ILythonMemoryBudget parent) : ILythonMemoryBudget
    {
        internal Action<long>? AfterGrowth;
        internal Func<long, bool>? RefuseGrowth;
        public ILythonMemoryScope CreateScope() => parent.CreateScope();
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (bytes > 0 && RefuseGrowth?.Invoke(bytes) == true) { reservation = null; return false; }
            if (!parent.TryReserve(bytes, out var admitted)) { reservation = null; return false; }
            reservation = new ProbeReservation(this, admitted);
            AfterGrowth?.Invoke(bytes);
            return true;
        }
        private sealed class ProbeReservation(AdmissionHook hook, ILythonMemoryReservation inner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                var old = inner.Bytes;
                if (bytes > old && hook.RefuseGrowth?.Invoke(bytes - old) == true) return false;
                if (!inner.TryResize(bytes)) return false;
                if (bytes > old) hook.AfterGrowth?.Invoke(bytes - old);
                return true;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
