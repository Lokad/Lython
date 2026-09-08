using System.Buffers.Binary;
using System.Text;

namespace Lokad.Lython.Runtime.Zip;

/// <summary>
/// Parsed immutable metadata for one central-directory entry. Sizes and
/// offsets are ZIP64-resolved <c>ulong</c> values kept wide until a consumer
/// validates them against representation and run limits. Names preserve raw
/// bytes alongside the decoded form; duplicate names keep directory order and
/// are distinguished by <see cref="DirectoryOrdinal"/>.
/// </summary>
internal readonly record struct ZipDirectoryEntry(
    string Name,
    byte[] NameBytes,
    bool Utf8Flag,
    ushort CompressionMethod,
    ushort GeneralPurposeFlags,
    uint Crc32,
    ulong CompressedSize,
    ulong UncompressedSize,
    ulong HeaderOffset,
    ulong DataOffset,
    ulong DataEndLimit,
    bool HasDataDescriptor,
    byte[] Comment,
    byte[] Extra,
    int CreateSystem,
    uint ExternalAttributes,
    ushort DosTime,
    ushort DosDate,
    int DateYear,
    int DateMonth,
    int DateDay,
    int DateHour,
    int DateMinute,
    int DateSecond,
    int DirectoryOrdinal)
{
    public bool IsDirectory => Name.EndsWith('/');
}

/// <summary>Bounded parse result for one archive directory.</summary>
internal sealed class ZipArchiveDirectory
{
    public ZipArchiveDirectory(IReadOnlyList<ZipDirectoryEntry> entries, byte[] comment, bool isZip64, long metadataCharge)
    {
        Entries = entries;
        Comment = comment;
        IsZip64 = isZip64;
        MetadataCharge = metadataCharge;
    }

    public IReadOnlyList<ZipDirectoryEntry> Entries { get; }

    public byte[] Comment { get; }

    public bool IsZip64 { get; }

    /// <summary>
    /// Governor bytes committed for the parsed metadata above. Handles that
    /// retain the directory keep this charge; recognition-only and failed
    /// parses release it because nothing takes ownership.
    /// </summary>
    public long MetadataCharge { get; }
}

/// <summary>
/// Bounded ZIP record reader (Z1): parses end-of-central-directory, ZIP64,
/// and central-directory structures with checked arithmetic, charging retained
/// metadata and checking work budgets as it goes. Compressed entry data is
/// never inflated here; <see cref="ZipDirectoryEntry.DataOffset"/> locates it
/// for governed member reads. Structural failures throw
/// <see cref="InvalidDataException"/> with logical detail; callers translate
/// to their own error identities (<c>BadZipFile</c> for zipfile).
/// Data checksums are validated when members are read, not here.
/// </summary>
internal static class ZipDirectoryReader
{
    private const uint EndOfCentralDirectorySignature = 0x06054B50;
    private const uint Zip64EndSignature = 0x06064B50;
    private const uint Zip64LocatorSignature = 0x07064B50;
    private const uint CentralHeaderSignature = 0x02014B50;
    private const uint LocalHeaderSignature = 0x04034B50;
    private const int EndRecordMinimumLength = 22;
    private const int MaxCommentLength = 65535;
    private const ushort Zip64ExtraTag = 0x0001;
    private const ushort Utf8NameFlag = 0x0800;
    private const ushort DataDescriptorFlag = 0x0008;
    private const long DirectoryEntryBaseBytes = 128;
    private const int BudgetCheckInterval = 64;
    private const int ScanBudgetChunkBytes = 4096;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    // CP437 high half (0x80-0xFF); low bytes are ASCII. Decoded explicitly so
    // metadata never depends on process-global encoding providers.
    private const string Cp437HighHalf =
        "\u00C7\u00FC\u00E9\u00E2\u00E4\u00E0\u00E5\u00E7\u00EA\u00EB\u00E8\u00EF\u00EE\u00EC\u00C4\u00C5\u00C9\u00E6\u00C6\u00F4\u00F6\u00F2\u00FB\u00F9\u00FF\u00D6\u00DC\u00A2\u00A3\u00A5\u20A7\u0192\u00E1\u00ED\u00F3\u00FA\u00F1\u00D1\u00AA\u00BA\u00BF\u2310\u00AC\u00BD\u00BC\u00A1\u00AB\u00BB\u2591\u2592\u2593\u2502\u2524\u2561\u2562\u2556\u2555\u2563\u2551\u2557\u255D\u255C\u255B\u2510\u2514\u2534\u252C\u251C\u2500\u253C\u255E\u255F\u255A\u2554\u2569\u2566\u2560\u2550\u256C\u2567\u2568\u2564\u2565\u2559\u2558\u2552\u2553\u256B\u256A\u2518\u250C\u2588\u2584\u258C\u2590\u2580\u03B1\u00DF\u0393\u03C0\u03A3\u03C3\u00B5\u03C4\u03A6\u0398\u03A9\u03B4\u221E\u03C6\u03B5\u2229\u2261\u00B1\u2265\u2264\u2320\u2321\u00F7\u2248\u00B0\u2219\u00B7\u221A\u207F\u00B2\u25A0\u00A0";

