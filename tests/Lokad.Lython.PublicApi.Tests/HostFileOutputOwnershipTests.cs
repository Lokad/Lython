using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// N36: host file-output memory ownership. A retaining host keeps every completed
/// payload while a sink host consumes and drops each one; both must show the same
/// bounded runtime peak because completed files are host-owned storage, never
/// execution memory. Delayed asynchronous completion must deliver intact bytes.
/// </summary>
public sealed class HostFileOutputOwnershipTests
{
    private const int FileCount = 16;
    private const int FileBytes = 32768;
    private const long BudgetBytes = 262144;
    private const long ExpectedHostBytes = (long)FileCount * FileBytes;

    private static string BatchScript => """
        s = "a" * 32768
        i = 0
        while i < 16:
            f = open("/f" + str(i) + ".txt", "w")
            f.write(s)
            f.close()
            i = i + 1
        return "done"
        """;

    private static string RepeatScript => """
        s = "a" * 32768
        i = 0
        while i < 64:
            f = open("/f.txt", "w")
            f.write(s)
            f.close()
            i = i + 1
        return "done"
        """;

    private static string ExpectedContent => new string('a', FileBytes);

    [Fact]
    public void RetainingAndSinkHostsAgreeOnRuntimePeak()
    {
        var script = new LythonEngine().Compile(BatchScript);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = BudgetBytes };

        var retaining = new RetainingFileHost();
        var retained = script.Run(retaining, options);
        Assert.True(retained.Success, retained.Failure?.Message);
        Assert.Equal(ExpectedHostBytes, retaining.RetainedBytes);
        for (var i = 0; i < FileCount; i++)
        {
            Assert.Equal(ExpectedContent, retaining.ReadText("/f" + i + ".txt"));
        }

        var sink = new SinkFileHost();
        var sunk = script.Run(sink, options);
        Assert.True(sunk.Success, sunk.Failure?.Message);
        Assert.Equal(ExpectedHostBytes, sink.ReceivedBytes);
        Assert.Equal(ExpectedHostBytes * (byte)'a', sink.Checksum);
        Assert.Equal(0, sink.RetainedBytes);

        Assert.Equal(sunk.PeakExecutionMemoryBytes, retained.PeakExecutionMemoryBytes);
        Assert.True(retained.PeakExecutionMemoryBytes <= BudgetBytes);
    }

    [Fact]
    public async Task RetainingAndSinkHostsAgreeOnRuntimePeakAsync()
    {
        var script = new LythonEngine().Compile(BatchScript);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = BudgetBytes };

        var retaining = new RetainingFileHost();
        var retained = await script.RunAsync(retaining, options);
        Assert.True(retained.Success, retained.Failure?.Message);
        Assert.Equal(ExpectedHostBytes, retaining.RetainedBytes);

        var sink = new SinkFileHost();
        var sunk = await script.RunAsync(sink, options);
        Assert.True(sunk.Success, sunk.Failure?.Message);
        Assert.Equal(ExpectedHostBytes, sink.ReceivedBytes);

        Assert.Equal(sunk.PeakExecutionMemoryBytes, retained.PeakExecutionMemoryBytes);
        Assert.True(retained.PeakExecutionMemoryBytes <= BudgetBytes);
    }

    [Fact]
    public async Task DelayedAsyncCompletionDeliversIntactBatch()
    {
        var script = new LythonEngine().Compile(BatchScript);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        var result = await script.RunAsync(host, new LythonRunOptions { MaxExecutionMemoryBytes = BudgetBytes });
        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(host.CompletedAsynchronously > 0);
        for (var i = 0; i < FileCount; i++)
        {
            Assert.Equal(ExpectedContent, host.ReadText("/f" + i + ".txt"));
        }
    }

    [Fact]
    public async Task RepeatedCloseToSamePathStaysBounded()
    {
        var script = new LythonEngine().Compile(RepeatScript);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = BudgetBytes };

        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(sync.PeakExecutionMemoryBytes <= BudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= BudgetBytes);
    }

    private sealed class RetainingFileHost : ILythonHost, ILythonSynchronousHostCapability
    {
        private readonly MockLythonHost _inner = new();
        private readonly Dictionary<string, byte[]> _retained = new(StringComparer.Ordinal);

        public bool CompletesSynchronously => true;
        public string Cwd => _inner.Cwd;
        public DateTimeOffset LocalNow => _inner.LocalNow;
        public DateTimeOffset UtcNow => _inner.UtcNow;
        public ILythonTextInput? StandardInput => _inner.StandardInput;
        public ILythonTextOutput? StandardOutput => _inner.StandardOutput;
        public ILythonTextOutput? StandardError => _inner.StandardError;
        public ILythonSubprocessRunner? SubprocessRunner => _inner.SubprocessRunner;
        public ILythonTiming? Timing => _inner.Timing;

        public long RetainedBytes { get; private set; }

        public string ReadText(string path) => _inner.ReadText(path);

        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
            => _inner.ReadTextUtf8Async(path, cancellationToken);

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_retained.TryGetValue(path, out var previous))
            {
                RetainedBytes -= previous.Length;
            }

            var copy = utf8.ToArray();
            _retained[path] = copy;
            RetainedBytes += copy.Length;
            return _inner.WriteTextUtf8Async(path, utf8, cancellationToken);
        }

        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_retained.TryGetValue(path, out var previous))
            {
                var combined = new byte[previous.Length + utf8.Length];
                previous.CopyTo(combined, 0);
                utf8.Span.CopyTo(combined.AsSpan(previous.Length));
                _retained[path] = combined;
                RetainedBytes += utf8.Length;
            }
            else
            {
                var copy = utf8.ToArray();
                _retained[path] = copy;
                RetainedBytes += copy.Length;
            }

            return _inner.AppendTextUtf8Async(path, utf8, cancellationToken);
        }

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

    private sealed class SinkFileHost : ILythonHost, ILythonSynchronousHostCapability
    {
        private readonly MockLythonHost _inner = new();

        public bool CompletesSynchronously => true;
        public string Cwd => _inner.Cwd;
        public DateTimeOffset LocalNow => _inner.LocalNow;
        public DateTimeOffset UtcNow => _inner.UtcNow;
        public ILythonTextInput? StandardInput => _inner.StandardInput;
        public ILythonTextOutput? StandardOutput => _inner.StandardOutput;
        public ILythonTextOutput? StandardError => _inner.StandardError;
        public ILythonSubprocessRunner? SubprocessRunner => _inner.SubprocessRunner;
        public ILythonTiming? Timing => _inner.Timing;

        public long ReceivedBytes { get; private set; }
        public long Checksum { get; private set; }
        public int RetainedBytes => 0;

        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
            => _inner.ReadTextUtf8Async(path, cancellationToken);

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Consume(utf8.Span);
            return ValueTask.CompletedTask;
        }

        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Consume(utf8.Span);
            return ValueTask.CompletedTask;
        }

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

        private void Consume(ReadOnlySpan<byte> payload)
        {
            foreach (var value in payload)
            {
                Checksum += value;
            }

            ReceivedBytes += payload.Length;
        }
    }
}