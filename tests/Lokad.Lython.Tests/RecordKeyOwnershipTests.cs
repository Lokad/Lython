using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class RecordKeyOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    [Fact]
    public void RefusedInsertionPreservesBothRowsAndCleanupNeedsNoAdmission()
    {
        const long limit = 1024 * 1024;
        var root = new LythonMemoryBudget(limit);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var (first, second, a, b) = Records(context);
            var c = PyString.FromString("c", context.MemoryGovernor, Span);
            var before = root.ReservedBytes;
            Assert.True(root.TryReserve(limit - before, out var blocker));
            using (blocker)
            {
                using var ambient = PyStructuralGuard.PushAmbient(context, Span);
                second.SetItem(a, new BigInteger(7));
                Assert.True(second.Remove(b));
                var failure = Assert.Throws<LythonRuntimeException>(() => second.SetItem(c, BigInteger.One));
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Same(failure, context.MemoryGovernor.AdmissionFailure);
                Assert.Equal(1, second.Count);
                Assert.Equal(new BigInteger(7), Assert.Single(second.Values));
                Assert.Equal(2, first.Count);
                Assert.Equal([BigInteger.One, new BigInteger(2)], first.Values.Cast<BigInteger>());
                Assert.Equal(limit, root.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                second.Clear();
                Assert.Empty(second.Items);
                Assert.Equal(2, first.Count);
            }
            Assert.Equal(before, root.ReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
        Assert.True(root.TryReserve(limit, out var reused));
        reused.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void CancellationDuringReplacementAdmissionPublishesNoPartialTable()
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var hooked = new AdmissionHook(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            {
                MemoryBudget = hooked, CancellationToken = cancellation.Token,
            });
            var (first, second, _, _) = Records(context);
            var c = PyString.FromString("c", context.MemoryGovernor, Span);
            var before = root.ReservedBytes;
            hooked.Admitted = cancellation.Cancel;
            using var ambient = PyStructuralGuard.PushAmbient(context, Span);
            var error = Assert.Throws<LythonRuntimeException>(() => second.SetItem(c, BigInteger.One));
            Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.True(cancellation.IsCancellationRequested);
            Assert.Null(context.MemoryGovernor.AdmissionFailure);
            Assert.Equal(before, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(["a", "b"], second.Keys.Cast<PyString>().Select(k => k.AsString()));
            Assert.Equal([new BigInteger(3), new BigInteger(4)], second.Values.Cast<BigInteger>());
            Assert.Equal([BigInteger.One, new BigInteger(2)], first.Values.Cast<BigInteger>());
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void ReplacementAdmissionUsesTheCurrentRowAfterNativeMutation()
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using (var owner = root.CreateScope())
        {
            var hooked = new AdmissionHook(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hooked });
            var (first, second, a, b) = Records(context);
            var c = PyString.FromString("c", context.MemoryGovernor, Span);
            hooked.Admitted = () =>
            {
                hooked.Admitted = null;
                Assert.True(second.Remove(b));
                second.SetItem(a, new BigInteger(9));
            };
            using var ambient = PyStructuralGuard.PushAmbient(context, Span);
            second.SetItem(c, new BigInteger(5));
            Assert.Equal(["a", "c"], second.Keys.Cast<PyString>().Select(k => k.AsString()));
            Assert.Equal([new BigInteger(9), new BigInteger(5)], second.Values.Cast<BigInteger>());
            Assert.Equal([BigInteger.One, new BigInteger(2)], first.Values.Cast<BigInteger>());
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void CachePublicationRevalidatesKeyKindsAfterAdmission()
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using (var owner = root.CreateScope())
        {
            var hooked = new AdmissionHook(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hooked });
            var record = new PyDict(4, context.MemoryGovernor, Span);
            record.SetItem(PyString.FromString("a", context.MemoryGovernor, Span), BigInteger.One);
            var before = root.ReservedBytes;
            hooked.Admitted = () =>
            {
                hooked.Admitted = null;
                record.SetItem(BigInteger.One, new BigInteger(2));
            };
            using var ambient = PyStructuralGuard.PushAmbient(context, Span);
            Assert.Null(record.ShareRecordKeys(context, Span));
            Assert.Equal(2, record.Count);
            Assert.Equal(new BigInteger(2), record.GetItem(BigInteger.One));
            Assert.Equal(before, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void RetiringRowsDoesNotRefundTheirRetainedCacheKeys()
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var baseline = root.ReservedBytes;
            var (first, second, _, _) = Records(context);
            var before = root.ReservedBytes;
            var firstBytes = first.CommittedStorageBytes;
            first.DiscardUnpublishedStorage();
            Assert.Equal(before - firstBytes, root.ReservedBytes);
            Assert.Equal([new BigInteger(3), new BigInteger(4)], second.Values.Cast<BigInteger>());
            second.DiscardUnpublishedStorage();
            Assert.True(root.ReservedBytes > baseline);
            var next = context.State.JsonRecordLayout!.CreateRecord(context, Span, 2);
            Assert.Empty(next.Items);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void APreviousWideLayoutDoesNotRequireWideWorkingValuesOrReplacement()
    {
        const long limit = 1024 * 1024;
        var root = new LythonMemoryBudget(limit);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var first = new PyDict(1024, context.MemoryGovernor, Span);
            PyString? firstKey = null;
            for (var i = 0; i < 1024; i++)
            {
                var key = PyString.FromString("column-" + i, context.MemoryGovernor, Span);
                firstKey ??= key;
                first.SetItem(key, BigInteger.One);
            }
            var layout = first.ShareRecordKeys(context, Span)!;
            var other = PyString.FromString("other", context.MemoryGovernor, Span);
            Assert.True(root.TryReserve(limit - root.ReservedBytes - 4096, out var blocker));
            using (blocker)
            {
                var small = layout.CreateRecord(context, Span, 2);
                small.SetRecordItem(firstKey!, new BigInteger(3), context, Span);
                using var ambient = PyStructuralGuard.PushAmbient(context, Span);
                small.SetItem(other, new BigInteger(4));
                Assert.Equal(["column-0", "other"], small.Keys.Cast<PyString>().Select(k => k.AsString()));
                Assert.Equal([new BigInteger(3), new BigInteger(4)], small.Values.Cast<BigInteger>());
                Assert.NotNull(small.ShareRecordKeys(context, Span));
                Assert.Equal(1024, first.Count);
                Assert.Null(context.MemoryGovernor.AdmissionFailure);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    [InlineData(8193)]
    public void Utf8NameLookupMatchesStoredHashesAcrossChunkBoundaries(int count)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var name = PyString.FromString(new string('é', count), context.MemoryGovernor, Span);
            var other = PyString.FromString("other", context.MemoryGovernor, Span);
            var record = new PyDict(context.MemoryGovernor, Span);
            record.SetItem(name, BigInteger.One);
            record.SetItem(other, new BigInteger(2));
            var layout = record.ShareRecordKeys(context, Span)!;
            var before = root.ReservedBytes;
            Assert.Same(name, layout.FindName(name.Utf8Bytes.Span, 1, context, Span));
            Assert.Same(other, layout.FindName(other.Utf8Bytes.Span, 0, context, Span));
            Assert.Equal(before, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    private static (PyDict First, PyDict Second, PyString A, PyString B) Records(LythonRuntime.ExecutionContext context)
    {
        var governor = context.MemoryGovernor;
        var a = PyString.FromString("a", governor, Span);
        var b = PyString.FromString("b", governor, Span);
        var first = new PyDict(governor, Span);
        first.SetItem(a, BigInteger.One);
        first.SetItem(b, new BigInteger(2));
        var keys = first.ShareRecordKeys(context, Span)!;
        context.State.JsonRecordLayout = keys;
        var second = keys.CreateRecord(context, Span, 2);
        second.SetRecordItem(a, new BigInteger(3), context, Span);
        second.SetRecordItem(b, new BigInteger(4), context, Span);
        return (first, second, a, b);
    }

    private sealed class AdmissionHook(ILythonMemoryBudget inner) : ILythonMemoryBudget
    {
        internal Action? Admitted;
        public ILythonMemoryScope CreateScope() => inner.CreateScope();
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!inner.TryReserve(bytes, out var actual)) { reservation = null; return false; }
            reservation = new Reservation(this, actual);
            Admitted?.Invoke();
            return true;
        }
        private sealed class Reservation(AdmissionHook owner, ILythonMemoryReservation inner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                var before = Bytes;
                if (!inner.TryResize(bytes)) return false;
                if (bytes > before) owner.Admitted?.Invoke();
                return true;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
