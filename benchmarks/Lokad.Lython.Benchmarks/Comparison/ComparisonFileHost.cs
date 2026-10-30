namespace Lokad.Lython.Benchmarks.Comparison;

internal sealed class ComparisonFileHost : ILythonHost, ILythonSynchronousHostCapability
{
    private readonly string _root;
    public ComparisonFileFixture Fixture { get; }
    public ComparisonFileHost(ComparisonFileFixture fixture, string ownedDirectory)
    {
        OwnedWorkerProcess.ValidateFixtureDirectory(ownedDirectory);
        _root = Path.Combine(Path.GetFullPath(ownedDirectory), "job");
        Fixture = fixture;
    }
    public string Cwd => "/";
    public DateTimeOffset LocalNow => new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
    public DateTimeOffset UtcNow => LocalNow;
    public bool CompletesSynchronously => true;

    // Matched per-invocation reset/staging is included in both worker clocks.
    public void Reset()
    {
        var owner = Path.GetDirectoryName(_root)!;
        OwnedWorkerProcess.ValidateFixtureDirectory(owner);
        if (Path.GetFullPath(_root) != Path.Combine(owner, "job")) throw new InvalidDataException("Fixture root changed.");
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        Directory.CreateDirectory(_root);
        foreach (var input in Fixture.Inputs)
        {
            var path = FixturePath("/" + input.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, input.Value);
        }
    }

