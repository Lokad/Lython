using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Existing input and construction overlap remain funded until ownership ends.
public sealed class GzipAppendAccountingScenarioTests
{
    private static byte[] MakeSeededGzip(int rawBytes)
    {
        var random = new Random(42);
        var raw = new byte[rawBytes];
        random.NextBytes(raw);
        using var output = new MemoryStream();
        using (var compressor = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.Fastest))
        {
            compressor.Write(raw, 0, raw.Length);
        }

        return output.ToArray();
    }

    [Fact]
    public async Task AppendConstructionOverlapRefusesBeforePublication()
    {
        var seeded = MakeSeededGzip(300000);
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            import gzip
            f = gzip.open("/a.gz", "ab")
            f.write(b"y")
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(524288) };
        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/a.gz", seeded);
        var sync = script.Run(syncHost, options);
        Assert.False(sync.Success);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, sync.FailureAttribution?.Reason);
        Assert.Equal(seeded, syncHost.ReadBytes("/a.gz"));

        var asyncHost = new MockLythonHost();
        asyncHost.SeedBytes("/a.gz", seeded);
        var asyncResult = await script.RunAsync(asyncHost, options);
        Assert.False(asyncResult.Success);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, asyncResult.FailureAttribution?.Reason);
        Assert.Equal(seeded, asyncHost.ReadBytes("/a.gz"));
    }

    [Fact]
    public async Task AppendedContentReadsBack()
    {
        var seeded = MakeSeededGzip(300000);
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            import gzip
            f = gzip.open("/a.gz", "ab")
            f.write(b"y")
            f.close()
            f2 = gzip.open("/a.gz", "rb")
            data = f2.read()
            f2.close()
            return len(data)
            """);
        Assert.True(script.IsValid);
        var expected = new BigInteger(300001);
        var host = new MockLythonHost();
        host.SeedBytes("/a.gz", seeded);
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncHost = new MockLythonHost();
        asyncHost.SeedBytes("/a.gz", seeded);
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}