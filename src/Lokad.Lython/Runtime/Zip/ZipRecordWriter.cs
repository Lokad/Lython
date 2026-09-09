using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime.Zip;

/// <summary>
/// Bounded archive serializer (Z3): encodes DOS timestamps, compresses
/// entries with raw numeric DEFLATE levels shared with the gzip mechanics,
/// and emits local/central/end records with ZIP64 structures only when sizes
/// require them. All output is staged in governed memory; callers publish a
/// fully validated payload.
/// </summary>
internal static class ZipRecordWriter
{
    private const int Zip64Version = 45;
    private const long Zip64Limit = (1L << 31) - 1;
    private const int Zip64CountLimit = (1 << 16) - 1;

    public static (ushort DosTime, ushort DosDate) EncodeDosDateTime(
        int year, int month, int day, int hour, int minute, int second, LythonSourceSpan? span)
    {
        // Packability mirrors CPython (raw DOS bit fields; only overflow fails),
        // while failures stay deliberate instead of escaping as structural errors.
        // The pre-1980 case keeps its dedicated compatibility message.
        if (year < 1980)
        {
            throw new LythonRuntimeException("ValueError", "ZIP does not support timestamps before 1980", span);
        }

        if (year > 2107 || month < 0 || month > 15 || day < 0 || day > 31 ||
            hour < 0 || hour > 31 || minute < 0 || minute > 63 || second < 0 || second > 63)
        {
            throw new LythonRuntimeException("ValueError", "ZipInfo date_time is out of range for DOS timestamps.", span);
        }

        var dosTime = (ushort)((second / 2) | (minute << 5) | (hour << 11));
        var dosDate = (ushort)(day | (month << 5) | ((year - 1980) << 9));
        return (dosTime, dosDate);
    }

    public sealed record StagedEntry(
        string Name,
        byte[] NameBytes,
        ushort Flags,
        ushort DosTime,
        ushort DosDate,
        ushort Method,
        int Level,
        byte[] Comment,
        byte[] Extra,
        int CreateSystem,
        uint ExternalAttributes,
        byte[] Data,
        Action<uint, ulong>? OnPublished,
        bool ForceZip64);

    public readonly record struct EntryResult(uint Crc, ulong CompressedSize);

    public sealed record SerializedArchive(byte[] Bytes, IReadOnlyList<EntryResult> Entries);

    /// <summary>
    /// Byte-preserved source entry for append mode: the compressed payload
    /// and sizes come from validated source metadata, so publication neither
    /// recompresses nor revalidates the payload.
    /// </summary>
    public sealed record PreservedEntry(
        string Name,
        byte[] NameBytes,
        ushort Flags,
        ushort DosTime,
        ushort DosDate,
        ushort Method,
        byte[] Comment,
        byte[] Extra,
        int CreateSystem,
        uint ExternalAttributes,
        uint Crc,
        ulong UncompressedSize,
        ReadOnlyMemory<byte> CompressedPayload);

    private readonly record struct LayoutEntry(
        string Name,
        byte[] NameBytes,
        ushort Flags,
        ushort DosTime,
        ushort DosDate,
        ushort Method,
        byte[] Comment,
        byte[] Extra,
        int CreateSystem,
        uint ExternalAttributes,
        ulong UncompressedSize,
        ReadOnlyMemory<byte> CompressedPayload,
        uint Crc,
        Action<uint, ulong>? OnPublished);

    public static SerializedArchive SerializeArchive(
        IReadOnlyList<StagedEntry> staged,
        byte[] comment,
        bool allowZip64,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan? span)
        => SerializeCore([], staged, comment, allowZip64, context, span);

    public static SerializedArchive SerializeMerge(
        IReadOnlyList<PreservedEntry> preserved,
        IReadOnlyList<StagedEntry> staged,
        byte[] comment,
        bool allowZip64,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan? span)
        => SerializeCore(preserved, staged, comment, allowZip64, context, span);

