namespace Lokad.Lython.PublicApi.Tests;

public sealed class HostAppendFallbackTests
{
    [Fact]
    public async Task DefaultAppendCreatesMissingFile()
    {
        ILythonHost host = new FallbackHost();

        await host.AppendBytesAsync("/new.bin", new byte[] { 1, 2, 3 }, CancellationToken.None);

        Assert.Equal(new byte[] { 1, 2, 3 }, ((FallbackHost)host).ReadRaw("/new.bin"));
    }

    [Fact]
    public async Task DefaultAppendPreservesLargePrefix()
    {
        ILythonHost host = new FallbackHost();
        var prefix = new byte[1024 * 1024];
        new Random(42).NextBytes(prefix);
        ((FallbackHost)host).SeedRaw("/big.bin", prefix);

        await host.AppendBytesAsync("/big.bin", new byte[] { 9, 8, 7 }, CancellationToken.None);

        var combined = ((FallbackHost)host).ReadRaw("/big.bin");
        Assert.Equal(prefix.Length + 3, combined.Length);
        Assert.Equal(prefix, combined[..prefix.Length]);
        Assert.Equal(new byte[] { 9, 8, 7 }, combined[prefix.Length..]);
    }

    [Fact]
    public async Task DefaultAppendHonorsCancellation()
    {
        ILythonHost host = new FallbackHost();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => host.AppendBytesAsync("/any.bin", new byte[] { 1 }, canceled.Token).AsTask());
    }

    [Fact]
    public async Task DefaultAppendComposesStatReadWriteInOrder()
    {
        var host = new FallbackHost();
        host.SeedRaw("/f.bin", new byte[] { 1 });

        await ((ILythonHost)host).AppendBytesAsync("/f.bin", new byte[] { 2 }, CancellationToken.None);

        Assert.Equal(new[] { "stat", "read", "write" }, host.CallLog);
        Assert.Equal(new byte[] { 1, 2 }, host.ReadRaw("/f.bin"));
    }

    [Fact]
    public async Task DefaultAppendLosesConcurrentReplacement()
    {
        var host = new FallbackHost();
        host.SeedRaw("/f.bin", new byte[] { 1, 1, 1, 1 });
        host.OnWriteBytes = () => host.SeedRaw("/f.bin", new byte[] { 2, 2, 2, 2 });

        await ((ILythonHost)host).AppendBytesAsync("/f.bin", new byte[] { 3 }, CancellationToken.None);

        Assert.Equal(new byte[] { 1, 1, 1, 1, 3 }, host.ReadRaw("/f.bin"));
    }

    [Fact]
    public async Task DefaultAppendPropagatesHostWriteFailure()
    {
        var host = new FallbackHost { FailWrites = true };
        host.SeedRaw("/boom.bin", new byte[] { 1 });

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ((ILythonHost)host).AppendBytesAsync("/boom.bin", new byte[] { 2 }, CancellationToken.None).AsTask());

        Assert.Contains("write boom", failure.Message, StringComparison.Ordinal);
        Assert.Equal(new byte[] { 1 }, host.ReadRaw("/boom.bin"));
    }

    [Fact]
    public async Task DefaultAppendPropagatesHostReadFailure()
    {
        var host = new FallbackHost { FailReads = true };
        host.SeedRaw("/boom.bin", new byte[] { 1 });

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ((ILythonHost)host).AppendBytesAsync("/boom.bin", new byte[] { 2 }, CancellationToken.None).AsTask());

        Assert.Contains("read boom", failure.Message, StringComparison.Ordinal);
    }

    private sealed class FallbackHost : ILythonHost
    {
        private static readonly DateTimeOffset Timestamp = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

        public bool FailReads { get; set; }

        public bool FailWrites { get; set; }

        public Action? OnWriteBytes { get; set; }

        public List<string> CallLog { get; } = new();

        public string Cwd => "/";

        public DateTimeOffset LocalNow => Timestamp;

        public DateTimeOffset UtcNow => Timestamp;

        public void SeedRaw(string path, byte[] payload) => _files[path] = payload;

        public byte[] ReadRaw(string path) => _files[path];

        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken) =>
            throw new LythonHostCapabilityUnavailableException("text file I/O");

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) =>
            throw new LythonHostCapabilityUnavailableException("text file I/O");

        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) =>
            throw new LythonHostCapabilityUnavailableException("text file I/O");

        public ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailReads)
            {
                throw new InvalidOperationException("read boom");
            }

            CallLog.Add("read");
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(_files[path]);
        }

        public ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailWrites)
            {
                throw new InvalidOperationException("write boom");
            }

            OnWriteBytes?.Invoke();
            CallLog.Add("write");
            _files[path] = bytes.ToArray();
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_files.ContainsKey(path));
        }

        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<string> names = Array.Empty<string>();
            return ValueTask.FromResult(names);
        }

        public ValueTask MkDirAsync(string path, CancellationToken cancellationToken) =>
            throw new NotSupportedException($"Fallback test host cannot create directory {path}.");

        public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _files.Remove(path);
            return ValueTask.CompletedTask;
        }

        public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _files.Add(destination, _files[source].ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _files.Add(destination, _files[source]);
            _files.Remove(source);
            return ValueTask.CompletedTask;
        }

        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallLog.Add("stat");
            var stat = _files.TryGetValue(path, out var bytes)
                ? new LythonPathStat(LythonPathKind.File, bytes.Length, Timestamp)
                : new LythonPathStat(LythonPathKind.Missing, 0, null);
            return ValueTask.FromResult(stat);
        }
    }
}
