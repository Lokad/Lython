using System.Buffers.Binary;

namespace Lokad.Lython.Runtime.Zip;

internal static partial class ZipDirectoryReader
{
    public static ZipArchiveDirectory Read(ReadOnlyMemory<byte> data, bool forceUtf8Names,
        LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
        => ReadAsync(new ZipReadSource(data), forceUtf8Names, context, span, false).GetAwaiter().GetResult();

    // Both storage paths use these same record parsers and eager local validation.
    internal static async ValueTask<ZipArchiveDirectory> ReadAsync(ZipReadSource source, bool forceUtf8Names,
        LythonRuntime.ExecutionContext context, LythonSourceSpan? span, bool asynchronous)
    {
        var tailSize = (int)Math.Min(source.Length, EndRecordMinimumLength + MaxCommentLength + 20UL);
        var tailOffset = source.Length - (ulong)tailSize;
        using var tail = await source.ReadAsync(tailOffset, tailSize, context, span, asynchronous).ConfigureAwait(false);
        var endOffset = FindEndRecordOffset(tail.Memory.Span, context, span);
        var endRecord = tail.Memory[endOffset..];
        var absoluteEnd = checked(tailOffset + (ulong)endOffset);
        var zip64 = RequiresZip64(endRecord.Span);
        using var wide = zip64
            ? await ReadZip64Async(source, absoluteEnd, context, span, asynchronous).ConfigureAwait(false)
            : default;
        var commentCharge = PyBytes.EstimateApproximateBytes(BinaryPrimitives.ReadUInt16LittleEndian(endRecord.Span[20..]));
        using var commentStorage = context.MemoryGovernor.ReserveTemporary(commentCharge, span);
        var end = ParseEndRecord(endRecord.Span, wide.Memory.Span);
        var metadataCharge = 0L;
        try
        {
            commentStorage.Commit(commentCharge);
            metadataCharge = commentCharge;
            if (end.EntryCount > int.MaxValue) throw Malformed("central directory entry count is not representable");
            if (end.DirectoryOffset > source.Length || end.DirectorySize > source.Length - end.DirectoryOffset)
                throw Malformed("central directory extends beyond the end of data");
            if (end.DirectorySize > (ulong)Array.MaxLength) throw Malformed("central directory size is not representable");
            using var central = await source.ReadAsync(end.DirectoryOffset, (int)end.DirectorySize,
                context, span, asynchronous).ConfigureAwait(false);
            var entries = new List<ZipDirectoryEntry>();
            var offset = 0UL;
            while ((ulong)entries.Count < end.EntryCount)
            {
                context.CheckExecution(span);
                var entry = ReadCentralEntry(central.Memory.Span, ref offset, end.DirectorySize,
                    entries.Count, context, span, ref metadataCharge);
                using var local = await ReadLocalHeaderAsync(source, entry.HeaderOffset, context, span, asynchronous).ConfigureAwait(false);
                var dataOffset = ValidateLocalHeader(local.Memory.Span, entry.HeaderOffset, source.Length,
                    entry.CompressionMethod, entry.GeneralPurposeFlags, entry.NameBytes,
                    entry.CompressedSize, entry.UncompressedSize, entry.DirectoryOrdinal);
                entries.Add(entry with
                {
                    DataOffset = dataOffset,
                    Name = DecodeEntryName(entry.NameBytes, forceUtf8Names || entry.Utf8Flag, entry.DirectoryOrdinal)
                });
            }
            if (offset != end.DirectorySize) throw Malformed("central directory has trailing bytes");
            SetDataEndLimits(entries, end.DirectoryOffset, context, span);
            return new ZipArchiveDirectory(entries, end.Comment, end.IsZip64, metadataCharge);
        }
        catch { context.MemoryGovernor.Release(metadataCharge); throw; }
    }

    private readonly record struct EndRecord(ulong DirectoryOffset, ulong DirectorySize, ulong EntryCount, byte[] Comment, bool IsZip64);

    private static void SetDataEndLimits(List<ZipDirectoryEntry> entries, ulong directoryOffset,
        LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
    {
        // Central-directory order need not be physical file order. CPython uses
        // descending header offsets, stable on ties; preserve central ordinals.
        using var scratch = context.MemoryGovernor.ReserveTemporary(checked(32L + 4L * entries.Count), span);
        var order = new int[entries.Count];
        for (var index = 0; index < order.Length; index++)
        {
            if ((index & (CancellationCheckInterval - 1)) == 0) context.CheckExecution(span);
            order[index] = index;
        }
        try
        {
            Array.Sort(order, (left, right) =>
            {
                context.CheckExecution(span);
                var compared = entries[right].HeaderOffset.CompareTo(entries[left].HeaderOffset);
                return compared != 0 ? compared : left.CompareTo(right);
            });
        }
        catch (InvalidOperationException error) when (error.InnerException is not null)
        {
            // Array.Sort wraps comparer failures. Only execution checks can fail
            // here; preserve cancellation and the original terminal admission.
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
        var limit = directoryOffset;
        foreach (var index in order)
        {
            context.CheckExecution(span);
            var entry = entries[index];
            entries[index] = entry with { DataEndLimit = limit };
            limit = entry.HeaderOffset;
        }
    }

    private static int FindEndRecordOffset(ReadOnlySpan<byte> bytes, LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
    {
        var window = Math.Min(bytes.Length, EndRecordMinimumLength + MaxCommentLength);
        var scanned = 0;
        for (var offset = bytes.Length - EndRecordMinimumLength; offset >= bytes.Length - window; offset--)
        {
            if ((++scanned & (CancellationScanBytes - 1)) == 0) context.CheckExecution(span);
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4)) != EndOfCentralDirectorySignature) continue;
            var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset + 20, 2));
            if ((long)offset + EndRecordMinimumLength + count <= bytes.Length) return offset;
        }
        throw Malformed("missing end-of-central-directory record");
    }

    private static bool RequiresZip64(ReadOnlySpan<byte> record)
        => BinaryPrimitives.ReadUInt16LittleEndian(record[8..]) == ushort.MaxValue
        || BinaryPrimitives.ReadUInt16LittleEndian(record[10..]) == ushort.MaxValue
        || BinaryPrimitives.ReadUInt32LittleEndian(record[12..]) == uint.MaxValue
        || BinaryPrimitives.ReadUInt32LittleEndian(record[16..]) == uint.MaxValue;

    private static async ValueTask<ZipReadSource.Region> ReadZip64Async(ZipReadSource source, ulong end,
        LythonRuntime.ExecutionContext context, LythonSourceSpan? span, bool asynchronous)
    {
        if (end < 20) throw Malformed("ZIP64 locator extends beyond the start of data");
        using var locator = await source.ReadAsync(end - 20, 20, context, span, asynchronous).ConfigureAwait(false);
        var offset = ParseZip64Locator(locator.Memory.Span);
        if (offset > source.Length || source.Length - offset < 56)
            throw Malformed("ZIP64 end record extends beyond the end of data");
        var record = await source.ReadAsync(offset, 56, context, span, asynchronous).ConfigureAwait(false);
        try
        {
            ValidateZip64Extent(record.Memory.Span, offset, end - 20, source.Length);
            return record;
        }
        catch { record.Dispose(); throw; }
    }

    private static ulong ParseZip64Locator(ReadOnlySpan<byte> bytes)
    {
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != Zip64LocatorSignature)
            throw Malformed("ZIP64 end record is missing its locator");
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]) != 0 || BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]) != 1)
            throw Malformed("multi-disk archives are unsupported");
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..]);
    }

    private static void ValidateZip64Extent(ReadOnlySpan<byte> record, ulong offset, ulong locator, ulong length)
    {
        if (BinaryPrimitives.ReadUInt32LittleEndian(record) != Zip64EndSignature)
            throw Malformed("ZIP64 locator points at a non-directory record");
        var size = BinaryPrimitives.ReadUInt64LittleEndian(record[4..]);
        if (size < 44 || size > length - offset - 12) throw Malformed("ZIP64 end record has an impossible size");
        if (offset + 12 + size != locator) throw Malformed("ZIP64 end record does not abut its locator");
    }

    private static EndRecord ParseEndRecord(ReadOnlySpan<byte> record, ReadOnlySpan<byte> wide)
    {
        if (BinaryPrimitives.ReadUInt16LittleEndian(record[4..]) != 0 || BinaryPrimitives.ReadUInt16LittleEndian(record[6..]) != 0)
            throw Malformed("multi-disk archives are unsupported");
        var diskEntries = BinaryPrimitives.ReadUInt16LittleEndian(record[8..]);
        var totalEntries = BinaryPrimitives.ReadUInt16LittleEndian(record[10..]);
        if (diskEntries != totalEntries) throw Malformed("end record entry counts disagree");
        var size = (ulong)BinaryPrimitives.ReadUInt32LittleEndian(record[12..]);
        var offset = (ulong)BinaryPrimitives.ReadUInt32LittleEndian(record[16..]);
        var count = (ulong)totalEntries;
        var zip64 = RequiresZip64(record);
        if (zip64)
        {
            var diskCount = BinaryPrimitives.ReadUInt64LittleEndian(wide[24..]);
            count = BinaryPrimitives.ReadUInt64LittleEndian(wide[32..]);
            if (diskCount != count) throw Malformed("ZIP64 end record entry counts disagree");
            if (totalEntries != ushort.MaxValue && count != totalEntries) throw Malformed("ZIP64 and end record entry counts disagree");
            size = BinaryPrimitives.ReadUInt64LittleEndian(wide[40..]);
            offset = BinaryPrimitives.ReadUInt64LittleEndian(wide[48..]);
        }
        var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(record[20..]);
        return new(offset, size, count, record.Slice(EndRecordMinimumLength, commentLength).ToArray(), zip64);
    }

    internal static async ValueTask<ZipReadSource.Region> ReadLocalHeaderAsync(ZipReadSource source, ulong offset,
        LythonRuntime.ExecutionContext context, LythonSourceSpan? span, bool asynchronous)
    {
        if (offset > source.Length || source.Length - offset < 30) throw Malformed("local header extends beyond the end of data");
        using var header = await source.ReadAsync(offset, 30, context, span, asynchronous).ConfigureAwait(false);
        var length = LocalHeaderLength(header.Memory.Span);
        return await source.ReadAsync(offset, length, context, span, asynchronous).ConfigureAwait(false);
    }

    private static int LocalHeaderLength(ReadOnlySpan<byte> header)
    {
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != LocalHeaderSignature)
            throw Malformed("local header has a bad signature");
        return checked(30 + BinaryPrimitives.ReadUInt16LittleEndian(header[26..]) + BinaryPrimitives.ReadUInt16LittleEndian(header[28..]));
    }
}
