using System.Threading.Tasks;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// R35: every ZIP host effect is registered before dispatch, discovery stats are
/// reused instead of repeated, and exact host traces hold at zero, exhausted, and
/// sufficient limits in both execution modes.
/// </summary>
public sealed class ZipHostAccountingTests
{
    private static byte[] OneMemberArchive()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            "import zipfile\nwith zipfile.ZipFile(\"/t.zip\", \"w\") as archive:\n    archive.writestr(\"data.txt\", b\"data\")\nreturn 1\n",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        return host.ReadBytes("/t.zip");
    }

    private static MockLythonHost SeedArchive()
    {
        var host = new MockLythonHost();
        host.SeedBytes("/t.zip", OneMemberArchive());
        return host;
    }

    private static DelayedLythonHost SeedDelayedArchive()
    {
        var host = new DelayedLythonHost("/");
        host.SeedBytes("/t.zip", OneMemberArchive());
        return host;
    }

    private static byte[] ReadSeeded(TracingLythonHost host, string path)
    {
        if (host.Inner is MockLythonHost mock)
        {
            return mock.ReadBytes(path);
        }

        return ((DelayedLythonHost)host.Inner).ReadBytes(path);
    }

    private static void AssertHostCallExceeded(LythonExecutionResult result, TracingLythonHost host)
    {
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum host call count exceeded", result.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
        Assert.Empty(host.Trace);
    }

    [Fact]
    public async Task IsZipFileMissingCountsDiscoveryCall()
    {
        const string script = "import zipfile\nreturn zipfile.is_zipfile(\"/missing\")\n";
        var options = new LythonRunOptions { DisableDefaultLimits = true, MaxHostCalls = 0 };
        var syncHost = new TracingLythonHost(new MockLythonHost());
        var sync = new LythonEngine().Run(script, syncHost, options);
        AssertHostCallExceeded(sync, syncHost);

        var asyncHost = new TracingLythonHost(new DelayedLythonHost("/"));
        var asyncResult = await new LythonEngine().RunAsync(script, asyncHost, options);
        AssertHostCallExceeded(asyncResult, asyncHost);
    }

    [Fact]
    public async Task IsZipFileTraceIsStatThenRead()
    {
        const string script = "import zipfile\nreturn zipfile.is_zipfile(\"/t.zip\")\n";
        var expected = new List<string> { "stat:/t.zip", "read:/t.zip" };
        var syncHost = new TracingLythonHost(SeedArchive());
        var sync = new LythonEngine().Run(script, syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(Assert.IsType<bool>(sync.ReturnValue));
        Assert.Equal(expected, syncHost.Trace);

        var asyncHost = new TracingLythonHost(SeedDelayedArchive());
        var asyncResult = await new LythonEngine().RunAsync(script, asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.True(Assert.IsType<bool>(asyncResult.ReturnValue));
        Assert.Equal(expected, asyncHost.Trace);
    }

    [Fact]
    public async Task OpenReadTraceIsStatThenRead()
    {
        const string script = "import zipfile\nwith zipfile.ZipFile(\"/t.zip\") as archive:\n    return archive.namelist()\n";
        var expected = new List<string> { "stat:/t.zip", "read:/t.zip" };
        var syncHost = new TracingLythonHost(SeedArchive());
        var sync = new LythonEngine().Run(script, syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { "data.txt" }, Assert.IsType<List<object?>>(sync.ReturnValue));
        Assert.Equal(expected, syncHost.Trace);

        var asyncHost = new TracingLythonHost(SeedDelayedArchive());
        var asyncResult = await new LythonEngine().RunAsync(script, asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { "data.txt" }, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
        Assert.Equal(expected, asyncHost.Trace);
    }

    [Fact]
    public async Task OpenMissingAndDirectoryLimits()
    {
        const string missing = "import zipfile\nwith zipfile.ZipFile(\"/missing\") as archive:\n    return 1\n";
        var zero = new LythonRunOptions { DisableDefaultLimits = true, MaxHostCalls = 0 };
        var syncHost = new TracingLythonHost(new MockLythonHost());
        AssertHostCallExceeded(new LythonEngine().Run(missing, syncHost, zero), syncHost);
        var asyncHost = new TracingLythonHost(new DelayedLythonHost("/"));
        AssertHostCallExceeded(await new LythonEngine().RunAsync(missing, asyncHost, zero), asyncHost);

        var absentHost = new TracingLythonHost(new MockLythonHost());
        var absent = new LythonEngine().Run(missing, absentHost);
        Assert.False(absent.Success);
        Assert.Equal("FileNotFoundError", absent.Failure?.ExceptionType);
        Assert.Equal(new List<string> { "stat:/missing" }, absentHost.Trace);

        const string directory = "import zipfile\nwith zipfile.ZipFile(\"/\") as archive:\n    return 1\n";
        var one = new LythonRunOptions { DisableDefaultLimits = true, MaxHostCalls = 1 };
        var dirHost = new TracingLythonHost(new MockLythonHost());
        var dirResult = new LythonEngine().Run(directory, dirHost, one);
        Assert.False(dirResult.Success);
        Assert.Equal("IsADirectoryError", dirResult.Failure?.ExceptionType);
        Assert.Equal(new List<string> { "stat:/" }, dirHost.Trace);
    }

    [Fact]
    public async Task WriteCloseTraceIsClockThenWrite()
    {
        const string script = "import zipfile\nwith zipfile.ZipFile(\"/out.zip\", \"w\") as archive:\n    archive.writestr(\"a.txt\", b\"abc\")\nreturn 1\n";
        const string asyncScript = "import zipfile\narchive = zipfile.ZipFile(\"/out.zip\", \"w\")\narchive.writestr(\"a.txt\", b\"abc\")\narchive.close()\nreturn 1\n";
        var expected = new List<string> { "clock", "write:/out.zip" };
        var syncHost = new TracingLythonHost(new MockLythonHost());
        var sync = new LythonEngine().Run(script, syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, syncHost.Trace);
        var reread = new LythonEngine().Run("import zipfile\nwith zipfile.ZipFile(\"/out.zip\") as archive:\n    return archive.read(\"a.txt\")\n", syncHost);
        Assert.True(reread.Success, reread.Failure?.Message);
        Assert.Equal(new byte[] { 97, 98, 99 }, Assert.IsType<byte[]>(reread.ReturnValue));

        var asyncHost = new TracingLythonHost(new DelayedLythonHost("/"));
        var asyncResult = await new LythonEngine().RunAsync(asyncScript, asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncHost.Trace);

        var one = new LythonRunOptions { DisableDefaultLimits = true, MaxHostCalls = 1 };
        var limitedHost = new TracingLythonHost(new MockLythonHost());
        var limited = new LythonEngine().Run(script, limitedHost, one);
        Assert.False(limited.Success);
        Assert.Equal("RuntimeError", limited.Failure?.ExceptionType);
        Assert.Contains("maximum host call count exceeded", limited.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(new List<string> { "clock" }, limitedHost.Trace);
    }

    [Fact]
    public async Task ExtractAllTraceMatchesPlannedEffects()
    {
        const string script = "import zipfile\nwith zipfile.ZipFile(\"/t.zip\") as archive:\n    archive.extractall(\"/out\")\nreturn 1\n";
        var expected = new List<string>
        {
            "stat:/t.zip", "read:/t.zip", "stat:/out", "mkdir:/out", "stat:/out", "stat:/out/data.txt", "write:/out/data.txt",
        };
        var syncHost = new TracingLythonHost(SeedArchive());
        var sync = new LythonEngine().Run(script, syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, syncHost.Trace);
        Assert.Equal(new byte[] { 100, 97, 116, 97 }, ReadSeeded(syncHost, "/out/data.txt"));

        var asyncHost = new TracingLythonHost(SeedDelayedArchive());
        var asyncResult = await new LythonEngine().RunAsync(script, asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncHost.Trace);
        Assert.Equal(new byte[] { 100, 97, 116, 97 }, ReadSeeded(asyncHost, "/out/data.txt"));

        var two = new LythonRunOptions { DisableDefaultLimits = true, MaxHostCalls = 2 };
        var limitedHost = new TracingLythonHost(SeedArchive());
        var limited = new LythonEngine().Run(script, limitedHost, two);
        Assert.False(limited.Success);
        Assert.Equal("RuntimeError", limited.Failure?.ExceptionType);
        Assert.Contains("maximum host call count exceeded", limited.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(new List<string> { "stat:/t.zip", "read:/t.zip" }, limitedHost.Trace);
    }

    [Fact]
    public async Task AppendMissingPublishesEmptyArchive()
    {
        const string script = "import zipfile\nwith zipfile.ZipFile(\"/new.zip\", \"a\") as archive:\n    pass\nreturn 1\n";
        // R37: a fresh append publishes a valid empty archive on close instead of
        // publishing nothing; the trace gains exactly the publication write.
        var expected = new List<string> { "stat:/new.zip", "write:/new.zip" };
        var syncHost = new TracingLythonHost(new MockLythonHost());
        var sync = new LythonEngine().Run(script, syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, syncHost.Trace);
        Assert.Equal(22, ReadSeeded(syncHost, "/new.zip").Length);

        var asyncHost = new TracingLythonHost(new DelayedLythonHost("/"));
        var asyncResult = await new LythonEngine().RunAsync(script, asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncHost.Trace);
        Assert.Equal(22, ReadSeeded(asyncHost, "/new.zip").Length);

        var zero = new LythonRunOptions { DisableDefaultLimits = true, MaxHostCalls = 0 };
        var limitedHost = new TracingLythonHost(new MockLythonHost());
        AssertHostCallExceeded(new LythonEngine().Run(script, limitedHost, zero), limitedHost);
    }

    [Fact]
    public async Task WriteSourceFileTrace()
    {
        const string script = "import zipfile\nwith zipfile.ZipFile(\"/out.zip\", \"w\", strict_timestamps=False) as archive:\n    archive.write(\"/data.txt\")\nreturn 1\n";
        const string asyncScript = "import zipfile\narchive = zipfile.ZipFile(\"/out.zip\", \"w\", strict_timestamps=False)\narchive.write(\"/data.txt\")\narchive.close()\nreturn 1\n";
        var expected = new List<string> { "stat:/data.txt", "read:/data.txt", "clock", "write:/out.zip" };
        var syncInner = new MockLythonHost();
        syncInner.SeedBytes("/data.txt", new byte[] { 104, 101, 108, 108, 111 });
        var syncHost = new TracingLythonHost(syncInner);
        var sync = new LythonEngine().Run(script, syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, syncHost.Trace);

        // The source-file stat/read in ZipFile.write is still synchronous (see R34),
        // so the async-mode half runs on a synchronous host; the counted lines are shared.
        var asyncInner = new MockLythonHost();
        asyncInner.SeedBytes("/data.txt", new byte[] { 104, 101, 108, 108, 111 });
        var asyncHost = new TracingLythonHost(asyncInner);
        var asyncResult = await new LythonEngine().RunAsync(asyncScript, asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncHost.Trace);

        var three = new LythonRunOptions { DisableDefaultLimits = true, MaxHostCalls = 3 };
        var limitedInner = new MockLythonHost();
        limitedInner.SeedBytes("/data.txt", new byte[] { 104, 101, 108, 108, 111 });
        var limitedHost = new TracingLythonHost(limitedInner);
        var limited = new LythonEngine().Run(script, limitedHost, three);
        Assert.False(limited.Success);
        Assert.Equal("RuntimeError", limited.Failure?.ExceptionType);
        Assert.Equal(new List<string> { "stat:/data.txt", "read:/data.txt", "clock" }, limitedHost.Trace);

        var four = new LythonRunOptions { DisableDefaultLimits = true, MaxHostCalls = 4 };
        var exactInner = new MockLythonHost();
        exactInner.SeedBytes("/data.txt", new byte[] { 104, 101, 108, 108, 111 });
        var exactHost = new TracingLythonHost(exactInner);
        var exact = new LythonEngine().Run(script, exactHost, four);
        Assert.True(exact.Success, exact.Failure?.Message);
        Assert.Equal(expected, exactHost.Trace);
    }
}
