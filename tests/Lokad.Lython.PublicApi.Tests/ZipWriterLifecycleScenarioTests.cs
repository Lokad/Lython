using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R37: data mutations after close fail explicitly, fresh appends publish an
/// empty archive on close while unmodified existing appends publish nothing,
/// closed archives still accept context entry, and failed closes stay retried.
/// </summary>
public sealed class ZipWriterLifecycleScenarioTests
{
    [Fact]
    public async Task MutationsAfterCloseRaiseValueError()
    {
        var script = new LythonEngine().Compile(
            """
            import zipfile
            archive = zipfile.ZipFile("/out.zip", "w")
            archive.writestr("a.txt", b"data")
            archive.close()
            results = []
            try:
                archive.writestr("b.txt", b"data")
                results.append("writestr-no-error")
            except ValueError:
                results.append("writestr-ValueError")
            try:
                archive.write("/data.txt")
                results.append("write-no-error")
            except ValueError:
                results.append("write-ValueError")
            try:
                archive.mkdir("docs")
                results.append("mkdir-no-error")
            except ValueError:
                results.append("mkdir-ValueError")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "writestr-ValueError", "write-ValueError", "mkdir-ValueError" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task FreshAppendMissingTargetPublishesEmptyArchive()
    {
        var script = new LythonEngine().Compile(
            """
            import zipfile
            archive = zipfile.ZipFile("/fresh.zip", "a")
            archive.close()
            with zipfile.ZipFile("/fresh.zip") as reread:
                return reread.namelist()
            """);
        Assert.True(script.IsValid);
        var syncHost = new MockLythonHost();
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Empty(Assert.IsType<List<object?>>(sync.ReturnValue));
        Assert.Equal(22, syncHost.ReadBytes("/fresh.zip").Length);
        var asyncHost = new MockLythonHost();
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Empty(Assert.IsType<List<object?>>(asyncResult.ReturnValue));
        Assert.Equal(22, asyncHost.ReadBytes("/fresh.zip").Length);
    }

    [Fact]
    public async Task FreshAppendEmptyFilePublishesEmptyArchive()
    {
        var script = new LythonEngine().Compile(
            """
            import zipfile
            archive = zipfile.ZipFile("/empty.zip", "a")
            archive.close()
            with zipfile.ZipFile("/empty.zip") as reread:
                return reread.namelist()
            """);
        Assert.True(script.IsValid);
        var syncHost = new MockLythonHost();
        syncHost.SeedBytes("/empty.zip", []);
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Empty(Assert.IsType<List<object?>>(sync.ReturnValue));
        Assert.Equal(22, syncHost.ReadBytes("/empty.zip").Length);
        var asyncHost = new MockLythonHost();
        asyncHost.SeedBytes("/empty.zip", []);
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Empty(Assert.IsType<List<object?>>(asyncResult.ReturnValue));
        Assert.Equal(22, asyncHost.ReadBytes("/empty.zip").Length);
    }

    [Fact]
    public async Task ExistingAppendWithoutModificationsPublishesNothing()
    {
        const string setup = """
            import zipfile
            archive = zipfile.ZipFile("/keep.zip", "w")
            archive.writestr("a.txt", b"data")
            archive.close()
            """;
        const string append = """
            import zipfile
            archive = zipfile.ZipFile("/keep.zip", "a")
            archive.close()
            with zipfile.ZipFile("/keep.zip") as reread:
                return reread.read("a.txt")
            """;
        var syncHost = new MockLythonHost();
        Assert.True(new LythonEngine().Run(setup, syncHost).Success);
        var before = syncHost.ReadBytes("/keep.zip");
        var sync = new LythonEngine().Run(append, syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new byte[] { 100, 97, 116, 97 }, Assert.IsType<byte[]>(sync.ReturnValue));
        Assert.Equal(before, syncHost.ReadBytes("/keep.zip"));
        var asyncHost = new MockLythonHost();
        Assert.True(new LythonEngine().Run(setup, asyncHost).Success);
        var asyncBefore = asyncHost.ReadBytes("/keep.zip");
        var asyncResult = await new LythonEngine().RunAsync(append, asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new byte[] { 100, 97, 116, 97 }, Assert.IsType<byte[]>(asyncResult.ReturnValue));
        Assert.Equal(asyncBefore, asyncHost.ReadBytes("/keep.zip"));
    }

    [Fact]
    public async Task ExistingAppendWithModificationsPublishes()
    {
        const string setup = """
            import zipfile
            archive = zipfile.ZipFile("/grow.zip", "w")
            archive.writestr("a.txt", b"data")
            archive.close()
            """;
        const string append = """
            import zipfile
            archive = zipfile.ZipFile("/grow.zip", "a")
            archive.writestr("b.txt", b"more")
            archive.close()
            with zipfile.ZipFile("/grow.zip") as reread:
                return [reread.namelist(), reread.read("b.txt")]
            """;
        var syncHost = new MockLythonHost();
        Assert.True(new LythonEngine().Run(setup, syncHost).Success);
        var sync = new LythonEngine().Run(append, syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        var values = Assert.IsType<List<object?>>(sync.ReturnValue);
        Assert.Equal(new List<object?> { "a.txt", "b.txt" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(new byte[] { 109, 111, 114, 101 }, Assert.IsType<byte[]>(values[1]));
        var asyncHost = new MockLythonHost();
        Assert.True(new LythonEngine().Run(setup, asyncHost).Success);
        var asyncResult = await new LythonEngine().RunAsync(append, asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        var asyncValues = Assert.IsType<List<object?>>(asyncResult.ReturnValue);
        Assert.Equal(new List<object?> { "a.txt", "b.txt" }, Assert.IsType<List<object?>>(asyncValues[0]));
        Assert.Equal(new byte[] { 109, 111, 114, 101 }, Assert.IsType<byte[]>(asyncValues[1]));
    }

    [Fact]
    public async Task ClosedArchiveAcceptsContextEntry()
    {
        var script = new LythonEngine().Compile(
            """
            import zipfile
            archive = zipfile.ZipFile("/out.zip", "w")
            archive.writestr("a.txt", b"data")
            archive.close()
            with archive:
                pass
            with zipfile.ZipFile("/out.zip") as reread:
                return reread.read("a.txt")
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new byte[] { 100, 97, 116, 97 }, Assert.IsType<byte[]>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new byte[] { 100, 97, 116, 97 }, Assert.IsType<byte[]>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task FailedCloseStaysRetriable()
    {
        var script = new LythonEngine().Compile(
            """
            import zipfile
            results = []
            archive = zipfile.ZipFile("/out.zip", "w")
            handle = archive.open("a.txt", "w")
            handle.write(b"data")
            try:
                archive.close()
                results.append("close-no-error")
            except ValueError:
                results.append("close-ValueError")
            handle.close()
            archive.close()
            with zipfile.ZipFile("/out.zip") as reread:
                results.append(reread.read("a.txt"))
            return results
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var values = Assert.IsType<List<object?>>(sync.ReturnValue);
        Assert.Equal("close-ValueError", values[0]);
        Assert.Equal(new byte[] { 100, 97, 116, 97 }, Assert.IsType<byte[]>(values[1]));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        var asyncValues = Assert.IsType<List<object?>>(asyncResult.ReturnValue);
        Assert.Equal("close-ValueError", asyncValues[0]);
        Assert.Equal(new byte[] { 100, 97, 116, 97 }, Assert.IsType<byte[]>(asyncValues[1]));
    }
}

