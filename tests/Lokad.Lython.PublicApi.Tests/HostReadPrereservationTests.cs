using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// Host reads fill admitted bounded windows. Growth refusal stops acquisition
/// before unfunded storage or further host reads; retained data stays charged.
/// </summary>
public sealed class HostReadPrereservationTests
{
    private sealed class CountingHost : ILythonHost, ILythonSynchronousHostCapability
    {
        public bool CompletesSynchronously => true;

        private readonly MockLythonHost _inner = new("/repo");

        public int WindowReads;
        public long WindowBytes;
        public int MaxWindowBytes;

        public string Cwd => _inner.Cwd;
        public DateTimeOffset LocalNow => _inner.LocalNow;
        public DateTimeOffset UtcNow => _inner.UtcNow;

        public void SeedFile(string path, string text) => _inner.SeedFile(path, text);
        public void SeedBytes(string path, byte[] payload) => _inner.SeedBytes(path, payload);

        public async ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest path, LythonFileOpenOptions options,
            ILythonMemoryBudget budget, CancellationToken token)
        {
            var file = await _inner.OpenFileAsync(path, options, budget, token);
            return new TestByteFile(file) { Read = async (buffer, current) =>
            {
                var count = await file.ReadAsync(buffer, current);
                WindowReads++; WindowBytes += count;
                MaxWindowBytes = Math.Max(MaxWindowBytes, count);
                return count;
            } };
        }

        public ValueTask<bool> ExistsAsync(LythonPathRequest path, CancellationToken cancellationToken)
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

        public ValueTask<LythonPathStat> StatAsync(LythonPathRequest path, CancellationToken cancellationToken)
            => _inner.StatAsync(path, cancellationToken);
    }

    [Fact]
    public async Task HostBatchesReadAllLinesWithoutWholeFileTransfers()
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            total = 0
            with open("/data.txt") as f:
                for line in f:
                    total = total + len(line)
            return total
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var executionBudget = new LythonMemoryBudget(65536);
        var options = new LythonRunOptions { MemoryBudget = executionBudget };
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new CountingHost();
            BigInteger total = 0;
            for (var start = 0; start < 100; start += 8)
            {
                var count = Math.Min(8, 100 - start);
                host.SeedFile("/data.txt", string.Concat(Enumerable.Repeat(new string('y', 999) + "\n", count)));
                using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
                {
                    Assert.True(result.Success, StreamingFailureDetails("batch", result, host));
                    total += Assert.IsType<BigInteger>(result.ReturnValue);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, executionBudget.ReservedBytes);
            }
            Assert.Equal(new BigInteger(100000), total);

            Assert.True(host.WindowReads > 1);
            Assert.Equal(100000, host.WindowBytes);
            Assert.InRange(host.MaxWindowBytes, 1, 16 * 1024);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static string StreamingFailureDetails(string phase, LythonExecutionResult result, CountingHost host)
        => $"{phase}: {result.Failure?.ExceptionType}: {result.Failure?.Message}; "
            + $"peak={result.PeakExecutionMemoryBytes}; denied={result.DeniedReservationBytes}; "
            + $"windowReads={host.WindowReads}; returnedBytes={host.WindowBytes}; "
            + $"maxWindowBytes={host.MaxWindowBytes}";

    [Fact]
    public async Task RetainedLinesTripWhileStreaming()
    {
        // MG21: retained line charges still trip mid-stream after several
        // windows: no whole-file transfer ever issues, and acquisition stops
        // well short of covering the file. Retained storage remains charged.
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            lines = []
            with open("/data.txt") as f:
                for line in f:
                    lines.append(line)
            return len(lines)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        var host = new CountingHost();
        host.SeedFile("/data.txt", string.Concat(Enumerable.Repeat(new string((char)121, 999) + "\n", 100)));
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        Assert.True(host.WindowReads > 1);
        Assert.True(host.WindowBytes >= 16384);
        Assert.True(host.WindowBytes < 100000);

        var host2 = new CountingHost();
        host2.SeedFile("/data.txt", string.Concat(Enumerable.Repeat(new string((char)121, 999) + "\n", 100)));
        var asyncResult = await script.RunAsync(host2, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);

        Assert.True(host2.WindowReads > 1);
        Assert.True(host2.WindowBytes >= 16384);
        Assert.True(host2.WindowBytes < 100000);
    }

    [Fact]
    public async Task FundedTextReadReachesHost()
    {
        // MG21: open() streams fixed windows through owned reads, so a funded
        // file arrives with no whole-file transfer: every read stays
        // within the window and the windows cover the file exactly once.
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("return len(open(\"/data.txt\").read())\n");
        Assert.True(script.IsValid);
        var expected = new BigInteger(100000);
        var host = new CountingHost();
        host.SeedFile("/data.txt", new string('y', 100000));
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        Assert.True(host.WindowBytes == 100000);
        Assert.True(host.WindowReads > 1);
        Assert.True(host.MaxWindowBytes <= 16 * 1024, "no single read exceeds the engine window");

        var host2 = new CountingHost();
        host2.SeedFile("/data.txt", new string('y', 100000));
        var asyncResult = await script.RunAsync(host2);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);

        Assert.True(host2.WindowBytes == 100000);
        Assert.True(host2.WindowReads > 1);
        Assert.True(host2.MaxWindowBytes <= 16 * 1024, "no single read exceeds the engine window");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OversizedBinaryReadStopsBeforeUnfundedWindow(bool asynchronous)
    {
        // Whole-file helpers acquire progressively; admission can fail after
        // funded reads, but must stop short of acquiring the full payload.
        using var script = new LythonEngine().Compile("from pathlib import Path\nreturn Path('/a.bin').read_bytes()\n");
        Assert.True(script.IsValid);
        var payload = new byte[100000];
        var budget = new LythonMemoryBudget(65536);
        var options = new LythonRunOptions { MemoryBudget = budget };
        var host = new CountingHost();
        host.SeedBytes("/a.bin", payload);
        using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
        }

        Assert.True(host.WindowReads > 0);
        Assert.InRange(host.WindowBytes, 1L, payload.Length - 1L);
        Assert.InRange(host.MaxWindowBytes, 1, 16 * 1024);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task MissingFileSkipsPrereserve()
    {
        // Fund path preparation, but keep the allowance below a text read window.
        // Missing files must not trigger acquisition of that window.
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("return len(open(\"/nope.txt\").read())\n");
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(8192);
        var tiny = new LythonRunOptions { MemoryBudget = budget };
        var tinyHost = new CountingHost();
        using var tinyResult = script.Run(tinyHost, tiny);
        Assert.False(tinyResult.Success);
        Assert.NotNull(tinyResult.Failure);
        Assert.NotEqual("MemoryError", tinyResult.Failure?.ExceptionType);

        Assert.Equal(0, tinyHost.WindowReads);


        var roomyHost = new CountingHost();
        using var roomyResult = script.Run(roomyHost);
        Assert.False(roomyResult.Success);
        Assert.Equal(tinyResult.Failure?.ExceptionType, roomyResult.Failure?.ExceptionType);
        tinyResult.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
