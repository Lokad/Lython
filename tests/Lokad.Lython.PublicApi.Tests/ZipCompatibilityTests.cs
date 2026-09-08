using System.IO.Compression;
using System.Numerics;
using System.Text;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ZipCompatibilityTests
{
    [Fact]
    public void ZipRunsAgainstTheReferencedProductionAssembly()
    {
        var host = new ZipPublicHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    archive.writestr("a.txt", b"hello")
with zipfile.ZipFile("/out.zip") as archive:
    return archive.read("a.txt")
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new byte[] { 104, 101, 108, 108, 111 }, Assert.IsType<byte[]>(result.ReturnValue));
    }

    [Fact]
    public void PublicReadSurfaceListsAndReadsFixtures()
    {
        var host = SeedFixture("zip-stored");
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    info = archive.getinfo("hello.txt")
    return [archive.namelist(), archive.read("hello.txt"), info.file_size, archive.testzip()]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "hello.txt", "data/blob.bin" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(Encoding.UTF8.GetBytes("hello stored\n"), Assert.IsType<byte[]>(values[1]));
        Assert.Equal(new BigInteger(13), values[2]);
        Assert.Null(values[3]);
    }

    [Fact]
    public void PublicDuplicatesKeepOrderLastWins()
    {
        var host = SeedFixture("zip-duplicates");
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    return [archive.namelist(), archive.read("dup.txt")]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "dup.txt", "other.txt", "dup.txt" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(Encoding.UTF8.GetBytes("second\n"), Assert.IsType<byte[]>(values[1]));
    }

    [Fact]
    public void PublicCorruptMembersFailExplicitly()
    {
        var host = SeedFixture("zip-corrupt-crc");
        var result = new LythonEngine().Run(
            """
import zipfile
out = []
with zipfile.ZipFile("/t.zip") as archive:
    out.append(archive.testzip())
    try:
        archive.read("data.txt")
    except zipfile.BadZipFile:
        out.append("read-blocked")
return out
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void PublicWriteRoundTrip()
    {
        var host = new ZipPublicHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w", compression=zipfile.ZIP_DEFLATED) as archive:
    archive.writestr("a.txt", b"hello")
    archive.writestr("b.txt", "caf\u00e9")
    archive.mkdir("docs")
    archive.comment = b"cmt"
with zipfile.ZipFile("/out.zip") as archive:
    return [archive.namelist(), archive.read("a.txt"), archive.read("b.txt"), archive.comment]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "a.txt", "b.txt", "docs/" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(new byte[] { 104, 101, 108, 108, 111 }, Assert.IsType<byte[]>(values[1]));
        Assert.Equal(Encoding.UTF8.GetBytes("caf\u00e9"), Assert.IsType<byte[]>(values[2]));
        Assert.Equal(new byte[] { (byte)'c', (byte)'m', (byte)'t' }, Assert.IsType<byte[]>(values[3]));
        var abandoned = new LythonEngine().Run(
            """
import zipfile
archive = zipfile.ZipFile("/abandoned.zip", "w")
archive.writestr("a.txt", b"hello")
return 1
""",
            host);
        Assert.True(abandoned.Success, abandoned.Failure?.Message);
        Assert.False(host.Exists("/abandoned.zip"));
    }

    [Fact]
    public void PublicWriterBasics()
    {
        var host = new ZipPublicHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w", compression=zipfile.ZIP_DEFLATED) as archive:
    handle = archive.open("a.txt", "w")
    counts = [handle.write(b"hello"), handle.write(b"")]
    attrs = [handle.name, handle.mode, handle.closed, handle.readable(), handle.writable(), handle.seekable(), handle.flush() is None]
    handle.close()
    attrs.append(handle.closed)
with zipfile.ZipFile("/out.zip") as archive:
    return [counts, attrs, archive.read("a.txt")]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { new BigInteger(5), BigInteger.Zero }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(
            new List<object?> { "a.txt", "wb", false, false, true, false, true, true },
            Assert.IsType<List<object?>>(values[1]));
        Assert.Equal(new byte[] { 104, 101, 108, 108, 111 }, Assert.IsType<byte[]>(values[2]));
    }

    [Fact]
    public void PublicWriterConflictsStayExplicit()
    {
        var host = new ZipPublicHost();
        host.SeedBytes("/seed.bin", new byte[] { 1, 2, 3 });
        var result = new LythonEngine().Run(
            """
import zipfile
events = []
with zipfile.ZipFile("/out.zip", "w") as archive:
    first = archive.open("a.txt", "w")
    try:
        archive.open("b.txt", "w")
    except ValueError:
        events.append("open-blocked")
    try:
        archive.writestr("c.txt", b"x")
    except ValueError:
        events.append("writestr-blocked")
    try:
        archive.write("/seed.bin")
    except ValueError:
        events.append("write-blocked")
    archive.mkdir("docs")
    events.append("mkdir-ok")
    archive.printdir()
    try:
        archive.close()
    except ValueError:
        events.append("close-blocked")
    first.write(b"hello")
    first.close()
    archive.writestr("c.txt", b"x")
    events.append("writestr-ok")
return events
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "open-blocked", "writestr-blocked", "write-blocked", "mkdir-ok", "close-blocked", "writestr-ok" },
            Assert.IsType<List<object?>>(result.ReturnValue));
        var printed = host.StandardOutputText;
        Assert.Contains("docs/", printed);
        Assert.DoesNotContain("a.txt", printed);
        var reread = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip") as archive:
    return [archive.namelist(), archive.read("a.txt")]
""",
            host);
        Assert.True(reread.Success, reread.Failure?.Message);
        var values = Assert.IsType<List<object?>>(reread.ReturnValue);
        Assert.Equal(new List<object?> { "docs/", "a.txt", "c.txt" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(new byte[] { 104, 101, 108, 108, 111 }, Assert.IsType<byte[]>(values[1]));
    }

    [Fact]
    public void PublicAppendPreservesAcrossRuns()
    {
        var host = new ZipPublicHost();
        var first = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/a.zip", "w") as archive:
    archive.writestr("old.txt", b"OLD")
return 1
""",
            host);
        Assert.True(first.Success, first.Failure?.Message);
        var second = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/a.zip", "a") as archive:
    archive.writestr("new.txt", b"NEW")
    archive.mkdir("docs")
return 1
""",
            host);
        Assert.True(second.Success, second.Failure?.Message);
        using (var appended = new ZipArchive(new MemoryStream(host.ReadBytes("/a.zip")), ZipArchiveMode.Read))
        {
            Assert.Equal(new[] { "old.txt", "new.txt", "docs/" }, appended.Entries.Select(e => e.FullName).ToArray());
        }
        var third = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/a.zip") as archive:
    return [archive.namelist(), archive.read("old.txt"), archive.read("new.txt")]
""",
            host);
        Assert.True(third.Success, third.Failure?.Message);
        var values = Assert.IsType<List<object?>>(third.ReturnValue);
        Assert.Equal(new List<object?> { "old.txt", "new.txt", "docs/" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(Encoding.UTF8.GetBytes("OLD"), Assert.IsType<byte[]>(values[1]));
        Assert.Equal(Encoding.UTF8.GetBytes("NEW"), Assert.IsType<byte[]>(values[2]));
    }

    [Fact]
    public void PublicExtractBuildsContainedTree()
    {
        var host = new ZipPublicHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip", "w") as archive:
    archive.writestr("a.txt", b"A")
    archive.writestr("d/b.txt", b"B")
    archive.writestr("/abs.txt", b"ABS")
    archive.writestr("../up.txt", b"UP")
    archive.writestr("a/../../mix.txt", b"MIX")
    archive.writestr("./dot.txt", b"DOT")
    archive.writestr("sub//dbl.txt", b"DBL")
with zipfile.ZipFile("/t.zip") as archive:
    target = archive.extract("a.txt", "/out")
    defaulted = archive.extract("a.txt")
    archive.extractall("/out")
    return [target, defaulted]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var targets = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal("/out/a.txt", targets[0]);
        Assert.Equal("/a.txt", targets[1]);
        Assert.Equal(new byte[] { (byte)'A' }, host.ReadBytes("/out/a.txt"));
        Assert.Equal(new byte[] { (byte)'B' }, host.ReadBytes("/out/d/b.txt"));
        Assert.Equal(new byte[] { (byte)'A', (byte)'B', (byte)'S' }, host.ReadBytes("/out/abs.txt"));
        Assert.Equal(new byte[] { (byte)'U', (byte)'P' }, host.ReadBytes("/out/up.txt"));
        Assert.Equal(new byte[] { (byte)'M', (byte)'I', (byte)'X' }, host.ReadBytes("/out/a/mix.txt"));
        Assert.Equal(new byte[] { (byte)'D', (byte)'O', (byte)'T' }, host.ReadBytes("/out/dot.txt"));
        Assert.Equal(new byte[] { (byte)'D', (byte)'B', (byte)'L' }, host.ReadBytes("/out/sub/dbl.txt"));
        Assert.False(host.Exists("/abs.txt"));
        Assert.False(host.Exists("/up.txt"));
        Assert.False(host.Exists("/mix.txt"));
    }

    [Fact]
    public void PublicExtractSymlinkDenied()
    {
        var host = new ZipPublicHost();
        var written = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip", "w") as archive:
    link = zipfile.ZipInfo("link")
    link.create_system = 3
    link.external_attr = 2717843456
    archive.writestr(link, b"target")
    archive.writestr("plain.txt", b"plain")
return 1
""",
            host);
        Assert.True(written.Success, written.Failure?.Message);
        var denied = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    archive.extract("link", "/out")
""",
            host);
        Assert.False(denied.Success);
        Assert.Equal("NotImplementedError", denied.Failure?.ExceptionType);
        Assert.False(host.Exists("/out/link"));
        var allowed = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    return archive.extract("plain.txt", "/out")
""",
            host);
        Assert.True(allowed.Success, allowed.Failure?.Message);
        Assert.Equal("/out/plain.txt", allowed.ReturnValue);
        Assert.Equal(Encoding.UTF8.GetBytes("plain"), host.ReadBytes("/out/plain.txt"));
    }

    [Fact]
    public void PublicLifecycleRecoversAndCommits()
    {
        var host = new ZipPublicHost();
        var result = new LythonEngine().Run(
            """
import zipfile
archive = zipfile.ZipFile("/out.zip", "w")
handle = archive.open("f.txt", "w")
handle.write(b"orphan")
blocked = False
try:
    archive.close()
except ValueError:
    blocked = True
handle.close()
archive.close()
with zipfile.ZipFile("/out.zip") as check:
    return [blocked, check.namelist()]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(true, values[0]);
        Assert.Equal(new List<object?> { "f.txt" }, Assert.IsType<List<object?>>(values[1]));
    }

    [Fact]
    public void PublicBodyErrorStillCommits()
    {
        var host = new ZipPublicHost();
        var failed = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    with archive.open("e.txt", "w") as handle:
        handle.write(b"committed?")
        raise ValueError("unrelated")
""",
            host);
        Assert.False(failed.Success);
        Assert.Equal("ValueError", failed.Failure?.ExceptionType);
        var reread = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip") as archive:
    return [archive.read("e.txt"), archive.testzip() is None]
""",
            host);
        Assert.True(reread.Success, reread.Failure?.Message);
        var values = Assert.IsType<List<object?>>(reread.ReturnValue);
        Assert.Equal(Encoding.UTF8.GetBytes("committed?"), Assert.IsType<byte[]>(values[0]));
        Assert.Equal(true, values[1]);
        var archived = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out2.zip", "w") as archive:
    archive.writestr("a.txt", b"abc")
    raise ValueError("unrelated")
