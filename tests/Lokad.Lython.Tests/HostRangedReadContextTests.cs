using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG21: the execution-context ranged-read wrappers thread host ranges through
/// the sync and async host pumps identically to whole-file reads.
/// </summary>
public sealed class HostRangedReadContextTests
{
    private const string CsvText =
        "id,name,note\r\n" +
        "1,\"h\u00e9llo\",\"a,b\"\r\n" +
        "2,\"\U0001F600\",\"multi\r\nline\"\r\n";

    [Fact]
    public async Task TextRangesReassembleThroughBothPumps()
    {
        var host = new MockLythonHost();
        host.SeedFile("/data.csv", CsvText);
        var whole = (await host.ReadTextUtf8Async("/data.csv", CancellationToken.None)).ToArray();
        var size = (long)(await host.StatAsync("/data.csv", CancellationToken.None)).Size;

        foreach (var window in new[] { 1, 7, 64 })
        {
            var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
            var syncAssembled = new List<byte>();
            for (long offset = 0; offset < size; offset += window)
            {
                syncAssembled.AddRange(context.ReadTextUtf8Range("/data.csv", offset, window, null).ToArray());
            }

            Assert.Equal(whole, syncAssembled.ToArray());

            var asyncAssembled = new List<byte>();
            for (long offset = 0; offset < size; offset += window)
            {
                asyncAssembled.AddRange((await context.ReadTextUtf8RangeAsync("/data.csv", offset, window, null)).ToArray());
            }

            Assert.Equal(whole, asyncAssembled.ToArray());
        }
    }

    [Fact]
    public async Task ByteRangesReassembleThroughBothPumps()
    {
        var payload = new byte[] { 0, 1, 2, 250, 251, 252, 253, 254, 255 };
        var host = new MockLythonHost();
        host.SeedBytes("/data.bin", payload);
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());

        var syncAssembled = new List<byte>();
        for (long offset = 0; offset < payload.Length; offset += 4)
        {
            syncAssembled.AddRange(context.ReadHostBytesRange("/data.bin", offset, 4, null).ToArray());
        }

        Assert.Equal(payload, syncAssembled.ToArray());

        var asyncAssembled = new List<byte>();
        for (long offset = 0; offset < payload.Length; offset += 4)
        {
            asyncAssembled.AddRange((await context.ReadHostBytesRangeAsync("/data.bin", offset, 4, null)).ToArray());
        }

        Assert.Equal(payload, asyncAssembled.ToArray());
    }

    [Fact]
    public async Task RangeFailuresMatchWholeReadFailures()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());

        var wholeSync = Assert.Throws<HostOperationException>(() => context.ReadTextUtf8("/missing.txt", null));
        var rangeSync = Assert.Throws<HostOperationException>(() => context.ReadTextUtf8Range("/missing.txt", 0, 8, null));
        Assert.Equal(wholeSync.Message, rangeSync.Message);

        var wholeAsync = await Assert.ThrowsAsync<HostOperationException>(() => context.ReadTextUtf8Async("/missing.txt", null).AsTask());
        var rangeAsync = await Assert.ThrowsAsync<HostOperationException>(() => context.ReadTextUtf8RangeAsync("/missing.txt", 0, 8, null).AsTask());
        Assert.Equal(wholeAsync.Message, rangeAsync.Message);
    }
}