using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG21: ranged host reads let large files be acquired in a bounded window.
/// Windows are byte-based and may split UTF-8 sequences, CRLF pairs and quoted
/// records; decoding split windows rests with the caller. The default interface
/// implementation composes whole-file reads (correct but unbounded) so existing
/// hosts keep working until they override the ranges natively.
/// </summary>
public sealed class HostRangedReadTests
{
    private const string CsvText =
        "id,name,note\r\n" +
        "1,\"h\u00e9llo\",\"a,b\"\r\n" +
        "2,\"\U0001F600\",\"multi\r\nline\"\r\n";

    private static readonly byte[] BinaryPayload = new byte[] { 0, 1, 2, 250, 251, 252, 253, 254, 255, 128, 129, 224, 164, 184, 240, 159, 152, 128 };

    [Fact]
    public async Task TextRangesReassembleWholeFileAtAnyWindow()
    {
        var host = new MockLythonHost();
        host.SeedFile("/data.csv", CsvText);
        var whole = (await host.ReadTextUtf8Async("/data.csv", CancellationToken.None)).ToArray();
        var size = (await host.StatAsync("/data.csv", CancellationToken.None)).Size;

        foreach (var window in new[] { 1, 2, 3, 5, 7, 64, 4096 })
        {
            var assembled = new List<byte>();
            for (long offset = 0; offset < (long)size; offset += window)
            {
                var chunk = await host.ReadTextUtf8RangeAsync("/data.csv", offset, window, CancellationToken.None);
                assembled.AddRange(chunk.ToArray());
            }

            Assert.Equal(whole, assembled.ToArray());
        }
    }

    [Fact]
    public async Task BinaryRangesReassembleWholeFileAtAnyWindow()
    {
        var host = new MockLythonHost();
        host.SeedBytes("/data.bin", BinaryPayload);

        foreach (var window in new[] { 1, 3, 7, 64 })
        {
            var assembled = new List<byte>();
            for (long offset = 0; offset < BinaryPayload.Length; offset += window)
            {
                var chunk = await host.ReadBytesRangeAsync("/data.bin", offset, window, CancellationToken.None);
                assembled.AddRange(chunk.ToArray());
            }

            Assert.Equal(BinaryPayload, assembled.ToArray());
        }
    }

    [Fact]
    public async Task RangedReadsStayWithinTheRequestedWindow()
    {
        var host = new MockLythonHost();
        host.SeedFile("/big.csv", new string('x', 9999) + "\n");
        var size = (long)(await host.StatAsync("/big.csv", CancellationToken.None)).Size;

        var assembled = new List<byte>();
        for (long offset = 0; offset < size; offset += 64)
        {
            var chunk = await host.ReadTextUtf8RangeAsync("/big.csv", offset, 64, CancellationToken.None);
            assembled.AddRange(chunk.ToArray());
        }

        Assert.Equal(10000, assembled.Count);
        Assert.True(host.RangedReadCount > 1);
        Assert.True(host.MaxRangeBytesServed <= 64, "no single ranged call served more than the window");
    }

    [Fact]
    public async Task RangesAtAndPastEndYieldEmpty()
    {
        var host = new MockLythonHost();
        host.SeedFile("/data.csv", CsvText);
        host.SeedBytes("/data.bin", BinaryPayload);
        var textSize = (long)(await host.StatAsync("/data.csv", CancellationToken.None)).Size;

        Assert.Equal(0, (await host.ReadTextUtf8RangeAsync("/data.csv", textSize, 64, CancellationToken.None)).Length);
        Assert.Equal(0, (await host.ReadTextUtf8RangeAsync("/data.csv", textSize + 100, 64, CancellationToken.None)).Length);
        Assert.Equal(0, (await host.ReadTextUtf8RangeAsync("/data.csv", 0, 0, CancellationToken.None)).Length);
        Assert.Equal(0, (await host.ReadBytesRangeAsync("/data.bin", BinaryPayload.Length, 64, CancellationToken.None)).Length);
        Assert.Equal(0, (await host.ReadBytesRangeAsync("/data.bin", BinaryPayload.Length + 7, 64, CancellationToken.None)).Length);
        Assert.Equal(0, (await host.ReadBytesRangeAsync("/data.bin", 0, 0, CancellationToken.None)).Length);
    }

