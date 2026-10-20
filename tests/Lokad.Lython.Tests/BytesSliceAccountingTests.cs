using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// Byte slices fund backing and construction overlap. Runtime slices use
/// normalized counts; context-free lazy callers use the admitted byte builder.
/// </summary>
public sealed class BytesSliceAccountingTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DerivedBytesUseTheActiveOwnerInsteadOfTheReceiverOwner(bool representation)
    {
        var sourceBudget = new LythonMemoryBudget(65536);
        var resultBudget = new LythonMemoryBudget(65536);
        using var sourceOwner = sourceBudget.CreateScope();
        using var resultOwner = resultBudget.CreateScope();
        var sourceContext = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = sourceOwner });
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = resultOwner });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var source = new PyBytes(new byte[256], sourceContext.MemoryGovernor, span);
        var baseline = sourceBudget.ReservedBytes;
        var result = representation ? (IPyGovernedValue)source.RenderPython(new PyRenderingContext(context))
                                    : (IPyGovernedValue)PyIndexing.ReadSliceCore(source, 1, null, 2, span, context);
        Assert.Same(context.MemoryGovernor, result.OwnerMemoryGovernor);
        Assert.Equal(baseline, sourceBudget.ReservedBytes);
        Assert.True(resultBudget.ReservedBytes > 0);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        resultOwner.Dispose();
        Assert.Equal(0, resultBudget.ReservedBytes);
        sourceOwner.Dispose();
        Assert.Equal(0, sourceBudget.ReservedBytes);
    }

    [Fact]
    public void LazySliceScratchStaysBounded()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var source = new PyBytes(new byte[20000], context.MemoryGovernor, span);
        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        var peakBefore = context.MemoryGovernor.PeakAccountedBytes;

        var slice = Assert.IsType<PyBytes>(source.GetSlice(PyIndexing.SliceIndices(source.Length, null, null, new BigInteger(2), span)));

        Assert.Equal(10000, slice.Length);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        // The final copy charge moves both the peak and the committed total
        // together; only covered build scratch can push the peak further.
        Assert.True(
            context.MemoryGovernor.PeakAccountedBytes - peakBefore >
                context.MemoryGovernor.CurrentCommittedBytes - committedBefore,
            "Slice scratch was never covered while the source was retained.");
    }

    [Fact]
    public void EmptySliceKeepsItsShellAndOwnedBufferFunded()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(128) },regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var source = new PyBytes(Array.Empty<byte>(), context.MemoryGovernor, span);
        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;

        var slice = Assert.IsType<PyBytes>(source.GetSlice(PyIndexing.SliceIndices(source.Length, null, null, null, span)));

        Assert.Equal(0, slice.Length);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes - committedBefore >= 32);
    }
}
