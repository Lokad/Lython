using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R34: ZIP host effects await real suspension on every async path. Protocol
/// exit routes through CloseAsync and write() awaits host stat/read, so
/// RunAsync works through hosts whose operations actually suspend.
/// </summary>
public sealed class ZipAsyncHostScenarioTests
{
    [Fact]
    public async Task ProtocolExitPublishesThroughDelayedHost()
    {
        var host = new DelayedLythonHost("/");
        var result = await new LythonEngine().RunAsync(
            """
            import zipfile
            with zipfile.ZipFile("/out.zip", "w") as archive:
                archive.writestr("a.txt", b"data")
            with zipfile.ZipFile("/out.zip") as archive:
                return archive.read("a.txt")
            """,
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new byte[] { 100, 97, 116, 97 }, Assert.IsType<byte[]>(result.ReturnValue));
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task ExplicitClosePublishesThroughDelayedHost()
    {
        var host = new DelayedLythonHost("/");
        var result = await new LythonEngine().RunAsync(
            """
            import zipfile
            archive = zipfile.ZipFile("/out.zip", "w")
            archive.writestr("a.txt", b"data")
            archive.close()
            archive.close()
            with zipfile.ZipFile("/out.zip") as reread:
                return reread.read("a.txt")
            """,
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new byte[] { 100, 97, 116, 97 }, Assert.IsType<byte[]>(result.ReturnValue));
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task WriteStagesSeededFileThroughDelayedHost()
    {
        var host = new DelayedLythonHost("/");
        host.SeedBytes("/data.txt", new byte[] { 104, 101, 108, 108, 111 });
        var result = await new LythonEngine().RunAsync(
            """
            import zipfile
            archive = zipfile.ZipFile("/out.zip", "w", strict_timestamps=False)
            archive.write("/data.txt")
            archive.close()
            with zipfile.ZipFile("/out.zip") as reread:
                return [reread.namelist(), reread.read("data.txt")]
            """,
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "data.txt" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(new byte[] { 104, 101, 108, 108, 111 }, Assert.IsType<byte[]>(values[1]));
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task WriteMissingFileFailsExplicitlyThroughDelayedHost()
    {
        var host = new DelayedLythonHost("/");
        var result = await new LythonEngine().RunAsync(
            """
            import zipfile
            archive = zipfile.ZipFile("/out.zip", "w")
            try:
                archive.write("/missing.txt")
                return "no-error"
            except FileNotFoundError:
                return "FileNotFoundError"
            """,
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("FileNotFoundError", result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task BodyExceptionStillPublishesThroughDelayedHost()
    {
        var host = new DelayedLythonHost("/");
        var result = await new LythonEngine().RunAsync(
            """
            import zipfile
            try:
                with zipfile.ZipFile("/out.zip", "w") as archive:
                    archive.writestr("a.txt", b"data")
                    raise ValueError("boom")
            except ValueError:
                pass
            with zipfile.ZipFile("/out.zip") as reread:
                return reread.read("a.txt")
            """,
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new byte[] { 100, 97, 116, 97 }, Assert.IsType<byte[]>(result.ReturnValue));
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task CancelledWriteLoopFailsExplicitly()
    {
        var host = new DelayedLythonHost("/");
        host.SeedBytes("/data.txt", new byte[] { 104, 101, 108, 108, 111 });
        using var cancellation = new CancellationTokenSource();
        var task = new LythonEngine().RunAsync(
            """
            import zipfile
            archive = zipfile.ZipFile("/out.zip", "w", strict_timestamps=False)
            for i in range(20):
                archive.write("/data.txt")
            archive.close()
            return 1
            """,
            host,
            cancellationToken: cancellation.Token);
        cancellation.Cancel();
        var result = await task;
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message, StringComparison.Ordinal);
    }
}

