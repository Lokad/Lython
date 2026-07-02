using System.Numerics;
using System.Text;

namespace Lokad.Lython.Tests.Harness;

internal sealed class MockLythonHost : ILythonHost
{
    private const string MockTimestamp = "1970-01-01T00:00:00Z";
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _binaryFiles = new(StringComparer.Ordinal);
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _listDirFailures = new(StringComparer.Ordinal);
    private readonly MockTextOutput _stdout = new();
    private readonly MockTextOutput _stderr = new();
    private MockTextInput? _stdin;
    private readonly MockSubprocessRunner _subprocess = new();

    public MockLythonHost(string cwd = "/")
    {
        Cwd = NormalizeDirectory(cwd);
        _directories.Add("/");
        EnsureDirectory(Cwd);
        UtcNow = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        LocalNow = UtcNow.ToOffset(TimeSpan.FromHours(1));
    }

    public string Cwd { get; }

    public DateTimeOffset LocalNow { get; set; }

    public DateTimeOffset UtcNow { get; set; }

    public ILythonTextInput? StandardInput => _stdin;

    public ILythonTextOutput? StandardOutput => _stdout;

    public ILythonTextOutput? StandardError => _stderr;

    public ILythonSubprocessRunner? SubprocessRunner => _subprocess.Enabled ? _subprocess : null;

    public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = NormalizePath(path);

        if (!_files.TryGetValue(path, out var text))
        {
            throw new InvalidOperationException($"File does not exist: {path}");
        }

