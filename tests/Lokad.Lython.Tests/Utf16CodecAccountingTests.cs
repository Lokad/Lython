using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class Utf16CodecAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public async Task EncodedOutputStaysFundedAcrossASuspendedWriteAndRefundsOnCancellation()
    {
        var host = new DelayedLythonHost();
        var original = new byte[] { 0xff, 0xfe, 0x41, 0x00 };
        host.SeedBytes("/output", original);
        var started = host.PauseWriteUntilCancellation("/output");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions
        {
            MaxExecutionMemoryBytes = 100000,
            CancellationToken = cancellation.Token,
        });
        var file = LythonRuntime.ExecutionContext.TextFileHandle.ForWrite("/output", context,
            LythonRuntime.TextEncodingMode.Utf16, LythonRuntime.TextErrorMode.Strict,
            LythonRuntime.TextNewlineMode.PreserveUniversal);
        _ = file.Write(PyString.FromString("\ud83d\ude00"));
        var stagedCharge = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        var pending = file.FlushAsync().AsTask();
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        Assert.True(context.MemoryGovernor.CurrentReservedBytes >= 6);
        cancellation.Cancel();
        Assert.Equal("execution canceled", (await Assert.ThrowsAsync<LythonRuntimeException>(() => pending)).Message);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(stagedCharge, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(original, host.ReadBytes("/output"));
    }

    [Fact]
    public void EncodingDenialKeepsStagedTextAndRefundsTemporaryFunding()
    {
        var host = new MockLythonHost();
        host.SeedBytes("/output", [1, 2, 3]);
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 14000 });
        var file = LythonRuntime.ExecutionContext.TextFileHandle.ForWrite("/output", context,
            LythonRuntime.TextEncodingMode.Utf16, LythonRuntime.TextErrorMode.Strict,
            LythonRuntime.TextNewlineMode.PreserveUniversal);
        _ = file.Write(PyString.FromString(new string('a', 5000)));
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            Assert.Equal("MemoryError", Assert.Throws<LythonRuntimeException>(() => file.Flush()).ExceptionType);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        }
        Assert.Equal(new byte[] { 1, 2, 3 }, host.ReadBytes("/output"));
    }

    [Theory]
    [InlineData(130)]
    [InlineData(200)]
    public void DeniedNativeDecodingRefundsOutputAndRegistration(long cap)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.DecodeText(
                new byte[] { 0xff, 0xfe, 0x41 }, LythonRuntime.TextEncodingMode.Utf16, context, Span,
                LythonRuntime.TextErrorMode.Replace, LythonRuntime.TextNewlineMode.PreserveUniversal));
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ClosingWindowsRefundsBothTextRepresentations(int errorMode)
    {
        foreach (var window in new[] { 1, 2, 3, 5 })
        {
            var host = new MockLythonHost();
            host.SeedBytes("/input", [0xff, 0xfe, 0x3d, 0xd8, 0x00, 0xde, 0x0d, 0x00, 0x0a, 0x00]);
            var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 100000 });
            var state = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState("/input", context,
                LythonRuntime.TextEncodingMode.Utf16, (LythonRuntime.TextErrorMode)errorMode,
                LythonRuntime.TextNewlineMode.TranslateUniversal, window);
            var before = context.MemoryGovernor.CurrentCommittedBytes;
            state.Prime();
            Assert.True(context.MemoryGovernor.CurrentCommittedBytes > before);
            state.Release();
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Fact]
    public void AstralWindowDecodedCacheDenialRefundsTheNativeString()
    {
        var host = new MockLythonHost();
        host.SeedBytes("/input", [0x3d, 0xd8, 0x00, 0xde]);
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 285 });
        var state = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState("/input", context,
            LythonRuntime.TextEncodingMode.Utf16LittleEndian, LythonRuntime.TextErrorMode.Strict,
            LythonRuntime.TextNewlineMode.PreserveUniversal, 1);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        try { Assert.Equal("MemoryError", Assert.Throws<LythonRuntimeException>(state.Prime).ExceptionType); }
        finally { state.Release(); }
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
