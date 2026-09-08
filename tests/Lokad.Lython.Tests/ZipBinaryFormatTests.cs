using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Zip;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// Focused white-box binary-format tests for the ZIP record serializer:
/// signatures, versions, field placement, timestamps, flags, comments,
/// ZIP64 structures, and explicit serializer rejections.
/// </summary>
public sealed class ZipBinaryFormatTests
{
    [Fact]
    public void EmptyArchiveSerializesValidRecords()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import zipfile
with zipfile.ZipFile("/empty.zip", "w") as archive:
    pass
return 1
""",
            host);
        Assert.True(result.Success, result.Failure?.Message);
        var expected = File.ReadAllBytes(Path.Combine(FindCasesRoot(), "zip-empty", "input.zip"));
        Assert.Equal(expected, host.ReadBytes("/empty.zip"));
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

    private const int MaxCommentLength = 65535;

    private static readonly LythonSourceSpan TestSpan = new(0, 0, 0, 0);

    private static LythonRuntime.ExecutionContext NewContext(MockLythonHost host)
        => new(host, new LythonRunOptions());

    private static ZipRecordWriter.StagedEntry StoredEntry(
        string name, byte[] data, ushort method = 0, int level = -1, bool forceZip64 = false)
    {
        var (dosTime, dosDate) = ZipRecordWriter.EncodeDosDateTime(2020, 5, 6, 7, 8, 10, TestSpan);
        return new ZipRecordWriter.StagedEntry(
            name,
            Encoding.ASCII.GetBytes(name),
            0,
            dosTime,
            dosDate,
            method,
            level,
            [],
            [],
            0,
            (uint)(384 << 16),
            data,
            null,
            forceZip64);
    }

    private static byte[] SerializeSingle(string name, byte[] data, ushort method = 0, int level = -1, bool forceZip64 = false)
    {
        var host = new MockLythonHost();
        var archive = ZipRecordWriter.SerializeArchive(
            [StoredEntry(name, data, method, level, forceZip64)], [], true, NewContext(host), TestSpan);
        Assert.Single(archive.Entries);
        return archive.Bytes;
    }

    private static (int DirectoryOffset, int DirectorySize, int EntryCount) ReadEndRecord(byte[] bytes)
    {
        Assert.True(bytes.Length >= 22, "archive holds an end record");
        var start = -1;
        for (var i = bytes.Length - 22; i >= Math.Max(0, bytes.Length - 22 - MaxCommentLength); i--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i, 4)) != 0x06054B50u)
            {
                continue;
            }

            if (i + 22 + BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i + 20, 2)) == bytes.Length)
            {
                start = i;
                break;
            }
        }

        Assert.True(start >= 0, "archive holds an end record");
        return (
            checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(start + 16, 4))),
            checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(start + 12, 4))),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(start + 8, 2)));
    }

    [Fact]
    public void StoredEntryLayoutMatchesFieldPlan()
    {
        var bytes = SerializeSingle("a.txt", Encoding.ASCII.GetBytes("hello"));
        Assert.Equal(0x04034B50u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, 4)));
        Assert.Equal((ushort)20, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8, 2)));
        Assert.Equal(5u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(18, 4)));
        Assert.Equal(5u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(22, 4)));
        Assert.Equal((ushort)5, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26, 2)));
        Assert.Equal("hello", Encoding.ASCII.GetString(bytes.AsSpan(35, 5)));
        var (directoryOffset, directorySize, entryCount) = ReadEndRecord(bytes);
        Assert.Equal(1, entryCount);
        Assert.Equal(30 + 5 + 5, directoryOffset);
        Assert.Equal(0x02014B50u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(directoryOffset, 4)));
        Assert.Equal(directorySize, bytes.Length - directoryOffset - 22);
    }

    [Fact]
    public void CrcAndSizesSitAtDocumentedOffsets()
    {
        var data = Encoding.ASCII.GetBytes("hello");
        var bytes = SerializeSingle("a.txt", data);
        var expectedCrc = Crc32.Compute(data, NewContext(new MockLythonHost()), TestSpan);
        Assert.Equal(expectedCrc, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(14, 4)));
        var (directoryOffset, _, _) = ReadEndRecord(bytes);
        Assert.Equal(expectedCrc, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(directoryOffset + 16, 4)));
        Assert.Equal((uint)data.Length, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(directoryOffset + 24, 4)));
    }

    [Fact]
    public void DosTimestampsEncodeDocumentedFields()
    {
        var (dosTime, dosDate) = ZipRecordWriter.EncodeDosDateTime(2020, 5, 6, 7, 8, 10, TestSpan);
        Assert.Equal((ushort)((7 << 11) | (8 << 5) | 5), dosTime);
        Assert.Equal((ushort)(6 | (5 << 5) | ((2020 - 1980) << 9)), dosDate);
        var bytes = SerializeSingle("a.txt", [1]);
        Assert.Equal(dosTime, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(10, 2)));
        Assert.Equal(dosDate, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(12, 2)));
        Assert.Equal("ValueError", Assert.Throws<LythonRuntimeException>(
            () => ZipRecordWriter.EncodeDosDateTime(1979, 12, 31, 23, 59, 58, TestSpan)).ExceptionType);
        Assert.Equal("ValueError", Assert.Throws<LythonRuntimeException>(
            () => ZipRecordWriter.EncodeDosDateTime(2020, 99, 1, 0, 0, 0, TestSpan)).ExceptionType);
    }

    [Fact]
    public void Utf8FlagFollowsNameBytes()
    {
        var ascii = SerializeSingle("plain.txt", [1]);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(ascii.AsSpan(6, 2)));
        var host = new MockLythonHost();
        var (dosTime, dosDate) = ZipRecordWriter.EncodeDosDateTime(2020, 5, 6, 7, 8, 10, TestSpan);
        var entry = new ZipRecordWriter.StagedEntry(
            "caf\u00e9.txt",
            Encoding.UTF8.GetBytes("caf\u00e9.txt"),
            0x0800,
            dosTime,
            dosDate,
            0,
            -1,
            [],
            [],
            0,
            (uint)(384 << 16),
            [1],
            null, false);
        var archive = ZipRecordWriter.SerializeArchive([entry], [], true, NewContext(host), TestSpan);
        Assert.Equal((ushort)0x0800, BinaryPrimitives.ReadUInt16LittleEndian(archive.Bytes.AsSpan(6, 2)));
        Assert.Equal(
            Encoding.UTF8.GetBytes("caf\u00e9.txt"),
            archive.Bytes.AsSpan(30, Encoding.UTF8.GetByteCount("caf\u00e9.txt")).ToArray());
    }

    [Fact]
    public void CommentsLandInCentralAndEndRecords()
    {
        var host = new MockLythonHost();
        var (dosTime, dosDate) = ZipRecordWriter.EncodeDosDateTime(2020, 5, 6, 7, 8, 10, TestSpan);
        var entry = new ZipRecordWriter.StagedEntry(
            "a.txt", Encoding.ASCII.GetBytes("a.txt"), 0, dosTime, dosDate, 0, -1,
            Encoding.ASCII.GetBytes("entry"), [], 0, (uint)(384 << 16), [7], null, false);
        var archive = ZipRecordWriter.SerializeArchive(
            [entry], Encoding.ASCII.GetBytes("archive"), true, NewContext(host), TestSpan);
        var (directoryOffset, _, _) = ReadEndRecord(archive.Bytes);
        Assert.Equal((ushort)5, BinaryPrimitives.ReadUInt16LittleEndian(archive.Bytes.AsSpan(directoryOffset + 32, 2)));
        Assert.Equal((ushort)7, BinaryPrimitives.ReadUInt16LittleEndian(archive.Bytes.AsSpan(archive.Bytes.Length - 9, 2)));
        Assert.Equal("archive", Encoding.ASCII.GetString(archive.Bytes.AsSpan(archive.Bytes.Length - 7, 7)));
    }

    [Fact]
    public void ForcedZip64EmitsVersion45Structures()
    {
        var bytes = SerializeSingle("f.bin", Encoding.ASCII.GetBytes("tiny"), forceZip64: true);
        Assert.Equal((ushort)45, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2)));
        Assert.Equal((ushort)28, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28, 2)));
        Assert.Equal((ushort)0x0001, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(35, 2)));
        Assert.Equal((ushort)24, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(37, 2)));
        Assert.Equal(uint.MaxValue, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(18, 4)));
        var (directoryOffset, _, _) = ReadEndRecord(bytes);
        Assert.Equal((ushort)45, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(directoryOffset + 6, 2)));
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.Equal("tiny", new StreamReader(archive.GetEntry("f.bin")!.Open()).ReadToEnd());
    }

    [Fact]
    public void NoDataDescriptorsAreEmitted()
    {
        var bytes = SerializeSingle("a.txt", Encoding.ASCII.GetBytes("hello"), method: 8, level: 6);
        for (var i = 0; i + 4 <= bytes.Length; i++)
        {
            Assert.NotEqual(0x08074B50u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i, 4)));
        }

        Assert.Equal((ushort)8, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8, 2)));
    }

    [Fact]
    public void DuplicateOrderAndOffsetsStayAscending()
    {
        var host = new MockLythonHost();
        var archive = ZipRecordWriter.SerializeArchive(
            [StoredEntry("dup.txt", [1]), StoredEntry("dup.txt", [1, 2])],
            [], true, NewContext(host), TestSpan);
        Assert.Equal(2, archive.Entries.Count);
        var (directoryOffset, _, entryCount) = ReadEndRecord(archive.Bytes);
        Assert.Equal(2, entryCount);
        var firstHeader = directoryOffset;
        var secondHeader = firstHeader + 46 + "dup.txt".Length;
        Assert.Equal(0x02014B50u, BinaryPrimitives.ReadUInt32LittleEndian(archive.Bytes.AsSpan(secondHeader, 4)));
        var firstOffset = BinaryPrimitives.ReadUInt32LittleEndian(archive.Bytes.AsSpan(firstHeader + 42, 4));
        var secondOffset = BinaryPrimitives.ReadUInt32LittleEndian(archive.Bytes.AsSpan(secondHeader + 42, 4));
        Assert.True(firstOffset < secondOffset);
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(archive.Bytes.AsSpan(secondHeader + 24, 4)));
    }

    [Fact]
    public void DisallowedZip64FailsBeforeEmission()
    {
        var host = new MockLythonHost();
        var failure = Assert.Throws<LythonRuntimeException>(() => ZipRecordWriter.SerializeArchive(
            [StoredEntry("f.bin", [1], forceZip64: true)], [], false, NewContext(host), TestSpan));
        Assert.Equal("LargeZipFile", failure.ExceptionType);
    }

    [Fact]
    public void DirectoryEntryKeepsAttributes()
    {
        var host = new MockLythonHost();
        var (dosTime, dosDate) = ZipRecordWriter.EncodeDosDateTime(1980, 1, 1, 0, 0, 0, TestSpan);
        var entry = new ZipRecordWriter.StagedEntry(
            "docs/", Encoding.ASCII.GetBytes("docs/"), 0, dosTime, dosDate, 0, -1,
            [], [], 0, (uint)(((16384 | 511) & 0xFFFF) << 16 | 0x10), [], null, false);
        var archive = ZipRecordWriter.SerializeArchive([entry], [], true, NewContext(host), TestSpan);
        var (directoryOffset, _, _) = ReadEndRecord(archive.Bytes);
        Assert.Equal((16384 | 511) << 16 | 0x10, (int)BinaryPrimitives.ReadUInt32LittleEndian(archive.Bytes.AsSpan(directoryOffset + 38, 4)));
        using var readback = new ZipArchive(new MemoryStream(archive.Bytes), ZipArchiveMode.Read);
        Assert.Single(readback.Entries);
    }
}