    private static SerializedArchive SerializeCore(
        IReadOnlyList<PreservedEntry> preserved,
        IReadOnlyList<StagedEntry> staged,
        byte[] comment,
        bool allowZip64,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan? span)
    {
        if (comment.Length > MaxCommentLength)
        {
            throw new LythonRuntimeException("ValueError", "Archive comment is too long.", span);
        }

        RequireRecordFieldWidths(preserved, staged, span);

        var total = checked(preserved.Count + staged.Count);
        // Compressed payloads, directory bytes and layout structures stay live
        // through publication; charge them here so an unbounded entry meets the
        // budget before downstream builders allocate for it.
        using var transient = context.MemoryGovernor.ReserveTemporary(0, span);
        transient.Grow(256L * total, span);
        var layout = new List<LayoutEntry>(total);
        var results = new List<EntryResult>(total);
        var contentZip64 = new bool[total];
        for (var preservedIndex = 0; preservedIndex < preserved.Count; preservedIndex++)
        {
            if ((preservedIndex & 63) == 0)
            {
                context.CheckExecutionBudget(span);
            }

            var source = preserved[preservedIndex];
            // Preserved bytes bypass the allowZip64 writer gate: the flag
            // governs what Lython creates, not what it carries over. Stored
            // sizes still drive header promotion so merged output stays valid.
            var carried = source.CompressedPayload;
            results.Add(new EntryResult(source.Crc, (ulong)carried.Length));
            contentZip64[preservedIndex] =
                (double)source.UncompressedSize * 1.05 > Zip64Limit ||
                (double)carried.Length * 1.05 > Zip64Limit;
            layout.Add(new LayoutEntry(
                source.Name, source.NameBytes, source.Flags, source.DosTime, source.DosDate,
                source.Method, source.Comment, source.Extra, source.CreateSystem, source.ExternalAttributes,
                source.UncompressedSize, carried, source.Crc, null));
        }

        var index = 0;
        foreach (var entry in staged)
        {
            if ((index++ & 63) == 0)
            {
                context.CheckExecutionBudget(span);
            }

            var crc = Crc32.Compute(entry.Data, context, span);
            var payload = CompressEntry(entry, context, span);
            if (entry.Method != 0)
            {
                // STORED payloads are the staged bytes themselves, already
                // charged; only compressed copies need cover here.
                transient.Grow(payload.Length, span);
            }
            results.Add(new EntryResult(crc, (ulong)payload.Length));
            var needsZip64 = entry.ForceZip64 || entry.Data.Length * 1.05 > Zip64Limit || payload.Length * 1.05 > Zip64Limit;
            if (needsZip64 && !allowZip64)
            {
                throw new LythonRuntimeException(LythonRuntime.ModuleException("zipfile", "LargeZipFile"), "Filesize would require ZIP64 extensions", span);
            }

            contentZip64[preserved.Count + index - 1] = needsZip64;
            layout.Add(new LayoutEntry(
                entry.Name, entry.NameBytes, entry.Flags, entry.DosTime, entry.DosDate,
                entry.Method, entry.Comment, entry.Extra, entry.CreateSystem, entry.ExternalAttributes,
                (ulong)entry.Data.Length, payload, crc, entry.OnPublished));
        }

        // Layout pass: offsets decide ZIP64 promotion for far entries; each
        // pass only promotes, so the loop terminates.
        var entryZip64 = (bool[])contentZip64.Clone();
        var centralOffsets = new ulong[total];
        ulong directoryStart = 0;
        while (true)
        {
            var promoted = false;
            ulong offset = 0;
            for (var i = 0; i < total; i++)
            {
                if ((i & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }

                centralOffsets[i] = offset;
                if (offset > uint.MaxValue && !entryZip64[i])
                {
                    entryZip64[i] = true;
                    promoted = true;
                }

                offset = checked(offset + (ulong)LocalHeaderLength(layout[i], entryZip64[i]) + (ulong)layout[i].CompressedPayload.Length);
            }

            if (!promoted)
            {
                directoryStart = offset;
                break;
            }

            if (!allowZip64)
            {
                throw new LythonRuntimeException(LythonRuntime.ModuleException("zipfile", "LargeZipFile"), "Zipfile size would require ZIP64 extensions", span);
            }
        }

        var directory = new GovernedByteBuilder(context.MemoryGovernor, span);
        try
        {
            for (var i = 0; i < total; i++)
            {
                if ((i & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }

                AppendCentralEntry(directory, layout[i], results[i], centralOffsets[i], entryZip64[i] ? MergeZip64Extra(layout[i].Extra, layout[i].UncompressedSize, results[i].CompressedSize, centralOffsets[i], span) : layout[i].Extra, entryZip64[i]);
            }

            var directoryBytes = directory.ToArrayAndRelease();
            transient.Grow(directoryBytes.Length, span);
            var useZip64 =
                (ulong)total >= (ulong)Zip64CountLimit ||
                (ulong)directoryBytes.Length > uint.MaxValue ||
                directoryStart > uint.MaxValue;
            if (useZip64 && !allowZip64)
            {
                if ((ulong)total >= (ulong)Zip64CountLimit)
                {
                    throw new LythonRuntimeException(LythonRuntime.ModuleException("zipfile", "LargeZipFile"), "Files count would require ZIP64 extensions", span);
                }

                throw new LythonRuntimeException(LythonRuntime.ModuleException("zipfile", "LargeZipFile"), "Central directory size would require ZIP64 extensions", span);
            }

            var output = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                for (var i = 0; i < total; i++)
                {
                    if ((i & 63) == 0)
                    {
                        context.CheckExecutionBudget(span);
                    }

                    output.Append(LocalHeader(layout[i], results[i].Crc, layout[i].CompressedPayload.Length, entryZip64[i] ? MergeZip64Extra(layout[i].Extra, layout[i].UncompressedSize, (ulong)layout[i].CompressedPayload.Length, centralOffsets[i], span) : [], entryZip64[i], centralOffsets[i]));
                    output.Append(layout[i].CompressedPayload.Span);
                }

                output.Append(directoryBytes);
                if (useZip64)
                {
                    WriteZip64End(output, (ulong)total, (ulong)directoryBytes.Length, directoryStart);
                }

                WriteEndRecord(output, total, directoryBytes.Length, directoryStart, comment, useZip64);
                return new SerializedArchive(output.ToArrayAndRelease(), results);
            }
            catch
            {
                output.Release();
                throw;
            }
        }
        finally
        {
            directory.Release();
        }
    }

    private const int MaxCommentLength = 65535;
    private const int MaxFieldLength = 65535;
    internal const int Zip64ExtraLength = 28;

    private static void RequireRecordFieldWidths(
        IReadOnlyList<PreservedEntry> preserved,
        IReadOnlyList<StagedEntry> staged,
        LythonSourceSpan? span)
    {
        for (var i = 0; i < preserved.Count; i++)
        {
            RequireRecordFieldWidths(preserved[i].NameBytes, preserved[i].Comment, preserved[i].Extra, span);
        }

        for (var i = 0; i < staged.Count; i++)
        {
            RequireRecordFieldWidths(staged[i].NameBytes, staged[i].Comment, staged[i].Extra, span);
        }
    }

    internal static void RequireRecordFieldWidths(byte[] nameBytes, byte[] comment, byte[] extra, LythonSourceSpan? span)
    {
        if (nameBytes.Length > MaxFieldLength)
        {
            throw new LythonRuntimeException("ValueError", "ZipInfo filename is too long.", span);
        }

        if (comment.Length > MaxFieldLength)
        {
            throw new LythonRuntimeException("ValueError", "ZipInfo comment is too long.", span);
        }

        if (extra.Length > MaxFieldLength)
        {
            throw new LythonRuntimeException("ValueError", "ZipInfo extra field is too long.", span);
        }
    }

    // R38: ZIP64 promotion merges a fresh 0x0001 field with the unrelated fields
    // already carried, replacing any stale ZIP64 field; a truncated tail is dropped
    // and the merged encoding must still fit the 16-bit length.
    internal static byte[] MergeZip64Extra(byte[] existing, ulong uncompressedSize, ulong compressedSize, ulong headerOffset, LythonSourceSpan? span)
    {
        if (MergedZip64ExtraLength(existing) > MaxFieldLength)
        {
            throw new LythonRuntimeException("ValueError", "ZipInfo extra field is too long.", span);
        }

        var merged = new byte[MergedZip64ExtraLength(existing)];
        var position = 0;
        var offset = 0;
        while (offset + 4 <= existing.Length)
        {
            var tag = BinaryPrimitives.ReadUInt16LittleEndian(existing.AsSpan(offset, 2));
            var size = BinaryPrimitives.ReadUInt16LittleEndian(existing.AsSpan(offset + 2, 2));
            if (offset + 4 + size > existing.Length)
            {
                break;
            }

            if (tag != 0x0001)
            {
                existing.AsSpan(offset, 4 + size).CopyTo(merged.AsSpan(position));
                position += 4 + size;
            }

            offset += 4 + size;
        }

        Zip64EntryExtra(uncompressedSize, compressedSize, headerOffset).CopyTo(merged.AsSpan(position));
        return merged;
    }

    private static int LocalHeaderLength(LayoutEntry entry, bool zip64)
        => 30 + entry.NameBytes.Length + (zip64 ? MergedZip64ExtraLength(entry.Extra) : 0);

    // Length half of the ZIP64 extra merge, for layout before offsets exist.
    internal static int MergedZip64ExtraLength(byte[] existing)
    {
        var keptLength = 0;
        var offset = 0;
        while (offset + 4 <= existing.Length)
        {
            var tag = BinaryPrimitives.ReadUInt16LittleEndian(existing.AsSpan(offset, 2));
            var size = BinaryPrimitives.ReadUInt16LittleEndian(existing.AsSpan(offset + 2, 2));
            if (offset + 4 + size > existing.Length)
            {
                break;
            }

            if (tag != 0x0001)
            {
                keptLength += 4 + size;
            }

            offset += 4 + size;
        }

        return keptLength + Zip64ExtraLength;
    }

    private static byte[] CompressEntry(
        StagedEntry entry,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan? span)
    {
        if (entry.Method == 0)
        {
            return entry.Data;
        }

        var output = new GovernedByteBuilder(context.MemoryGovernor, span);
        try
        {
            // Disposal finalizes the raw DEFLATE stream: the final block is
            // written to the governed builder before the snapshot is taken,
            // so the returned payload is complete and no fresh charge leaks.
            using (var stream = new DeflateStream(
                new LythonRuntime.GzipBufferWriteStream(output),
                new ZLibCompressionOptions { CompressionLevel = entry.Level },
                leaveOpen: true))
            {
                stream.Write(entry.Data, 0, entry.Data.Length);
            }

            if (output.Length == 0)
            {
                // BCL emits no bytes at all for empty input, not even a final
                // block. Spell the canonical empty final fixed block instead,
                // byte-identical to what zlib and CPython emit, so empty members
                // stay interoperable and strictly complete.
                output.Append(new byte[] { 3, 0 });
            }

            return output.ToArrayAndRelease();
        }
        catch
        {
            output.Release();
            throw;
        }
    }

    private static byte[] LocalHeader(
        LayoutEntry entry,
        uint crc,
        int compressedLength,
        byte[] extra,
        bool zip64,
        ulong headerOffset)
    {
        var header = new byte[30];
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0, 4), 0x04034B50);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4, 2), (ushort)(zip64 ? Zip64Version : 20));
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(6, 2), entry.Flags);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(8, 2), entry.Method);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(10, 2), entry.DosTime);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(12, 2), entry.DosDate);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(14, 4), crc);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(18, 4), zip64 ? uint.MaxValue : (uint)compressedLength);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(22, 4), zip64 ? uint.MaxValue : (uint)entry.UncompressedSize);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(26, 2), (ushort)entry.NameBytes.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(28, 2), (ushort)extra.Length);
        var result = new byte[header.Length + entry.NameBytes.Length + extra.Length];
        header.CopyTo(result, 0);
        entry.NameBytes.CopyTo(result, header.Length);
        extra.CopyTo(result, header.Length + entry.NameBytes.Length);
        return result;
    }

    private static void AppendCentralEntry(
        GovernedByteBuilder directory,
        LayoutEntry entry,
        EntryResult result,
        ulong headerOffset,
        byte[] extra,
        bool zip64)
    {
        var header = new byte[46];
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0, 4), 0x02014B50);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4, 2), (ushort)(entry.CreateSystem << 8 | (zip64 ? Zip64Version : 20)));
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(6, 2), (ushort)(zip64 ? Zip64Version : 20));
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(8, 2), entry.Flags);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(10, 2), entry.Method);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(12, 2), entry.DosTime);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(14, 2), entry.DosDate);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16, 4), result.Crc);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20, 4), zip64 ? uint.MaxValue : (uint)result.CompressedSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(24, 4), zip64 ? uint.MaxValue : (uint)entry.UncompressedSize);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(28, 2), (ushort)entry.NameBytes.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(30, 2), (ushort)extra.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(32, 2), (ushort)entry.Comment.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(34, 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(36, 2), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(38, 4), entry.ExternalAttributes);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(42, 4), zip64 ? uint.MaxValue : (uint)headerOffset);
        directory.Append(header);
        directory.Append(entry.NameBytes);
        directory.Append(extra);
        directory.Append(entry.Comment);
    }

    private static byte[] Zip64EntryExtra(ulong uncompressedSize, ulong compressedSize, ulong headerOffset)
    {
        var extra = new byte[28];
        BinaryPrimitives.WriteUInt16LittleEndian(extra.AsSpan(0, 2), 0x0001);
        BinaryPrimitives.WriteUInt16LittleEndian(extra.AsSpan(2, 2), 24);
        BinaryPrimitives.WriteUInt64LittleEndian(extra.AsSpan(4, 8), uncompressedSize);
        BinaryPrimitives.WriteUInt64LittleEndian(extra.AsSpan(12, 8), compressedSize);
        BinaryPrimitives.WriteUInt64LittleEndian(extra.AsSpan(20, 8), headerOffset);
        return extra;
    }

    private static void WriteZip64End(GovernedByteBuilder output, ulong entryCount, ulong directorySize, ulong directoryOffset)
    {
        var record = new byte[56];
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(0, 4), 0x06064B50);
        BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(4, 8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(12, 2), 45);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(14, 2), 45);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(16, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(20, 4), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(24, 8), entryCount);
        BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(32, 8), entryCount);
        BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(40, 8), directorySize);
        BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(48, 8), directoryOffset);
        output.Append(record);
        var locator = new byte[20];
        var eocd64Offset = directoryOffset + directorySize;
        BinaryPrimitives.WriteUInt32LittleEndian(locator.AsSpan(0, 4), 0x07064B50);
        BinaryPrimitives.WriteUInt32LittleEndian(locator.AsSpan(4, 4), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(locator.AsSpan(8, 8), (ulong)eocd64Offset);
        BinaryPrimitives.WriteUInt32LittleEndian(locator.AsSpan(16, 4), 1);
        output.Append(locator);
    }

    private static void WriteEndRecord(GovernedByteBuilder output, int entryCount, int directorySize, ulong directoryOffset, byte[] comment, bool zip64)
    {
        var record = new byte[22];
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(0, 4), 0x06054B50);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(4, 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(6, 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(8, 2), zip64 ? ushort.MaxValue : (ushort)entryCount);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(10, 2), zip64 ? ushort.MaxValue : (ushort)entryCount);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(12, 4), zip64 ? uint.MaxValue : (uint)directorySize);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(16, 4), zip64 ? uint.MaxValue : (uint)directoryOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(20, 2), (ushort)comment.Length);
        output.Append(record);
        output.Append(comment);
    }
}
