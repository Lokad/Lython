using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CasingStorageLifetimeTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

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
        public ILythonMemoryScope CreateScope() => parent.CreateScope();
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
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
                if (!inner.TryResize(bytes)) return false;
                if (bytes > old) hook.AfterGrowth?.Invoke(bytes - old);
                return true;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