""",
            host);
        Assert.False(archived.Success);
        Assert.Equal("ValueError", archived.Failure?.ExceptionType);
        var rereadArchived = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out2.zip") as archive:
    return archive.read("a.txt")
""",
            host);
        Assert.True(rereadArchived.Success, rereadArchived.Failure?.Message);
        Assert.Equal(new byte[] { 97, 98, 99 }, Assert.IsType<byte[]>(rereadArchived.ReturnValue));
    }

    [Fact]
    public async Task PublicCancellationFailsExplicitly()
    {
        var host = new ZipPublicHost();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = await new LythonEngine().RunAsync(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    handle = archive.open("a.txt", "w")
    for i in range(200):
        handle.write(b"Z")
    handle.close()
return 1
""",
            host,
            cancellationToken: cancellation.Token);
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublicSyncAsyncParity()
    {
        var script = new LythonEngine().Compile(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    archive.writestr("a.txt", b"hello")
with zipfile.ZipFile("/out.zip") as archive:
    return archive.namelist()[0] + "|" + archive.read("a.txt").decode("utf-8")
""");
        Assert.True(script.IsValid);
        var sync = script.Run(new ZipPublicHost());
        var asyncResult = await script.RunAsync(new ZipPublicHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("a.txt|hello", sync.ReturnValue);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public void PublicExtractPartialProgressOnHostFailure()
    {
        var host = SeedFixture("zip-stored");
        host.FailWriteBytes("/out/hello.txt", "disk full");
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    archive.extract("data/blob.bin", "/out")
    archive.extract("hello.txt", "/out")
""",
            host);
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("write_bytes", result.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(256, host.ReadBytes("/out/data/blob.bin").Length);
        Assert.False(host.Exists("/out/hello.txt"));
    }

    [Fact]
    public void PublicRunsStayIsolated()
    {
        const string script = """
            import zipfile
            with zipfile.ZipFile("/a.zip", "w") as archive:
                archive.writestr("x.txt", b"X")
            with zipfile.ZipFile("/a.zip") as archive:
                return archive.read("x.txt")
            """;
        var firstHost = new ZipPublicHost();
        var secondHost = new ZipPublicHost();
        var first = new LythonEngine().Run(script, firstHost);
        var second = new LythonEngine().Run(script, secondHost);
        Assert.True(first.Success, first.Failure?.Message);
        Assert.True(second.Success, second.Failure?.Message);
        Assert.Equal(firstHost.ReadBytes("/a.zip"), secondHost.ReadBytes("/a.zip"));
        Assert.False(firstHost.Exists("/b.zip"));
    }

    [Fact]
    public void PublicBindingErrorsStayExplicit()
    {
        var host = new ZipPublicHost();
        var cases = new (string Expression, string Type)[]
        {
            ("ZipFile(\"/t.zip\", \"q\")", "ValueError"),
            ("ZipFile(\"/n.zip\", \"x\")", "NotImplementedError"),
            ("ZipFile(\"/missing.zip\")", "FileNotFoundError"),
            ("ZipFile(\"/t.zip\", compression=\"x\")", "NotImplementedError"),
            ("ZipFile(123)", "TypeError"),
            ("ZipInfo(\"x\", date_time=(1970, 1, 1, 0, 0, 0))", "ValueError"),
            ("ZipInfo(123)", "TypeError"),
            ("ZipFile(\"/t.zip\", metadata_encoding=\"rot13\")", "LookupError"),
        };
        foreach (var (expression, typeName) in cases)
        {
            var setup = new LythonEngine().Run(
                "import zipfile\nwith zipfile.ZipFile(\"/t.zip\", \"w\") as archive:\n    archive.writestr(\"a.txt\", b\"A\")\n",
                host);
            Assert.True(setup.Success, setup.Failure?.Message);
            var result = new LythonEngine().Run("import zipfile\nzipfile." + expression + "\n", host);
            Assert.False(result.Success, expression);
            Assert.Equal(typeName, result.Failure?.ExceptionType);
        }
    }

    [Fact]
    public void PublicForceZip64AndComment()
    {
        var host = new ZipPublicHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    plain = archive.open("plain.txt", "w")
    plain.write(b"p")
    plain.close()
    forced = archive.open("f.bin", "w", force_zip64=True)
    forced.write(b"tiny")
    forced.close()
    archive.comment = b"cmt"
    archive.printdir()
return 1
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var raw = host.ReadBytes("/out.zip");
        Assert.Equal(20, raw[4]);
        Assert.Equal(45, raw[44]);
        Assert.Contains("f.bin", host.StandardOutputText, StringComparison.Ordinal);
        var second = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    truthy = archive.open("truthy.bin", "w", force_zip64=1)
    truthy.close()
return 1
""",
            host);
        Assert.True(second.Success, second.Failure?.Message);
        var denied = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/z.zip", "w", allowZip64=False) as archive:
    archive.open("s.txt", "w", force_zip64=True)
""",
            new ZipPublicHost());
        Assert.False(denied.Success);
        Assert.Equal("ValueError", denied.Failure?.ExceptionType);
        Assert.Contains("allowZip64", denied.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicBclReadsGeneratedArchives()
    {
        var host = new ZipPublicHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w", compression=zipfile.ZIP_DEFLATED) as archive:
    archive.writestr("a.txt", b"hello")
    info = zipfile.ZipInfo("keep.bin")
    info.compress_type = zipfile.ZIP_STORED
    stored = archive.open(info, "w")
    stored.write(b"raw")
    stored.close()
    archive.mkdir("docs")
return 1
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        using var archive = new ZipArchive(new MemoryStream(host.ReadBytes("/out.zip")), ZipArchiveMode.Read);
        Assert.Equal(new[] { "a.txt", "keep.bin", "docs/" }, archive.Entries.Select(e => e.FullName).ToArray());
    }

    private static string FindCasesRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "Fixtures", "zipfile", "cases");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("ZIP fixture cases not found.");
    }

    private static ZipPublicHost SeedFixture(string caseId, string path = "/t.zip")
    {
        var host = new ZipPublicHost();
        host.SeedBytes(path, File.ReadAllBytes(Path.Combine(FindCasesRoot(), caseId, "input.zip")));
        return host;
    }

    private static byte[] HexToBytes(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return bytes;
    }

    private static uint ComputeCrc32(byte[] content)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in content)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xEDB88320u;
            }
        }

        return ~crc;
    }

    private static byte[] BuildRawArchive(params (byte[] Name, ushort Flags, ushort Method, byte[] Content)[] records)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            var offsets = new List<long>();
            foreach (var (name, flags, method, content) in records)
            {
                offsets.Add(stream.Position);
                var crc = ComputeCrc32(content);
                writer.Write(0x04034B50u);
                writer.Write((ushort)20);
                writer.Write(flags);
                writer.Write(method);
                writer.Write((ushort)0x5C64);
                writer.Write((ushort)0xD938);
                writer.Write(crc);
                writer.Write((uint)content.Length);
                writer.Write((uint)content.Length);
                writer.Write((ushort)name.Length);
                writer.Write((ushort)0);
                writer.Write(name);
                writer.Write(content);
            }

            var directoryOffset = stream.Position;
            for (var i = 0; i < records.Length; i++)
            {
                var (name, flags, method, content) = records[i];
                var crc = ComputeCrc32(content);
                writer.Write(0x02014B50u);
                writer.Write((ushort)20);
                writer.Write((ushort)20);
                writer.Write(flags);
                writer.Write(method);
                writer.Write((ushort)0x5C64);
                writer.Write((ushort)0xD938);
                writer.Write(crc);
                writer.Write((uint)content.Length);
                writer.Write((uint)content.Length);
                writer.Write((ushort)name.Length);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write(0u);
                writer.Write((uint)offsets[i]);
                writer.Write(name);
            }

            var directorySize = stream.Position - directoryOffset;
            writer.Write(0x06054B50u);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)records.Length);
            writer.Write((ushort)records.Length);
            writer.Write((uint)directorySize);
            writer.Write((uint)directoryOffset);
            writer.Write((ushort)0);
        }

        return stream.ToArray();
    }

    [Fact]
    public void PublicCommentAndPrintdir()
    {
        var host = new ZipPublicHost();
        host.LocalNow = new DateTimeOffset(2024, 5, 6, 7, 8, 10, TimeSpan.FromHours(2));
        var stamp = "2024-05-06 07:08:10";
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    archive.writestr("b.txt", b"22")
    archive.writestr("a.txt", b"1")
    archive.comment = b"hello \xff"
    archive.printdir()
    try:
        archive.comment = "text"
    except TypeError:
        outcome = "type-error"
    return [archive.comment, outcome]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new byte[] { 104, 101, 108, 108, 111, 32, 255 }, Assert.IsType<byte[]>(values[0]));
        Assert.Equal("type-error", values[1]);
        var printed = host.StandardOutputText;
        var names = new List<string> { "b.txt", "a.txt" };
        var expected = "File Name".PadRight(46) + " " + "Modified" + " " + "Size".PadLeft(12) + "\n";
        foreach (var entry in new[] { (names[0], 2), (names[1], 1) })
        {
            expected += entry.Item1.PadRight(46) + " " + stamp + " " + entry.Item2.ToString().PadLeft(12) + "\n";
        }

        Assert.Equal(expected, printed);
    }

    [Fact]
    public void WriterRejectsNonBytesAndClosedUse()
    {
        var host = new ZipPublicHost();
        var result = new LythonEngine().Run(
            """
import zipfile
checks = []
with zipfile.ZipFile("/out.zip", "w") as archive:
    handle = archive.open("a.txt", "w")
    try:
        handle.write("nope")
    except TypeError:
        checks.append("str-type")
    try:
        handle.write(123)
    except TypeError:
        checks.append("int-type")
    checks.append(handle.closed)
    handle.close()
    checks.append(handle.closed)
    checks.append(handle.close() is None)
    try:
        handle.write(b"late")
    except ValueError:
        checks.append("closed-value")
    try:
        handle.read()
    except NotImplementedError:
        checks.append("read-blocked")
    try:
        handle.tell()
    except NotImplementedError:
        checks.append("tell-blocked")
    try:
        handle.seek(0)
    except NotImplementedError:
        checks.append("seek-blocked")
return checks
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "str-type", "int-type", false, true, true, "closed-value", "read-blocked", "tell-blocked", "seek-blocked" },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void WriterWritelinesKeepsPartialItems()
    {
        var host = new ZipPublicHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    done = archive.open("w.txt", "w")
    done.writelines([b"a", b"bc"])
    done.close()
    partial = archive.open("p.txt", "w")
    try:
        partial.writelines([b"a", 7])
    except TypeError:
        partial.close()
with zipfile.ZipFile("/out.zip") as archive:
    return [archive.read("w.txt"), archive.read("p.txt")]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(Encoding.UTF8.GetBytes("abc"), Assert.IsType<byte[]>(values[0]));
        Assert.Equal(Encoding.UTF8.GetBytes("a"), Assert.IsType<byte[]>(values[1]));
    }

    [Fact]
    public void WriterEmptyMemberAndDirectory()
    {
        var host = new ZipPublicHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    empty = archive.open("empty.txt", "w")
    empty.close()
    empty.close()
    folder = archive.open("docs/", "w")
    folder.close()
with zipfile.ZipFile("/out.zip") as archive:
    return [archive.namelist(), archive.getinfo("empty.txt").file_size, archive.getinfo("docs/").is_dir(), archive.read("empty.txt")]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "empty.txt", "docs/" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(BigInteger.Zero, values[1]);
        Assert.Equal(true, values[2]);
        Assert.Equal(Array.Empty<byte>(), Assert.IsType<byte[]>(values[3]));
    }

    [Fact]
    public async Task ZipInfoBoolDateTimeIsPreserved()
    {
        // R18: CPython exposes supplied booleans as bools instead of rewriting
        // them to integers; the values still encode like 0/1 on write.
        var script = new LythonEngine().Compile(
            """
            import zipfile
            info = zipfile.ZipInfo("b.bin", date_time=(1980, 1, 1, True, False, 0))
            first = info.date_time
            info.date_time = [2020, 5, 6, True, 8, 10]
            with zipfile.ZipFile("/out.zip", "w") as archive:
                archive.writestr(info, b"data")
            with zipfile.ZipFile("/out.zip") as archive:
                stored = archive.getinfo("b.bin").date_time
            return [first, info.date_time, stored]
            """);
        Assert.True(script.IsValid);
        var host = new ZipPublicHost();
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                new List<object?> { new BigInteger(1980), new BigInteger(1), new BigInteger(1), true, false, new BigInteger(0) },
                new List<object?> { new BigInteger(2020), new BigInteger(5), new BigInteger(6), true, new BigInteger(8), new BigInteger(10) },
                new List<object?> { new BigInteger(2020), new BigInteger(5), new BigInteger(6), new BigInteger(1), new BigInteger(8), new BigInteger(10) },
            },
            Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new ZipPublicHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                new List<object?> { new BigInteger(1980), new BigInteger(1), new BigInteger(1), true, false, new BigInteger(0) },
                new List<object?> { new BigInteger(2020), new BigInteger(5), new BigInteger(6), true, new BigInteger(8), new BigInteger(10) },
                new List<object?> { new BigInteger(2020), new BigInteger(5), new BigInteger(6), new BigInteger(1), new BigInteger(8), new BigInteger(10) },
            },
            Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public void ZipInfoWriterInputsAreHonored()
    {
        var host = new ZipPublicHost();
        var result = new LythonEngine().Run(
            """
import zipfile
results = []
with zipfile.ZipFile("/out.zip", "w") as archive:
    info = zipfile.ZipInfo("zi.bin", date_time=(2020, 5, 6, 7, 8, 10))
    info.compress_type = zipfile.ZIP_STORED
    info.comment = b"zc"
    handle = archive.open(info, "w")
    results.append(handle.name)
    total = 0
    for i in range(100):
        total = total + handle.write(b"Z")
    results.append(total)
    handle.close()
results.append([info.file_size, info.compress_size, info.CRC == 0])
with zipfile.ZipFile("/out.zip") as archive:
    got = archive.getinfo("zi.bin")
    results.append([got.filename, got.compress_type, got.date_time, got.comment, got.file_size])
return results
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal("zi.bin", values[0]);
        Assert.Equal(new BigInteger(100), values[1]);
        Assert.Equal(new List<object?> { new BigInteger(100), new BigInteger(100), false }, Assert.IsType<List<object?>>(values[2]));
        Assert.Equal(
            new List<object?>
            {
                "zi.bin",
                BigInteger.Zero,
                new List<object?> { new BigInteger(2020), new BigInteger(5), new BigInteger(6), new BigInteger(7), new BigInteger(8), new BigInteger(10) },
                new byte[] { 122, 99 },
                new BigInteger(100),
            },
            Assert.IsType<List<object?>>(values[3]));
    }

    [Fact]
    public async Task WriterSurfaceWorksAsync()
    {
        var host = new ZipPublicHost();
        var result = await new LythonEngine().RunAsync(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    with archive.open("a.txt", "w") as handle:
        handle.write(b"hello")
        handle.writelines([b" ", b"world"])
with zipfile.ZipFile("/out.zip") as archive:
    return archive.read("a.txt")
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(Encoding.UTF8.GetBytes("hello world"), Assert.IsType<byte[]>(result.ReturnValue));
    }

    [Fact]
    public void DuplicatesAndSuppliedInfoSurvive()
    {
        var host = new ZipPublicHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    archive.writestr("dup.txt", b"first")
    info = zipfile.ZipInfo("dup.txt", date_time=(2020, 5, 6, 7, 8, 10))
    info.comment = b"cmt"
    info.external_attr = 420
    archive.writestr(info, b"second")
    archive.writestr("plain.txt", b"x")
with zipfile.ZipFile("/out.zip") as archive:
    second = archive.getinfo("dup.txt")
    return [archive.namelist(), archive.read("dup.txt"), archive.read(archive.infolist()[0]),
            second.date_time, second.comment, second.external_attr, second.file_size, info.file_size]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "dup.txt", "dup.txt", "plain.txt" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(Encoding.UTF8.GetBytes("second"), Assert.IsType<byte[]>(values[1]));
        Assert.Equal(Encoding.UTF8.GetBytes("first"), Assert.IsType<byte[]>(values[2]));
        Assert.Equal(
            new List<object?> { new BigInteger(2020), new BigInteger(5), new BigInteger(6), new BigInteger(7), new BigInteger(8), new BigInteger(10) },
            values[3]);
        Assert.Equal(Encoding.UTF8.GetBytes("cmt"), Assert.IsType<byte[]>(values[4]));
        Assert.Equal(new BigInteger(420), values[5]);
        Assert.Equal(new BigInteger(6), values[6]);
        Assert.Equal(new BigInteger(6), values[7]);
    }

    [Fact]
    public void AppendMissingCreatesEmptyAppendSkipsPublish()
    {
        var created = new ZipPublicHost();
        var missing = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/n.zip", "a") as archive:
    archive.writestr("n.txt", b"n")
with zipfile.ZipFile("/n.zip") as archive:
    return archive.namelist()
""",
            created);
        Assert.True(missing.Success, missing.Failure?.Message);
        Assert.Equal(new List<object?> { "n.txt" }, Assert.IsType<List<object?>>(missing.ReturnValue));

        var host = SeedFixture("zip-stored");
        var before = host.ReadBytes("/t.zip");
        var untouched = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip", "a") as archive:
    archive.comment = archive.comment
return 1
""",
            host);
        Assert.True(untouched.Success, untouched.Failure?.Message);
        Assert.Equal(before, host.ReadBytes("/t.zip"));

        var guarded = SeedFixture("zip-stored");
        guarded.FailNextWriteBytes("/t.zip", "must not publish an unmodified append");
        var skipped = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip", "a"):
    pass
return 1
""",
            guarded);
        Assert.True(skipped.Success, skipped.Failure?.Message);
    }

    [Fact]
    public void AppendCommentPreservedUnlessReassigned()
    {
        var host = SeedFixture("zip-comments");
        var preserved = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip", "a") as archive:
    archive.writestr("b.txt", b"B")
with zipfile.ZipFile("/t.zip") as archive:
    return archive.comment
""",
            host);
        Assert.True(preserved.Success, preserved.Failure?.Message);
        Assert.Equal(
            HexToBytes("61726368697665205a3020c3a920ff20726177"),
            Assert.IsType<byte[]>(preserved.ReturnValue));

        var replaced = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip", "a") as archive:
    archive.comment = b"new"
with zipfile.ZipFile("/t.zip") as archive:
    return archive.comment
""",
            host);
        Assert.True(replaced.Success, replaced.Failure?.Message);
        Assert.Equal(new byte[] { (byte)'n', (byte)'e', (byte)'w' }, Assert.IsType<byte[]>(replaced.ReturnValue));
    }

    [Fact]
    public void AppendDuplicatesAcrossBoundary()
    {
        var host = SeedFixture("zip-duplicates");
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip", "a") as archive:
    archive.writestr("dup.txt", b"third")
with zipfile.ZipFile("/t.zip") as archive:
    return [archive.namelist(), archive.read("dup.txt"), archive.testzip() is None]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "dup.txt", "other.txt", "dup.txt", "dup.txt" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(Encoding.UTF8.GetBytes("third"), Assert.IsType<byte[]>(values[1]));
        Assert.Equal(true, values[2]);
    }

    [Fact]
    public void AppendPreservesUnreadableEntries()
    {
        var host = new ZipPublicHost();
        host.SeedBytes("/t.zip", BuildRawArchive(
            (Encoding.ASCII.GetBytes("plain.txt"), (ushort)0, (ushort)0, Encoding.ASCII.GetBytes("plain")),
            (Encoding.ASCII.GetBytes("enc.txt"), (ushort)1, (ushort)0, Encoding.ASCII.GetBytes("secret"))));
        var result = new LythonEngine().Run(
            """
import zipfile
out = []
with zipfile.ZipFile("/t.zip", "a") as archive:
    archive.writestr("new.txt", b"new")
with zipfile.ZipFile("/t.zip") as archive:
    out.append(archive.namelist())
    out.append(archive.read("new.txt"))
    out.append(archive.read("plain.txt"))
    try:
        archive.read("enc.txt")
    except RuntimeError:
        out.append("enc-blocked")
return out
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "plain.txt", "enc.txt", "new.txt" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(Encoding.UTF8.GetBytes("new"), Assert.IsType<byte[]>(values[1]));
        Assert.Equal(Encoding.UTF8.GetBytes("plain"), Assert.IsType<byte[]>(values[2]));
        Assert.Equal("enc-blocked", values[3]);
    }

    [Fact]
    public void AppendZip64Source()
    {
        var host = SeedFixture("zip-zip64");
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip", "a") as archive:
    archive.writestr("b.txt", b"B")
with zipfile.ZipFile("/t.zip") as archive:
    return [archive.namelist(), archive.read("tiny.txt"), archive.read("b.txt"), archive.testzip() is None]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "tiny.txt", "b.txt" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(Encoding.UTF8.GetBytes("zip64 forced\n"), Assert.IsType<byte[]>(values[1]));
        Assert.Equal(new byte[] { (byte)'B' }, Assert.IsType<byte[]>(values[2]));
        Assert.Equal(true, values[3]);
    }

    [Fact]
    public void ExtractMembersSubset()
    {
        var host = SeedFixture("zip-stored");
        var result = new LythonEngine().Run(
            """
import zipfile
out = []
with zipfile.ZipFile("/t.zip") as archive:
    archive.extractall("/out", members=["hello.txt", archive.getinfo("data/blob.bin")])
    out.append(archive.extract("hello.txt", "/other"))
    try:
        archive.extract("nope.txt", "/out")
    except KeyError:
        out.append("missing-key")
    try:
        archive.extractall("/out", members=["nope.txt"])
    except KeyError:
        out.append("members-key")
return out
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "/other/hello.txt", "missing-key", "members-key" },
            Assert.IsType<List<object?>>(result.ReturnValue));
        Assert.Equal(Encoding.UTF8.GetBytes("hello stored\n"), host.ReadBytes("/out/hello.txt"));
        Assert.Equal(256, host.ReadBytes("/out/data/blob.bin").Length);
    }

    [Fact]
    public void ExtractFileDirectoryConflictsFail()
    {
        var host = new ZipPublicHost();
        var written = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip", "w") as archive:
    archive.mkdir("clash")
    archive.writestr("clash", b"file")
    archive.writestr("solo", b"solo")
return 1
""",
            host);
        Assert.True(written.Success, written.Failure?.Message);
        var conflict = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    archive.extractall("/out")
""",
            host);
        Assert.False(conflict.Success);
        Assert.Equal("ValueError", conflict.Failure?.ExceptionType);
        Assert.True(host.Exists("/out/clash"));
        Assert.False(host.Exists("/out/solo"));

        var blocked = new ZipPublicHost();
        blocked.SeedBytes("/out/d2", new byte[] { 1 });
        var setup = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip", "w") as archive:
    archive.mkdir("d2")
