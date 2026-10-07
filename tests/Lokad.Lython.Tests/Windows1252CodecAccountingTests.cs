using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class Windows1252CodecAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

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
                new byte[] { 0x81 }, LythonRuntime.TextEncodingMode.Windows1252, context, Span,
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
        var errors = (LythonRuntime.TextErrorMode)errorMode;
        foreach (var window in new[] { 1, 2, 3, 5 })
        {
            var host = new MockLythonHost();
            host.SeedBytes("/input", [0x81, 0xc3, 0xa9, 0x0d, 0x0a]);
            var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 100000 });
            var state = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState("/input", context,
                LythonRuntime.TextEncodingMode.Windows1252, errors, LythonRuntime.TextNewlineMode.TranslateUniversal, window);
            var before = context.MemoryGovernor.CurrentCommittedBytes;
            state.Prime();
            Assert.True(context.MemoryGovernor.CurrentCommittedBytes > before);
            state.Release();
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Fact]
    public void WindowDecodedCacheDenialRefundsTheNativeString()
    {
        var host = new MockLythonHost();
        host.SeedBytes("/input", [0x81]);
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 283 });
        var state = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState("/input", context,
            LythonRuntime.TextEncodingMode.Windows1252, LythonRuntime.TextErrorMode.Replace,
            LythonRuntime.TextNewlineMode.PreserveUniversal, 1);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        try
        {
            Assert.Equal("MemoryError", Assert.Throws<LythonRuntimeException>(state.Prime).ExceptionType);
        }
        finally { state.Release(); }
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
