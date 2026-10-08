using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ZlibAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);
    private static readonly byte[] Text = Convert.FromHexString("789CCB48CDC9C957C84090003A2E067D");
    private static readonly byte[] Expansion = Convert.FromHexString("789CEDC13101000000C2A0DA8B6F0D0FA00000000000000000000000000000000000000078306547A11D");

    [Fact]
    public void InvalidStreamsRefundPrivateOutputWithoutCollectingGuestValues()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var badChecksum = Text.ToArray();
        badChecksum[^1] ^= 1;
        for (var attempt = 0; attempt < 30; attempt++)
            foreach (var data in new[] { Text[..^1], badChecksum, new byte[] { 120, 156, 7, 0, 0, 0, 1 } })
            {
                var error = Assert.Throws<LythonRuntimeException>(() =>
                    LythonRuntime.ZlibModule.DecompressBytes(data, context, Span));
                Assert.Equal(PythonExceptionIdentity.Module("zlib", "error"), error.Identity);
                Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(0, context.State.CallTemporaries.Count);
            }
    }

    [Theory]
    [InlineData(80000)]
    [InlineData(131500)]
    [InlineData(140000)]
    [InlineData(165000)]
    [InlineData(180000)]
    public void InflationDenialsRefundNativeScratchAndOutput(long cap)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() =>
                LythonRuntime.ZlibModule.DecompressBytes(Expansion, context, Span));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(context.State.CallTemporaries.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.State.CallTemporaries.Count);
        }
    }

    [Theory]
    [InlineData(524000)]
    [InlineData(526000)]
    [InlineData(540000)]
    [InlineData(564000)]
    public void CompressionDenialsRefundNativeScratchAndOutput(long cap)
    {
        var data = new byte[20000];
        new Random(42).NextBytes(data);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() =>
                LythonRuntime.ZlibModule.CompressBytes(data, 6, context, Span));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(context.State.CallTemporaries.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.State.CallTemporaries.Count);
        }
    }

    [Fact]
    public void PublicationRegistryDenialRefundsTheExactCopy()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 256 });
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() =>
                LythonRuntime.ZlibModule.CompressBytes(ReadOnlyMemory<byte>.Empty, 6, context, Span));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(context.State.CallTemporaries.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.State.CallTemporaries.Count);
        }
    }

    [Fact]
    public void EmptyBlocksStillConsumeWorkAndRefundOnFailure()
    {
        var input = new byte[2 + 5 * 10000 + 2 + 4];
        input[0] = 120;
        input[1] = 156;
        for (var i = 2; i < 50002; i += 5) { input[i + 3] = 255; input[i + 4] = 255; }
        input[50002] = 3;
        input[^1] = 1;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionSteps = 8 });
        var error = Assert.Throws<LythonRuntimeException>(() =>
            LythonRuntime.ZlibModule.DecompressBytes(input, context, Span));
        Assert.Equal("RuntimeError", error.ExceptionType);
        Assert.Contains("step", error.Message);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void PreCanceledCompressionAndInflationDoNotReserve()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { CancellationToken = cancellation.Token });
        foreach (var action in new Action[]
        {
            () => LythonRuntime.ZlibModule.CompressBytes(new byte[1000], 6, context, Span),
            () => LythonRuntime.ZlibModule.DecompressBytes(Text, context, Span),
        })
        {
            var error = Assert.Throws<LythonRuntimeException>(action);
            Assert.Equal("execution canceled", error.Message);
            Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Fact]
    public void ReturnedOutputOwnsOnlyItsRetainedCoupon()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var bytes = LythonRuntime.ZlibModule.DecompressBytes(Expansion, context, Span);
        Assert.Equal(20000, bytes.Length);
        Assert.Same(context.MemoryGovernor, bytes.OwnerMemoryGovernor);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(20032 + 128 + context.State.CallTemporaries.CommittedBackingBytes,
            context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(context.MemoryGovernor.PeakReservedBytes >= LythonRuntime.ZlibModule.InflationScratchBytes);
        GC.KeepAlive(bytes);
    }

    [Fact]
    public void DroppedOutputReclaimsItsPayload()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var weak = CreateDroppedOutput(context);
        for (var attempt = 0; attempt < 4; attempt++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
        Assert.False(weak.IsAlive);
        context.State.CallTemporaries.Sweep(full: true);
        Assert.Equal(0, context.State.CallTemporaries.Count);
        Assert.Equal(context.State.CallTemporaries.CommittedBackingBytes, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDroppedOutput(LythonRuntime.ExecutionContext context)
        => new(LythonRuntime.ZlibModule.DecompressBytes(Expansion, context, Span));
}
