using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class UrlPercentAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData(130)]
    [InlineData(200)]
    public void DeniedQuotedOutputRefundsConstructionAndRegistration(long cap)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.UrllibParseModule.QuoteBytes(
                new byte[] { 0xff }, default, false, context, Span));
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Theory]
    [InlineData(32)]
    [InlineData(100)]
    public void DeniedPercentBytesRefundConstructionAndRegistration(long cap)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.UrllibParseModule.PercentDecodeBytes(
                "%ff"u8, context, Span));
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Fact]
    public void QuotedLengthIsDeniedBeforeOutputAllocation()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxStringLength = 2 });
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Contains("maximum string length", Assert.Throws<LythonRuntimeException>(() =>
            LythonRuntime.UrllibParseModule.QuoteBytes(new byte[] { 0xff }, default, false, context, Span)).Message);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