    public static ZipArchiveDirectory Read(
        ReadOnlyMemory<byte> data,
        bool forceUtf8Names,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan? span)
    {
        var bytes = data.Span;
        var end = FindEndRecord(bytes, context, span);
        return ReadCentralDirectory(bytes, end, forceUtf8Names, context, span);
    }

    private readonly record struct EndRecord(
        ulong DirectoryOffset,
        ulong DirectorySize,
        ulong EntryCount,
        byte[] Comment,
        bool IsZip64);

    private static EndRecord FindEndRecord(ReadOnlySpan<byte> bytes, LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
    {
        // The end record carries a variable comment, so scan the tail window
        // backwards for its last occurrence. Leading prefixes are tolerated;
        // trailing bytes after the record are tolerated.
        var window = Math.Min(bytes.Length, EndRecordMinimumLength + MaxCommentLength);
        var scanned = 0;
        for (var offset = bytes.Length - EndRecordMinimumLength; offset >= bytes.Length - window; offset--)
        {
            if ((++scanned & (ScanBudgetChunkBytes - 1)) == 0)
            {
                context.CheckExecutionBudget(span);
            }

            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4)) != EndOfCentralDirectorySignature)
            {
                continue;
            }

            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset + 20, 2));
            var recordEnd = checked((long)offset + EndRecordMinimumLength + commentLength);
            if (recordEnd > bytes.Length)
            {
                continue;
            }

            var diskNumber = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset + 4, 2));
            var directoryDisk = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset + 6, 2));
            if (diskNumber != 0 || directoryDisk != 0)
            {
                throw Malformed("multi-disk archives are unsupported");
            }

            var diskEntries = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset + 8, 2));
            var totalEntries = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset + 10, 2));
            var directorySize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + 12, 4));
            var directoryOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + 16, 4));
            if (diskEntries != totalEntries)
            {
                throw Malformed("end record entry counts disagree");
            }

            if (diskEntries == ushort.MaxValue || totalEntries == ushort.MaxValue ||
                directorySize == uint.MaxValue || directoryOffset == uint.MaxValue)
            {
                return ReadZip64EndRecord(bytes, offset, (ulong)totalEntries, commentLength);
            }

            return new EndRecord(
                directoryOffset,
                directorySize,
                totalEntries,
                bytes.Slice(offset + EndRecordMinimumLength, commentLength).ToArray(),
                IsZip64: false);
        }

        throw Malformed("missing end-of-central-directory record");
    }

    private static EndRecord ReadZip64EndRecord(
        ReadOnlySpan<byte> bytes,
        int endOffset,
        ulong entryCount,
        int commentLength)
    {
        const int locatorLength = 20;
        var locatorOffset = checked((long)endOffset - locatorLength);
        if (locatorOffset < 0)
        {
            throw Malformed("ZIP64 locator extends beyond the start of data");
        }

        var locator = (int)locatorOffset;
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(locator, 4)) != Zip64LocatorSignature)
        {
            throw Malformed("ZIP64 end record is missing its locator");
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(locator + 4, 4)) != 0 ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(locator + 16, 4)) != 1)
        {
            throw Malformed("multi-disk archives are unsupported");
        }

        var zip64Offset = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(locator + 8, 8));
        if (zip64Offset > (ulong)bytes.Length || zip64Offset + 12 > (ulong)bytes.Length)
        {
            throw Malformed("ZIP64 end record extends beyond the end of data");
        }

        var recordOffset = (int)zip64Offset;
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(recordOffset, 4)) != Zip64EndSignature)
        {
            throw Malformed("ZIP64 locator points at a non-directory record");
        }

        var recordSize = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(recordOffset + 4, 8));
        if (recordSize < 44 || zip64Offset + 12 + recordSize > (ulong)bytes.Length)
        {
            throw Malformed("ZIP64 end record has an impossible size");
        }

        if (checked((long)zip64Offset + 12 + (long)recordSize) != locatorOffset)
        {
            throw Malformed("ZIP64 end record does not abut its locator");
        }

        var diskEntries = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(recordOffset + 24, 8));
        var totalEntries = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(recordOffset + 32, 8));
        if (diskEntries != totalEntries)
        {
            throw Malformed("ZIP64 end record entry counts disagree");
        }

        if (entryCount != ushort.MaxValue && totalEntries != entryCount)
        {
            throw Malformed("ZIP64 and end record entry counts disagree");
        }

        var directorySize = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(recordOffset + 40, 8));
        var directoryOffset = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(recordOffset + 48, 8));
        return new EndRecord(
            directoryOffset,
            directorySize,
            totalEntries,
            bytes.Slice(endOffset + EndRecordMinimumLength, commentLength).ToArray(),
            IsZip64: true);
    }

    private static ZipArchiveDirectory ReadCentralDirectory(
        ReadOnlySpan<byte> bytes,
        EndRecord end,
        bool forceUtf8Names,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan? span)
    {
        if (end.EntryCount > int.MaxValue)
        {
            throw Malformed("central directory entry count is not representable");
        }

        var directoryEnd = checked(end.DirectoryOffset + end.DirectorySize);
        if (directoryEnd > (ulong)bytes.Length)
        {
            throw Malformed("central directory extends beyond the end of data");
        }

        var entries = new List<ZipDirectoryEntry>();
        var offset = end.DirectoryOffset;
        var scanned = 0;
        var metadataCharge = 0L;
        try
        {
            // The end-record comment was already copied; charge it with the
            // entries below so one balance covers the whole directory.
            var commentCharge = PyBytes.EstimateApproximateBytes(end.Comment.Length);
            context.MemoryGovernor.Reserve(commentCharge, span);
            context.MemoryGovernor.Commit(commentCharge);
            metadataCharge = checked(metadataCharge + commentCharge);

            while ((ulong)entries.Count < end.EntryCount)
            {
                if ((++scanned & (BudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }

                entries.Add(ReadCentralEntry(bytes, ref offset, directoryEnd, entries.Count, forceUtf8Names, context, span, ref metadataCharge));
            }

            if (offset != directoryEnd)
            {
                throw Malformed("central directory has trailing bytes");
            }
        }
        catch
        {
            // Nothing takes ownership of a failed parse; release everything
            // committed above so recognition and open failures stay balanced.
            context.MemoryGovernor.Release(metadataCharge);
            throw;
        }

        // Overlap limits chain in central order (matching CPython): each entry
        // ends where the next header begins, or where the directory begins.
        for (var index = 0; index < entries.Count; index++)
        {
            var limit = index + 1 < entries.Count
                ? entries[index + 1].HeaderOffset
                : end.DirectoryOffset;
            entries[index] = entries[index] with { DataEndLimit = limit };
        }

        return new ZipArchiveDirectory(entries, end.Comment, end.IsZip64, metadataCharge);
    }

    private static ZipDirectoryEntry ReadCentralEntry(
        ReadOnlySpan<byte> bytes,
        ref ulong offset,
        ulong directoryEnd,
        int ordinal,
        bool forceUtf8Names,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan? span,
        ref long metadataCharge)
    {
        const int fixedLength = 46;
        if (offset + fixedLength > directoryEnd)
        {
            throw Malformed($"central entry #{ordinal} extends beyond its directory");
        }

        var cursor = (int)offset;
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(cursor, 4)) != CentralHeaderSignature)
        {
            throw Malformed($"central entry #{ordinal} has a bad header signature");
        }

        var createSystem = bytes[cursor + 5];
        var flags = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(cursor + 8, 2));
        var method = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(cursor + 10, 2));
        var dosTime = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(cursor + 12, 2));
        var dosDate = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(cursor + 14, 2));
        var crc = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(cursor + 16, 4));
        var compressedSize32 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(cursor + 20, 4));
        var uncompressedSize32 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(cursor + 24, 4));
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(cursor + 28, 2));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(cursor + 30, 2));
        var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(cursor + 32, 2));
        var externalAttributes = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(cursor + 38, 4));
        var headerOffset32 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(cursor + 42, 4));
        var recordEnd = checked(offset + fixedLength + nameLength + extraLength + commentLength);
        if (recordEnd > directoryEnd)
        {
            throw Malformed($"central entry #{ordinal} overruns its directory");
        }

        // Reserve before copying: the header already states every length, so the
        // charge is known before any allocation below.
        var charge = checked(DirectoryEntryBaseBytes + nameLength + extraLength + commentLength);
        context.MemoryGovernor.Reserve(charge, span);
        context.MemoryGovernor.Commit(charge);
        metadataCharge = checked(metadataCharge + charge);

        var nameBytes = bytes.Slice(cursor + fixedLength, nameLength).ToArray();
        var extra = bytes.Slice(cursor + fixedLength + nameLength, extraLength).ToArray();
        var comment = bytes.Slice(cursor + fixedLength + nameLength + extraLength, commentLength).ToArray();
        offset = recordEnd;

        context.ObserveCollectionCount(ordinal + 1, span);

        var compressedSize = (ulong)compressedSize32;
        var uncompressedSize = (ulong)uncompressedSize32;
        var headerOffset = (ulong)headerOffset32;
        if (compressedSize32 == uint.MaxValue || uncompressedSize32 == uint.MaxValue || headerOffset32 == uint.MaxValue)
        {
            (compressedSize, uncompressedSize, headerOffset) = ResolveZip64Extra(
                extra, compressedSize32, uncompressedSize32, headerOffset32, ordinal);
        }

        var dataOffset = ValidateLocalHeader(
            bytes, headerOffset, method, flags, nameBytes, compressedSize, uncompressedSize, ordinal);
        var name = DecodeEntryName(nameBytes, forceUtf8Names || (flags & Utf8NameFlag) != 0, ordinal);
        DecodeDosDateTime(dosTime, dosDate, out var year, out var month, out var day, out var hour, out var minute, out var second);
        return new ZipDirectoryEntry(
            name,
            nameBytes,
            (flags & Utf8NameFlag) != 0,
            method,
            flags,
            crc,
            compressedSize,
            uncompressedSize,
            headerOffset,
            dataOffset,
            ulong.MaxValue,
            (flags & DataDescriptorFlag) != 0,
            comment,
            extra,
            createSystem,
            externalAttributes,
            dosTime,
            dosDate,
            year,
            month,
            day,
            hour,
            minute,
            second,
            ordinal);
    }

    private static (ulong CompressedSize, ulong UncompressedSize, ulong HeaderOffset) ResolveZip64Extra(
        ReadOnlySpan<byte> extra,
        uint compressedSize32,
        uint uncompressedSize32,
        uint headerOffset32,
        int ordinal)
    {
        var compressedSize = (ulong)compressedSize32;
        var uncompressedSize = (ulong)uncompressedSize32;
        var headerOffset = (ulong)headerOffset32;
        var cursor = 0;
        while (cursor + 4 <= extra.Length)
        {
            var tag = BinaryPrimitives.ReadUInt16LittleEndian(extra.Slice(cursor, 2));
            var size = BinaryPrimitives.ReadUInt16LittleEndian(extra.Slice(cursor + 2, 2));
            if (cursor + 4 + size > extra.Length)
            {
                throw Malformed($"central entry #{ordinal} has a truncated extra field");
            }

            if (tag == Zip64ExtraTag)
            {
                var values = extra.Slice(cursor + 4, size);
                var position = 0;
                if (compressedSize32 == uint.MaxValue)
                {
                    if (values.Length - position < 8)
                    {
                        throw Malformed($"central entry #{ordinal} has a short ZIP64 extra field");
                    }

                    compressedSize = BinaryPrimitives.ReadUInt64LittleEndian(values.Slice(position, 8));
                    position += 8;
                }

                if (uncompressedSize32 == uint.MaxValue)
                {
                    if (values.Length - position < 8)
                    {
                        throw Malformed($"central entry #{ordinal} has a short ZIP64 extra field");
                    }

                    uncompressedSize = BinaryPrimitives.ReadUInt64LittleEndian(values.Slice(position, 8));
                    position += 8;
                }

                if (headerOffset32 == uint.MaxValue)
                {
                    if (values.Length - position < 8)
                    {
                        throw Malformed($"central entry #{ordinal} has a short ZIP64 extra field");
                    }

                    headerOffset = BinaryPrimitives.ReadUInt64LittleEndian(values.Slice(position, 8));
                    position += 8;
                }

                return (compressedSize, uncompressedSize, headerOffset);
            }

            cursor += 4 + size;
        }

        if (compressedSize32 == uint.MaxValue || uncompressedSize32 == uint.MaxValue || headerOffset32 == uint.MaxValue)
        {
            throw Malformed($"central entry #{ordinal} needs ZIP64 sizes without a ZIP64 extra field");
        }

        return (compressedSize, uncompressedSize, headerOffset);
    }

    private static ulong ValidateLocalHeader(
        ReadOnlySpan<byte> bytes,
        ulong headerOffset,
        ushort method,
        ushort flags,
        byte[] nameBytes,
        ulong compressedSize,
        ulong uncompressedSize,
        int ordinal)
    {
        const int fixedLength = 30;
        if (headerOffset + fixedLength > (ulong)bytes.Length)
        {
            throw Malformed($"central entry #{ordinal} points beyond the end of data");
        }

        var cursor = (int)headerOffset;
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(cursor, 4)) != LocalHeaderSignature)
        {
            throw Malformed($"central entry #{ordinal} points at a non-entry record");
        }

        var localFlags = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(cursor + 6, 2));
        var localMethod = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(cursor + 8, 2));
        if (localMethod != method)
        {
            throw Malformed($"central entry #{ordinal} disagrees with its local method");
        }

        if ((localFlags & DataDescriptorFlag) != (flags & DataDescriptorFlag))
        {
            throw Malformed($"central entry #{ordinal} disagrees with its local descriptor flag");
        }

        var localNameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(cursor + 26, 2));
        var localExtraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(cursor + 28, 2));
        var dataOffset = checked(headerOffset + (ulong)fixedLength + localNameLength + (ulong)localExtraLength);
        if (dataOffset > (ulong)bytes.Length)
        {
            throw Malformed($"central entry #{ordinal} has local lengths beyond the end of data");
        }

        if (localNameLength != nameBytes.Length ||
            !bytes.Slice((int)checked(headerOffset + fixedLength), localNameLength).SequenceEqual(nameBytes))
        {
            throw Malformed($"central entry #{ordinal} disagrees with its local name");
        }

        var dataEnd = checked(dataOffset + compressedSize);
        if (dataEnd > (ulong)bytes.Length)
        {
            throw Malformed($"central entry #{ordinal} has data beyond the end of data");
        }

        if ((flags & DataDescriptorFlag) == 0)
        {
            var localCompressed32 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(cursor + 18, 4));
            var localUncompressed32 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(cursor + 22, 4));
            ulong localCompressed = localCompressed32;
            ulong localUncompressed = localUncompressed32;
            if (localCompressed32 == uint.MaxValue || localUncompressed32 == uint.MaxValue)
            {
                // R38: ZIP64 entries carry real sizes in the local extra field,
                // merged after any unrelated carried fields.
                var extraStart = checked(headerOffset + (ulong)fixedLength + localNameLength);
                (localCompressed, localUncompressed, _) = ResolveZip64Extra(bytes.Slice((int)extraStart, localExtraLength), localCompressed32, localUncompressed32, 0, ordinal);
            }

            if (localCompressed != compressedSize || localUncompressed != uncompressedSize)
            {
                throw Malformed($"central entry #{ordinal} disagrees with its local sizes");
            }
        }
        return dataOffset;
    }

    private static string DecodeEntryName(byte[] nameBytes, bool utf8Flag, int ordinal)
    {
        if (utf8Flag)
        {
            try
            {
                return StrictUtf8.GetString(nameBytes);
            }
            catch (DecoderFallbackException exception)
            {
                throw Malformed($"central entry #{ordinal} has malformed UTF-8 name bytes ({exception.Message})");
            }
        }

        var builder = new StringBuilder(nameBytes.Length);
        foreach (var value in nameBytes)
        {
            builder.Append(value < 0x80 ? (char)value : Cp437HighHalf[value - 0x80]);
        }

        return builder.ToString();
    }

    internal static void DecodeDosDateTime(
        ushort dosTime,
        ushort dosDate,
        out int year,
        out int month,
        out int day,
        out int hour,
        out int minute,
        out int second)
    {
        year = (dosDate >> 9) + 1980;
        month = (dosDate >> 5) & 15;
        day = dosDate & 31;
        hour = dosTime >> 11;
        minute = (dosTime >> 5) & 63;
        second = (dosTime & 31) * 2;
    }

    private static InvalidDataException Malformed(string detail)
        => new($"Invalid ZIP archive: {detail}.");

    /// <summary>
    /// Locates entry data for caller-supplied metadata (foreign or
    /// manually constructed <c>ZipInfo</c>): validates the local signature
    /// and name at <c>headerOffset</c> and returns the data offset. Sizes,
    /// CRC, and method come from the supplied info, mirroring CPython.
    /// </summary>
    public static ulong ReadLocalDataOffset(
        ReadOnlyMemory<byte> data,
        ulong headerOffset,
        string expectedName,
        bool forceUtf8Names,
        LythonSourceSpan? span)
    {
        _ = span;
        var bytes = data.Span;
        const int fixedLength = 30;
        if (headerOffset + fixedLength > (ulong)bytes.Length)
        {
            throw Malformed("local header extends beyond the end of data");
        }

        var cursor = (int)headerOffset;
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(cursor, 4)) != LocalHeaderSignature)
        {
            throw Malformed("local header has a bad signature");
        }

        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(cursor + 26, 2));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(cursor + 28, 2));
        var dataOffset = checked(headerOffset + (ulong)fixedLength + nameLength + (ulong)extraLength);
        if (dataOffset > (ulong)bytes.Length)
        {
            throw Malformed("local header has lengths beyond the end of data");
        }

        var nameBytes = bytes.Slice(cursor + fixedLength, nameLength).ToArray();
        var actualName = DecodeEntryName(nameBytes, forceUtf8Names, -1);
        if (!actualName.Equals(expectedName, StringComparison.Ordinal))
        {
            throw Malformed($"File name in directory '{expectedName}' and header '{actualName}' differ.");
        }

        return dataOffset;
    }
}
