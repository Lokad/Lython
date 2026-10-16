using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class TextReadEmissionOwnershipTests
{
    [Theory]
    [InlineData(false, -1)]
    [InlineData(true, -1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 4)]
    [InlineData(true, 4)]


    public async Task DeniedEmissionPayloadReturnsUnpublishedStorage(
        bool asynchronous, int readSize)
    {
        const long cap = 65536;
        var host = new MockLythonHost();
        host.SeedFile("/data.txt", "abc\nxyz\n");
        var context = new LythonRuntime.ExecutionContext(host,
            new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        var state = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState(
            "/data.txt", context, LythonRuntime.TextEncodingMode.Utf8,
            LythonRuntime.TextErrorMode.Strict,
            LythonRuntime.TextNewlineMode.TranslateUniversal, windowBytes: 4);
        if (asynchronous) await state.PrimeAsync();
        else state.Prime();

        var governor = context.MemoryGovernor;
        var committed = governor.CurrentCommittedBytes;
        var reserved = governor.CurrentReservedBytes;
        // A line or bounded slice builds a fresh value. An exact-window read
        // also holds builder scratch. Leave the payload admission one byte short.
        // Keep the reader and current window strongly owned.
        var payloadLength = readSize < 0 ? 4 : readSize;
        var builderScratch = readSize == 4 ? 32 + 4 * payloadLength : 0;
        var pressure = cap - governor.CurrentAccountedBytes
            - (128 + payloadLength + builderScratch - 1);
        Assert.True(pressure > 0);
        governor.Reserve(pressure, null);

        LythonRuntimeException error;
        if (asynchronous)
            error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (readSize < 0) await state.ReadLineAsync(-1);
                else await state.ReadAsync(readSize);
            });
        else
            error = Assert.Throws<LythonRuntimeException>(() =>
            {
                if (readSize < 0) state.ReadLine(-1);
                else state.Read(readSize);
            });

        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(reserved + pressure, governor.CurrentReservedBytes);
        Assert.Equal(committed, governor.CurrentCommittedBytes);
        governor.ReleaseReserved(pressure);
        var denied = asynchronous
            ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await state.ReadLineAsync(-1))
            : Assert.Throws<LythonRuntimeException>(() => state.ReadLine(-1));
        Assert.Same(error, denied);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(state);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedLineStaysChargedAfterReaderCloses(bool asynchronous)
    {
        var host = new MockLythonHost();
        host.SeedFile("/data.txt", "abc\nxyz\n");
        var context = new LythonRuntime.ExecutionContext(host, null);
        var state = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState(
            "/data.txt", context, LythonRuntime.TextEncodingMode.Utf8,
            LythonRuntime.TextErrorMode.Strict,
            LythonRuntime.TextNewlineMode.TranslateUniversal, windowBytes: 4);
        var line = asynchronous ? await state.ReadLineAsync(-1) : state.ReadLine(-1);
        var pool = context.State.MemoryGovernor;
        state.Release();
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal("abc\n", line.AsString());
        GC.KeepAlive(line);
        GC.KeepAlive(state);
    }
}