    [Fact]
    public async Task NegativeRangeArgumentsFailExplicitly()
    {
        var host = new MockLythonHost();
        host.SeedFile("/data.csv", CsvText);
        host.SeedBytes("/data.bin", BinaryPayload);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => host.ReadTextUtf8RangeAsync("/data.csv", -1, 8, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => host.ReadTextUtf8RangeAsync("/data.csv", 0, -1, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => host.ReadBytesRangeAsync("/data.bin", -1, 8, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => host.ReadBytesRangeAsync("/data.bin", 0, -1, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task RangesOnMissingPathsMatchWholeReadErrors()
    {
        var host = new MockLythonHost();

        var wholeText = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.ReadTextUtf8Async("/missing.txt", CancellationToken.None).AsTask());
        var rangeText = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.ReadTextUtf8RangeAsync("/missing.txt", 0, 64, CancellationToken.None).AsTask());
        Assert.Equal(wholeText.Message, rangeText.Message);

        var wholeBytes = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.ReadBytesAsync("/missing.bin", CancellationToken.None).AsTask());
        var rangeBytes = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.ReadBytesRangeAsync("/missing.bin", 0, 64, CancellationToken.None).AsTask());
        Assert.Equal(wholeBytes.Message, rangeBytes.Message);
    }

    [Fact]
    public async Task RangesHonorCancellation()
    {
        var host = new MockLythonHost();
        host.SeedFile("/data.csv", CsvText);
        host.SeedBytes("/data.bin", BinaryPayload);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => host.ReadTextUtf8RangeAsync("/data.csv", 0, 8, canceled.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => host.ReadBytesRangeAsync("/data.bin", 0, 8, canceled.Token).AsTask());
    }

    [Fact]
    public async Task DefaultRangesComposeWholeReads()
    {
        ILythonHost host = new WholeOnlyHost();
        ((WholeOnlyHost)host).Seed("/data.bin", BinaryPayload);

        var assembled = new List<byte>();
        for (long offset = 0; offset < BinaryPayload.Length; offset += 5)
        {
            var chunk = await host.ReadBytesRangeAsync("/data.bin", offset, 5, CancellationToken.None);
            assembled.AddRange(chunk.ToArray());
        }

        Assert.Equal(BinaryPayload, assembled.ToArray());
        Assert.Equal(0, (await host.ReadBytesRangeAsync("/data.bin", BinaryPayload.Length + 1, 5, CancellationToken.None)).Length);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => host.ReadBytesRangeAsync("/data.bin", -1, 5, CancellationToken.None).AsTask());

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.ReadBytesRangeAsync("/absent.bin", 0, 5, CancellationToken.None).AsTask());
        Assert.Contains("absent", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DefaultRangesHonorCancellation()
    {
        ILythonHost host = new WholeOnlyHost();
        ((WholeOnlyHost)host).Seed("/data.bin", BinaryPayload);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => host.ReadBytesRangeAsync("/data.bin", 0, 5, canceled.Token).AsTask());
    }

    private sealed class WholeOnlyHost : ILythonHost
    {
        private static readonly DateTimeOffset Timestamp = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

        public string Cwd => "/";

        public DateTimeOffset LocalNow => Timestamp;

        public DateTimeOffset UtcNow => Timestamp;

        public void Seed(string path, byte[] payload) => _files[path] = payload;

        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken) =>
            throw new LythonHostCapabilityUnavailableException("text file I/O");

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) =>
            throw new LythonHostCapabilityUnavailableException("text file I/O");

        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) =>
            throw new LythonHostCapabilityUnavailableException("text file I/O");

        public ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_files.TryGetValue(path, out var payload))
            {
                throw new InvalidOperationException($"no such file: {path}");
            }

            return ValueTask.FromResult<ReadOnlyMemory<byte>>(payload.ToArray());
        }

        public ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
            throw new LythonHostCapabilityUnavailableException("binary file I/O");

        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult(_files.ContainsKey(path));

        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
        {
            IReadOnlyList<string> names = Array.Empty<string>();
            return ValueTask.FromResult(names);
        }

        public ValueTask MkDirAsync(string path, CancellationToken cancellationToken) =>
            throw new LythonHostCapabilityUnavailableException("directories");

        public ValueTask RemoveAsync(string path, CancellationToken cancellationToken) =>
            throw new LythonHostCapabilityUnavailableException("removal");

        public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken) =>
            throw new LythonHostCapabilityUnavailableException("copies");

        public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken) =>
            throw new LythonHostCapabilityUnavailableException("moves");

        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
        {
            var stat = _files.TryGetValue(path, out var payload)
                ? new LythonPathStat(LythonPathKind.File, payload.Length, Timestamp)
                : new LythonPathStat(LythonPathKind.Missing, 0, null);
            return ValueTask.FromResult(stat);
        }
    }
}
