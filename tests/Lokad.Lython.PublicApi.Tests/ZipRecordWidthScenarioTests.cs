using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R38: record-width validation rejects over-wide names, comments, and extras
/// before staging or narrowing while preserving archive contents, and ZIP64
/// promotion merges fresh size fields with unrelated carried fields.
/// </summary>
public sealed class ZipRecordWidthScenarioTests
{
    [Fact]
    public async Task OversizedNameRejectedPreservingContents()
    {
        var script = new LythonEngine().Compile(
            """
            import zipfile
            archive = zipfile.ZipFile("/out.zip", "w")
            results = []
            try:
                archive.writestr("a" * 65536, b"x")
                results.append("no-error")
            except ValueError:
                results.append("ValueError")
            archive.writestr("ok", b"data")
            archive.close()
            with zipfile.ZipFile("/out.zip") as reread:
                results.append(reread.namelist())
                results.append(reread.read("ok"))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "ValueError", new List<object?> { "ok" }, new byte[] { 100, 97, 116, 97 } };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task MaxWidthNameRoundTrips()
    {
        var script = new LythonEngine().Compile(
            """
            import zipfile
            archive = zipfile.ZipFile("/out.zip", "w")
            archive.writestr("a" * 65535, b"x")
            archive.close()
            with zipfile.ZipFile("/out.zip") as reread:
                names = reread.namelist()
                return [len(names[0]), reread.read(names[0])]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var values = Assert.IsType<List<object?>>(sync.ReturnValue);
        Assert.Equal(new BigInteger(65535), values[0]);
        Assert.Equal(new byte[] { 120 }, Assert.IsType<byte[]>(values[1]));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        var asyncValues = Assert.IsType<List<object?>>(asyncResult.ReturnValue);
        Assert.Equal(new BigInteger(65535), asyncValues[0]);
        Assert.Equal(new byte[] { 120 }, Assert.IsType<byte[]>(asyncValues[1]));
    }

    [Fact]
    public async Task MultibyteNameWidthsAreByteBased()
    {
        var script = new LythonEngine().Compile(
            """
            import zipfile
            archive = zipfile.ZipFile("/out.zip", "w")
            results = []
            try:
                archive.writestr("é" * 32768, b"x")
                results.append("no-error")
            except ValueError:
                results.append("ValueError")
            archive.writestr("é" * 32767, b"y")
            archive.close()
            with zipfile.ZipFile("/out.zip") as reread:
                names = reread.namelist()
                results.append(len(names))
                results.append(reread.read(names[0]))
            return results
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var values = Assert.IsType<List<object?>>(sync.ReturnValue);
        Assert.Equal("ValueError", values[0]);
        Assert.Equal(new BigInteger(1), values[1]);
        Assert.Equal(new byte[] { 121 }, Assert.IsType<byte[]>(values[2]));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        var asyncValues = Assert.IsType<List<object?>>(asyncResult.ReturnValue);
        Assert.Equal("ValueError", asyncValues[0]);
        Assert.Equal(new BigInteger(1), asyncValues[1]);
        Assert.Equal(new byte[] { 121 }, Assert.IsType<byte[]>(asyncValues[2]));
    }

    [Fact]
    public async Task OversizedCommentAndExtraRejected()
    {
        var script = new LythonEngine().Compile(
            """
            import zipfile
            results = []
            archive = zipfile.ZipFile("/out.zip", "w")
            info = zipfile.ZipInfo("a.txt")
            info.comment = bytes(__n)
            try:
                archive.writestr(info, b"data")
                results.append("comment-no-error")
            except ValueError:
                results.append("comment-ValueError")
            info2 = zipfile.ZipInfo("b.txt")
            info2.extra = bytes(__n)
            try:
                archive.writestr(info2, b"data")
                results.append("extra-no-error")
            except ValueError:
                results.append("extra-ValueError")
            archive.writestr("ok", b"data")
            archive.close()
            with zipfile.ZipFile("/out.zip") as reread:
                results.append(reread.namelist())
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "comment-ValueError", "extra-ValueError", new List<object?> { "ok" } };
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["__n"] = new BigInteger(65536) } };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task OversizedArchiveCommentFailsAtClose()
    {
        var script = new LythonEngine().Compile(
            """
            import zipfile
            archive = zipfile.ZipFile("/out.zip", "w")
            archive.writestr("ok", b"data")
            archive.comment = bytes(__n)
            archive.close()
            return 1
            """);
        Assert.True(script.IsValid);
        var commentOptions = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["__n"] = new BigInteger(65536) } };
        var sync = script.Run(new MockLythonHost(), commentOptions);
        Assert.False(sync.Success);
        Assert.Equal("ValueError", sync.Failure?.ExceptionType);
        var asyncResult = await script.RunAsync(new MockLythonHost(), commentOptions);
        Assert.False(asyncResult.Success);
        Assert.Equal("ValueError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task Zip64PromotionPreservesUnrelatedExtraFields()
    {
        var script = new LythonEngine().Compile(
            """
            import zipfile
            archive = zipfile.ZipFile("/out.zip", "w")
            info = zipfile.ZipInfo("x.bin")
            info.extra = b"\x02\x00\x04\x00ABCD"
            handle = archive.open(info, "w", force_zip64=True)
            handle.write(b"data")
            handle.close()
            archive.close()
            with zipfile.ZipFile("/out.zip") as reread:
                return reread.getinfo("x.bin").extra
            """);
        Assert.True(script.IsValid);
        var unrelated = new byte[] { 2, 0, 4, 0, 65, 66, 67, 68 };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var extra = Assert.IsType<byte[]>(sync.ReturnValue);
        Assert.Equal(36, extra.Length);
        Assert.Equal(unrelated, extra[..8]);
        Assert.Equal(1, extra[8] | (extra[9] << 8));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        var asyncExtra = Assert.IsType<byte[]>(asyncResult.ReturnValue);
        Assert.Equal(36, asyncExtra.Length);
        Assert.Equal(unrelated, asyncExtra[..8]);
        Assert.Equal(1, asyncExtra[8] | (asyncExtra[9] << 8));
    }
}

