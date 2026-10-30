using System.Numerics;
using System.Text;

namespace Lokad.Lython.Tests.Harness;

internal sealed partial class MockLythonHost : ILythonHost, ILythonSynchronousHostCapability
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly Dictionary<string, FileEntry> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _listDirFailures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _writeFailures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _nextWriteFailures = new(StringComparer.Ordinal);
    private readonly MockTextOutput _stdout = new();
    private readonly MockTextOutput _stderr = new();
    private ILythonTextOutput? _stderrOverride;
    private MockTextInput? _stdin;
    private readonly MockSubprocessRunner _subprocess = new();
    private ILythonTiming? _timing;

    public MockLythonHost()
        : this("/")
    {
    }

    public MockLythonHost(string cwd)
    {
        Cwd = NormalizeDirectory(cwd);
        _directories.Add("/");
        EnsureDirectory(Cwd);
        UtcNow = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        LocalNow = UtcNow.ToOffset(TimeSpan.FromHours(1));
    }

    public string Cwd { get; }

    public bool CompletesSynchronously => true;

    public DateTimeOffset LocalNow { get; set; }

    public DateTimeOffset UtcNow { get; set; }

    public ILythonTextInput? StandardInput => _stdin;

    public ILythonTextOutput? StandardOutput => _stdout;

    public ILythonTextOutput? StandardError => _stderrOverride ?? _stderr;

    public void SetStandardError(ILythonTextOutput output) => _stderrOverride = output;

    public ILythonSubprocessRunner? SubprocessRunner => _subprocess.Enabled ? _subprocess : null;

    public ILythonTiming? Timing => _timing;

    public MockTiming EnableTiming()
        => EnableTiming(0, 1);

    public MockTiming EnableTiming(long monotonicNanoseconds)
        => EnableTiming(monotonicNanoseconds, 1);

    public MockTiming EnableTiming(long monotonicNanoseconds, long resolutionNanoseconds)
    {
        var timing = new MockTiming(monotonicNanoseconds, resolutionNanoseconds);
        _timing = timing;
        return timing;
    }

    public void SetTiming(ILythonTiming? timing) => _timing = timing;

    public int RangedReadCount { get; private set; }
    public int MaxRangeBytesServed { get; private set; }

    private ValueTask WriteContent(string path, ReadOnlyMemory<byte> bytes, bool append, bool text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (text) _ = Utf8.GetCharCount(bytes.Span);
        path = NormalizePath(path);
        var failures = _writeFailures;
        if (failures.TryGetValue(path, out var failure)) throw new InvalidOperationException(failure);
        var next = _nextWriteFailures;
        if (next.Remove(path, out failure)) throw new InvalidOperationException(failure);
        EnsureDirectory(ParentOf(path));
        var entry = _files.GetValueOrDefault(path);
        var prefix = append ? entry?.Bytes : null;
        if (prefix is null) SetFileBytes(path, bytes.ToArray());
        else
        {
            var combined = new byte[checked(prefix.Length + bytes.Length)];
            prefix.CopyTo(combined, 0);
            bytes.CopyTo(combined.AsMemory(prefix.Length));
            SetFileBytes(path, combined);
        }
        return ValueTask.CompletedTask;
    }

    public async ValueTask<bool> ExistsAsync(LythonPathRequest request, CancellationToken cancellationToken)
    {
        try { return (await StatAsync(request, cancellationToken)).Exists; }
        catch (LythonHostException error) when (error.Kind == LythonHostFailureKind.NotDirectory) { return false; }
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

        foreach (var file in _files.Keys)
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
        if (_directories.Contains(normalized) || _files.ContainsKey(normalized))
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

        if (_files.Remove(path))
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
            _directories.Any(d => d.StartsWith(prefix, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Directory is not empty: {dir}");
        }

        _directories.Remove(dir);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveFileAsync(string path, bool missingOk, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = NormalizePath(path);
        if (_directories.Contains(NormalizeDirectory(path))) throw new IOException("Is a directory: " + path);
        if (!_files.Remove(path) && !missingOk)
            throw new FileNotFoundException("File does not exist: " + path);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = NormalizePath(path);
        if (_files.ContainsKey(path))
            throw new IOException("Not a directory: " + path);
        return RemoveAsync(path, cancellationToken);
    }

    public ValueTask ReplaceAsync(string source, string destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        source = NormalizePath(source); destination = NormalizePath(destination);
        if (_directories.Contains(NormalizeDirectory(source)))
            throw new LythonHostCapabilityUnavailableException("directory replacement");
        if (!_files.ContainsKey(source))
            throw new FileNotFoundException("File does not exist: " + source);
        if (source == destination) return ValueTask.CompletedTask;
        if (_directories.Contains(NormalizeDirectory(destination))) throw new IOException("Is a directory: " + destination);
        if (!_directories.Contains(ParentOf(destination))) throw new DirectoryNotFoundException(ParentOf(destination));
        _files[destination] = _files[source];
        _files.Remove(source);
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        source = NormalizePath(source); destination = NormalizePath(destination);
        if (!_files.TryGetValue(source, out var payload)) throw new InvalidOperationException($"File does not exist: {source}");
        if (Exists(destination)) throw new InvalidOperationException($"Destination already exists: {destination}");
        EnsureDirectory(ParentOf(destination));
        _files[destination] = new FileEntry(payload.Bytes.ToArray());
        return ValueTask.CompletedTask;
    }

    public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        source = NormalizePath(source); destination = NormalizePath(destination);
        if (!_files.TryGetValue(source, out var payload)) throw new InvalidOperationException($"File does not exist: {source}");
        if (Exists(destination)) throw new InvalidOperationException($"Destination already exists: {destination}");
        EnsureDirectory(ParentOf(destination));
        _files[destination] = payload;
        _files.Remove(source);
        return ValueTask.CompletedTask;
    }

    public ValueTask<LythonPathStat> StatAsync(LythonPathRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path;
        try { path = ResolveFilePath(request); }
        catch (LythonHostException error) when (error.Kind == LythonHostFailureKind.NotFound)
        { return ValueTask.FromResult(new LythonPathStat(LythonPathKind.Missing, 0, null)); }

        if (_files.TryGetValue(path, out var payload))
            return ValueTask.FromResult(new LythonPathStat(LythonPathKind.File, payload.Bytes.Length, DateTimeOffset.UnixEpoch));

        var dir = NormalizeDirectory(path);
        if (_directories.Contains(dir))
        {
            return ValueTask.FromResult(new LythonPathStat(
                kind: LythonPathKind.Directory,
                size: BigInteger.Zero,
                modifiedAt: DateTimeOffset.UnixEpoch));
        }

        return ValueTask.FromResult(new LythonPathStat(
            kind: LythonPathKind.Missing,
            size: BigInteger.Zero,
            modifiedAt: null));
    }

    public bool Exists(string path) => ExistsAsync(new LythonPathRequest(path, Cwd), CancellationToken.None).GetAwaiter().GetResult();

    public IReadOnlyList<string> ListDir(string path) => ListDirAsync(path, CancellationToken.None).GetAwaiter().GetResult();

    public void MkDir(string path) => MkDirAsync(path, CancellationToken.None).GetAwaiter().GetResult();

    public void Remove(string path) => RemoveAsync(path, CancellationToken.None).GetAwaiter().GetResult();

    public void Copy(string source, string destination) => CopyAsync(source, destination, CancellationToken.None).GetAwaiter().GetResult();

    public void Move(string source, string destination) => MoveAsync(source, destination, CancellationToken.None).GetAwaiter().GetResult();

    public LythonPathStat Stat(string path) => StatAsync(new LythonPathRequest(path, Cwd), CancellationToken.None).GetAwaiter().GetResult();

    public void SeedFile(string path, string text)
    {
        WriteText(path, text);
    }

    public void SeedRawTextUtf8(string path, byte[] utf8)
    {
        path = NormalizePath(path);
        EnsureDirectory(ParentOf(path));
        SetFileBytes(path, utf8.ToArray());
    }

    public void SeedWorkbook(string path, byte[] payload)
    {
        WriteContent(path, payload, append: false, text: false, CancellationToken.None).GetAwaiter().GetResult();
    }

    public void SeedBytes(string path, byte[] payload) => SeedWorkbook(path, payload);

    public byte[] ReadWorkbook(string path)
    {
        var normalized = NormalizePath(path);
        if (!_files.TryGetValue(normalized, out var file)) throw new InvalidOperationException($"File does not exist: {normalized}");
        return file.Bytes.ToArray();
    }

    public byte[] ReadBytes(string path) => ReadWorkbook(path);

    public void FailListDir(string path, string message)
    {
        _listDirFailures[NormalizeDirectory(path)] = message;
    }

    public void FailFileWrite(string path, string message) => _writeFailures[NormalizePath(path)] = message;

    public void FailNextFileWrite(string path, string message) => _nextWriteFailures[NormalizePath(path)] = message;

    public void ClearFileWriteFailures()
    {
        _writeFailures.Clear();
        _nextWriteFailures.Clear();
    }

    public void SeedStandardInput(string text)
    {
        _stdin = new MockTextInput(text);
    }

    public string CapturedStandardOutput() => _stdout.Text;

    /// <summary>
    /// Optional hook invoked synchronously on the run thread after each captured stdout write.
    /// Lets a test rendezvous with script progress (for example, cancelling mid-run)
    /// without wall-clock races. Null by default (no behavior change).
    /// </summary>
    public Action? OnStandardOutputWrite
    {
        get => _stdout.OnWrite;
        set => _stdout.OnWrite = value;
    }

    public string CapturedStandardError() => _stderr.Text;

    public void EnableSubprocess() => _subprocess.Enabled = true;

    public void UseSubprocessHandler(Func<LythonSubprocessRequest, CancellationToken, ValueTask<LythonSubprocessResult>> handler)
    {
        EnableSubprocess();
        _subprocess.Handler = handler;
    }

    public void EnableSubprocessShell()
    {
        EnableSubprocess();
        _subprocess.AllowsShellInvocation = true;
    }

    public void CompleteSubprocessAsynchronously() => _subprocess.CompleteAsynchronously = true;

    public Task PauseSubprocessUntilCancellation(IReadOnlyList<string> args)
        => _subprocess.PauseUntilCancellation(args);

    public void SeedSubprocessResult(IReadOnlyList<string> args, int returnCode)
        => SeedSubprocessResult(args, returnCode, string.Empty, string.Empty);

    public void SeedSubprocessResult(IReadOnlyList<string> args, int returnCode, string stdout)
        => SeedSubprocessResult(args, returnCode, stdout, string.Empty);

    public void SeedSubprocessResult(IReadOnlyList<string> args, int returnCode, string stdout, string stderr)
    {
        _subprocess.SeedResult(args, returnCode, stdout, stderr);
    }

    public void SeedSubprocessTimeout(IReadOnlyList<string> args) => _subprocess.SeedTimeout(args);

    public LythonSubprocessRequest? LastSubprocessRequest => _subprocess.LastRequest;

    public IReadOnlyList<LythonSubprocessRequest> SubprocessRequests => _subprocess.Requests;

    public bool SubprocessCompletedAsynchronously => _subprocess.CompletedAsynchronously;

    public string ReadText(string path)
    {
        var normalized = NormalizePath(path);
        if (!_files.TryGetValue(normalized, out var file)) throw new InvalidOperationException($"File does not exist: {normalized}");
        return Utf8.GetString(file.Bytes);
    }

    public void WriteText(string path, string text)
    {
        WriteContent(path, Utf8.GetBytes(text), append: false, text: true, CancellationToken.None).GetAwaiter().GetResult();
    }

    public void AppendText(string path, string text)
    {
        WriteContent(path, Utf8.GetBytes(text), append: true, text: true, CancellationToken.None).GetAwaiter().GetResult();
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

    private sealed class MockTextInput : ILythonTextInput, ILythonSynchronousHostCapability
    {
        private readonly string[] _lines;
        private int _index;

        public bool CompletesSynchronously => true;

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

    private sealed class MockTextOutput : ILythonTextOutput, ILythonSynchronousHostCapability
    {
        private readonly StringBuilder _builder = new();

        public Action? OnWrite { get; set; }

        public string Text => _builder.ToString();

        public bool CompletesSynchronously => true;

        public ValueTask WriteUtf8Async(ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _builder.Append(Utf8.GetString(utf8.Span));
            OnWrite?.Invoke();
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }

    public sealed class MockTiming : ILythonTiming, ILythonSynchronousHostCapability
    {
        private readonly List<TimeSpan> _delays = [];

        public MockTiming(long monotonicNanoseconds, long resolutionNanoseconds)
        {
            MonotonicNanoseconds = monotonicNanoseconds;
            MonotonicResolutionNanoseconds = resolutionNanoseconds;
        }

        public long MonotonicNanoseconds { get; private set; }

        public bool CompletesSynchronously => true;

        public long MonotonicResolutionNanoseconds { get; }

        public IReadOnlyList<TimeSpan> Delays => _delays;

        public ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _delays.Add(duration);
            MonotonicNanoseconds = checked(MonotonicNanoseconds + duration.Ticks * 100);
            return ValueTask.CompletedTask;
        }

        public void Advance(long nanoseconds)
            => MonotonicNanoseconds = checked(MonotonicNanoseconds + nanoseconds);
    }

    private sealed class MockSubprocessRunner : ILythonSubprocessRunner, ILythonSynchronousHostCapability
    {
        private readonly Dictionary<string, (int ReturnCode, byte[] Stdout, byte[] Stderr)> _results = new(StringComparer.Ordinal);
        private readonly HashSet<string> _timeouts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TaskCompletionSource> _cancellationPauses = new(StringComparer.Ordinal);
        private readonly List<LythonSubprocessRequest> _requests = [];

        public bool Enabled { get; set; }

        public bool AllowsShellInvocation { get; set; }

        public bool CompletesSynchronously => !CompleteAsynchronously && Handler is null;

        public Func<LythonSubprocessRequest, CancellationToken, ValueTask<LythonSubprocessResult>>? Handler { get; set; }

        public bool CompleteAsynchronously { get; set; }

        public bool CompletedAsynchronously { get; private set; }

        public LythonSubprocessRequest? LastRequest { get; private set; }

        public IReadOnlyList<LythonSubprocessRequest> Requests => _requests;

        public void SeedResult(IReadOnlyList<string> args, int returnCode, string stdout, string stderr)
        {
            _results[Key(args)] = (returnCode, Utf8.GetBytes(stdout), Utf8.GetBytes(stderr));
        }

        public void SeedTimeout(IReadOnlyList<string> args) => _timeouts.Add(Key(args));

        public Task PauseUntilCancellation(IReadOnlyList<string> args)
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _cancellationPauses.Add(Key(args), started);
            return started.Task;
        }

        public ValueTask<LythonSubprocessResult> RunAsync(LythonSubprocessRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Handler is not null) return Handler(request, cancellationToken);
            if (CompleteAsynchronously)
            {
                return RunDelayedAsync(request, cancellationToken);
            }

            return Run(request, cancellationToken);
        }

        private async ValueTask<LythonSubprocessResult> RunDelayedAsync(LythonSubprocessRequest request, CancellationToken cancellationToken)
        {
            if (_cancellationPauses.TryGetValue(Key(request.Args), out var started))
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            await Task.Delay(25, cancellationToken);
            CompletedAsynchronously = true;
            return await Run(request, cancellationToken).ConfigureAwait(false);
        }

        private ValueTask<LythonSubprocessResult> Run(LythonSubprocessRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            _requests.Add(request);
            if (_timeouts.Contains(Key(request.Args)))
            {
                throw new TimeoutException("mock subprocess timeout");
            }

            if (!_results.TryGetValue(Key(request.Args), out var result))
            {
                throw new InvalidOperationException($"No subprocess result seeded for: {Key(request.Args)}");
            }

            return LythonSubprocessCompletion.CompleteBufferedAsync(request, result.ReturnCode, result.Stdout, result.Stderr,
                static (_, _) => ValueTask.CompletedTask, static (_, _) => ValueTask.CompletedTask, cancellationToken);
        }

        private static string Key(IReadOnlyList<string> args) => string.Join("\u001F", args);
    }
}
