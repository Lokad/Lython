using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BinaryFileWindowAndLifecycleTests
{
    [Fact]
    public async Task SequentialScanUsesBoundedNativeWindows()
    {
        var source = """
            count = 0
            with open('/source.bin', 'rb') as f:
                while True:
                    piece = f.read(4096)
                    if not piece: break
                    count += len(piece)
            print(count)
            """;
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            host.SeedBytes("/source.bin", new byte[4 * 1024 * 1024]);
            var options = new LythonRunOptions { MaxExecutionMemoryBytes = 128 * 1024 };
            var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("4194304\n", result.StandardOutput);
            Assert.True(host.RangedReadCount > 250);
            Assert.InRange(host.MaxRangeBytesServed, 1, 16 * 1024);
        }
    }

    [Fact]
    public async Task LinesAndSizedReadsCrossWindowsWithoutTranslation()
    {
        var payload = Enumerable.Repeat((byte)'A', 16 * 1024 - 1)
            .Concat(new byte[] { 13, 10, 255, 0, 10, (byte)'B' }).ToArray();
        var script = new LythonEngine().Compile("""
            with open('/source.bin','rb') as f:
                line = f.readline()
                print(len(line),line[-2:].hex(),f.tell())
                print(f.read(2).hex(),f.readline().hex(),f.read().hex())
            """);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            host.SeedBytes("/source.bin", payload);
            var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("16385 0d0a 16385\nff00 0a 42\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task ShortHostWindowsDoNotMeanEndOfFile()
    {
        var script = new LythonEngine().Compile("""
            with open('/source.bin','rb') as f:
                print(f.read(7).hex(),f.tell())
                print(f.read(5).hex(),f.tell(),f.read().hex())
            """);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new WindowHost { MaximumChunk = 3 };
            host.Inner.SeedBytes("/source.bin", Enumerable.Range(0, 10).Select(i => (byte)i).ToArray());
            var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("00010203040506 7\n070809 10 \n", result.StandardOutput);
            Assert.True(host.Inner.RangedReadCount >= 5);
        }
    }

    [Fact]
    public async Task StaleStatCannotBypassCumulativeReadLimit()
    {
        var script = new LythonEngine().Compile("with open('/source.bin','rb') as f:\n    print(f.read())");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new WindowHost { MaximumChunk = 3, ReportedSize = 1 };
            host.Inner.SeedBytes("/source.bin", new byte[10]);
            var options = new LythonRunOptions { MaxHostReadBytes = 5 };
            var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
            Assert.False(result.Success);
            Assert.Contains("host binary read exceeded maximum bytes", result.Failure?.Message);
            Assert.Equal("", result.StandardOutput);
        }
    }

    [Theory]
    [InlineData("open('/output.bin','wb').write(b'abc')", true)]
    [InlineData("open('/output.bin','ab').write(b'abc')", true)]
    [InlineData("f=open('/output.bin','wb'); f.write(b'abc'); return 7", true)]
    [InlineData("f=open('/output.bin','wb'); f.write(b'ab'); f.flush(); f.write(b'c')", true)]
    [InlineData("f=open('/output.bin','wb'); f.write(b'abc'); raise ValueError('original')", false)]
    public async Task UnclosedBinaryWritersFollowExecutionEndPublication(string source, bool success)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
            Assert.Equal(success, result.Success);
            Assert.Equal(new byte[] { 97, 98, 99 }, host.ReadBytes("/output.bin"));
            if (!success) Assert.Contains("original", result.Failure?.Message);
        }
    }

    [Fact]
    public async Task UnclosedBinaryWriterAwaitsPublication()
    {
        var host = new DelayedLythonHost();
        var result = await new LythonEngine().RunAsync("open('/output.bin','wb').write(b'abc')", host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new byte[] { 97, 98, 99 }, host.ReadBytes("/output.bin"));
        Assert.True(host.CompletedAsynchronously >= 2);
    }

    private sealed class WindowHost : ILythonHost, ILythonSynchronousHostCapability
    {
        public MockLythonHost Inner { get; } = new();
        public int MaximumChunk { get; init; } = int.MaxValue;
        public long? ReportedSize { get; init; }
        public bool CompletesSynchronously => true;
        public string Cwd => Inner.Cwd;
        public DateTimeOffset LocalNow => Inner.LocalNow;
        public DateTimeOffset UtcNow => Inner.UtcNow;
        public ValueTask<ReadOnlyMemory<byte>> ReadBytesRangeAsync(string path, long offset, int count, CancellationToken token)
            => Inner.ReadBytesRangeAsync(path, offset, Math.Min(count, MaximumChunk), token);
        public async ValueTask<LythonPathStat> StatAsync(string path, CancellationToken token)
        {
            var stat = await Inner.StatAsync(path, token);
            return ReportedSize is { } size && stat.IsFile
                ? new LythonPathStat(stat.Kind, size, stat.ModifiedAtTimestamp) : stat;
        }
        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken token) => Inner.ReadTextUtf8Async(path, token);
        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> text, CancellationToken token) => Inner.WriteTextUtf8Async(path, text, token);
        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> text, CancellationToken token) => Inner.AppendTextUtf8Async(path, text, token);
        public ValueTask<bool> ExistsAsync(string path, CancellationToken token) => Inner.ExistsAsync(path, token);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken token) => Inner.ListDirAsync(path, token);
        public ValueTask MkDirAsync(string path, CancellationToken token) => Inner.MkDirAsync(path, token);
        public ValueTask RemoveAsync(string path, CancellationToken token) => Inner.RemoveAsync(path, token);
        public ValueTask CopyAsync(string source, string destination, CancellationToken token) => Inner.CopyAsync(source, destination, token);
        public ValueTask MoveAsync(string source, string destination, CancellationToken token) => Inner.MoveAsync(source, destination, token);
    }
}