        return ValueTask.FromResult<ReadOnlyMemory<byte>>(Utf8.GetBytes(text));
    }

    public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = Utf8.GetString(utf8.Span);
        ArgumentNullException.ThrowIfNull(text);

        path = NormalizePath(path);
        EnsureDirectory(ParentOf(path));
        _files[path] = text;
        return ValueTask.CompletedTask;
    }

    public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = Utf8.GetString(utf8.Span);
        ArgumentNullException.ThrowIfNull(text);

        path = NormalizePath(path);
        EnsureDirectory(ParentOf(path));
        _files[path] = _files.TryGetValue(path, out var current)
            ? current + text
            : text;
        return ValueTask.CompletedTask;
    }

    public ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = NormalizePath(path);

        if (!_binaryFiles.TryGetValue(path, out var payload))
        {
            throw new InvalidOperationException($"Binary file does not exist: {path}");
        }

        return ValueTask.FromResult<ReadOnlyMemory<byte>>(payload.ToArray());
    }

    public ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = NormalizePath(path);
        EnsureDirectory(ParentOf(path));
        _binaryFiles[path] = bytes.ToArray();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = NormalizePath(path);
        return ValueTask.FromResult(_files.ContainsKey(path) || _binaryFiles.ContainsKey(path) || _directories.Contains(path));
    }

    public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = NormalizeDirectory(path);

        if (_listDirFailures.TryGetValue(path, out var failure))
        {
            throw new InvalidOperationException(failure);
        }

        if (!_directories.Contains(path))
        {
            throw new InvalidOperationException($"Directory does not exist: {path}");
        }

        var prefix = path == "/" ? "/" : path + "/";
        var names = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in _files.Keys.Concat(_binaryFiles.Keys))
        {
            if (file.StartsWith(prefix, StringComparison.Ordinal))
            {
                var tail = file[prefix.Length..];
                var slash = tail.IndexOf('/');
                names.Add(slash >= 0 ? tail[..slash] : tail);
            }
        }

        foreach (var dir in _directories)
        {
            if (dir == path || !dir.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var tail = dir[prefix.Length..];
            if (tail.Length == 0)
            {
                continue;
            }

            var slash = tail.IndexOf('/');
            names.Add(slash >= 0 ? tail[..slash] : tail);
        }

        return ValueTask.FromResult<IReadOnlyList<string>>(names.ToArray());
    }

    public ValueTask MkDirAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = NormalizeDirectory(path);
        if (_directories.Contains(normalized) || _files.ContainsKey(normalized) || _binaryFiles.ContainsKey(normalized))
        {
            throw new InvalidOperationException($"Path already exists: {normalized}");
        }

        var parent = ParentOf(normalized);
        if (!_directories.Contains(parent))
        {
            throw new InvalidOperationException($"Parent directory does not exist: {parent}");
        }

        _directories.Add(normalized);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = NormalizePath(path);

        if (_files.Remove(path) || _binaryFiles.Remove(path))
        {
            return ValueTask.CompletedTask;
        }

        var dir = NormalizeDirectory(path);
        if (!_directories.Contains(dir))
        {
            throw new InvalidOperationException($"Path does not exist: {path}");
        }

        if (dir == "/")
        {
            throw new InvalidOperationException("Cannot remove root directory.");
        }

        var prefix = dir + "/";
        if (_files.Keys.Any(k => k.StartsWith(prefix, StringComparison.Ordinal)) ||
            _binaryFiles.Keys.Any(k => k.StartsWith(prefix, StringComparison.Ordinal)) ||
            _directories.Any(d => d.StartsWith(prefix, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Directory is not empty: {dir}");
        }

        _directories.Remove(dir);
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        source = NormalizePath(source);
        destination = NormalizePath(destination);

        if (_files.TryGetValue(source, out var text))
        {
            if (Exists(destination))
            {
                throw new InvalidOperationException($"Destination already exists: {destination}");
            }

            EnsureDirectory(ParentOf(destination));
            _files[destination] = text;
            return ValueTask.CompletedTask;
        }

        if (!_binaryFiles.TryGetValue(source, out var payload))
        {
            throw new InvalidOperationException($"File does not exist: {source}");
        }

        if (Exists(destination))
        {
            throw new InvalidOperationException($"Destination already exists: {destination}");
        }

        EnsureDirectory(ParentOf(destination));
        _binaryFiles[destination] = payload.ToArray();
        return ValueTask.CompletedTask;
    }

    public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        source = NormalizePath(source);
        destination = NormalizePath(destination);

        if (_files.TryGetValue(source, out var text))
        {
            if (Exists(destination))
            {
                throw new InvalidOperationException($"Destination already exists: {destination}");
            }

            EnsureDirectory(ParentOf(destination));
            _files.Remove(source);
            _files[destination] = text;
            return ValueTask.CompletedTask;
        }

        if (!_binaryFiles.TryGetValue(source, out var payload))
        {
            throw new InvalidOperationException($"File does not exist: {source}");
        }

        if (Exists(destination))
        {
            throw new InvalidOperationException($"Destination already exists: {destination}");
        }

        EnsureDirectory(ParentOf(destination));
        _binaryFiles.Remove(source);
        _binaryFiles[destination] = payload;
        return ValueTask.CompletedTask;
    }

    public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = NormalizePath(path);

        if (_files.TryGetValue(path, out var text))
        {
            return ValueTask.FromResult(new LythonPathStat(
                Exists: true,
                IsFile: true,
                IsDir: false,
                Size: new BigInteger(Utf8.GetByteCount(text)),
                ModifiedAt: MockTimestamp));
        }

        if (_binaryFiles.TryGetValue(path, out var payload))
        {
            return ValueTask.FromResult(new LythonPathStat(
                Exists: true,
                IsFile: true,
                IsDir: false,
                Size: new BigInteger(payload.Length),
                ModifiedAt: MockTimestamp));
        }

        var dir = NormalizeDirectory(path);
        if (_directories.Contains(dir))
        {
            return ValueTask.FromResult(new LythonPathStat(
                Exists: true,
                IsFile: false,
                IsDir: true,
                Size: BigInteger.Zero,
                ModifiedAt: MockTimestamp));
        }

        return ValueTask.FromResult(new LythonPathStat(
            Exists: false,
            IsFile: false,
            IsDir: false,
            Size: BigInteger.Zero,
            ModifiedAt: MockTimestamp));
    }

    public bool Exists(string path) => ExistsAsync(path, CancellationToken.None).GetAwaiter().GetResult();

    public IReadOnlyList<string> ListDir(string path) => ListDirAsync(path, CancellationToken.None).GetAwaiter().GetResult();

    public void MkDir(string path) => MkDirAsync(path, CancellationToken.None).GetAwaiter().GetResult();

    public void Remove(string path) => RemoveAsync(path, CancellationToken.None).GetAwaiter().GetResult();

    public void Copy(string source, string destination) => CopyAsync(source, destination, CancellationToken.None).GetAwaiter().GetResult();

    public void Move(string source, string destination) => MoveAsync(source, destination, CancellationToken.None).GetAwaiter().GetResult();

    public LythonPathStat Stat(string path) => StatAsync(path, CancellationToken.None).GetAwaiter().GetResult();

    public void SeedFile(string path, string text)
    {
        WriteText(path, text);
    }

    public void SeedWorkbook(string path, byte[] payload)
    {
        WriteBytesAsync(path, payload, CancellationToken.None).GetAwaiter().GetResult();
    }

    public byte[] ReadWorkbook(string path)
    {
        return ReadBytesAsync(path, CancellationToken.None).GetAwaiter().GetResult().ToArray();
    }

    public void FailListDir(string path, string message)
    {
        _listDirFailures[NormalizeDirectory(path)] = message;
    }

    public void SeedStandardInput(string text)
    {
        _stdin = new MockTextInput(text);
    }

    public string CapturedStandardOutput() => _stdout.Text;

    public string CapturedStandardError() => _stderr.Text;

    public void EnableSubprocess() => _subprocess.Enabled = true;

    public void CompleteSubprocessAsynchronously() => _subprocess.CompleteAsynchronously = true;

    public void SeedSubprocessResult(IReadOnlyList<string> args, int returnCode, string stdout = "", string stderr = "")
    {
        _subprocess.SeedResult(args, returnCode, stdout, stderr);
    }

    public LythonSubprocessRequest? LastSubprocessRequest => _subprocess.LastRequest;

    public bool SubprocessCompletedAsynchronously => _subprocess.CompletedAsynchronously;

    public string ReadText(string path)
    {
        return Utf8.GetString(ReadTextUtf8Async(path, CancellationToken.None).GetAwaiter().GetResult().Span);
    }

    public void WriteText(string path, string text)
    {
        WriteTextUtf8Async(path, Utf8.GetBytes(text), CancellationToken.None).GetAwaiter().GetResult();
    }

    public void AppendText(string path, string text)
    {
        AppendTextUtf8Async(path, Utf8.GetBytes(text), CancellationToken.None).GetAwaiter().GetResult();
    }

    private void EnsureDirectory(string path)
    {
        path = NormalizeDirectory(path);
        if (_directories.Contains(path))
        {
            return;
        }

        var parent = ParentOf(path);
        if (!_directories.Contains(parent))
        {
            EnsureDirectory(parent);
        }

        _directories.Add(path);
    }

    private string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var absolute = path.StartsWith("/", StringComparison.Ordinal)
            ? path
            : Combine(Cwd, path);

        var parts = new List<string>();
        foreach (var rawPart in absolute.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (rawPart == ".")
            {
                continue;
            }

            if (rawPart == "..")
            {
                if (parts.Count > 0)
                {
                    parts.RemoveAt(parts.Count - 1);
                }
                continue;
            }

            parts.Add(rawPart);
        }

        return "/" + string.Join("/", parts);
    }

    private string NormalizeDirectory(string path)
    {
        var normalized = NormalizePath(path);
        return normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal)
            ? normalized.TrimEnd('/')
            : normalized;
    }

    private static string ParentOf(string path)
    {
        path = path.TrimEnd('/');
        var slash = path.LastIndexOf('/');
        return slash <= 0 ? "/" : path[..slash];
    }

    private static string Combine(string left, string right)
    {
        return left.EndsWith("/", StringComparison.Ordinal)
            ? left + right
            : left + "/" + right;
    }

    private sealed class MockTextInput : ILythonTextInput
    {
        private readonly string[] _lines;
        private int _index;

        public MockTextInput(string text)
        {
            _lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadToEndUtf8Async(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_index >= _lines.Length)
            {
                return ValueTask.FromResult<ReadOnlyMemory<byte>>(ReadOnlyMemory<byte>.Empty);
            }

            var remaining = string.Join("\n", _lines[_index..]);
            _index = _lines.Length;
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(Utf8.GetBytes(remaining));
        }

        public ValueTask<ReadOnlyMemory<byte>?> ReadLineUtf8Async(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_index >= _lines.Length)
            {
                return ValueTask.FromResult<ReadOnlyMemory<byte>?>(null);
            }

            var line = _lines[_index++];
            return ValueTask.FromResult<ReadOnlyMemory<byte>?>(Utf8.GetBytes(line + "\n"));
        }
    }

    private sealed class MockTextOutput : ILythonTextOutput
    {
        private readonly StringBuilder _builder = new();

        public string Text => _builder.ToString();

        public ValueTask WriteUtf8Async(ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _builder.Append(Utf8.GetString(utf8.Span));
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MockSubprocessRunner : ILythonSubprocessRunner
    {
        private readonly Dictionary<string, LythonSubprocessResult> _results = new(StringComparer.Ordinal);

        public bool Enabled { get; set; }

        public bool CompleteAsynchronously { get; set; }

        public bool CompletedAsynchronously { get; private set; }

        public LythonSubprocessRequest? LastRequest { get; private set; }

        public void SeedResult(IReadOnlyList<string> args, int returnCode, string stdout, string stderr)
        {
            _results[Key(args)] = new LythonSubprocessResult(returnCode, Utf8.GetBytes(stdout), Utf8.GetBytes(stderr));
        }

        public ValueTask<LythonSubprocessResult> RunAsync(LythonSubprocessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CompleteAsynchronously)
            {
                return RunDelayedAsync(request, cancellationToken);
            }

            return ValueTask.FromResult(Run(request));
        }

        private async ValueTask<LythonSubprocessResult> RunDelayedAsync(LythonSubprocessRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(25, cancellationToken);
            CompletedAsynchronously = true;
            return Run(request);
        }

        private LythonSubprocessResult Run(LythonSubprocessRequest request)
        {
            LastRequest = request;
            if (!_results.TryGetValue(Key(request.Args), out var result))
            {
                throw new InvalidOperationException($"No subprocess result seeded for: {Key(request.Args)}");
            }

            return result;
        }

        private static string Key(IReadOnlyList<string> args) => string.Join("\u001F", args);
    }
}
