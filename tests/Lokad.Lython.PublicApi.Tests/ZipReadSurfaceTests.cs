using System.IO.Compression;
using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// Z2 read surface: <c>is_zipfile</c>, <c>ZipInfo</c>, and read-only
/// <c>ZipFile</c> (listing, lookup, governed reads, validation, lifecycle)
/// against the trusted fixture catalog in both execution modes.
/// </summary>
public sealed class ZipReadSurfaceTests
{
    private static readonly string CasesRoot = FindCasesRoot();

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

        throw new DirectoryNotFoundException(
            "Could not locate tests/Fixtures/zipfile/cases from " + AppContext.BaseDirectory);
    }

    private static MockLythonHost SeedFixture(string caseId, string path = "/t.zip")
    {
        var host = new MockLythonHost();
        host.SeedWorkbook(path, File.ReadAllBytes(Path.Combine(CasesRoot, caseId, "input.zip")));
        return host;
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

    private static uint ComputeCrc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) == 1 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }








































    [Fact]
    public async Task WriterHonorsRunCancellation()
    {
        var host = new DelayedLythonHost("/");
        using var cancellation = new CancellationTokenSource();
        var task = new LythonEngine().RunAsync(
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
        cancellation.Cancel();
        var result = await task;
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message, StringComparison.Ordinal);
    }








    [Fact]
    public async Task ZipMemberReadlineWithHugeSizeStaysBounded()
    {
        // R05: readline must clamp the requested size against remaining bytes before
        // addition, so cursor 1 plus 2147483647 cannot overflow into a CLR range failure.
        const string scriptText = """
            import zipfile
            with zipfile.ZipFile("/r05.zip", "w") as archive:
                archive.writestr("a.txt", bytes([97, 98, 99, 10]))
            with zipfile.ZipFile("/r05.zip") as archive:
                with archive.open("a.txt") as f:
                    first = f.read(1)
                    rest = f.readline(2147483647)
                    eof = f.readline(2147483647)
                    zero = f.readline(0)
                with archive.open("a.txt") as g:
                    neg = g.readline(-1)
                    after = g.readline(10)
                with archive.open("a.txt") as h:
                    huge0 = h.readline(2147483647)
                return [first, rest, eof, zero, neg, after, huge0]
            """;
        var script = new LythonEngine().Compile(scriptText);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        CheckR05Bounded(sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        CheckR05Bounded(asyncResult.ReturnValue);
    }

    private static void CheckR05Bounded(object? value)
    {
        var values = Assert.IsType<List<object?>>(value);
        Assert.Equal(7, values.Count);
        Assert.Equal(new byte[] { 97 }, Assert.IsType<byte[]>(values[0]));
        Assert.Equal(new byte[] { 98, 99, 10 }, Assert.IsType<byte[]>(values[1]));
        Assert.Equal(Array.Empty<byte>(), Assert.IsType<byte[]>(values[2]));
        Assert.Equal(Array.Empty<byte>(), Assert.IsType<byte[]>(values[3]));
        Assert.Equal(new byte[] { 97, 98, 99, 10 }, Assert.IsType<byte[]>(values[4]));
        Assert.Equal(Array.Empty<byte>(), Assert.IsType<byte[]>(values[5]));
        Assert.Equal(new byte[] { 97, 98, 99, 10 }, Assert.IsType<byte[]>(values[6]));
    }
    private static byte[] BuildDeflatedArchive(byte[] name, byte[] payload, int uncompressedSize, uint crc)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0x04034B50u);
            writer.Write((ushort)20);
            writer.Write((ushort)0);
            writer.Write((ushort)8);
            writer.Write((ushort)0x5C64);
            writer.Write((ushort)0xD938);
            writer.Write(crc);
            writer.Write((uint)payload.Length);
            writer.Write((uint)uncompressedSize);
            writer.Write((ushort)name.Length);
            writer.Write((ushort)0);
            writer.Write(name);
            writer.Write(payload);
            var directoryOffset = stream.Position;
            writer.Write(0x02014B50u);
            writer.Write((ushort)20);
            writer.Write((ushort)20);
            writer.Write((ushort)0);
            writer.Write((ushort)8);
            writer.Write((ushort)0x5C64);
            writer.Write((ushort)0xD938);
            writer.Write(crc);
            writer.Write((uint)payload.Length);
            writer.Write((uint)uncompressedSize);
            writer.Write((ushort)name.Length);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write(0u);
            writer.Write(0u);
            writer.Write(name);
            var directorySize = stream.Position - directoryOffset;
            writer.Write(0x06054B50u);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)1);
            writer.Write((uint)directorySize);
            writer.Write((uint)directoryOffset);
            writer.Write((ushort)0);
        }

        return stream.ToArray();
    }

    private static byte[] BclDeflate(byte[] data)
    {
        using var buffer = new MemoryStream();
        using (var compressor = new DeflateStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            compressor.Write(data, 0, data.Length);
        }

        return buffer.ToArray();
    }

    [Fact]
    public async Task TruncatedDeflatedMemberFailsReadAndTestzip()
    {
        // Strict end-of-stream: the payload is missing its final byte while the
        // declared size and CRC still describe the complete text, so only an
        // end-of-stream check can reject it. CPython itself accepts this input.
        const string scriptText = """
            import zipfile
            with zipfile.ZipFile("/t.zip") as archive:
                return archive.read("a.txt")
            """;
        var text = new byte[300];
        for (var i = 0; i < text.Length; i++) text[i] = (byte)(65 + (i % 26));
        var full = BclDeflate(text);
        var archive = BuildDeflatedArchive(Encoding.ASCII.GetBytes("a.txt"), full[..^1], text.Length, ComputeCrc32(text));
        var script = new LythonEngine().Compile(scriptText);
        Assert.True(script.IsValid);
        var syncHost = new MockLythonHost();
        syncHost.SeedWorkbook("/t.zip", archive);
        var sync = script.Run(syncHost);
        Assert.False(sync.Success);
        Assert.Equal("BadZipFile", sync.Failure?.ExceptionType);
        Assert.Contains("truncated", sync.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
        var asyncHost = new MockLythonHost();
        asyncHost.SeedWorkbook("/t.zip", archive);
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.False(asyncResult.Success);
        Assert.Equal("BadZipFile", asyncResult.Failure?.ExceptionType);
        Assert.Contains("truncated", asyncResult.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
        var zipHost = new MockLythonHost();
        zipHost.SeedWorkbook("/t.zip", archive);
        var tested = new LythonEngine().Run(
            """
            import zipfile
            with zipfile.ZipFile("/t.zip") as archive:
                return archive.testzip()
            """,
            zipHost);
        Assert.True(tested.Success, tested.Failure?.Message);
        Assert.Equal("a.txt", tested.ReturnValue);
    }

    private static MockLythonHost SeedMany(params (string CaseId, string Path)[] seeds)
    {
        var host = new MockLythonHost();
        foreach (var (caseId, path) in seeds)
        {
            host.SeedWorkbook(path, File.ReadAllBytes(Path.Combine(CasesRoot, caseId, "input.zip")));
        }

        return host;
    }

    private static byte[] HexToBytes(string hex)
    {
        var result = new byte[hex.Length / 2];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return result;
    }
}

