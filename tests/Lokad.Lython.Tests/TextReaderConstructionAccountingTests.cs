using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using System.Runtime.CompilerServices;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class TextReaderConstructionAccountingTests
{
    [Theory]
    [InlineData(false, 200)]
    [InlineData(true, 200)]
    public async Task FailedReaderConstructionRefundsStorage(bool asynchronous, long cap)
    {
        var host = new MockLythonHost();
        host.SeedFile("/invalid", "x");
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(cap) },regexProvider:Utf8RegexProvider.Instance);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            Assert.Equal("MemoryError", await DeniedOpen(context, asynchronous));
            // No reader storage may remain after an unpublished shell fails.
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<string> DeniedOpen(LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        var failure = asynchronous
            ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecutionContext.TextFileHandle.ForReadAsync(
                "/invalid", context, LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.PreserveUniversal))
            : Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecutionContext.TextFileHandle.ForRead(
                "/invalid", context, LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict, LythonRuntime.TextNewlineMode.PreserveUniversal));
        return failure.ExceptionType;
    }
}
