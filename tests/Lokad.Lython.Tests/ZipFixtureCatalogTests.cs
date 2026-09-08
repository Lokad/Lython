using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Lokad.Lython.Tests;

/// <summary>
/// Z0 fixture-validity gate for the contained-`zipfile` plan. The catalog under
/// tests/Fixtures/zipfile is authored by CPython 3.13 (tools/NewZipFixtures.py)
/// and carries trusted CPython observations in each case.json manifest. Lython
/// has no zipfile module yet, so these tests pin the fixtures using only the
/// independent BCL reader plus raw byte checks, and they document exactly where
/// BCL behavior diverges from the Python contract. The future implementation
/// must agree with the manifests, not with BCL:
/// duplicate lookup (BCL first-wins versus Python last-wins), CRC validation
/// (BCL reads corrupt members without complaint), CP437/comment decoding (BCL
/// lossy replacement versus Python CP437/raw bytes), and error categories
/// (BCL InvalidDataException versus typed BadZipFile).
/// </summary>
public sealed class ZipFixtureCatalogTests
{
    private static readonly string CasesRoot = FindCasesRoot();

    private static readonly string[] ExpectedCaseIds =
    [
        "zip-comments",
        "zip-corrupt-central",
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
        "zip-truncated",
        "zip-utf8-names",
        "zip-zip64",
    ];

    // Cases the BCL reader opens successfully. The corrupt central directory
    // and the truncated payload fail before any entry is visible.
    private static readonly string[] BclReadableCaseIds =
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
    {
        var path = Path.Combine(CasesRoot, caseId, "case.json");
        return JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
    }

    private static byte[] LoadPayload(string caseId)
        => File.ReadAllBytes(Path.Combine(CasesRoot, caseId, "input.zip"));

    private static byte[] HexToBytes(string hex)
    {
        Assert.True(hex.Length % 2 == 0, "Odd hex string.");
        var result = new byte[hex.Length / 2];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return result;
    }

    private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var sink = new MemoryStream();
        stream.CopyTo(sink);
        return sink.ToArray();
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

    private static int FindByteSequence(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i + needle.Length <= haystack.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }

