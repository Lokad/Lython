using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// R30: deterministic host-traffic measurements for the archive scenarios behind
/// the Baselines.md traffic table. Call counts and byte totals do not depend on
/// timing or build configuration, so these assertions are the repeatable
/// measurement entry point and double as regression coverage for serializer
/// output sizes.
/// </summary>
public sealed class ZipHostTrafficTests
{
    private const string WriteManySource = "import zipfile\nwith zipfile.ZipFile(\"/a.zip\", \"w\", compression=zipfile.ZIP_DEFLATED) as archive:\n    for i in range(200):\n        archive.writestr(\"f\" + str(i) + \".txt\", \"x\" * (i + 1))\nreturn 1\n";
    private const string ReadManySource = "import zipfile\ntotal = 0\nwith zipfile.ZipFile(\"/a.zip\") as archive:\n    for name in archive.namelist():\n        total = total + len(archive.read(name))\nreturn total\n";
    private const string AppendManySource = "import zipfile\nwith zipfile.ZipFile(\"/a.zip\", \"a\") as archive:\n    for i in range(20):\n        archive.writestr(\"n\" + str(i) + \".txt\", b\"new\")\nreturn 1\n";
    private const string ExtractManySource = "import zipfile\nwith zipfile.ZipFile(\"/a.zip\") as archive:\n    archive.extractall(\"/out\")\nreturn 1\n";

