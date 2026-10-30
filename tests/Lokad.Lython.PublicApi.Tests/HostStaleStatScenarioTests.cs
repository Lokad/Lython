using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG21: host size prechecks cannot trust the stat alone: a file that grows
/// (or lies) between stat and read must still fail closed on its actual
/// length. A lying stat also proves the post-read check does the work.
/// Delayed reads additionally prove the run path really suspends.
/// </summary>
public sealed class HostStaleStatScenarioTests
{
    private sealed class StaleStatHost : ILythonHost, ILythonSynchronousHostCapability
    {
        public bool CompletesSynchronously => true;

        private readonly MockLythonHost _inner = new("/repo");

        public string Cwd => _inner.Cwd;
        public DateTimeOffset LocalNow => _inner.LocalNow;
        public DateTimeOffset UtcNow => _inner.UtcNow;

        public void SeedFile(string path, string text) => _inner.SeedFile(path, text);
        public void SeedBytes(string path, byte[] payload) => _inner.SeedBytes(path, payload);
        public int WindowReads { get; private set; }
        public long ReadBytes { get; private set; }

        public async ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest path, LythonFileOpenOptions options,
            ILythonMemoryBudget budget, CancellationToken token)
        {
            var file = await _inner.OpenFileAsync(path, options, budget, token);
            return new TestByteFile(file)
            {
                SizeHint = () => 10,
                Read = async (buffer, activeToken) =>
                {
                    WindowReads++;
                    var count = await file.ReadAsync(buffer, activeToken);
                    ReadBytes += count;
                    return count;
                }
            };
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

        public async ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
        {
            var real = await _inner.StatAsync(path, cancellationToken).ConfigureAwait(false);
            if (real.IsFile)
            {
                return new LythonPathStat(LythonPathKind.File, new BigInteger(10), null);
            }

            return real;
        }
    }

    [Fact]
    public async Task GrownTextReadFailsClosed()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("return len(open(\"/data.txt\").read())\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(262144) };
        var host = new StaleStatHost();
        host.SeedFile("/data.txt", new string('y', 1000000));
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.Contains("shared memory budget", sync.Failure?.Message, StringComparison.Ordinal);

        var host2 = new StaleStatHost();
        host2.SeedFile("/data.txt", new string('y', 1000000));
        var asyncResult = await script.RunAsync(host2, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("shared memory budget", asyncResult.Failure?.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GrownBinaryReadFailsClosed(bool asynchronous)
    {
        // Whole-file acquisition must admit the actual payload even if stat lies.
        using var script = new LythonEngine().Compile("from pathlib import Path\nreturn Path('/a.bin').read_bytes()\n");
        Assert.True(script.IsValid);
        var payload = new byte[1000000];
        var budget = new LythonMemoryBudget(262144);
        var options = new LythonRunOptions { MemoryBudget = budget };
        var host = new StaleStatHost();
        host.SeedBytes("/a.bin", payload);
        using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Contains("shared memory budget", result.Failure?.Message, StringComparison.Ordinal);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
        }
        Assert.True(host.WindowReads > 0);
        Assert.InRange(host.ReadBytes, 1L, payload.Length - 1L);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task HonestStatReadsFunded()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("return len(open(\"/data.txt\").read())\n");
        Assert.True(script.IsValid);
        var expected = new BigInteger(1000);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(262144) };
        var host = new MockLythonHost();
        host.SeedFile("/data.txt", new string('y', 1000));
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedFile("/data.txt", new string('y', 1000));
        var asyncResult = await script.RunAsync(host2, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DelayedReadsReallySuspend()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("return len(open(\"/data.txt\").read())\n");
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/data.txt", new string('y', 1000));
        var asyncResult = await script.RunAsync(host);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(1000), asyncResult.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }
}
