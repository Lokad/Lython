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
            var path = Resolve("/" + input.Key);
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
            var path = Resolve("/" + output.Key);
            if (!File.Exists(path) || new FileInfo(path).Length != output.Value.Length)
                throw new InvalidDataException("Missing or wrong-length output file: " + output.Key);
            var payload = File.ReadAllBytes(path);
            if (!payload.AsSpan().SequenceEqual(output.Value))
                throw new InvalidDataException("Output bytes differ: " + output.Key);
            actual.Add(output.Key, payload);
        }
        return actual;
    }

    private string Resolve(string path)
    {
        if (path == "/") return _root;
        if (!path.StartsWith('/')) throw new InvalidDataException("Expected contained absolute path.");
        ComparisonFileFixture.ValidatePath(path[1..]);
        var result = Path.GetFullPath(Path.Combine(_root, path[1..].Replace('/', Path.DirectorySeparatorChar)));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!result.StartsWith(_root + Path.DirectorySeparatorChar, comparison)) throw new InvalidDataException("Path escaped fixture root.");
        return result;
    }
    private ReadOnlyMemory<byte> Read(string path, long offset, int count, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (offset < 0 || count < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        using var file = File.OpenRead(Resolve(path));
        var length = checked((int)Math.Min(count, Math.Max(0, file.Length - offset)));
        var result = new byte[length];
        file.Position = Math.Min(offset, file.Length); file.ReadExactly(result);
        return result;
    }
    private ValueTask Write(string path, ReadOnlyMemory<byte> bytes, bool append, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var file = new FileStream(Resolve(path), append ? FileMode.Append : FileMode.Create, FileAccess.Write);
        file.Write(bytes.Span); return ValueTask.CompletedTask;
    }
    public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var length = new FileInfo(Resolve(path)).Length;
        if (length > ComparisonFileFixture.MaximumBytes) throw new InvalidDataException("Whole-file adapter read exceeds fixture bound.");
        return ValueTask.FromResult(Read(path, 0, checked((int)length), token));
    }
    public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8RangeAsync(string path, long offset, int count, CancellationToken token) => ValueTask.FromResult(Read(path, offset, count, token));
    public ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken token) => ReadTextUtf8Async(path, token);
    public ValueTask<ReadOnlyMemory<byte>> ReadBytesRangeAsync(string path, long offset, int count, CancellationToken token) => ReadTextUtf8RangeAsync(path, offset, count, token);
    public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> bytes, CancellationToken token) => Write(path, bytes, false, token);
    public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> bytes, CancellationToken token) => Write(path, bytes, true, token);
    public ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken token) => Write(path, bytes, false, token);
    public ValueTask AppendBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken token) => Write(path, bytes, true, token);
    public ValueTask<bool> ExistsAsync(string path, CancellationToken token) { token.ThrowIfCancellationRequested(); var p = Resolve(path); return ValueTask.FromResult(File.Exists(p) || Directory.Exists(p)); }
    public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken token) { token.ThrowIfCancellationRequested(); return ValueTask.FromResult<IReadOnlyList<string>>(Directory.EnumerateFileSystemEntries(Resolve(path)).Select(p => Path.GetFileName(p)).ToArray()); }
    public ValueTask MkDirAsync(string path, CancellationToken token) { token.ThrowIfCancellationRequested(); Directory.CreateDirectory(Resolve(path)); return ValueTask.CompletedTask; }
    public ValueTask RemoveAsync(string path, CancellationToken token) { token.ThrowIfCancellationRequested(); var p = Resolve(path); if (Directory.Exists(p)) Directory.Delete(p); else File.Delete(p); return ValueTask.CompletedTask; }
    public ValueTask RemoveFileAsync(string path, bool missingOk, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var p = Resolve(path);
        if (!missingOk && !File.Exists(p) && !Directory.Exists(p)) throw new FileNotFoundException(p);
        File.Delete(p); return ValueTask.CompletedTask;
    }
    public ValueTask RemoveDirectoryAsync(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Directory.Delete(Resolve(path)); return ValueTask.CompletedTask;
    }
    public ValueTask ReplaceAsync(string source, string destination, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var from = Resolve(source); var to = Resolve(destination);
        if (from == to && File.Exists(from)) return ValueTask.CompletedTask;
        File.Move(from, to, true); return ValueTask.CompletedTask;
    }
    public ValueTask CopyAsync(string source, string destination, CancellationToken token) { token.ThrowIfCancellationRequested(); File.Copy(Resolve(source), Resolve(destination)); return ValueTask.CompletedTask; }
    public ValueTask MoveAsync(string source, string destination, CancellationToken token) { token.ThrowIfCancellationRequested(); File.Move(Resolve(source), Resolve(destination)); return ValueTask.CompletedTask; }
    public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var p = Resolve(path);
        return ValueTask.FromResult(File.Exists(p) ? new LythonPathStat(LythonPathKind.File, new FileInfo(p).Length, null)
            : new LythonPathStat(Directory.Exists(p) ? LythonPathKind.Directory : LythonPathKind.Missing, 0, null));
    }
}