return 1
""",
            blocked);
        Assert.True(setup.Success, setup.Failure?.Message);
        var dirConflict = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    archive.extract("d2/", "/out")
""",
            blocked);
        Assert.False(dirConflict.Success);
        Assert.Equal("ValueError", dirConflict.Failure?.ExceptionType);
    }

    [Fact]
    public void ExtractOverwritesAndKeepsLastDuplicate()
    {
        var host = SeedFixture("zip-duplicates");
        host.SeedBytes("/out/dup.txt", Encoding.ASCII.GetBytes("OLD"));
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    archive.extractall("/out")
    return archive.extract("dup.txt", "/other")
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/other/dup.txt", result.ReturnValue);
        Assert.Equal(Encoding.UTF8.GetBytes("second\n"), host.ReadBytes("/out/dup.txt"));
        Assert.Equal(Encoding.UTF8.GetBytes("second\n"), host.ReadBytes("/other/dup.txt"));
    }

    [Fact]
    public async Task ExtractSurfaceWorksAsync()
    {
        var host = SeedFixture("zip-deflated");
        var result = await new LythonEngine().RunAsync(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    archive.extractall("/out", members=["default.txt"])
    return archive.extract("empty.txt", "/out")
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/out/empty.txt", result.ReturnValue);
        Assert.Equal(900, host.ReadBytes("/out/default.txt").Length);
        Assert.Empty(host.ReadBytes("/out/empty.txt"));
    }

    [Fact]
    public void ExtractModeGuards()
    {
        var wrote = new ZipPublicHost();
        var setup = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip", "w") as archive:
    archive.writestr("a.txt", b"A")
return 1
""",
            wrote);
        Assert.True(setup.Success, setup.Failure?.Message);
        foreach (var mode in new[] { "w", "a" })
        {
            var denied = new LythonEngine().Run(
                "import zipfile\nwith zipfile.ZipFile(\"/t.zip\", \"" + mode + "\") as archive:\n    archive.extract(\"a.txt\", \"/out\")\n",
                wrote);
            Assert.False(denied.Success);
            Assert.Equal("ValueError", denied.Failure?.ExceptionType);
            Assert.Contains("requires mode", denied.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AppendModeGuards()
    {
        var host = SeedFixture("zip-stored");
        var result = new LythonEngine().Run(
            """
import zipfile
checks = []
with zipfile.ZipFile("/t.zip", "a") as archive:
    checks.append([archive.filename, archive.mode])
    try:
        archive.namelist()
    except ValueError:
        checks.append("namelist-blocked")
    try:
        archive.read("hello.txt")
    except ValueError:
        checks.append("read-blocked")
    try:
        archive.getinfo("hello.txt")
    except ValueError:
        checks.append("getinfo-blocked")
    try:
        archive.testzip()
    except ValueError:
        checks.append("testzip-blocked")
    try:
        archive.open("hello.txt", "r")
    except ValueError:
        checks.append("open-r-blocked")
    archive.writestr("b.txt", b"B")
    checks.append("writestr-ok")
return checks
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                new List<object?> { "/t.zip", "a" },
                "namelist-blocked",
                "read-blocked",
                "getinfo-blocked",
                "testzip-blocked",
                "open-r-blocked",
                "writestr-ok",
            },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public async Task AppendSurfaceWorksAsync()
    {
        var host = SeedFixture("zip-deflated");
        var result = await new LythonEngine().RunAsync(
            """
import zipfile
with zipfile.ZipFile("/t.zip", "a") as archive:
    archive.writestr("b.txt", b"B")
with zipfile.ZipFile("/t.zip") as archive:
    return [archive.namelist(), archive.read("b.txt")]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "default.txt", "level1.txt", "level9.txt", "empty.txt", "b.txt" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(new byte[] { (byte)'B' }, Assert.IsType<byte[]>(values[1]));
    }

    [Fact]
    public void AppendRejectsGarbageTruncatedAndDirectories()
    {
        var garbage = new ZipPublicHost();
        garbage.SeedBytes("/t.zip", Encoding.ASCII.GetBytes("this is not a zip file"));
        var denied = new LythonEngine().Run(
            "import zipfile\nzipfile.ZipFile(\"/t.zip\", \"a\")\n",
            garbage);
        Assert.False(denied.Success);
        Assert.Equal("BadZipFile", denied.Failure?.ExceptionType);

        var truncated = new ZipPublicHost();
        var full = File.ReadAllBytes(Path.Combine(FindCasesRoot(), "zip-stored", "input.zip"));
        truncated.SeedBytes("/t.zip", full[..Math.Min(40, full.Length)]);
        var shortDenied = new LythonEngine().Run(
            "import zipfile\nzipfile.ZipFile(\"/t.zip\", \"a\")\n",
            truncated);
        Assert.False(shortDenied.Success);
        Assert.Equal("BadZipFile", shortDenied.Failure?.ExceptionType);

        var rooted = new ZipPublicHost();
        var directory = new LythonEngine().Run(
            "import zipfile\nzipfile.ZipFile(\"/\", \"a\")\n",
            rooted);
        Assert.False(directory.Success);
        Assert.Equal("IsADirectoryError", directory.Failure?.ExceptionType);
    }

    [Fact]
    public void TimestampsFollowHostClockAndBackslashesSanitize()
    {
        var host = new ZipPublicHost();
        host.LocalNow = new DateTimeOffset(2024, 5, 6, 7, 8, 57, TimeSpan.FromHours(2));
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    archive.writestr("odd.txt", b"x")
    archive.writestr("a\\b.txt", b"y")
with zipfile.ZipFile("/out.zip") as archive:
    return [archive.namelist(), archive.getinfo("odd.txt").date_time]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "odd.txt", "a/b.txt" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(
            new List<object?> { new BigInteger(2024), new BigInteger(5), new BigInteger(6), new BigInteger(7), new BigInteger(8), new BigInteger(56) },
            values[1]);
    }

    [Fact]
    public void CompressionOptionsHonored()
    {
        var host = new ZipPublicHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w", compression=zipfile.ZIP_DEFLATED) as archive:
    archive.writestr("d.txt", "z" * 1000)
    archive.writestr("s.txt", "z" * 1000, compress_type=zipfile.ZIP_STORED)
with zipfile.ZipFile("/out.zip") as archive:
    return [[archive.getinfo("d.txt").compress_type, archive.getinfo("d.txt").compress_size],
            [archive.getinfo("s.txt").compress_type, archive.getinfo("s.txt").compress_size],
            len(archive.read("d.txt")), archive.read("d.txt")[:4]]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        var deflated = Assert.IsType<List<object?>>(values[0]);
        Assert.Equal(new BigInteger(8), deflated[0]);
        Assert.True(Assert.IsType<BigInteger>(deflated[1]).CompareTo(new BigInteger(1000)) < 0);
        var stored = Assert.IsType<List<object?>>(values[1]);
        Assert.Equal(new BigInteger(0), stored[0]);
        Assert.Equal(new BigInteger(1000), stored[1]);
        Assert.Equal(new BigInteger(1000), values[2]);
        Assert.Equal(new byte[] { 122, 122, 122, 122 }, Assert.IsType<byte[]>(values[3]));
    }

    [Fact]
    public void CompressionOptionErrorsAreExplicit()
    {
        foreach (var (expression, typeName, message) in new (string, string, string)[]
        {
            ("writestr(\"a.txt\", b\"x\", compress_type=12)", "NotImplementedError", "not supported"),
            ("writestr(\"a.txt\", b\"x\", compresslevel=10)", "ValueError", "compression level"),
            ("writestr(123, b\"x\")", "TypeError", "str or ZipInfo"),
            ("writestr(\"a.txt\", 123)", "TypeError", "bytes or str"),
            ("mkdir(123)", "TypeError", "str or ZipInfo"),
        })
        {
            var host = new ZipPublicHost();
            var result = new LythonEngine().Run(
                "import zipfile\nwith zipfile.ZipFile(\"/out.zip\", \"w\") as archive:\n    archive." + expression + "\n",
                host);
            Assert.False(result.Success);
            Assert.Equal(typeName, result.Failure?.ExceptionType);
            Assert.Contains(message, result.Failure?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FailedWritestrStagesNothing()
    {
        var host = new ZipPublicHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    try:
        archive.writestr(123, b"x")
    except TypeError:
        pass
    archive.writestr("good.txt", b"ok")
with zipfile.ZipFile("/out.zip") as archive:
    return archive.namelist()
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { "good.txt" }, Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void FailedCloseIsRetryableWithoutDoublePublish()
    {
        var host = new ZipPublicHost();
        host.FailWriteBytes("/out.zip", "disk is full");
        var failed = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    archive.writestr("a.txt", b"hello")
    try:
        archive.close()
    except RuntimeError:
        seen = True
    archive.writestr("b.txt", b"world")
    archive.close()
    return seen
""",
            host);
        Assert.False(failed.Success);
        Assert.Equal("RuntimeError", failed.Failure?.ExceptionType);
        Assert.False(host.Exists("/out.zip"));
        host.ClearWriteBytesFailures();
        var recovered = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    archive.writestr("a.txt", b"hello")
    archive.close()
    archive.close()
with zipfile.ZipFile("/out.zip") as archive:
    return archive.namelist()
""",
            host);
        Assert.True(recovered.Success, recovered.Failure?.Message);
        Assert.Equal(new List<object?> { "a.txt" }, Assert.IsType<List<object?>>(recovered.ReturnValue));
    }

    [Fact]
    public void HugeDeclaredSizesRequireZip64Explicitly()
    {
        var host = new ZipPublicHost();
        var denied = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w", allowZip64=False) as archive:
    info = zipfile.ZipInfo("big.bin")
    info.file_size = 3000000000
    archive.writestr(info, b"tiny")
""",
            host);
        Assert.False(denied.Success);
        Assert.Equal("LargeZipFile", denied.Failure?.ExceptionType);
        var reopened = new LythonEngine().Run(
            "import zipfile\nwith zipfile.ZipFile(\"/out.zip\") as archive:\n    return archive.namelist()\n",
            host);
        Assert.True(reopened.Success, reopened.Failure?.Message);
        Assert.Equal(new List<object?>(), Assert.IsType<List<object?>>(reopened.ReturnValue));
    }

    [Fact]
    public void PrintdirReadsDirectoryListings()
    {
        var host = SeedFixture("zip-stored");
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    archive.printdir()
    return archive.namelist()
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var printed = host.StandardOutputText;
        var expected = "File Name".PadRight(46) + " " + "Modified" + " " + "Size".PadLeft(12) + "\n";
        expected += "hello.txt".PadRight(46) + " 2024-02-29 12:34:56" + " " + "13".PadLeft(12) + "\n";
        expected += "data/blob.bin".PadRight(46) + " 2024-02-29 12:34:56" + " " + "256".PadLeft(12) + "\n";
        Assert.Equal(expected, printed);
    }

    [Fact]
    public void ExtractUnsafeNamesFailExplicitly()
    {
        var host = new ZipPublicHost();
        host.SeedBytes("/t.zip", BuildRawArchive(
            (new byte[] { (byte)'b', (byte)'a', (byte)'c', (byte)'\\', (byte)'k' }, (ushort)0, (ushort)0, Encoding.ASCII.GetBytes("x")),
            (new byte[] { (byte)'C', (byte)':', (byte)'d' }, (ushort)0, (ushort)0, Encoding.ASCII.GetBytes("x")),
            (new byte[] { (byte)'n', 0, (byte)'b' }, (ushort)0, (ushort)0, Encoding.ASCII.GetBytes("x")),
            (new byte[] { (byte)'.', (byte)'.' }, (ushort)0, (ushort)0, Encoding.ASCII.GetBytes("x")),
            (new byte[] { (byte)'/', (byte)'/', (byte)'s' }, (ushort)0, (ushort)0, Encoding.ASCII.GetBytes("x"))));
        var cases = new (string Expression, string Message)[]
        {
            ("\"bac\\\\k\"", "backslashes"),
            ("\"C:d\"", "drive"),
            ("\"n\\x00b\"", "NUL"),
            ("\"..\"", "no path"),
            ("\"//s\"", "UNC"),
        };
        foreach (var (expression, message) in cases)
        {
            var result = new LythonEngine().Run(
                "import zipfile\nwith zipfile.ZipFile(\"/t.zip\") as archive:\n    archive.extract(" + expression + ", \"/out\")\n",
                host);
            Assert.False(result.Success, expression);
            Assert.Equal("ValueError", result.Failure?.ExceptionType);
            Assert.Contains(message, result.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
        }
        Assert.False(host.Exists("/out/bac/k"));
    }

    [Fact]
    public void ExclusiveCreationIsRejected()
    {
        var missing = new LythonEngine().Run(
            "import zipfile\nzipfile.ZipFile(\"/n.zip\", \"x\")\n",
            new ZipPublicHost());
        Assert.False(missing.Success);
        Assert.Equal("NotImplementedError", missing.Failure?.ExceptionType);

        var existing = new LythonEngine().Run(
            "import zipfile\nzipfile.ZipFile(\"/t.zip\", \"x\")\n",
            SeedFixture("zip-stored"));
        Assert.False(existing.Success);
        Assert.Equal("NotImplementedError", existing.Failure?.ExceptionType);
    }

    [Fact]
    public void IndependentBclReaderConsumesWrittenArchives()
    {
        var host = new ZipPublicHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w", compression=zipfile.ZIP_DEFLATED) as archive:
    archive.writestr("a.txt", b"hello")
    archive.writestr("u.txt", "x" * 500)
    archive.mkdir("docs")
    archive.comment = b"cmt"
return 1
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        using var archive = new ZipArchive(new MemoryStream(host.ReadBytes("/out.zip")), ZipArchiveMode.Read);
        Assert.Equal(new[] { "a.txt", "u.txt", "docs/" }, archive.Entries.Select(e => e.FullName).ToArray());
        Assert.Equal("cmt", archive.Comment);
        using (var stream = archive.GetEntry("a.txt")!.Open())
        using (var sink = new MemoryStream())
        {
            stream.CopyTo(sink);
            Assert.Equal(new byte[] { 104, 101, 108, 108, 111 }, sink.ToArray());
        }
    }

    [Fact]
    public void WriteHostFileAndMkdir()
    {
        var host = new ZipPublicHost();
        host.SeedBytes("/src.txt", Encoding.UTF8.GetBytes("host data"));
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w", strict_timestamps=False) as archive:
    archive.write("/src.txt")
    archive.write("/src.txt", arcname="sub/copy.txt")
    archive.mkdir("docs")
with zipfile.ZipFile("/out.zip") as archive:
    info = archive.getinfo("src.txt")
    nested = archive.getinfo("sub/copy.txt")
    folder = archive.getinfo("docs/")
    return [archive.namelist(), archive.read("sub/copy.txt"),
            info.date_time, nested.date_time, folder.is_dir(), folder.external_attr]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "src.txt", "sub/copy.txt", "docs/" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(Encoding.UTF8.GetBytes("host data"), Assert.IsType<byte[]>(values[1]));
        var clamped = new List<object?> { new BigInteger(1980), BigInteger.One, BigInteger.One, BigInteger.Zero, BigInteger.Zero, BigInteger.Zero };
        Assert.Equal(clamped, values[2]);
        Assert.Equal(clamped, values[3]);
        Assert.Equal(true, values[4]);
        Assert.Equal(new BigInteger((16895 << 16) | 0x10), values[5]); // 0o40777
    }

    [Fact]
    public void WriteStrictTimestampsRejectOldHostTime()
    {
        var host = new ZipPublicHost();
        host.SeedBytes("/src.txt", Encoding.UTF8.GetBytes("host data"));
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    archive.write("/src.txt")
""",
            host);
        Assert.False(result.Success);
        Assert.Equal("ValueError", result.Failure?.ExceptionType);
    }

    [Fact]
    public void OpenListsOrderedNames()
    {
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    return archive.namelist()
""",
            SeedFixture("zip-stored"));
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "hello.txt", "data/blob.bin" },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void DuplicatesKeepOrderLastWinsExplicitInfoSelectsEarly()
    {
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    names = archive.namelist()
    last = archive.read("dup.txt")
    first = archive.read(archive.infolist()[0])
    info = archive.getinfo("dup.txt")
    return [names, last, first, info.file_size]
""",
            SeedFixture("zip-duplicates"));
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "dup.txt", "other.txt", "dup.txt" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(Encoding.UTF8.GetBytes("second\n"), Assert.IsType<byte[]>(values[1]));
        Assert.Equal(Encoding.UTF8.GetBytes("first\n"), Assert.IsType<byte[]>(values[2]));
        Assert.Equal(new BigInteger(7), values[3]);
    }

    [Fact]
    public void GetinfoExposesEntryMetadata()
    {
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    info = archive.getinfo("hello.txt")
    return [info.filename, info.file_size, info.compress_size, info.compress_type,
            info.CRC, info.header_offset, info.flag_bits, info.is_dir(),
            info.create_system, info.external_attr, info.date_time,
            info.comment, info.extra]
""",
            SeedFixture("zip-stored"));
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                "hello.txt",
                new BigInteger(13),
                new BigInteger(13),
                new BigInteger(0),
                new BigInteger(0x38F3DB17),
                new BigInteger(0),
                new BigInteger(0),
                false,
                new BigInteger(0),
                new BigInteger(25165824),
                new List<object?> { new BigInteger(2024), new BigInteger(2), new BigInteger(29), new BigInteger(12), new BigInteger(34), new BigInteger(56) },
                Array.Empty<byte>(),
                Array.Empty<byte>(),
            },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void ReadDeflatedAndEmptyMembers()
    {
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    full = archive.read("default.txt")
    empty = archive.read("empty.txt")
    return [len(full), full[:4], len(empty), empty]
""",
            SeedFixture("zip-deflated"));
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(900), new byte[] { (byte)'T', (byte)'h', (byte)'e', (byte)' ' }, new BigInteger(0), Array.Empty<byte>() },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void MissingMembersRaiseKeyError()
    {
        foreach (var expression in new[] { "getinfo(\"missing\")", "read(\"missing\")" })
        {
            var result = new LythonEngine().Run(
                "import zipfile\nwith zipfile.ZipFile(\"/t.zip\") as archive:\n    archive." + expression + "\n",
                SeedFixture("zip-stored"));
            Assert.False(result.Success);
            Assert.Equal("KeyError", result.Failure?.ExceptionType);
            Assert.Contains("There is no item named", result.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PasswordsAreIgnoredForPlainMembers()
    {
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    return [archive.read("hello.txt", pwd=b"x"), archive.read("hello.txt", pwd="x")]
""",
            SeedFixture("zip-stored"));
        Assert.True(result.Success, result.Failure?.Message);
        var expected = Encoding.UTF8.GetBytes("hello stored\n");
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(expected, Assert.IsType<byte[]>(values[0]));
        Assert.Equal(expected, Assert.IsType<byte[]>(values[1]));
    }

    [Fact]
    public void EncryptedMembersFailExplicitly()
    {
        var host = new ZipPublicHost();
        host.SeedBytes("/t.zip", BuildRawArchive((Encoding.ASCII.GetBytes("enc.txt"), (ushort)1, (ushort)0, Encoding.ASCII.GetBytes("secret"))));
        foreach (var (expression, typeName, message) in new[]
        {
            ("read(\"enc.txt\")", "RuntimeError", "password required"),
            ("read(\"enc.txt\", pwd=b\"x\")", "RuntimeError", "Bad password"),
            ("read(\"enc.txt\", pwd=\"x\")", "TypeError", "pwd: expected bytes"),
        })
        {
            var result = new LythonEngine().Run(
                "import zipfile\nwith zipfile.ZipFile(\"/t.zip\") as archive:\n    archive." + expression + "\n",
                host);
            Assert.False(result.Success);
            Assert.Equal(typeName, result.Failure?.ExceptionType);
            Assert.Contains(message, result.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void UnsupportedMethodsFailExplicitly()
    {
        var host = new ZipPublicHost();
        host.SeedBytes("/t.zip", BuildRawArchive((Encoding.ASCII.GetBytes("m99.txt"), (ushort)0, (ushort)99, Encoding.ASCII.GetBytes("data"))));
        foreach (var expression in new[] { "read(\"m99.txt\")", "testzip()" })
        {
            var result = new LythonEngine().Run(
                "import zipfile\nwith zipfile.ZipFile(\"/t.zip\") as archive:\n    archive." + expression + "\n",
                host);
            Assert.False(result.Success);
            Assert.Equal("NotImplementedError", result.Failure?.ExceptionType);
            Assert.Contains("not supported", result.Failure?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CorruptChecksumReadFailsTestzipNames()
    {
        var read = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    return archive.read("data.txt")
""",
            SeedFixture("zip-corrupt-crc"));
        Assert.False(read.Success);
        Assert.Equal("BadZipFile", read.Failure?.ExceptionType);
        Assert.Contains("Bad CRC-32", read.Failure?.Message ?? string.Empty, StringComparison.Ordinal);

        var host = SeedFixture("zip-corrupt-crc");
        var tested = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    return archive.testzip()
""",
            host);
        Assert.True(tested.Success, tested.Failure?.Message);
        Assert.Equal("data.txt", tested.ReturnValue);
    }

    [Fact]
    public void MalformedArchivesFailOpenAsBadZipFile()
    {
        foreach (var caseId in new[] { "zip-corrupt-central", "zip-truncated" })
        {
            var result = new LythonEngine().Run(
                """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    return archive.namelist()
""",
                SeedFixture(caseId));
            Assert.False(result.Success);
            Assert.Equal("BadZipFile", result.Failure?.ExceptionType);
        }
    }

    [Fact]
    public void CloseLifecycleIsExplicit()
    {
        var host = SeedFixture("zip-deflated");
        var result = new LythonEngine().Run(
            """
import zipfile
archive = zipfile.ZipFile("/t.zip")
names = archive.namelist()
archive.close()
archive.close()
try:
    archive.read("default.txt")
except ValueError:
    after = "closed-read-raises"
return [names, after, archive.namelist()]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new BigInteger(4), Assert.IsType<List<object?>>(values[0]).Count);
        Assert.Equal("closed-read-raises", values[1]);
        Assert.Equal(new BigInteger(4), Assert.IsType<List<object?>>(values[2]).Count);
    }

    [Fact]
    public void IsZipfileRecognizesArchivesOnly()
    {
        var host = SeedFixture("zip-stored");
        host.SeedBytes("/note.txt", Encoding.UTF8.GetBytes("plain text"));
        var result = new LythonEngine().Run(
            """
import zipfile
return [zipfile.is_zipfile("/t.zip"), zipfile.is_zipfile("/missing.zip"),
        zipfile.is_zipfile("/note.txt"), zipfile.is_zipfile("/")]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, false, false, false },
            Assert.IsType<List<object?>>(result.ReturnValue));
        foreach (var probe in new[] { "123", "None" })
        {
            var denied = new LythonEngine().Run(
                "import zipfile\nreturn zipfile.is_zipfile(" + probe + ")\n",
                host);
            Assert.False(denied.Success, probe);
            Assert.Equal("TypeError", denied.Failure?.ExceptionType);
        }
    }

    [Fact]
    public void MemberOpenValidatesArguments()
    {
        var host = SeedFixture("zip-duplicates");
        var cases = new (string Expression, string Type, string Message)[]
        {
            ("open(\"nope.txt\")", "KeyError", "There is no item named"),
            ("open(\"dup.txt\", \"x\")", "ValueError", "requires mode"),
            ("open(\"dup.txt\", \"w\")", "ValueError", "requires mode"),
            ("open(\"dup.txt\", \"w\", pwd=b\"x\")", "ValueError", "only supported for reading"),
            ("open(\"dup.txt\", None)", "ValueError", "requires mode"),
            ("open(\"dup.txt\", mode=None)", "ValueError", "requires mode"),
        };
        foreach (var (expression, typeName, message) in cases)
        {
            var result = new LythonEngine().Run(
                "import zipfile\nwith zipfile.ZipFile(\"/t.zip\") as archive:\n    archive." + expression + "\n",
                host);
            Assert.False(result.Success);
            Assert.Equal(typeName, result.Failure?.ExceptionType);
            Assert.Contains(message, result.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
        }

        var early = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    with archive.open(archive.infolist()[0]) as handle:
        return handle.read()
""",
            host);
        Assert.True(early.Success, early.Failure?.Message);
        Assert.Equal(Encoding.UTF8.GetBytes("first\n"), Assert.IsType<byte[]>(early.ReturnValue));
    }

    [Fact]
    public void MemberOpenEnforcesIntegrityUpFront()
    {
        var host = SeedFixture("zip-corrupt-crc");
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    with archive.open("data.txt") as handle:
        return handle.read()
""",
            host);
        Assert.False(result.Success);
        Assert.Equal("BadZipFile", result.Failure?.ExceptionType);
        Assert.Contains("Bad CRC-32", result.Failure?.Message ?? string.Empty, StringComparison.Ordinal);

        var encrypted = new ZipPublicHost();
        encrypted.SeedBytes("/t.zip", BuildRawArchive((Encoding.ASCII.GetBytes("enc.txt"), (ushort)1, (ushort)0, Encoding.ASCII.GetBytes("secret"))));
        var denied = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    with archive.open("enc.txt") as handle:
        return handle.read()
""",
            encrypted);
        Assert.False(denied.Success);
        Assert.Equal("RuntimeError", denied.Failure?.ExceptionType);
    }

    [Fact]
    public void MemberSeekIsUnsupported()
    {
        var host = SeedFixture("zip-stored");
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    with archive.open("hello.txt") as handle:
        try:
            handle.seek(0)
        except NotImplementedError:
            return "seek-unsupported"
        return "seek-worked"
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("seek-unsupported", result.ReturnValue);
    }

    [Fact]
    public async Task MemberReadsWorkAsync()
    {
        var host = SeedFixture("zip-deflated");
        var result = await new LythonEngine().RunAsync(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    with archive.open("default.txt") as handle:
        head = handle.read(4)
        tail = [line for line in handle]
        return [head, len(tail)]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new byte[] { (byte)'T', (byte)'h', (byte)'e', (byte)' ' }, Assert.IsType<byte[]>(values[0]));
        Assert.Equal(new BigInteger(1), values[1]);
    }

    [Fact]
    public async Task ReadSurfaceWorksAsync()
    {
        var host = SeedFixture("zip-deflated");
        var result = await new LythonEngine().RunAsync(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    data = archive.read("default.txt")
    probe = archive.testzip()
    archive.close()
    return [len(data), probe, archive.namelist()]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new BigInteger(900), values[0]);
        Assert.Null(values[1]);
        Assert.Equal(new BigInteger(4), Assert.IsType<List<object?>>(values[2]).Count);
    }

    [Fact]
    public async Task CorruptTestzipWorksAsync()
    {
        var host = SeedFixture("zip-corrupt-crc");
        var result = await new LythonEngine().RunAsync(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    return archive.testzip()
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("data.txt", result.ReturnValue);
    }

    [Fact]
    public void ZipInfoDefaultsAndErrorAlias()
    {
        var result = new LythonEngine().Run(
            """
import zipfile
from zipfile import error, BadZipfile
info = zipfile.ZipInfo()
return [info.filename, info.date_time, info.compress_type, info.file_size,
        error is zipfile.BadZipFile, BadZipfile is zipfile.BadZipFile]
""",
            new ZipPublicHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                "NoName",
                new List<object?> { new BigInteger(1980), BigInteger.One, BigInteger.One, BigInteger.Zero, BigInteger.Zero, BigInteger.Zero },
                new BigInteger(0),
                new BigInteger(0),
                true,
                true,
            },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void MetadataEncodingUtf8DecodesUnflaggedNames()
    {
        var host = SeedFixture("zip-cp437-names");
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip", metadata_encoding="cp437") as archive:
    names = archive.namelist()
return names
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { "café.txt", "plain.txt" }, Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void MemberReadsAreSequential()
    {
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    with archive.open("default.txt") as handle:
        first = handle.read(4)
        second = handle.read(4)
        position = handle.tell()
        rest = handle.read()
        after = handle.read()
        return [first, second, position, len(rest), after, handle.name, handle.mode,
                handle.readable(), handle.writable(), handle.seekable(), handle.closed]
""",
            SeedFixture("zip-deflated"));
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                new byte[] { (byte)'T', (byte)'h', (byte)'e', (byte)' ' },
                new byte[] { (byte)'q', (byte)'u', (byte)'i', (byte)'c' },
                new BigInteger(8),
                new BigInteger(892),
                Array.Empty<byte>(),
                "default.txt",
                "rb",
                true,
                false,
                false,
                false,
            },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void MemberLinesSplitOnLineFeed()
    {
        var host = new ZipPublicHost();
        host.SeedBytes("/t.zip", BuildRawArchive((Encoding.ASCII.GetBytes("lines.txt"), (ushort)0, (ushort)0, Encoding.ASCII.GetBytes("l1\nl2\r\nl3\n"))));
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    with archive.open("lines.txt") as handle:
        first = handle.readline()
        second = handle.readline(100)
        iterated = [line for line in archive.open("lines.txt")]
    with archive.open("lines.txt") as handle:
        partial = handle.readlines(4)
        full = archive.open("lines.txt").readlines()
    return [first, second, iterated, partial, full]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var l1 = Encoding.ASCII.GetBytes("l1\n");
        var l2 = Encoding.ASCII.GetBytes("l2\r\n");
        var l3 = Encoding.ASCII.GetBytes("l3\n");
        Assert.Equal(
            new List<object?> { l1, l2, new List<object?> { l1, l2, l3 }, new List<object?> { l1, l2 }, new List<object?> { l1, l2, l3 } },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void MemberSurvivesParentCloseWithinOwnedBudget()
    {
        var host = SeedFixture("zip-deflated");
        var result = new LythonEngine().Run(
            """
import zipfile
archive = zipfile.ZipFile("/t.zip")
handle = archive.open("default.txt")
archive.close()
data = handle.read()
is_closed = handle.closed
handle.close()
return [len(data), data[:4], is_closed, handle.closed]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(900), new byte[] { (byte)'T', (byte)'h', (byte)'e', (byte)' ' }, false, true },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void MemberCloseLifecycleIsExplicit()
    {
        var host = SeedFixture("zip-stored");
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    handle = archive.open("hello.txt")
    handle.close()
    handle.close()
    try:
        handle.read()
    except ValueError:
        late = "closed-read-raises"
    try:
        handle.tell()
    except ValueError:
        late_tell = "closed-tell-raises"
    return [handle.closed, late, late_tell]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, "closed-read-raises", "closed-tell-raises" },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void NamesTimestampsAndDirectoriesMatchManifests()
    {
        var result = new LythonEngine().Run(
            """
import zipfile
out = []
with zipfile.ZipFile("/cp.zip") as archive:
    out.append(archive.namelist())
with zipfile.ZipFile("/utf.zip") as archive:
    out.append(archive.namelist())
with zipfile.ZipFile("/ts.zip") as archive:
    out.append(archive.getinfo("odd-second.txt").date_time)
with zipfile.ZipFile("/dirs.zip") as archive:
    first = archive.infolist()[0]
    out.append([first.is_dir(), archive.getinfo("docs/a.txt").is_dir()])
    out.append(archive.read("docs/a.txt"))
with zipfile.ZipFile("/desc.zip") as archive:
    out.append(archive.read("stream.bin")[:16])
with zipfile.ZipFile("/extra.zip") as archive:
    out.append(archive.read("extra.txt"))
return out
""",
            SeedMany(("zip-cp437-names", "/cp.zip"), ("zip-utf8-names", "/utf.zip"), ("zip-timestamps", "/ts.zip"), ("zip-dirs", "/dirs.zip"), ("zip-datadescriptor", "/desc.zip"), ("zip-extra-field", "/extra.zip")));
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "café.txt", "plain.txt" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(new List<object?> { "日本語.txt", "emoji-🎉.txt" }, Assert.IsType<List<object?>>(values[1]));
        Assert.Equal(
            new List<object?> { new BigInteger(2024), new BigInteger(2), new BigInteger(29), new BigInteger(12), new BigInteger(34), new BigInteger(56) },
            values[2]);
        Assert.Equal(new List<object?> { true, false }, Assert.IsType<List<object?>>(values[3]));
        Assert.Equal(Encoding.UTF8.GetBytes("nested\n"), Assert.IsType<byte[]>(values[4]));
        Assert.Equal(Encoding.UTF8.GetBytes("descriptor data\n"), Assert.IsType<byte[]>(values[5]));
        Assert.Equal(Encoding.UTF8.GetBytes("extra\n"), Assert.IsType<byte[]>(values[6]));
    }

    [Fact]
    public void SmallZip64ReadsUnderDefaultPolicy()
    {
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip", allowZip64=False) as archive:
    return [archive.namelist(), archive.read("tiny.txt")]
""",
            SeedFixture("zip-zip64"));
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "tiny.txt" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(Encoding.UTF8.GetBytes("zip64 forced\n"), Assert.IsType<byte[]>(values[1]));
    }

    [Fact]
    public void ArchiveAttributesExposeMetadata()
    {
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip") as archive:
    return [archive.filename, archive.mode, archive.compression, archive.comment]
""",
            SeedFixture("zip-comments"));
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal("/t.zip", values[0]);
        Assert.Equal("r", values[1]);
        Assert.Equal(new BigInteger(0), values[2]);
        Assert.Equal(HexToBytes("61726368697665205a3020c3a920ff20726177"), Assert.IsType<byte[]>(values[3]));
    }
    private static ZipPublicHost SeedMany(params (string CaseId, string Path)[] seeds)
    {
        var host = new ZipPublicHost();
        foreach (var (caseId, path) in seeds)
        {
            host.SeedBytes(path, File.ReadAllBytes(Path.Combine(FindCasesRoot(), caseId, "input.zip")));
        }

        return host;
    }


    [Fact]
    public async Task WriteSurfaceWorksAsync()
    {
        var host = new ZipPublicHost();
        var result = await new LythonEngine().RunAsync(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    archive.writestr("a.txt", b"hello", compress_type=zipfile.ZIP_DEFLATED)
    archive.mkdir("docs")
with zipfile.ZipFile("/out.zip") as archive:
    return [archive.namelist(), archive.read("a.txt")]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "a.txt", "docs/" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(new byte[] { 104, 101, 108, 108, 111 }, Assert.IsType<byte[]>(values[1]));
    }
    [Fact]
    public void AppendPreservesAndExtends()
    {
        var host = SeedFixture("zip-stored");
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/t.zip", "a") as archive:
    archive.writestr("b.txt", b"B")
    archive.mkdir("newdir")
with zipfile.ZipFile("/t.zip") as archive:
    return [archive.namelist(), archive.read("hello.txt"), archive.read("b.txt")]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(
            new List<object?> { "hello.txt", "data/blob.bin", "b.txt", "newdir/" },
            Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(Encoding.UTF8.GetBytes("hello stored\n"), Assert.IsType<byte[]>(values[1]));
        Assert.Equal(new byte[] { (byte)'B' }, Assert.IsType<byte[]>(values[2]));
        using var archive = new ZipArchive(new MemoryStream(host.ReadBytes("/t.zip")), ZipArchiveMode.Read);
        Assert.Equal(
            new[] { "hello.txt", "data/blob.bin", "b.txt", "newdir/" },
            archive.Entries.Select(e => e.FullName).ToArray());
    }

    [Fact]
    public void UnsupportedModuleMembersFailStatically()
    {
        foreach (var member in new[] { "PyZipFile", "ZipExtFile", "Path" })
        {
            var compiled = new LythonEngine().Compile("import zipfile\nvalue = zipfile." + member + "\n");
            Assert.False(compiled.IsValid);
            Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3113");
        }
    }

    [Fact]
    public void ZipImportStaysUnavailable()
    {
        var result = new LythonEngine().Run(
            "import zipimport\n",
            new ZipPublicHost());
        Assert.False(result.Success);
        Assert.Equal("ModuleNotFoundError", result.Failure?.ExceptionType);
    }

    [Fact]
    public void PathlikeArgumentsWork()
    {
        var host = SeedFixture("zip-stored");
        var result = new LythonEngine().Run(
            """
import zipfile
from pathlib import Path
with zipfile.ZipFile(Path("/t.zip")) as archive:
    names = archive.namelist()
with zipfile.ZipFile(Path("/out.zip"), "w") as archive:
    archive.writestr("a.txt", b"A")
return [names, zipfile.is_zipfile(Path("/t.zip")), zipfile.is_zipfile(Path("/missing.zip"))]
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "hello.txt", "data/blob.bin" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(true, values[1]);
        Assert.Equal(false, values[2]);
    }

    [Fact]
    public void MissingBinaryCapabilityFailsExplicitly()
    {
        var host = new NoBinaryZipHost();
        host.SeedText("/seed.txt", "text");
        var read = new LythonEngine().Run(
            "import zipfile\nwith zipfile.ZipFile(\"/seed.txt\") as archive:\n    return archive.namelist()\n",
            host);
        Assert.False(read.Success);
        Assert.Equal("RuntimeError", read.Failure?.ExceptionType);
        Assert.Contains("binary file I/O", read.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
        var write = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/out.zip", "w") as archive:
    archive.writestr("a.txt", b"A")
return 1
""",
            host);
        Assert.False(write.Success);
        Assert.Equal("RuntimeError", write.Failure?.ExceptionType);
        var pure = new LythonEngine().Run(
            """
import zipfile
info = zipfile.ZipInfo("a.txt")
return [info.filename, zipfile.ZIP_STORED, zipfile.is_zipfile("/missing.txt")]
""",
            host);
        Assert.True(pure.Success, pure.Failure?.Message);
    }

    [Fact]
    public void AppendOverlappingEntriesFails()
    {
        var host = new ZipPublicHost();
        var written = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/o.zip", "w") as archive:
    archive.writestr("a", b"x")
return 1
""",
            host);
        Assert.True(written.Success, written.Failure?.Message);
        var raw = host.ReadBytes("/o.zip");
        var eocd = FindEndRecord(raw);
        var first = ReadCentralRecord(raw, eocd.DirectoryOffset);
        var duplicated = new byte[raw.Length + first.Length];
        Buffer.BlockCopy(raw, 0, duplicated, 0, eocd.DirectoryOffset + eocd.DirectorySize);
        Buffer.BlockCopy(first, 0, duplicated, eocd.DirectoryOffset + eocd.DirectorySize, first.Length);
        Buffer.BlockCopy(raw, eocd.DirectoryOffset + eocd.DirectorySize, duplicated, eocd.DirectoryOffset + eocd.DirectorySize + first.Length, raw.Length - eocd.DirectoryOffset - eocd.DirectorySize);
        var movedEnd = eocd.Offset + first.Length;
        WriteUInt16(duplicated, movedEnd + 8, (ushort)(eocd.EntryCount + 1));
        WriteUInt16(duplicated, movedEnd + 10, (ushort)(eocd.EntryCount + 1));
        WriteUInt32(duplicated, movedEnd + 12, (uint)(eocd.DirectorySize + first.Length));
        host.SeedBytes("/d.zip", duplicated);
        var readable = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/d.zip") as archive:
    return [archive.namelist(), archive.read("a")]
""",
            host);
        Assert.True(readable.Success, readable.Failure?.Message);
        var denied = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/d.zip", "a") as archive:
    archive.writestr("b", b"y")
""",
            host);
        Assert.False(denied.Success);
        Assert.Equal("BadZipFile", denied.Failure?.ExceptionType);
    }

    private sealed class NoBinaryZipHost : ILythonHost, ILythonSynchronousHostCapability
    {
        private readonly Dictionary<string, string> _text = new(StringComparer.Ordinal);

        public string Cwd => "/";

        public bool CompletesSynchronously => true;

        public DateTimeOffset LocalNow => new(2024, 1, 2, 4, 4, 5, TimeSpan.FromHours(1));

        public DateTimeOffset UtcNow => new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);

        public void SeedText(string path, string text) => _text[path] = text;

        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(Encoding.UTF8.GetBytes(_text[path]));
        }

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _text[path] = Encoding.UTF8.GetString(utf8.Span);
            return ValueTask.CompletedTask;
        }

        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _text[path] = (_text.TryGetValue(path, out var existing) ? existing : string.Empty) + Encoding.UTF8.GetString(utf8.Span);
            return ValueTask.CompletedTask;
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken cancellationToken) =>
            throw new LythonHostCapabilityUnavailableException("binary file I/O");

        public ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
            throw new LythonHostCapabilityUnavailableException("binary file I/O");

        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(path == "/" || _text.ContainsKey(path));
        }

        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<string> names = Array.Empty<string>();
            return ValueTask.FromResult(names);
        }

        public ValueTask MkDirAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _text.Remove(path);
            return ValueTask.CompletedTask;
        }

        public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _text[destination] = _text[source];
            return ValueTask.CompletedTask;
        }

        public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _text[destination] = _text[source];
            _text.Remove(source);
            return ValueTask.CompletedTask;
        }

        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_text.TryGetValue(path, out var text))
            {
                return ValueTask.FromResult(new LythonPathStat(LythonPathKind.File, Encoding.UTF8.GetByteCount(text), UtcNow));
            }

            if (path == "/")
            {
                return ValueTask.FromResult(new LythonPathStat(LythonPathKind.Directory, BigInteger.Zero, UtcNow));
            }

            return ValueTask.FromResult(new LythonPathStat(LythonPathKind.Missing, BigInteger.Zero, null));
        }
    }

    private readonly record struct EndRecord(int Offset, int DirectoryOffset, int DirectorySize, int EntryCount);

    private static EndRecord FindEndRecord(byte[] bytes)
    {
        for (var i = bytes.Length - 22; i >= Math.Max(0, bytes.Length - 22 - 65535); i--)
        {
            if (ReadUInt32(bytes, i) != 0x06054B50u)
            {
                continue;
            }

            if (ReadUInt16(bytes, i + 20) == 0 && i + 22 == bytes.Length)
            {
                return new EndRecord(
                    i,
                    checked((int)ReadUInt32(bytes, i + 16)),
                    checked((int)ReadUInt32(bytes, i + 12)),
                    ReadUInt16(bytes, i + 8));
            }
        }

        throw new InvalidOperationException("Test archive has no end record.");
    }

    private static byte[] ReadCentralRecord(byte[] bytes, int offset)
    {
        if (ReadUInt32(bytes, offset) != 0x02014B50u)
        {
            throw new InvalidOperationException("Test archive has no central entry.");
        }

        var length = 46
            + ReadUInt16(bytes, offset + 28)
            + ReadUInt16(bytes, offset + 30)
            + ReadUInt16(bytes, offset + 32);
        var record = new byte[length];
        Buffer.BlockCopy(bytes, offset, record, 0, length);
        return record;
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }

    private static ushort ReadUInt16(byte[] bytes, int offset) => (ushort)(bytes[offset] | (bytes[offset + 1] << 8));

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        (uint)bytes[offset] | ((uint)bytes[offset + 1] << 8) | ((uint)bytes[offset + 2] << 16) | ((uint)bytes[offset + 3] << 24);

    private sealed class ZipPublicHost : ILythonHost, ILythonSynchronousHostCapability
    {
        private static readonly DateTimeOffset Timestamp = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        private readonly Dictionary<string, byte[]> _binary = new(StringComparer.Ordinal);
        private readonly HashSet<string> _directories = new(StringComparer.Ordinal) { "/" };
        private readonly HostTextSink _standardOutput = new();
        private readonly Dictionary<string, string> _writeFailures = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _nextWriteFailures = new(StringComparer.Ordinal);

        public string Cwd => "/";

        public bool CompletesSynchronously => true;

        public DateTimeOffset LocalNow { get; set; } = Timestamp.ToOffset(TimeSpan.FromHours(1));

        public DateTimeOffset UtcNow { get; set; } = Timestamp;

        public ILythonTextOutput? StandardOutput => _standardOutput;

        public string StandardOutputText => _standardOutput.Text;

        public void SeedBytes(string path, byte[] payload) => _binary[path] = payload;

        public byte[] ReadBytes(string path) => _binary[path];

        public bool Exists(string path) => _binary.ContainsKey(path) || _directories.Contains(path);

        public void FailWriteBytes(string path, string message) => _writeFailures[path] = message;

        public void FailNextWriteBytes(string path, string message) => _nextWriteFailures[path] = message;

        public void ClearWriteBytesFailures()
        {
            _writeFailures.Clear();
            _nextWriteFailures.Clear();
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(_binary[path]);
        }

        public ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_nextWriteFailures.TryGetValue(path, out var nextMessage))
            {
                _nextWriteFailures.Remove(path);
                throw new InvalidOperationException(nextMessage);
            }

            if (_writeFailures.TryGetValue(path, out var message))
            {
                throw new InvalidOperationException(message);
            }

            EnsureDirectory(ParentOf(path));
            _binary[path] = bytes.ToArray();
            return ValueTask.CompletedTask;
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(_binary[path]);
        }

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _binary[path] = utf8.ToArray();
            return ValueTask.CompletedTask;
        }

        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prefix = _binary.TryGetValue(path, out var existing) ? existing : [];
            var appended = new byte[prefix.Length + utf8.Length];
            prefix.CopyTo(appended, 0);
            utf8.CopyTo(appended.AsMemory(prefix.Length));
            _binary[path] = appended;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Exists(path));
        }

        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = path.TrimEnd('/');
            if (normalized.Length == 0)
            {
                normalized = "/";
            }

            if (!_directories.Contains(normalized))
            {
                throw new DirectoryNotFoundException(path);
            }

            var prefix = path == "/" ? "/" : path.TrimEnd('/') + "/";
            IReadOnlyList<string> names = _binary.Keys
                .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(key => key[prefix.Length..].Split('/')[0])
                .Distinct()
                .Order(StringComparer.Ordinal)
                .ToArray();
            return ValueTask.FromResult(names);
        }

        public ValueTask MkDirAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = path.TrimEnd('/');
            if (normalized.Length == 0)
            {
                normalized = "/";
            }

            if (_directories.Contains(normalized) || _binary.ContainsKey(normalized))
            {
                throw new InvalidOperationException($"Path already exists: {normalized}");
            }

            if (!_directories.Contains(ParentOf(normalized)))
            {
                throw new InvalidOperationException($"Parent directory does not exist: {normalized}");
            }

            _directories.Add(normalized);
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _binary.Remove(path);
            _directories.Remove(path.TrimEnd('/'));
            return ValueTask.CompletedTask;
        }

        public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _binary[destination] = _binary[source].ToArray();
            return ValueTask.CompletedTask;
        }

        public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _binary[destination] = _binary[source];
            _binary.Remove(source);
            return ValueTask.CompletedTask;
        }

        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_binary.TryGetValue(path, out var payload))
            {
                return ValueTask.FromResult(new LythonPathStat(LythonPathKind.File, payload.Length, DateTimeOffset.UnixEpoch));
            }

            var normalized = path.TrimEnd('/');
            if (normalized.Length == 0)
            {
                normalized = "/";
            }

            if (_directories.Contains(normalized))
            {
                return ValueTask.FromResult(new LythonPathStat(LythonPathKind.Directory, BigInteger.Zero, Timestamp));
            }

            return ValueTask.FromResult(new LythonPathStat(LythonPathKind.Missing, BigInteger.Zero, null));
        }

        private void EnsureDirectory(string path)
        {
            if (!_directories.Contains(path))
            {
                EnsureDirectory(ParentOf(path));
                _directories.Add(path);
            }
        }

        private static string ParentOf(string path)
        {
            var slash = path.LastIndexOf('/');
            return slash <= 0 ? "/" : path[..slash];
        }

        private sealed class HostTextSink : ILythonTextOutput, ILythonSynchronousHostCapability
        {
            private readonly StringBuilder _builder = new();

            public bool CompletesSynchronously => true;

            public string Text => _builder.ToString();

            public ValueTask WriteUtf8Async(ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _builder.Append(Encoding.UTF8.GetString(utf8.Span));
                return ValueTask.CompletedTask;
            }

            public ValueTask FlushAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            }
        }
    }
}
