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
    [InlineData(false, 4250)]
    [InlineData(true, 4250)]
    [InlineData(false, 4410)]
    [InlineData(true, 4410)]
    public async Task FailedReaderConstructionRefundsStorage(bool asynchronous, long cap)
    {
        var host = new MockLythonHost();
        host.SeedFile("/invalid", "x");
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            Assert.Equal("MemoryError", await DeniedOpen(context, asynchronous));
            GC.Collect();
            // The shared pool retains funded capacity; no reader storage or
            // ownership entry may remain after a failed unpublished construction.
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
