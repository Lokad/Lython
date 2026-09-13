using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG21: reads with a known stat size check the execution budget before the
/// host allocates and transfers the payload, instead of tripping only after
/// decoding. A counting host pins that oversized reads never reach it.
/// </summary>
public sealed class HostReadPrereservationTests
{
    private sealed class CountingHost : ILythonHost, ILythonSynchronousHostCapability
    {
        public bool CompletesSynchronously => true;

        private readonly MockLythonHost _inner = new("/repo");

        public int TextReads;
        public int BinaryReads;
        public int TextRangeReads;
        public long TextRangeBytes;
        public int MaxTextRangeBytes;

        public string Cwd => _inner.Cwd;
        public DateTimeOffset LocalNow => _inner.LocalNow;
        public DateTimeOffset UtcNow => _inner.UtcNow;

        public void SeedFile(string path, string text) => _inner.SeedFile(path, text);
        public void SeedBytes(string path, byte[] payload) => _inner.SeedBytes(path, payload);

        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
        {
            TextReads++;
            return _inner.ReadTextUtf8Async(path, cancellationToken);
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8RangeAsync(string path, long offset, int count, CancellationToken cancellationToken)
        {
            var payload = await _inner.ReadTextUtf8RangeAsync(path, offset, count, cancellationToken).ConfigureAwait(false);
            TextRangeReads++;
            TextRangeBytes += payload.Length;
            MaxTextRangeBytes = Math.Max(MaxTextRangeBytes, payload.Length);
            return payload;
        }

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => _inner.WriteTextUtf8Async(path, utf8, cancellationToken);

        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => _inner.AppendTextUtf8Async(path, utf8, cancellationToken);

        public ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken cancellationToken)
        {
            BinaryReads++;
            return _inner.ReadBytesAsync(path, cancellationToken);
        }

        public ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
            => _inner.WriteBytesAsync(path, bytes, cancellationToken);

        public ValueTask AppendBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
            => _inner.AppendBytesAsync(path, bytes, cancellationToken);

        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
            => _inner.ExistsAsync(path, cancellationToken);

        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
            => _inner.ListDirAsync(path, cancellationToken);

        public ValueTask MkDirAsync(string path, CancellationToken cancellationToken)
            => _inner.MkDirAsync(path, cancellationToken);

        public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
            => _inner.RemoveAsync(path, cancellationToken);

        public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
            => _inner.CopyAsync(source, destination, cancellationToken);

        public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
            => _inner.MoveAsync(source, destination, cancellationToken);

        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
            => _inner.StatAsync(path, cancellationToken);
    }

    [Fact]
    public async Task OversizedTextReadTripsBeforeHostRead()
    {
        var script = new LythonEngine().Compile("return len(open(\"/data.txt\").read())\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var host = new CountingHost();
        host.SeedFile("/data.txt", new string('y', 100000));
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.Equal(0, host.TextReads);

        var host2 = new CountingHost();
        host2.SeedFile("/data.txt", new string('y', 100000));
        var asyncResult = await script.RunAsync(host2, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.Equal(0, host2.TextReads);
    }

    [Fact]
    public async Task FundedTextReadReachesHost()
    {
        // MG21: open() streams fixed windows through ranged reads, so a funded
        // file arrives with no whole-file transfer: every ranged call stays
        // within the window and the windows cover the file exactly once.
        var script = new LythonEngine().Compile("return len(open(\"/data.txt\").read())\n");
        Assert.True(script.IsValid);
        var expected = new BigInteger(100000);
        var host = new CountingHost();
        host.SeedFile("/data.txt", new string('y', 100000));
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        Assert.Equal(0, host.TextReads);
        Assert.Equal(100000, host.TextRangeBytes);
        Assert.True(host.TextRangeReads > 1);
        Assert.True(host.MaxTextRangeBytes <= 16 * 1024, "no single ranged call exceeds the engine window");

        var host2 = new CountingHost();
        host2.SeedFile("/data.txt", new string('y', 100000));
        var asyncResult = await script.RunAsync(host2);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
        Assert.Equal(0, host2.TextReads);
        Assert.Equal(100000, host2.TextRangeBytes);
        Assert.True(host2.TextRangeReads > 1);
        Assert.True(host2.MaxTextRangeBytes <= 16 * 1024, "no single ranged call exceeds the engine window");
    }

    [Fact]
    public async Task OversizedBinaryReadTripsBeforeHostRead()
    {
        var script = new LythonEngine().Compile("import filecmp\nreturn filecmp.cmp(\"/a.bin\", \"/b.bin\", shallow=False)\n");
        Assert.True(script.IsValid);
        var payload = new byte[100000];
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var host = new CountingHost();
        host.SeedBytes("/a.bin", payload);
        host.SeedBytes("/b.bin", payload);
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.Equal(0, host.BinaryReads);

        var host2 = new CountingHost();
        host2.SeedBytes("/a.bin", payload);
        host2.SeedBytes("/b.bin", payload);
        var asyncResult = await script.RunAsync(host2, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.Equal(0, host2.BinaryReads);
    }

    [Fact]
    public async Task MissingFileSkipsPrereserve()
    {
        // Unknown lengths fall through to the regular missing-file error even
        // under a tiny budget; the prereserve must not mask it.
        var script = new LythonEngine().Compile("return len(open(\"/nope.txt\").read())\n");
        Assert.True(script.IsValid);
        var tiny = new LythonRunOptions { MaxExecutionMemoryBytes = 1024 };
        var tinyHost = new CountingHost();
        var tinyResult = script.Run(tinyHost, tiny);
        Assert.False(tinyResult.Success);
        Assert.NotNull(tinyResult.Failure);
        Assert.NotEqual("MemoryError", tinyResult.Failure?.ExceptionType);

        var roomyHost = new CountingHost();
        var roomyResult = script.Run(roomyHost);
        Assert.False(roomyResult.Success);
        Assert.Equal(tinyResult.Failure?.ExceptionType, roomyResult.Failure?.ExceptionType);
    }
}