using System.Runtime.InteropServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class SubtextOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(2, 3, 4, 5);

    [Fact]
    public async Task NestedSplitAndTrimKeepBackingFundedAfterSourceShellsAreRelinquished()
    {
        var budget = new LythonMemoryBudget();
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var governor = context.MemoryGovernor;
            PyString retained;
            ArraySegment<byte> backing;
            // Ambient scopes are thread-local and may only cover synchronous
            // helpers. The retained owner, rather than that scope, spans await.
            using (PyStructuralGuard.PushAmbient(context, Span))
            {
                PyString source;
                using (var builder = new GovernedByteBuilder(governor, Span, 1024))
                {
                    builder.AppendString("prefix-αabcdefghij😀  \n βklmnopqrst😀 -suffix");
                    source = builder.ToPyStringAndRelease();
                }
                Assert.True(MemoryMarshal.TryGetArray(source.Utf8Bytes, out backing));
                var sliced = source.SliceByByteRange(7, source.ByteLength - 7);
                var lines = PyStringOps.SplitLines(sliced, false, governor, Span, context);
                var first = Assert.IsType<PyString>(lines[0]);
                var second = Assert.IsType<PyString>(lines[1]);
                retained = PyStringOps.Strip(second);
                Assert.True(MemoryMarshal.TryGetArray(retained.Utf8Bytes, out var window));
                Assert.Same(backing.Array, window.Array);
                governor.RefundUnpublishedValue(source);
                governor.RefundUnpublishedValue(sliced);
                governor.RefundUnpublishedValue(first);
                governor.RefundUnpublishedValue(second);
            }
            await Task.Yield();
            Assert.Equal("βklmnopqrst😀", retained.AsString());
            Assert.Equal(12, retained.Length);
            Assert.Equal(PyString.FromString("βklmnopqrst😀").GetHashCode(), retained.GetHashCode());
            Assert.True(budget.ReservedBytes >= backing.Array!.Length + 32 + retained.CommittedOwnedBytes);
            governor.RefundUnpublishedValue(retained);
            Assert.True(budget.ReservedBytes >= backing.Array.Length + 32);
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ForeignOwnerSplitCopiesBeforeSourceOwnershipEnds()
    {
        var budget = new LythonMemoryBudget();
        using (var targetOwner = budget.CreateScope())
        {
            var target = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = targetOwner });
            PyString retained;
            using (var sourceOwner = budget.CreateScope())
            {
                var governor = new MemoryGovernor(sourceOwner);
                var source = PyString.FromString("αabcdefghij😀\nβklmnopqrst😀", governor, Span);
                Assert.True(MemoryMarshal.TryGetArray(source.Utf8Bytes, out var backing));
                var parts = PyStringOps.SplitLines(source, false, target.MemoryGovernor, Span, target);
                retained = Assert.IsType<PyString>(parts[1]);
                Assert.True(MemoryMarshal.TryGetArray(retained.Utf8Bytes, out var copied));
                Assert.NotSame(backing.Array, copied.Array);
                Assert.Same(target.MemoryGovernor, retained.OwnerMemoryGovernor);
            }
            Assert.Equal("βklmnopqrst😀", retained.AsString());
            Assert.True(budget.ReservedBytes >= retained.CommittedOwnedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RefusedSharingKeepsOriginalBackingAndFirstFailure()
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var source = PyString.FromString("_αabcdefghij😀_", governor, Span);
            var originalCharge = source.CommittedOwnedBytes;
            var baseline = budget.ReservedBytes;
            using (governor.ReserveTemporary(limit - baseline, Span))
            {
                var error = Assert.Throws<LythonRuntimeException>(() => source.SliceByByteRange(1, source.ByteLength - 1));
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Same(error, governor.AdmissionFailure);
                Assert.Equal(originalCharge, source.CommittedOwnedBytes);
                Assert.Equal("_αabcdefghij😀_", source.AsString());
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
