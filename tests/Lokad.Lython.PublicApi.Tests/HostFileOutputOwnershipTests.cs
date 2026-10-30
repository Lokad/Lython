using System;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Completed publication belongs to the host. Bounded host-driven executions
// relinquish Lython's conservative buffers between batches, independently of GC.
public sealed class HostFileOutputOwnershipTests
{
    private const int FileCount = 16;
    private const int FileBytes = 32768;
    private const long BudgetBytes = 262144;
    private const long PeakAgreementToleranceBytes = 16384;
    private const long ExpectedHostBytes = (long)FileCount * FileBytes;
    private const string BatchScript = """
        with open(path, 'w') as output:
            output.write('a' * 32768)
        return 'done'
        """;
    private static string ExpectedContent => new string('a', FileBytes);

    private static async Task<long> RunBatches(ILythonHost host, bool asynchronous, bool samePath = false)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(BatchScript);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(BudgetBytes);
        long peak = 0;
        for (var i = 0; i < (samePath ? 64 : FileCount); i++)
        {
            var options = new LythonRunOptions
            {
                MemoryBudget = budget,
                Globals = new Dictionary<string, object?> { ["path"] = "/f" + (samePath ? "" : i.ToString()) + ".txt" },
            };
            using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("done", result.ReturnValue);
                Assert.InRange(budget.ReservedBytes, 1, BudgetBytes);
                Assert.InRange(result.PeakExecutionMemoryBytes, 1, BudgetBytes);
                peak = Math.Max(peak, result.PeakExecutionMemoryBytes);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
        return peak;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainingAndSinkHostsAgreeAcrossBoundedBatches(bool asynchronous)
    {
        var retaining = new RetainingFileHost();
        var retainedPeak = await RunBatches(retaining, asynchronous);
        Assert.Equal(ExpectedHostBytes, retaining.RetainedBytes);
        for (var i = 0; i < FileCount; i++)
            Assert.Equal(ExpectedContent, retaining.ReadText("/f" + i + ".txt"));
        var sink = new SinkFileHost();
        var sinkPeak = await RunBatches(sink, asynchronous);
        Assert.Equal(ExpectedHostBytes, sink.ReceivedBytes);
        Assert.Equal(ExpectedHostBytes * (byte)'a', sink.Checksum);
        Assert.Equal(0, sink.RetainedBytes);
        Assert.InRange(Math.Abs(sinkPeak - retainedPeak), 0, PeakAgreementToleranceBytes);
    }

    [Fact]
    public async Task DelayedAsyncCompletionDeliversIntactBatch()
    {
        var host = new DelayedLythonHost();
        await RunBatches(host, true);
        Assert.True(host.CompletedAsynchronously > 0);
        for (var i = 0; i < FileCount; i++)
            Assert.Equal(ExpectedContent, host.ReadText("/f" + i + ".txt"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedPathPublicationReturnsAllowanceBetweenBatches(bool asynchronous)
    {
        var host = new MockLythonHost();
        await RunBatches(host, asynchronous, samePath: true);
        Assert.Equal(ExpectedContent, host.ReadText("/f.txt"));
    }

    private sealed class RetainingFileHost : ILythonHost, ILythonSynchronousHostCapability
    {
        private readonly MockLythonHost _inner = new();
        private readonly HashSet<string> _paths = new(StringComparer.Ordinal);
        public bool CompletesSynchronously => true;
        public string Cwd => _inner.Cwd;
        public DateTimeOffset LocalNow => _inner.LocalNow;
        public DateTimeOffset UtcNow => _inner.UtcNow;
        public long RetainedBytes => _paths.Sum(path => (long)_inner.Stat(path).Size);
        public string ReadText(string path) => _inner.ReadText(path);
        public async ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest request, LythonFileOpenOptions options,
            ILythonMemoryBudget budget, CancellationToken token)
        {
            var file = await _inner.OpenFileAsync(request, options, budget, token);
            _paths.Add(request.Path.StartsWith('/') ? request.Path : request.Cwd.TrimEnd('/') + "/" + request.Path);
            return file;
        }
        public ValueTask<bool> ExistsAsync(string p, CancellationToken t) => _inner.ExistsAsync(p, t);
        public ValueTask<IReadOnlyList<string>> ListDirAsync(string p, CancellationToken t) => _inner.ListDirAsync(p, t);
        public ValueTask MkDirAsync(string p, CancellationToken t) => _inner.MkDirAsync(p, t);
        public ValueTask RemoveAsync(string p, CancellationToken t) => _inner.RemoveAsync(p, t);
        public ValueTask CopyAsync(string p, string q, CancellationToken t) => _inner.CopyAsync(p, q, t);
        public ValueTask MoveAsync(string p, string q, CancellationToken t) => _inner.MoveAsync(p, q, t);
        public ValueTask<LythonPathStat> StatAsync(string p, CancellationToken t) => _inner.StatAsync(p, t);
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

        public async ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest request, LythonFileOpenOptions options,
            ILythonMemoryBudget budget, CancellationToken token)
        {
            if (options.Access != LythonFileAccess.Write || !options.Truncate)
                throw new LythonHostCapabilityUnavailableException("sink replacement writes only");
            var file = await _inner.OpenFileAsync(request, options, budget, token);
            long position = 0;
            return new TestByteFile(file)
            {
                Cursor = () => position,
                Write = (buffer, current) =>
            {
                current.ThrowIfCancellationRequested(); Consume(buffer.Span); position += buffer.Length;
                return ValueTask.FromResult(new LythonFileWriteResult(buffer.Length));
            }
            };
        }

        public long ReceivedBytes { get; private set; }
        public long Checksum { get; private set; }
        public int RetainedBytes => 0;

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