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

        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
            => _inner.ReadTextUtf8Async(path, cancellationToken);

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => _inner.WriteTextUtf8Async(path, utf8, cancellationToken);

        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            => _inner.AppendTextUtf8Async(path, utf8, cancellationToken);

        public ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken cancellationToken)
            => _inner.ReadBytesAsync(path, cancellationToken);

        public ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
            => _inner.WriteBytesAsync(path, bytes, cancellationToken);

        public ValueTask AppendBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
            => _inner.AppendBytesAsync(path, bytes, cancellationToken);

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
        var script = new LythonEngine().Compile("return len(open(\"/data.txt\").read())\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxHostReadBytes = 65536 };
        var host = new StaleStatHost();
        host.SeedFile("/data.txt", new string('y', 1000000));
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
        Assert.Contains("host text read exceeded maximum bytes", sync.Failure?.Message, StringComparison.Ordinal);

        var host2 = new StaleStatHost();
        host2.SeedFile("/data.txt", new string('y', 1000000));
        var asyncResult = await script.RunAsync(host2, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("RuntimeError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("host text read exceeded maximum bytes", asyncResult.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GrownBinaryReadFailsClosed()
    {
        var script = new LythonEngine().Compile("import filecmp\nreturn filecmp.cmp(\"/a.bin\", \"/b.bin\", shallow=False)\n");
        Assert.True(script.IsValid);
        var payload = new byte[1000000];
        var options = new LythonRunOptions { MaxHostReadBytes = 65536 };
        var host = new StaleStatHost();
        host.SeedBytes("/a.bin", payload);
        host.SeedBytes("/b.bin", payload);
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
        Assert.Contains("host binary read exceeded maximum bytes", sync.Failure?.Message, StringComparison.Ordinal);

        var host2 = new StaleStatHost();
        host2.SeedBytes("/a.bin", payload);
        host2.SeedBytes("/b.bin", payload);
        var asyncResult = await script.RunAsync(host2, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("RuntimeError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("host binary read exceeded maximum bytes", asyncResult.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HonestStatReadsFunded()
    {
        var script = new LythonEngine().Compile("return len(open(\"/data.txt\").read())\n");
        Assert.True(script.IsValid);
        var expected = new BigInteger(1000);
        var options = new LythonRunOptions { MaxHostReadBytes = 65536 };
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
        var script = new LythonEngine().Compile("return len(open(\"/data.txt\").read())\n");
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/data.txt", new string('y', 1000));
        var asyncResult = await script.RunAsync(host);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(1000), asyncResult.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }
}
