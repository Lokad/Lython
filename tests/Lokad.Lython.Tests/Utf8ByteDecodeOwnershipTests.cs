using System.Runtime.InteropServices;
using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class Utf8ByteDecodeOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);
    private const string Value = "abcdefghijé😀\r\nkept";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnedBytesFundOnePayloadAcrossPrivateInputCleanup(bool bom)
    {
        var budget = new LythonMemoryBudget();
        using (var scope = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = scope });
            var payload = Encoding.UTF8.GetBytes((bom ? "\uFEFF" : "") + Value);
            var source = new PyBytes(payload, context.MemoryGovernor, Span);
            using var original = GovernedHostBytes.FromValue(source);
            var before = budget.ReservedBytes;
            var decoded = LythonRuntime.DecodeText(source, bom ? LythonRuntime.TextEncodingMode.Utf8Bom : LythonRuntime.TextEncodingMode.Utf8,
                context, Span, LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.PreserveUniversal);
            Assert.True(MemoryMarshal.TryGetArray(decoded.Utf8Bytes, out var window));
            Assert.Same(payload, window.Array);
            Assert.Equal(bom ? 3 : 0, window.Offset);
            Assert.Equal(before + PyString.EstimateApproximateBytes(0), budget.ReservedBytes);
            original.Dispose();
            context.MemoryGovernor.RefundUnpublishedValue(decoded);
            Assert.True(budget.ReservedBytes >= PyBytes.EstimateApproximateBytes(payload.Length));
            Assert.Equal(Value, decoded.AsString());
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnownedBytesRetainIndependentFundingAndExistingAdoption(bool bom)
    {
        var budget = new LythonMemoryBudget();
        using (var scope = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = scope });
            var payload = Encoding.UTF8.GetBytes((bom ? "\uFEFF" : "") + Value);
            var source = new PyBytes(payload);
            var before = budget.ReservedBytes;
            var decoded = LythonRuntime.DecodeText(source, bom ? LythonRuntime.TextEncodingMode.Utf8Bom : LythonRuntime.TextEncodingMode.Utf8,
                context, Span, LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.PreserveUniversal);
            Assert.True(MemoryMarshal.TryGetArray(decoded.Utf8Bytes, out var array));
            if (bom) Assert.NotSame(payload, array.Array);
            else Assert.Same(payload, array.Array);
            Assert.Equal(before + PyString.EstimateApproximateBytes(Encoding.UTF8.GetByteCount(Value)), budget.ReservedBytes);
            Assert.Equal(Value, decoded.AsString());
            context.MemoryGovernor.RefundUnpublishedValue(decoded);
            Assert.Equal(before, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASecondOwnerFundsItsResultAfterTheInputOwnerEnds(bool bom)
    {
        var budget = new LythonMemoryBudget();
        using (var resultScope = budget.CreateScope())
        {
            var resultContext = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = resultScope });
            var before = budget.ReservedBytes;
            PyString decoded;
            using (var inputScope = budget.CreateScope())
            {
                var inputContext = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = inputScope });
                var source = new PyBytes(Encoding.UTF8.GetBytes((bom ? "\uFEFF" : "") + Value), inputContext.MemoryGovernor, Span);
                decoded = LythonRuntime.DecodeText(source, bom ? LythonRuntime.TextEncodingMode.Utf8Bom : LythonRuntime.TextEncodingMode.Utf8,
                    resultContext, Span, LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.PreserveUniversal);
                Assert.Equal(PyBytes.EstimateApproximateBytes(source.Length), source.CommittedStorageBytes);
            }
            Assert.Equal(Value, decoded.AsString());
            Assert.Equal(before + decoded.CommittedOwnedBytes, budget.ReservedBytes);
            resultContext.MemoryGovernor.RefundUnpublishedValue(decoded);
            Assert.Equal(before, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdmissionFailureOrCancellationDoesNotPublishABorrow(bool cancel)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var scope = budget.CreateScope())
        {
            var admission = new AdmissionHookMemoryBudget(scope);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = admission, CancellationToken = cancellation.Token });
            var source = new PyBytes(Encoding.UTF8.GetBytes(Value), context.MemoryGovernor, Span);
            var before = budget.ReservedBytes;
            if (cancel) admission.AfterGrowth = _ => cancellation.Cancel();
            else admission.RefuseGrowth = _ => true;
            var error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.DecodeText(source, LythonRuntime.TextEncodingMode.Utf8,
                context, Span, LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.PreserveUniversal));
            Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
            Assert.Equal(before, budget.ReservedBytes);
            Assert.Equal(PyBytes.EstimateApproximateBytes(source.Length), source.CommittedStorageBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(4093)]
    [InlineData(4094)]
    [InlineData(4095)]
    [InlineData(4096)]
    public void ChunkEdgesPreserveSupplementaryScalars(int prefix)
    {
        var budget = new LythonMemoryBudget();
        using (var scope = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = scope });
            var value = new string('a', prefix) + "😀é\r\nend";
            var source = new PyBytes(Encoding.UTF8.GetBytes(value), context.MemoryGovernor, Span);
            var decoded = LythonRuntime.DecodeText(source, LythonRuntime.TextEncodingMode.Utf8,
                context, Span, LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.PreserveUniversal);
            Assert.Equal(value, decoded.AsString());
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
