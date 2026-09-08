using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R40: member lookup resolves through a shared last-name-to-ordinal index.
/// Duplicate names keep order with last-wins lookup, renamed infos look up
/// nothing new (matching CPython), and explicit infos bypass names entirely.
/// </summary>
public sealed class ZipNameLookupScenarioTests
{
    [Fact]
    public async Task DuplicateNamesKeepOrderWithLastWinsLookup()
    {
        var script = new LythonEngine().Compile(
            """
            import zipfile
            archive = zipfile.ZipFile("/out.zip", "w")
            archive.writestr("dup", b"first")
            archive.writestr("dup", b"second")
            archive.writestr("other", b"x")
            archive.close()
            with zipfile.ZipFile("/out.zip") as reread:
                info = reread.getinfo("dup")
                return [info.file_size, reread.namelist(), reread.read("dup")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new BigInteger(6),
            new List<object?> { "dup", "dup", "other" },
            new byte[] { 115, 101, 99, 111, 110, 100 },
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task RenamedInfoLooksUpNothingNew()
    {
        var script = new LythonEngine().Compile(
            """
            import zipfile
            archive = zipfile.ZipFile("/out.zip", "w")
            archive.writestr("dup", b"first")
            archive.writestr("dup", b"second")
            archive.close()
            results = []
            with zipfile.ZipFile("/out.zip") as reread:
                info = reread.getinfo("dup")
                info.filename = "zzz"
                try:
                    reread.getinfo("zzz")
                    results.append("found")
                except KeyError:
                    results.append("KeyError")
                results.append(reread.read(info))
                results.append(reread.read("dup"))
            return results
            """);
        Assert.True(script.IsValid);
        var second = new byte[] { 115, 101, 99, 111, 110, 100 };
        var expected = new List<object?> { "KeyError", second, second };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task MissingAndNonStringNamesRaiseKeyError()
    {
        var script = new LythonEngine().Compile(
            """
            import zipfile
            archive = zipfile.ZipFile("/out.zip", "w")
            archive.writestr("a.txt", b"data")
            archive.close()
            results = []
            with zipfile.ZipFile("/out.zip") as reread:
                try:
                    reread.getinfo("missing")
                    results.append("getinfo-no-error")
                except KeyError:
                    results.append("getinfo-KeyError")
                try:
                    reread.read("missing")
                    results.append("read-no-error")
                except KeyError:
                    results.append("read-KeyError")
                try:
                    reread.getinfo(42)
                    results.append("int-no-error")
                except KeyError:
                    results.append("int-KeyError")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "getinfo-KeyError", "read-KeyError", "int-KeyError" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task ClosedArchiveLookupStaysAvailable()
    {
        var script = new LythonEngine().Compile(
            """
            import zipfile
            archive = zipfile.ZipFile("/out.zip", "w")
            archive.writestr("a.txt", b"data")
            archive.writestr("a.txt", b"newer")
            archive.close()
            reread = zipfile.ZipFile("/out.zip")
            reread.close()
            info = reread.getinfo("a.txt")
            return [info.file_size, reread.namelist()]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(5), new List<object?> { "a.txt", "a.txt" } };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task ExtractByDuplicateNameTakesLast()
    {
        var script = new LythonEngine().Compile(
            """
            import zipfile
            archive = zipfile.ZipFile("/out.zip", "w")
            archive.writestr("dup", b"first")
            archive.writestr("dup", b"second")
            archive.close()
            with zipfile.ZipFile("/out.zip") as reread:
                path = reread.extract("dup", "/out")
            return path
            """);
        Assert.True(script.IsValid);
        var syncHost = new MockLythonHost();
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("/out/dup", sync.ReturnValue);
        Assert.Equal(new byte[] { 115, 101, 99, 111, 110, 100 }, syncHost.ReadBytes("/out/dup"));
        var asyncHost = new MockLythonHost();
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("/out/dup", asyncResult.ReturnValue);
        Assert.Equal(new byte[] { 115, 101, 99, 111, 110, 100 }, asyncHost.ReadBytes("/out/dup"));
    }
}

