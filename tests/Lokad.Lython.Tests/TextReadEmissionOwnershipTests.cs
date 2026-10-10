using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class TextReadEmissionOwnershipTests
{
    [Theory]
    [InlineData(false, -1, 127)]
    [InlineData(true, -1, 127)]
    [InlineData(false, 2, 127)]
    [InlineData(true, 2, 127)]
    [InlineData(false, 4, 127)]
    [InlineData(true, 4, 127)]
    [InlineData(false, -1, 159)]
    [InlineData(true, -1, 159)]
    public async Task DeniedEmissionRegistrationReturnsUnpublishedPayload(
        bool asynchronous, int readSize, int registrationHeadroom)
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
        // also holds builder scratch. Leave its registry entry short by one
        // byte, or fund its entry but leave tier growth short by one byte.
        // Keep the reader and current window strongly owned.
        var payloadLength = readSize < 0 ? 4 : readSize;
        var builderScratch = readSize == 4 ? 32 + 4 * payloadLength : 0;
        var pressure = cap - governor.CurrentAccountedBytes
            - (128 + payloadLength + builderScratch + registrationHeadroom);
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
        var next = asynchronous ? await state.ReadLineAsync(-1) : state.ReadLine(-1);
        Assert.Equal(readSize == 2 ? "c\n" : "xyz\n", next.AsString());
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(state);
        GC.KeepAlive(next);
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
        var pool = context.State.LiveReclamationPools().Single(p => p.Count > 0);
        Assert.True(pool.IsTracked(line));
        state.Release();
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.Equal(0, pool.Sweep(full: true));
        Assert.Equal(1, pool.Count);
        Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal("abc\n", line.AsString());
        GC.KeepAlive(line);
        GC.KeepAlive(state);
    }
}