    public IReadOnlyDictionary<string, byte[]> ReadOutputs()
    {
        var actual = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(_root, file).Replace(Path.DirectorySeparatorChar, '/');
            if (!Fixture.Inputs.ContainsKey(relative) && !Fixture.Outputs.ContainsKey(relative))
                throw new InvalidDataException("Unexpected published file: " + relative);
        }
        foreach (var output in Fixture.Outputs)
        {
            var path = FixturePath("/" + output.Key);
            if (!File.Exists(path) || new FileInfo(path).Length != output.Value.Length)
                throw new InvalidDataException("Missing or wrong-length output file: " + output.Key);
            var payload = File.ReadAllBytes(path);
            if (!payload.AsSpan().SequenceEqual(output.Value))
                throw new InvalidDataException("Output bytes differ: " + output.Key);
            actual.Add(output.Key, payload);
        }
        return actual;
    }

    private string Resolve(LythonPathRequest request, CancellationToken token, bool creatingDirectory = false)
    {
        if (request.Path.Length == 0) throw new LythonHostException(LythonHostFailureKind.NotFound, "Empty path");
        var path = request.Path.StartsWith('/') ? request.Path : request.Cwd.TrimEnd('/') + "/" + request.Path;
        if (!path.StartsWith('/') || path.Contains('\\') || path.Contains('\0'))
            throw new LythonHostException(LythonHostFailureKind.PermissionDenied, "Invalid contained path");
        var current = _root;
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            token.ThrowIfCancellationRequested();
            if (!Directory.Exists(current))
                throw new LythonHostException(File.Exists(current) ? LythonHostFailureKind.NotDirectory : LythonHostFailureKind.NotFound, "Invalid intermediate directory");
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new LythonHostCapabilityUnavailableException("fixture symlinks");
            if (part == ".") continue;
            if (part == "..") { if (current != _root) current = Path.GetDirectoryName(current)!; }
            else
            {
                ComparisonFileFixture.ValidatePath(part);
                current = Path.Combine(current, part);
            }
        }
        if (request.Path.EndsWith('/') && !Directory.Exists(current) && (!creatingDirectory || File.Exists(current)))
            throw new LythonHostException(File.Exists(current) ? LythonHostFailureKind.NotDirectory : LythonHostFailureKind.NotFound, "Not a directory");
        if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            throw new LythonHostCapabilityUnavailableException("fixture symlinks");
        return current;
    }

    private string FixturePath(string path)
    {
        if (path == "/") return _root;
        if (!path.StartsWith('/')) throw new InvalidDataException("Expected contained absolute path.");
        ComparisonFileFixture.ValidatePath(path[1..]);
        var result = Path.GetFullPath(Path.Combine(_root, path[1..].Replace('/', Path.DirectorySeparatorChar)));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!result.StartsWith(_root + Path.DirectorySeparatorChar, comparison)) throw new InvalidDataException("Path escaped fixture root.");
        return result;
    }
    public ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest request, LythonFileOpenOptions options,
        ILythonMemoryBudget budget, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var owner = budget.CreateScope();
        FileStream? stream = null;
        try
        {
            var estimate = checked(1024L + 64L * (request.Path.Length + request.Cwd.Length + _root.Length));
            if (!owner.TryReserve(estimate, out _))
                throw new LythonHostException(LythonHostFailureKind.MemoryAdmission, "file acquisition refused", requestedBytes: estimate);
            var access = options.Access switch
            {
                LythonFileAccess.Read => FileAccess.Read,
                LythonFileAccess.Write => FileAccess.Write,
                _ => FileAccess.ReadWrite,
            };
            var mode = options.Exclusive ? FileMode.CreateNew
                : options.Truncate ? (options.Create ? FileMode.Create : FileMode.Truncate)
                : options.Create ? FileMode.OpenOrCreate : FileMode.Open;
            stream = new FileStream(Resolve(request, token), mode, access, FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 1, FileOptions.SequentialScan);
            if (options.Append) stream.Seek(0, SeekOrigin.End);
            return ValueTask.FromResult<ILythonByteFile>(new ByteFile(stream, owner, options.Append));
        }
        catch
        {
            try { stream?.Dispose(); }
            finally { owner.Dispose(); }
            throw;
        }
    }

    private sealed class ByteFile(FileStream stream, ILythonMemoryScope owner, bool append)
        : ILythonByteFile, ILythonSynchronousHostCapability
    {
        private bool _closed;
        public bool CompletesSynchronously => true;
        public long Position => stream.Position;
        public long? LengthHint => stream.Length;
        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return ValueTask.FromResult(stream.Read(buffer.Span));
        }
        public ValueTask<LythonFileWriteResult> WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                // This trusted benchmark host serializes fixture access.
                if (append) stream.Seek(0, SeekOrigin.End);
                stream.Write(buffer.Span);
                return ValueTask.FromResult(new LythonFileWriteResult(buffer.Length));
            }
            catch (IOException error)
            {
                return ValueTask.FromResult(new LythonFileWriteResult(null,
                    new LythonHostException(LythonHostFailureKind.Transport, "file write failed", error)));
            }
        }
        public ValueTask RewindAndTruncateAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); ObjectDisposedException.ThrowIf(_closed, this);
            stream.SetLength(0);
            stream.Position = 0;
            return ValueTask.CompletedTask;
        }
        public ValueTask FlushAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); stream.Flush(); return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync()
        {
            if (_closed) return ValueTask.CompletedTask;
            _closed = true;
            try { stream.Dispose(); }
            finally { owner.Dispose(); }
            return ValueTask.CompletedTask;
        }
    }
    public ValueTask<bool> ExistsAsync(LythonPathRequest path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { var p = Resolve(path, token); return ValueTask.FromResult(File.Exists(p) || Directory.Exists(p)); }
        catch (LythonHostException error) when (error.Kind is LythonHostFailureKind.NotFound or LythonHostFailureKind.NotDirectory)
        { return ValueTask.FromResult(false); }
    }
    public ValueTask<IReadOnlyList<string>> ListDirAsync(LythonPathRequest path, CancellationToken token) { token.ThrowIfCancellationRequested(); return ValueTask.FromResult<IReadOnlyList<string>>(Directory.EnumerateFileSystemEntries(Resolve(path, token)).Select(p => Path.GetFileName(p)).ToArray()); }
    public ValueTask MkDirAsync(LythonPathRequest path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var resolved = Resolve(path, token, creatingDirectory: true);
        if (Directory.Exists(resolved) || File.Exists(resolved)) throw new LythonHostException(LythonHostFailureKind.AlreadyExists, "Path already exists");
        Directory.CreateDirectory(resolved); return ValueTask.CompletedTask;
    }
    public ValueTask RemoveAsync(LythonPathRequest path, CancellationToken token) { token.ThrowIfCancellationRequested(); var p = Resolve(path, token); if (Directory.Exists(p)) Directory.Delete(p); else File.Delete(p); return ValueTask.CompletedTask; }
    public ValueTask RemoveFileAsync(LythonPathRequest path, bool missingOk, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var p = Resolve(path, token);
        if (!missingOk && !File.Exists(p) && !Directory.Exists(p)) throw new FileNotFoundException(p);
        File.Delete(p); return ValueTask.CompletedTask;
    }
    public ValueTask RemoveDirectoryAsync(LythonPathRequest path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Directory.Delete(Resolve(path, token)); return ValueTask.CompletedTask;
    }
    public ValueTask ReplaceAsync(LythonPathRequest source, LythonPathRequest destination, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var from = Resolve(source, token); var to = Resolve(destination, token);
        if (from == to && File.Exists(from)) return ValueTask.CompletedTask;
        File.Move(from, to, true); return ValueTask.CompletedTask;
    }
    public ValueTask CopyAsync(LythonPathRequest source, LythonPathRequest destination, CancellationToken token) { token.ThrowIfCancellationRequested(); File.Copy(Resolve(source, token), Resolve(destination, token), overwrite: true); return ValueTask.CompletedTask; }
    public ValueTask MoveAsync(LythonPathRequest source, LythonPathRequest destination, CancellationToken token) { token.ThrowIfCancellationRequested(); File.Move(Resolve(source, token), Resolve(destination, token)); return ValueTask.CompletedTask; }
    public ValueTask<LythonPathStat> StatAsync(LythonPathRequest path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            var p = Resolve(path, token);
            return ValueTask.FromResult(File.Exists(p) ? new LythonPathStat(LythonPathKind.File, new FileInfo(p).Length, null)
                : new LythonPathStat(Directory.Exists(p) ? LythonPathKind.Directory : LythonPathKind.Missing, 0, null));
        }
        catch (LythonHostException error) when (error.Kind == LythonHostFailureKind.NotFound)
        { return ValueTask.FromResult(new LythonPathStat(LythonPathKind.Missing, 0, null)); }
    }
}
