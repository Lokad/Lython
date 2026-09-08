using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Zip;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// Z1 bounded record arithmetic: the directory reader resolves every Z0
/// fixture to its trusted CPython manifest using checked arithmetic, explicit
/// UTF-8/CP437 decoding, and governed metadata. Malformed structures fail as
/// <c>InvalidDataException</c> (translated to <c>BadZipFile</c> by later
/// stages); data-checksum mismatches stay readable here because checksums are
/// enforced when members are read, not when directories are parsed.
/// </summary>
public sealed class ZipDirectoryReaderTests
{
    private static readonly string CasesRoot = FindCasesRoot();

    private static readonly string[] ManifestCases =
    [
        "zip-comments",
        "zip-corrupt-crc",
        "zip-cp437-names",
        "zip-datadescriptor",
        "zip-deflated",
        "zip-dirs",
        "zip-duplicates",
        "zip-empty",
        "zip-extra-field",
        "zip-stored",
        "zip-timestamps",
        "zip-utf8-names",
        "zip-zip64",
    ];

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

    private static JsonDocument LoadManifest(string caseId)
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(CasesRoot, caseId, "case.json"), Encoding.UTF8));

    private static byte[] LoadPayload(string caseId)
        => File.ReadAllBytes(Path.Combine(CasesRoot, caseId, "input.zip"));

    private static ZipArchiveDirectory Read(
        byte[] payload,
        LythonRunOptions? options = null)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options);
        return ZipDirectoryReader.Read(payload, false, context, span: null);
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

    [Fact]
    public void FixturesMatchTrustedManifests()
    {
        foreach (var caseId in ManifestCases)
        {
            using var manifest = LoadManifest(caseId);
            var root = manifest.RootElement;
            var directory = Read(LoadPayload(caseId));
            Assert.Equal(caseId == "zip-zip64", directory.IsZip64);
            Assert.Equal(
                HexToBytes(root.GetProperty("archiveCommentHex").GetString() ?? string.Empty),
                directory.Comment);
            var expected = root.GetProperty("entries").EnumerateArray().ToArray();
            Assert.Equal(expected.Length, directory.Entries.Count);
            Assert.Equal(
                root.GetProperty("namelist").EnumerateArray().Select(e => e.GetString()).ToArray(),
                directory.Entries.Select(e => e.Name).ToArray());
            for (var i = 0; i < expected.Length; i++)
            {
                var actual = directory.Entries[i];
                Assert.Equal(expected[i].GetProperty("name").GetString(), actual.Name);
                Assert.Equal(expected[i].GetProperty("utf8Flag").GetBoolean(), actual.Utf8Flag);
                Assert.Equal(expected[i].GetProperty("descriptorFlag").GetBoolean(), actual.HasDataDescriptor);
                Assert.Equal(expected[i].GetProperty("compressType").GetInt32(), actual.CompressionMethod);
                Assert.Equal(expected[i].GetProperty("fileSize").GetInt64(), (long)actual.UncompressedSize);
                Assert.Equal(expected[i].GetProperty("compressSize").GetInt64(), (long)actual.CompressedSize);
                Assert.Equal(
                    Convert.ToUInt32(expected[i].GetProperty("crc32Hex").GetString(), 16),
                    actual.Crc32);
                var dateTime = expected[i].GetProperty("dateTime");
                Assert.Equal(
                    dateTime.EnumerateArray().Select(e => e.GetInt32()).ToArray(),
                    new[] { actual.DateYear, actual.DateMonth, actual.DateDay, actual.DateHour, actual.DateMinute, actual.DateSecond });
                Assert.Equal(
                    HexToBytes(expected[i].GetProperty("commentHex").GetString() ?? string.Empty),
                    actual.Comment);
                Assert.Equal(expected[i].GetProperty("createSystem").GetInt32(), actual.CreateSystem);
                Assert.Equal(expected[i].GetProperty("externalAttr").GetInt32(), (int)actual.ExternalAttributes);
                Assert.Equal(expected[i].GetProperty("headerOffset").GetInt64(), (long)actual.HeaderOffset);
                if (caseId == "zip-zip64")
                {
                    Assert.StartsWith("01001800", Convert.ToHexString(actual.Extra).ToLowerInvariant(), StringComparison.Ordinal);
                }
                else
                {
                    Assert.Equal(
                        HexToBytes(expected[i].GetProperty("extraHex").GetString() ?? string.Empty),
                        actual.Extra);
                }
            }
        }
    }

    [Fact]
    public void CorruptChecksumStaysReadableAsMetadata()
    {
        using var manifest = LoadManifest("zip-corrupt-crc");
        Assert.Equal("data.txt", manifest.RootElement.GetProperty("testzip").GetString());
        var directory = Read(LoadPayload("zip-corrupt-crc"));
        var entry = Assert.Single(directory.Entries);
        Assert.Equal("data.txt", entry.Name);
        Assert.Equal(
            Convert.ToUInt32(manifest.RootElement.GetProperty("entries")[0].GetProperty("crc32Hex").GetString(), 16),
            entry.Crc32);
    }

    [Fact]
    public void CorruptCentralDirectoryFailsDeliberately()
    {
        var failure = Assert.Throws<InvalidDataException>(() => Read(LoadPayload("zip-corrupt-central")));
        Assert.Contains("central", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TruncatedArchiveFailsDeliberately()
    {
        var failure = Assert.Throws<InvalidDataException>(() => Read(LoadPayload("zip-truncated")));
        Assert.Contains("end-of-central-directory", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CentralOffsetBeyondEndFailsDeliberately()
    {
        var payload = LoadPayload("zip-stored");
        // No-comment EOCD: the directory offset field sits six bytes from the end.
        payload[^6] = 0xFE;
        payload[^5] = 0xFF;
        payload[^4] = 0xFF;
        payload[^3] = 0xFF;
        var failure = Assert.Throws<InvalidDataException>(() => Read(payload));
        Assert.Contains("beyond the end of data", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocalCentralMismatchesFailDeliberately()
    {
        var sizeMismatch = LoadPayload("zip-stored");
        sizeMismatch[18] += 1;
        var sizeFailure = Assert.Throws<InvalidDataException>(() => Read(sizeMismatch));
        Assert.Contains("local sizes", sizeFailure.Message, StringComparison.OrdinalIgnoreCase);

        var nameMismatch = LoadPayload("zip-stored");
        nameMismatch[30] ^= 0x20;
        var nameFailure = Assert.Throws<InvalidDataException>(() => Read(nameMismatch));
        Assert.Contains("local name", nameFailure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MalformedUtf8NameFailsDeliberately()
    {
        // CPython raises UnicodeDecodeError here; the reader reports malformed
        // names structurally so the future module can choose deliberately.
        var payload = BuildRawArchive([(new byte[] { 0xFF, 0xFE }, 0x0800, new byte[] { (byte)'x' })]);
        var failure = Assert.Throws<InvalidDataException>(() => Read(payload));
        Assert.Contains("UTF-8", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Cp437RowsDecodeDeterministically()
    {
        var names = new byte[][] { new byte[] { 0x9B }, new byte[] { 0xA8 }, new byte[] { 0xB3 }, new byte[] { 0xC5 }, new byte[] { 0xDB }, new byte[] { 0xE4 }, new byte[] { 0xF9 }, new byte[] { 0xFF } };
        var records = names.Select(name => (name, (ushort)0, new byte[] { (byte)'x' })).ToArray();
        var directory = Read(BuildRawArchive(records));
        Assert.Equal(
            new[] { "\u00A2", "\u00BF", "\u2502", "\u253C", "\u2588", "\u03A3", "\u2219", "\u00A0" },
            directory.Entries.Select(e => e.Name).ToArray());
    }

    [Fact]
    public void EntryCountRespectsCollectionLimit()
    {
        var records = Enumerable.Range(0, 200)
            .Select(i => (Encoding.ASCII.GetBytes("f" + i), (ushort)0, new byte[] { (byte)'x' }))
            .ToArray();
        var failure = Assert.Throws<LythonRuntimeException>(
            () => Read(BuildRawArchive(records), new LythonRunOptions { MaxCollectionSize = 100 }));
        Assert.Equal("RuntimeError", failure.ExceptionType);
        Assert.Contains("maximum collection size exceeded", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RetainedMetadataRespectsMemoryBudget()
    {
        var failure = Assert.Throws<LythonRuntimeException>(
            () => Read(LoadPayload("zip-stored"), new LythonRunOptions { MaxExecutionMemoryBytes = 100 }));
        Assert.Equal("MemoryError", failure.ExceptionType);
    }

    private static byte[] BuildRawArchive(params (byte[] Name, ushort Flags, byte[] Content)[] records)
    {
        var fixedTime = (ushort)0x5C64;
        var fixedDate = (ushort)0xD938;
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            var offsets = new List<long>();
            foreach (var (name, flags, content) in records)
            {
                offsets.Add(stream.Position);
                var crc = ComputeCrc32(content);
                writer.Write(0x04034B50u);
                writer.Write((ushort)20);
                writer.Write(flags);
                writer.Write((ushort)0);
                writer.Write(fixedTime);
                writer.Write(fixedDate);
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
                var (name, flags, content) = records[i];
                var crc = ComputeCrc32(content);
                writer.Write(0x02014B50u);
                writer.Write((ushort)20);
                writer.Write((ushort)20);
                writer.Write(flags);
                writer.Write((ushort)0);
                writer.Write(fixedTime);
                writer.Write(fixedDate);
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
}
