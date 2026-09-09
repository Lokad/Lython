using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG05: byte-slice materialization must cover its scratch. Slice bounds
/// arrive lazily, so every b[x:y:z] drained into an uncharged list before the
/// final copy was charged.
/// </summary>
public sealed class BytesSliceAccountingTests
{
    [Fact]
    public void LazySliceScratchStaysBounded()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
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
    public void EmptySliceChargesOnlyItsEstimate()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 128 });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var source = new PyBytes(Array.Empty<byte>(), context.MemoryGovernor, span);
        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;

        var slice = Assert.IsType<PyBytes>(source.GetSlice(PyIndexing.SliceIndices(source.Length, null, null, null, span)));

        Assert.Equal(0, slice.Length);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(32, context.MemoryGovernor.CurrentCommittedBytes - committedBefore);
    }
}