    private static ZipArchive OpenReadable(byte[] payload)
    {
        var stream = new MemoryStream(payload, writable: false);
        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    // The BCL reader parses the central directory lazily: construction alone
    // does not surface a corrupt directory, only entry access does.
    private static void TouchEntries(byte[] payload)
    {
        using var archive = OpenReadable(payload);
        _ = archive.Entries.Count;
    }

    [Fact]
    public void CatalogIsCompleteAndHygienic()
    {
        var onDisk = Directory.GetDirectories(CasesRoot)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(ExpectedCaseIds, onDisk);
        foreach (var caseId in ExpectedCaseIds)
        {
            var payload = LoadPayload(caseId);
            Assert.True(payload.Length > 0, caseId);
            Assert.True(payload.Length <= 32 * 1024, caseId + " exceeds the fixture size budget.");
            using var manifest = LoadManifest(caseId);
            var root = manifest.RootElement;
            Assert.Equal(caseId, root.GetProperty("id").GetString());
            Assert.Equal("zip", root.GetProperty("kind").GetString());
            Assert.Equal("tools/NewZipFixtures.py", root.GetProperty("createdByTool").GetString());
            Assert.StartsWith("CPython 3.13.", root.GetProperty("producer").GetString());
        }
    }

    [Fact]
    public void ValidArchivesMatchManifestThroughIndependentReader()
    {
        foreach (var caseId in BclReadableCaseIds)
        {
            using var manifest = LoadManifest(caseId);
            var root = manifest.RootElement;
            Assert.Null(OpenErrorTypeOrNull(root));
            var payload = LoadPayload(caseId);
            using var archive = OpenReadable(payload);
            var expected = root.GetProperty("entries").EnumerateArray().ToArray();
            Assert.Equal(expected.Length, archive.Entries.Count);
            var reads = root.GetProperty("readByIndex").EnumerateArray().ToArray();
            Assert.Equal(expected.Length, reads.Length);
            for (var i = 0; i < expected.Length; i++)
            {
                var actual = archive.Entries[i];
                // CP437 names without the UTF-8 flag decode differently in BCL;
                // that divergence has its own dedicated test below.
                if (caseId != "zip-cp437-names")
                {
                    Assert.Equal(expected[i].GetProperty("name").GetString(), actual.FullName);
                }

                Assert.Equal(expected[i].GetProperty("fileSize").GetInt64(), actual.Length);
                Assert.Equal(expected[i].GetProperty("compressSize").GetInt64(), actual.CompressedLength);
                var expectedCrc = Convert.ToUInt32(expected[i].GetProperty("crc32Hex").GetString(), 16);
                Assert.Equal(expectedCrc, actual.Crc32);
                var dateTime = expected[i].GetProperty("dateTime");
                Assert.Equal(dateTime[0].GetInt32(), actual.LastWriteTime.Year);
                Assert.Equal(dateTime[1].GetInt32(), actual.LastWriteTime.Month);
                Assert.Equal(dateTime[2].GetInt32(), actual.LastWriteTime.Day);
                Assert.Equal(dateTime[3].GetInt32(), actual.LastWriteTime.Hour);
                Assert.Equal(dateTime[4].GetInt32(), actual.LastWriteTime.Minute);
                Assert.Equal(dateTime[5].GetInt32(), actual.LastWriteTime.Second);
                var read = reads[i];
                if (read.GetProperty("error").ValueKind == JsonValueKind.Null)
                {
                    Assert.Equal(
                        HexToBytes(read.GetProperty("hex").GetString() ?? string.Empty),
                        ReadEntryBytes(actual));
                }
            }
        }
    }

    private static string? OpenErrorTypeOrNull(JsonElement root)
    {
        var error = root.GetProperty("openError");
        return error.ValueKind == JsonValueKind.Null ? null : error.GetProperty("type").GetString();
    }

    [Fact]
    public void DuplicateLookupDivergesFirstVersusLast()
    {
        using var manifest = LoadManifest("zip-duplicates");
        var root = manifest.RootElement;
        Assert.Equal(
            new[] { "dup.txt", "other.txt", "dup.txt" },
            root.GetProperty("namelist").EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray());
        // Trusted Python behavior: name lookup selects the last duplicate while
        // positional access still reaches the earlier one.
        Assert.Equal("second\n", Encoding.UTF8.GetString(HexToBytes(
            root.GetProperty("readByName")[0].GetProperty("hex").GetString() ?? string.Empty)));
        Assert.Equal("first\n", Encoding.UTF8.GetString(HexToBytes(
            root.GetProperty("readByIndex")[0].GetProperty("hex").GetString() ?? string.Empty)));

        using var archive = OpenReadable(LoadPayload("zip-duplicates"));
        Assert.Equal(3, archive.Entries.Count);
        // The BCL lookup returns the FIRST duplicate: a narrow record reader
        // must implement last-wins explicitly instead of reusing this lookup.
        var selected = archive.GetEntry("dup.txt");
        Assert.NotNull(selected);
        Assert.Equal("first\n", Encoding.UTF8.GetString(ReadEntryBytes(selected)));
    }

    [Fact]
    public void Cp437NameDecodesWithReplacementInBcl()
    {
        using var manifest = LoadManifest("zip-cp437-names");
        var entry = manifest.RootElement.GetProperty("entries")[0];
        Assert.Equal("caf\u00e9.txt", entry.GetProperty("name").GetString());
        Assert.False(entry.GetProperty("utf8Flag").GetBoolean());

        using var archive = OpenReadable(LoadPayload("zip-cp437-names"));
        Assert.Equal("caf\ufffd.txt", archive.Entries[0].FullName);
    }

    [Fact]
    public void RawCommentBytesArePreservedOnDiskButDecodedLossyByBcl()
    {
        using var manifest = LoadManifest("zip-comments");
        var root = manifest.RootElement;
        var commentHex = root.GetProperty("archiveCommentHex").GetString() ?? string.Empty;
        Assert.Contains("ff", commentHex, StringComparison.Ordinal);
        var payload = LoadPayload("zip-comments");
        Assert.True(FindByteSequence(payload, HexToBytes(commentHex)) >= 0);
        // BCL decodes the comment as UTF-8 with replacement: raw bytes need an
        // explicit byte-preserving path in the future implementation.
        using var archive = OpenReadable(payload);
        Assert.Equal("archive Z0 \u00e9 \ufffd raw", archive.Comment);
        Assert.Equal("7065722d66696c6520c3a9", root.GetProperty("entries")[0].GetProperty("commentHex").GetString());
        Assert.Equal("72617720ff2062797465", root.GetProperty("entries")[1].GetProperty("commentHex").GetString());
    }

    [Fact]
    public void CorruptCrcReadsWithoutValidationInBcl()
    {
        using var manifest = LoadManifest("zip-corrupt-crc");
        var root = manifest.RootElement;
        Assert.Equal("data.txt", root.GetProperty("testzip").GetString());
        Assert.Equal(
            "BadZipFile",
            root.GetProperty("readByIndex")[0].GetProperty("error").GetProperty("type").GetString());
        var expectedCrc = Convert.ToUInt32(root.GetProperty("entries")[0].GetProperty("crc32Hex").GetString(), 16);

        // The BCL reader returns the corrupt bytes without complaint, so CRC
        // enforcement must be manual in the future implementation.
        using var archive = OpenReadable(LoadPayload("zip-corrupt-crc"));
        var content = ReadEntryBytes(archive.Entries[0]);
        Assert.NotEqual(expectedCrc, ComputeCrc32(content));
        Assert.Equal(expectedCrc, archive.Entries[0].Crc32);
    }

    [Fact]
    public void CorruptCentralDirectoryFailsInBothReaders()
    {
        using var manifest = LoadManifest("zip-corrupt-central");
        var root = manifest.RootElement;
        Assert.True(root.GetProperty("isZipfile").GetBoolean());
        var openError = root.GetProperty("openError");
        Assert.Equal("BadZipFile", openError.GetProperty("type").GetString());
        Assert.Contains("central directory", openError.GetProperty("message").GetString(), StringComparison.Ordinal);

        var failure = Assert.Throws<InvalidDataException>(() => TouchEntries(LoadPayload("zip-corrupt-central")));
        Assert.Contains("Central Directory", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TruncatedArchiveIsNotRecognized()
    {
        using var manifest = LoadManifest("zip-truncated");
        var root = manifest.RootElement;
        Assert.False(root.GetProperty("isZipfile").GetBoolean());
        var openError = root.GetProperty("openError");
        Assert.Equal("BadZipFile", openError.GetProperty("type").GetString());
        Assert.Contains("not a zip file", openError.GetProperty("message").GetString(), StringComparison.Ordinal);

        var failure = Assert.Throws<InvalidDataException>(() => TouchEntries(LoadPayload("zip-truncated")));
        Assert.Contains("Central Directory record could not be found", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DescriptorZip64ExtraDirectoryAndEmptyStructuresRead()
    {
        using var descriptorManifest = LoadManifest("zip-datadescriptor");
        Assert.True(descriptorManifest.RootElement.GetProperty("entries")[0].GetProperty("descriptorFlag").GetBoolean());
        var descriptorPayload = LoadPayload("zip-datadescriptor");
        Assert.True(FindByteSequence(descriptorPayload, [0x50, 0x4B, 0x07, 0x08]) >= 0);
        using (var archive = OpenReadable(descriptorPayload))
        {
            Assert.Equal(
                descriptorManifest.RootElement.GetProperty("readByIndex")[0].GetProperty("hex").GetString(),
                Convert.ToHexString(ReadEntryBytes(archive.Entries[0])).ToLowerInvariant());
        }

        var zip64Payload = LoadPayload("zip-zip64");
        Assert.True(FindByteSequence(zip64Payload, [0x50, 0x4B, 0x06, 0x06]) >= 0);
        Assert.True(FindByteSequence(zip64Payload, [0x50, 0x4B, 0x06, 0x07]) >= 0);
        using (var archive = OpenReadable(zip64Payload))
        {
            Assert.Equal("zip64 forced\n", Encoding.UTF8.GetString(ReadEntryBytes(archive.Entries[0])));
        }

        using var extraManifest = LoadManifest("zip-extra-field");
        var extraHex = extraManifest.RootElement.GetProperty("entries")[0].GetProperty("extraHex").GetString() ?? string.Empty;
        Assert.StartsWith("feca0400", extraHex, StringComparison.Ordinal);
        using (var archive = OpenReadable(LoadPayload("zip-extra-field")))
        {
            Assert.Equal("extra\n", Encoding.UTF8.GetString(ReadEntryBytes(archive.Entries[0])));
        }

        using (var archive = OpenReadable(LoadPayload("zip-dirs")))
        {
            Assert.Equal(new[] { "docs/", "docs/a.txt" }, archive.Entries.Select(e => e.FullName).ToArray());
            Assert.Equal("nested\n", Encoding.UTF8.GetString(ReadEntryBytes(archive.Entries[1])));
        }

        using (var archive = OpenReadable(LoadPayload("zip-empty")))
        {
            Assert.Empty(archive.Entries);
            Assert.Equal(string.Empty, archive.Comment);
        }
    }

    [Fact]
    public void DosTimestampsFollowTwoSecondResolution()
    {
        using var manifest = LoadManifest("zip-timestamps");
        var entries = manifest.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        // The fixture asked for second 57; DOS time keeps two-second resolution.
        Assert.Equal([2024, 2, 29, 12, 34, 56], entries[2].GetProperty("dateTime").EnumerateArray().Select(e => e.GetInt32()).ToArray());
        Assert.Equal([1980, 1, 1, 0, 0, 0], entries[0].GetProperty("dateTime").EnumerateArray().Select(e => e.GetInt32()).ToArray());

        using var archive = OpenReadable(LoadPayload("zip-timestamps"));
        Assert.Equal(56, archive.Entries[2].LastWriteTime.Second);
        Assert.Equal(new DateTime(1980, 1, 1, 0, 0, 0), archive.Entries[0].LastWriteTime.DateTime);
    }

    [Fact]
    public void Utf8NamesRoundTripExactly()
    {
        using var manifest = LoadManifest("zip-utf8-names");
        var entries = manifest.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        Assert.All(entries, entry => Assert.True(entry.GetProperty("utf8Flag").GetBoolean()));
        using var archive = OpenReadable(LoadPayload("zip-utf8-names"));
        Assert.Equal(entries[0].GetProperty("name").GetString(), archive.Entries[0].FullName);
        Assert.Equal(entries[1].GetProperty("name").GetString(), archive.Entries[1].FullName);
    }
}