    private static byte[] BuildArchive(string source)
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(source, host);
        Assert.True(result.Success, result.Failure?.Message);
        return host.ReadBytes("/a.zip");
    }

    private static (int Calls, long Bytes) SumTransfers(TracingLythonHost host, string operation)
    {
        var calls = 0;
        long bytes = 0;
        foreach (var transfer in host.Transfers)
        {
            if (transfer.Operation == operation)
            {
                calls++;
                bytes += transfer.Bytes;
            }
        }

        return (calls, bytes);
    }

    private static int CountTracePrefix(TracingLythonHost host, string prefix)
    {
        var count = 0;
        foreach (var line in host.Trace)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    [Fact]
    public async Task WriteMixedArchiveTraffic()
    {
        var syncHost = new TracingLythonHost(new MockLythonHost());
        var sync = new LythonEngine().Run(WriteManySource, syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal([new TracingLythonHost.HostTransfer("write", "/a.zip", 19366)], syncHost.Transfers);

        var asyncHost = new TracingLythonHost(new MockLythonHost());
        var asyncResult = await new LythonEngine().RunAsync(WriteManySource, asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal([new TracingLythonHost.HostTransfer("write", "/a.zip", 19366)], asyncHost.Transfers);
    }

    [Fact]
    public async Task ReadMixedArchiveTraffic()
    {
        var archive = BuildArchive(WriteManySource);
        var syncInner = new MockLythonHost();
        var syncHost = new TracingLythonHost(syncInner);
        syncInner.SeedBytes("/a.zip", archive);
        var sync = new LythonEngine().Run(ReadManySource, syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(20100), Assert.IsType<BigInteger>(sync.ReturnValue));
        Assert.Equal([new TracingLythonHost.HostTransfer("read", "/a.zip", 19366)], syncHost.Transfers);
        Assert.Equal(1, CountTracePrefix(syncHost, "stat:"));

        var asyncInner = new MockLythonHost();
        var asyncHost = new TracingLythonHost(asyncInner);
        asyncInner.SeedBytes("/a.zip", archive);
        var asyncResult = await new LythonEngine().RunAsync(ReadManySource, asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(20100), Assert.IsType<BigInteger>(asyncResult.ReturnValue));
        Assert.Equal([new TracingLythonHost.HostTransfer("read", "/a.zip", 19366)], asyncHost.Transfers);
        Assert.Equal(1, CountTracePrefix(asyncHost, "stat:"));
    }

    [Fact]
    public async Task AppendMixedArchiveTraffic()
    {
        var archive = BuildArchive(WriteManySource);
        var syncInner = new MockLythonHost();
        var syncHost = new TracingLythonHost(syncInner);
        syncInner.SeedBytes("/a.zip", archive);
        var sync = new LythonEngine().Run(AppendManySource, syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal((1, 19366L), SumTransfers(syncHost, "read"));
        Assert.Equal((1, 21206L), SumTransfers(syncHost, "write"));
        Assert.Equal(1, CountTracePrefix(syncHost, "stat:"));

        var asyncInner = new MockLythonHost();
        var asyncHost = new TracingLythonHost(asyncInner);
        asyncInner.SeedBytes("/a.zip", archive);
        var asyncResult = await new LythonEngine().RunAsync(AppendManySource, asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal((1, 19366L), SumTransfers(asyncHost, "read"));
        Assert.Equal((1, 21206L), SumTransfers(asyncHost, "write"));
        Assert.Equal(1, CountTracePrefix(asyncHost, "stat:"));
    }

    [Fact]
    public async Task ExtractMixedArchiveTraffic()
    {
        var archive = BuildArchive(WriteManySource);
        var syncInner = new MockLythonHost();
        var syncHost = new TracingLythonHost(syncInner);
        syncInner.SeedBytes("/a.zip", archive);
        var sync = new LythonEngine().Run(ExtractManySource, syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal((1, 19366L), SumTransfers(syncHost, "read"));
        Assert.Equal((200, 20100L), SumTransfers(syncHost, "write"));
        Assert.Equal(402, CountTracePrefix(syncHost, "stat:"));
        Assert.Equal(1, CountTracePrefix(syncHost, "mkdir:"));

        var asyncInner = new MockLythonHost();
        var asyncHost = new TracingLythonHost(asyncInner);
        asyncInner.SeedBytes("/a.zip", archive);
        var asyncResult = await new LythonEngine().RunAsync(ExtractManySource, asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal((1, 19366L), SumTransfers(asyncHost, "read"));
        Assert.Equal((200, 20100L), SumTransfers(asyncHost, "write"));
        Assert.Equal(402, CountTracePrefix(asyncHost, "stat:"));
        Assert.Equal(1, CountTracePrefix(asyncHost, "mkdir:"));
    }

    [Theory]
    [InlineData(50, 4802, 12800)]
    [InlineData(200, 19402, 51200)]
    [InlineData(800, 78202, 204800)]
    public async Task FixedArchiveTraffic(int count, int archiveBytes, int expanded)
    {
        var writeSource = "import zipfile\nwith zipfile.ZipFile(\"/a.zip\", \"w\", compression=zipfile.ZIP_DEFLATED) as archive:\n    for i in range(" + count + "):\n        archive.writestr(\"f\" + str(i) + \".txt\", \"v\" * 256)\nreturn 1\n";
        const string ReadSource = "import zipfile\ntotal = 0\nwith zipfile.ZipFile(\"/a.zip\") as archive:\n    for name in archive.namelist():\n        total = total + len(archive.read(name))\nreturn total\n";
        var buildInner = new MockLythonHost();
        var buildHost = new TracingLythonHost(buildInner);
        var built = new LythonEngine().Run(writeSource, buildHost);
        Assert.True(built.Success, built.Failure?.Message);
        Assert.Equal([new TracingLythonHost.HostTransfer("write", "/a.zip", archiveBytes)], buildHost.Transfers);
        var archive = buildInner.ReadBytes("/a.zip");

        var syncInner = new MockLythonHost();
        var syncHost = new TracingLythonHost(syncInner);
        syncInner.SeedBytes("/a.zip", archive);
        var sync = new LythonEngine().Run(ReadSource, syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(expanded), Assert.IsType<BigInteger>(sync.ReturnValue));
        Assert.Equal([new TracingLythonHost.HostTransfer("read", "/a.zip", archiveBytes)], syncHost.Transfers);
        Assert.Equal(1, CountTracePrefix(syncHost, "stat:"));

        var asyncInner = new MockLythonHost();
        var asyncHost = new TracingLythonHost(asyncInner);
        asyncInner.SeedBytes("/a.zip", archive);
        var asyncResult = await new LythonEngine().RunAsync(ReadSource, asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(expanded), Assert.IsType<BigInteger>(asyncResult.ReturnValue));
        Assert.Equal([new TracingLythonHost.HostTransfer("read", "/a.zip", archiveBytes)], asyncHost.Transfers);
        Assert.Equal(1, CountTracePrefix(asyncHost, "stat:"));
    }



    [Fact]
    public void CompressibleEntryTraffic()
    {
        const string source = "import random\nimport zipfile\nrandom.seed(42)\ndata = (\"v\" * 500000 + \"v\" * 500000).encode()\nwith zipfile.ZipFile(\"/a.zip\", \"w\", compression=zipfile.ZIP_DEFLATED) as archive:\n    archive.writestr(\"zeros.bin\", data)\nreturn 1\n";
        var syncHost = new TracingLythonHost(new MockLythonHost());
        var sync = new LythonEngine().Run(source, syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal([new TracingLythonHost.HostTransfer("write", "/a.zip", 1101)], syncHost.Transfers);
    }

    [Fact]
    public void IncompressibleEntryTraffic()
    {
        const string source = "import random\nimport zipfile\nrandom.seed(42)\ndata = random.randbytes(1000000)\nwith zipfile.ZipFile(\"/a.zip\", \"w\", compression=zipfile.ZIP_DEFLATED) as archive:\n    archive.writestr(\"random.bin\", data)\nreturn 1\n";
        var syncHost = new TracingLythonHost(new MockLythonHost());
        var sync = new LythonEngine().Run(source, syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal([new TracingLythonHost.HostTransfer("write", "/a.zip", 1000428)], syncHost.Transfers);
    }

}
