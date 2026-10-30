namespace Lokad.Lython.Tests.Harness;

internal sealed partial class MockLythonHost
{
    // The mock's persistent store is host-owned, independent of a command's
    // allowance. Open cursors retain entry identity across rename/replacement.
    private sealed class FileEntry(byte[] bytes) { public byte[] Bytes = bytes; }

    private void SetFileBytes(string path, byte[] bytes)
    {
        if (_files.TryGetValue(path, out var entry)) entry.Bytes = bytes;
        else _files[path] = new FileEntry(bytes);
    }

    public ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest request, LythonFileOpenOptions options,
        ILythonMemoryBudget budget, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var pathLength = checked((long)request.Path.Length + request.Cwd.Length);
        var acquisitionBytes = checked(256 + 32 * pathLength);
        using var reservation = budget.TryReserve(acquisitionBytes, out var admitted) ? admitted
            : throw new LythonHostException(LythonHostFailureKind.MemoryAdmission, "file acquisition refused", requestedBytes: acquisitionBytes);
        var scope = budget.CreateScope();
        try
        {
            var retainedBytes = checked(256 + 2 * pathLength);
            if (!scope.TryReserve(retainedBytes, out _))
                throw new LythonHostException(LythonHostFailureKind.MemoryAdmission, "file handle refused", requestedBytes: retainedBytes);
            var path = ResolveFilePath(request);
            if (options.Access != LythonFileAccess.Read && _writeBytesFailures.TryGetValue(path, out var failure))
                throw new LythonHostException(LythonHostFailureKind.Transport, failure);
            if (_directories.Contains(path)) throw new LythonHostException(LythonHostFailureKind.IsDirectory, "Is a directory: " + path);
            if (!_files.TryGetValue(path, out var entry))
            {
                if (!options.Create) throw new LythonHostException(LythonHostFailureKind.NotFound, "No such file: " + path);
                if (!_directories.Contains(ParentOf(path)))
                    throw new LythonHostException(LythonHostFailureKind.NotFound, "No such parent directory: " + path);
            }
            else if (options.Exclusive)
                throw new LythonHostException(LythonHostFailureKind.AlreadyExists, "File exists: " + path);
            var file = new MockByteFile(this, path, entry ?? new FileEntry([]), options, scope);
            if (entry is null) _files.Add(path, file.Entry);
            if (options.Truncate) file.Entry.Bytes = [];
            OpenFileCount++;
            return ValueTask.FromResult<ILythonByteFile>(file);
        }
        catch { scope.Dispose(); throw; }
    }

    public int OpenFileCount { get; private set; }
    public int ClosedFileCount { get; private set; }

    private string ResolveFilePath(LythonPathRequest request)
    {
        if (request.Path.Length == 0) throw new LythonHostException(LythonHostFailureKind.NotFound, "Empty file path");
        var absolute = request.Path.StartsWith('/') ? request.Path : Combine(request.Cwd, request.Path);
        var parts = new List<string>();
        foreach (var part in absolute.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var parent = "/" + string.Join('/', parts);
            if (!_directories.Contains(parent))
                throw new LythonHostException(_files.ContainsKey(parent) ? LythonHostFailureKind.NotDirectory : LythonHostFailureKind.NotFound,
                    "Invalid intermediate directory: " + parent);
            if (part == ".") continue;
            if (part == "..") { if (parts.Count != 0) parts.RemoveAt(parts.Count - 1); }
            else parts.Add(part);
        }
        var path = "/" + string.Join('/', parts);
        if (request.Path.EndsWith('/') && !_directories.Contains(path))
            throw new LythonHostException(_files.ContainsKey(path) ? LythonHostFailureKind.NotDirectory : LythonHostFailureKind.NotFound,
                "Not a directory: " + path);
        return path;
    }

    private sealed class MockByteFile(MockLythonHost host, string path, FileEntry entry,
        LythonFileOpenOptions options, ILythonMemoryScope scope) : ILythonByteFile, ILythonSynchronousHostCapability
    {
        public FileEntry Entry { get; } = entry;
        private bool _closed;
        public bool CompletesSynchronously => true;
        public long Position { get; private set; } = options.Append ? entry.Bytes.Length : 0;
        public long? LengthHint => Entry.Bytes.Length;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); ObjectDisposedException.ThrowIf(_closed, this);
            if (options.Access == LythonFileAccess.Write) throw new LythonHostException(LythonHostFailureKind.PermissionDenied, "File not open for reading");
            var count = (int)Math.Min(buffer.Length, Math.Max(0, Entry.Bytes.LongLength - Position));
            Entry.Bytes.AsMemory((int)Math.Min(Position, Entry.Bytes.Length), count).CopyTo(buffer);
            Position += count;
            host.RangedReadCount++;
            host.MaxRangeBytesServed = Math.Max(host.MaxRangeBytesServed, count);
            return ValueTask.FromResult(count);
        }

        public ValueTask<LythonFileWriteResult> WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); ObjectDisposedException.ThrowIf(_closed, this);
            if (options.Access == LythonFileAccess.Read) throw new LythonHostException(LythonHostFailureKind.PermissionDenied, "File not open for writing");
            if (host._writeBytesFailures.TryGetValue(path, out var failure) || host._nextWriteBytesFailures.Remove(path, out failure))
                return ValueTask.FromResult(new LythonFileWriteResult(0,
                    new LythonHostException(LythonHostFailureKind.Transport, failure)));
            var position = options.Append ? Entry.Bytes.Length : checked((int)Position);
            var length = Math.Max(Entry.Bytes.Length, checked(position + buffer.Length));
            var bytes = new byte[length];
            Entry.Bytes.CopyTo(bytes, 0);
            buffer.CopyTo(bytes.AsMemory(position));
            Entry.Bytes = bytes;
            Position = position + buffer.Length;
            return ValueTask.FromResult(new LythonFileWriteResult(buffer.Length));
        }

        public ValueTask FlushAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); ObjectDisposedException.ThrowIf(_closed, this);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (!_closed) { _closed = true; host.ClosedFileCount++; scope.Dispose(); }
            return ValueTask.CompletedTask;
        }
    }
}
