using System.Text;

namespace Lokad.Lython.Benchmarks;

internal sealed class BenchmarkHost : ILythonHost, ILythonSynchronousHostCapability
{
    public bool CompletesSynchronously => true;
    private readonly Dictionary<string, FileData> _files = new(StringComparer.Ordinal);

    private sealed class FileData(byte[] bytes) { public byte[] Bytes = bytes; }
    private void SetBytes(string path, byte[] bytes)
    {
        if (_files.TryGetValue(path, out var file)) file.Bytes = bytes;
        else _files[path] = new FileData(bytes);
    }
    public ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest request, LythonFileOpenOptions options,
        ILythonMemoryBudget budget, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var owner = budget.CreateScope();
        try
        {
            var estimate = checked(512L + 4L * (request.Path.Length + request.Cwd.Length));
            if (!owner.TryReserve(estimate, out _))
                throw new LythonHostException(LythonHostFailureKind.MemoryAdmission, "file acquisition refused", requestedBytes: estimate);
            var path = ResolvePath(request);
            if (!_files.TryGetValue(path, out var data) && !options.Create)
                throw new LythonHostException(LythonHostFailureKind.NotFound, "No such file: " + path);
            if (data is not null && options.Exclusive)
                throw new LythonHostException(LythonHostFailureKind.AlreadyExists, "File exists: " + path);
            var file = new ByteFile(data ?? new FileData([]), owner, options);
            if (data is null) _files.Add(path, file.Data);
            if (options.Truncate) file.Data.Bytes = [];
            return ValueTask.FromResult<ILythonByteFile>(file);
        }
        catch { owner.Dispose(); throw; }
    }
    private sealed class ByteFile(FileData data, ILythonMemoryScope owner, LythonFileOpenOptions options)
        : ILythonByteFile, ILythonSynchronousHostCapability
    {
        public FileData Data { get; } = data;
        public bool CompletesSynchronously => true;
        private bool _closed;
        public long Position { get; private set; } = options.Append ? data.Bytes.Length : 0;
        public long? LengthHint => Data.Bytes.Length;
        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); ObjectDisposedException.ThrowIf(_closed, this);
            var count = (int)Math.Min(buffer.Length, Math.Max(0, Data.Bytes.LongLength - Position));
            Data.Bytes.AsMemory((int)Math.Min(Position, Data.Bytes.Length), count).CopyTo(buffer);
            Position += count; return ValueTask.FromResult(count);
        }
        public ValueTask<LythonFileWriteResult> WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); ObjectDisposedException.ThrowIf(_closed, this);
            var position = options.Append ? Data.Bytes.Length : checked((int)Position);
            var bytes = new byte[Math.Max(Data.Bytes.Length, checked(position + buffer.Length))];
            Data.Bytes.CopyTo(bytes, 0); buffer.CopyTo(bytes.AsMemory(position));
            Data.Bytes = bytes; Position = position + buffer.Length;
            return ValueTask.FromResult(new LythonFileWriteResult(buffer.Length));
        }
        public ValueTask RewindAndTruncateAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); ObjectDisposedException.ThrowIf(_closed, this);
            if (options.Access == LythonFileAccess.Read) throw new LythonHostException(LythonHostFailureKind.PermissionDenied, "File not open for writing");
            Data.Bytes = [];
            Position = 0;
            return ValueTask.CompletedTask;
        }
        public ValueTask FlushAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() { if (!_closed) { _closed = true; owner.Dispose(); } return ValueTask.CompletedTask; }
    }

    public string Cwd => "/";

    public DateTimeOffset LocalNow => new(2024, 1, 2, 4, 4, 5, TimeSpan.FromHours(1));

    public DateTimeOffset UtcNow => new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);

    internal static string ResolvePath(LythonPathRequest request)
    {
        var path = request.Path.StartsWith('/') ? request.Path : request.Cwd.TrimEnd('/') + "/" + request.Path;
        if (path.Split('/').Any(part => part is "." or ".."))
            throw new LythonHostException(LythonHostFailureKind.UnsupportedOperation, "benchmark fixture paths require resolved segments");
        return path;
    }

    public void SeedBytes(string path, byte[] bytes) => SetBytes(path, bytes);
    public byte[] ReadBytes(string path) => _files[path].Bytes;

    public void SeedUtf8(string path, string text)
    {
        SetBytes(path, Encoding.UTF8.GetBytes(text));
    }

    public ValueTask<bool> ExistsAsync(LythonPathRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(request);
        return ValueTask.FromResult(_files.ContainsKey(path));
    }

    public ValueTask<IReadOnlyList<string>> ListDirAsync(LythonPathRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(request);
        return ValueTask.FromResult<IReadOnlyList<string>>(_files.Keys.Where(key => key.StartsWith(path, StringComparison.Ordinal)).ToArray());
    }

    public ValueTask MkDirAsync(LythonPathRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(request);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(LythonPathRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(request);
        _files.Remove(path);
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyAsync(LythonPathRequest sourceRequest, LythonPathRequest destinationRequest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = ResolvePath(sourceRequest); var destination = ResolvePath(destinationRequest);
        _files[destination] = new FileData(_files[source].Bytes.ToArray());
        return ValueTask.CompletedTask;
    }

    public ValueTask MoveAsync(LythonPathRequest sourceRequest, LythonPathRequest destinationRequest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = ResolvePath(sourceRequest); var destination = ResolvePath(destinationRequest);
        _files[destination] = _files[source];
        _files.Remove(source);
        return ValueTask.CompletedTask;
    }

    public ValueTask<LythonPathStat> StatAsync(LythonPathRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(request);
        if (!_files.TryGetValue(path, out var utf8))
        {
            return ValueTask.FromResult(new LythonPathStat(
                path == "/" ? LythonPathKind.Directory : LythonPathKind.Missing, 0, null));
        }

        return ValueTask.FromResult(new LythonPathStat(
            kind: LythonPathKind.File,
            size: utf8.Bytes.Length,
            modifiedAt: DateTimeOffset.UnixEpoch));
    }

    public ReadOnlyMemory<byte> ReadTextUtf8(string path)
        => _files[path].Bytes;
}
