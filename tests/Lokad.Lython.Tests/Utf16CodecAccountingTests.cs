using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class Utf16CodecAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData(0, 4)] // UTF-8
    [InlineData(1, 7)] // UTF-8-sig
    [InlineData(2, 6)] // UTF-16
    public async Task EncodedOutputStaysFundedAcrossASuspendedWriteAndRefundsOnCancellation(int encoding, int encodedBytes)
    {
        var host = new DelayedLythonHost();
        var original = new byte[] { 0xff, 0xfe, 0x41, 0x00 };
        host.SeedBytes("/output", original);
        var started = host.PauseWriteUntilCancellation("/output");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions
        {
            MemoryBudget = new LythonMemoryBudget(100000),
            CancellationToken = cancellation.Token,
        },regexProvider:Utf8RegexProvider.Instance);
        var codec = encoding switch
        {
            0 => LythonRuntime.TextEncodingMode.Utf8,
            1 => LythonRuntime.TextEncodingMode.Utf8Bom,
            _ => LythonRuntime.TextEncodingMode.Utf16,
        };
        var file = await LythonRuntime.ExecutionContext.TextFileHandle.ForWriteAsync("/output", context,
            codec, LythonRuntime.TextErrorMode.Strict,
            LythonRuntime.TextNewlineMode.PreserveUniversal);
        var shellCharge = context.MemoryGovernor.CurrentCommittedBytes;
        _ = file.Write(PyString.FromString("\ud83d\ude00"));
        var stagedCharge = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        var pending = file.FlushAsync().AsTask();
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        if (encoding == 0) Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        else Assert.True(context.MemoryGovernor.CurrentReservedBytes >= encodedBytes);
        Assert.Equal(stagedCharge, context.MemoryGovernor.CurrentCommittedBytes);
        cancellation.Cancel();
        Assert.Equal("execution canceled", (await Assert.ThrowsAsync<LythonRuntimeException>(() => pending)).Message);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(shellCharge, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(file.IsClosed);
        Assert.Empty(host.ReadBytes("/output"));
    }

    [Fact]
    public void EncodingDenialKeepsStagedTextAndRefundsTemporaryFunding()
    {
        var host = new MockLythonHost();
        host.SeedBytes("/output", [1, 2, 3]);
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(14000) },regexProvider:Utf8RegexProvider.Instance);
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
        Assert.Empty(host.ReadBytes("/output"));
    }

    [Theory]
    [InlineData(130)]
    public void DeniedNativeDecodingRefundsUnusedAdmission(long cap)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(cap) },regexProvider:Utf8RegexProvider.Instance);
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
    public async Task ClosingWindowsRefundsOwnedStorage(int errorMode)
    {
        foreach (var window in new[] { 1, 2, 3, 5 })
        {
            var host = new MockLythonHost();
            host.SeedBytes("/input", [0xff, 0xfe, 0x3d, 0xd8, 0x00, 0xde, 0x0d, 0x00, 0x0a, 0x00]);
            var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(100000) },regexProvider:Utf8RegexProvider.Instance);
            var file = new OpenedByteFile(context.Services);
            await file.OpenAsync("/input", new LythonFileOpenOptions(LythonFileAccess.Read), null, false);
            using var state = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState(file, context,
                LythonRuntime.TextEncodingMode.Utf16, (LythonRuntime.TextErrorMode)errorMode,
                LythonRuntime.TextNewlineMode.TranslateUniversal, window);
            var before = context.MemoryGovernor.CurrentCommittedBytes;
            var value = state.Read(1);
            Assert.Equal(1, value.Length);
            Assert.True(context.MemoryGovernor.CurrentCommittedBytes > before);
            var owned = state.OwnedBytes - LythonRuntime.ExecutionContext.ChunkedTextFileReadState.ShellBytes;
            var beforeClose = context.MemoryGovernor.CurrentCommittedBytes;
            state.Release();
            Assert.Equal(beforeClose - owned, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= before + value.CommittedOwnedBytes);
            Assert.Equal(1, value.Length);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }
}
