using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BinaryFileWindowAndLifecycleTests
{
    [Fact]
    public async Task SequentialBatchesUseBoundedManagedReadWindows()
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
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        var baseline = budget.ReservedBytes;
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        foreach (var asynchronous in new[] { false, true })
        {
            var reads = 0;
            var total = 0;
            for (var batch = 0; batch < 256; batch++)
            {
                var host = new MockLythonHost();
                host.SeedBytes("/source.bin", new byte[16 * 1024]);
                var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(128 * 1024) };
                using var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("16384\n", result.StandardOutput);
                reads += host.RangedReadCount;
                total += 16 * 1024;
                Assert.InRange(host.MaxRangeBytesServed, 1, 16 * 1024);
                result.Dispose();
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            Assert.Equal(4194304, total);
            Assert.True(reads > 250);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task LinesAndSizedReadsCrossWindowsWithoutTranslation()
    {
        var payload = Enumerable.Repeat((byte)'A', 16 * 1024 - 1)
            .Concat(new byte[] { 13, 10, 255, 0, 10, (byte)'B' }).ToArray();
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
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
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
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

    [Theory]
    [InlineData("open('/output.bin','wb').write(b'abc')", true)]
    [InlineData("open('/output.bin','ab').write(b'abc')", true)]
    [InlineData("f=open('/output.bin','wb'); f.write(b'abc'); return 7", true)]
    [InlineData("f=open('/output.bin','wb'); f.write(b'ab'); f.flush(); f.write(b'c')", true)]
    [InlineData("f=open('/output.bin','wb'); f.write(b'abc'); raise ValueError('original')", false)]
    public async Task UnclosedBinaryWritersFollowExecutionEndPublication(string source, bool success)
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
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
        var result = await new LythonEngine(Utf8RegexProvider.Instance).RunAsync("open('/output.bin','wb').write(b'abc')", host);
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
        public async ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest request, LythonFileOpenOptions options,
            ILythonMemoryBudget budget, CancellationToken token)
            => new WindowFile(await Inner.OpenFileAsync(request, options, budget, token), MaximumChunk);
        private sealed class WindowFile(ILythonByteFile file, int chunk) : ILythonByteFile, ILythonSynchronousHostCapability
        {
            public bool CompletesSynchronously => true;
            public long Position => file.Position;
            public long? LengthHint => file.LengthHint;
            public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token) => file.ReadAsync(buffer[..Math.Min(buffer.Length, chunk)], token);
            public ValueTask<LythonFileWriteResult> WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token) => file.WriteAsync(buffer, token);
            public ValueTask FlushAsync(CancellationToken token) => file.FlushAsync(token);
            public ValueTask DisposeAsync() => file.DisposeAsync();
        }
        public async ValueTask<LythonPathStat> StatAsync(string path, CancellationToken token)
        {
            var stat = await Inner.StatAsync(path, token);
            return ReportedSize is { } size && stat.IsFile
                ? new LythonPathStat(stat.Kind, size, stat.ModifiedAtTimestamp) : stat;
        }
        public ValueTask<bool> ExistsAsync(string path, CancellationToken token) => Inner.ExistsAsync(path, token);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken token) => Inner.ListDirAsync(path, token);
        public ValueTask MkDirAsync(string path, CancellationToken token) => Inner.MkDirAsync(path, token);
        public ValueTask RemoveAsync(string path, CancellationToken token) => Inner.RemoveAsync(path, token);
        public ValueTask CopyAsync(string source, string destination, CancellationToken token) => Inner.CopyAsync(source, destination, token);
        public ValueTask MoveAsync(string source, string destination, CancellationToken token) => Inner.MoveAsync(source, destination, token);
    }
}
