using System.Threading;
using System.Text;
using System.Runtime.InteropServices;

namespace Lokad.Lython.Runtime.Text;

internal sealed class PyString : IEquatable<PyString>, IPyTruthyValue, IPyIndexableValue, IPySliceableValue, IPyIterableValue, IPyRenderableValue, IPyHashableValue, IPyGovernedValue, IPySizedValue, IPyOwnershipSnapshot
{

    private readonly record struct RuneByteRange(int Start, int Length);

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly PyString[] AsciiCharacters = CreateAsciiCharacters();

    private readonly byte[] _utf8;
    private readonly int _byteLength;
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly LythonSourceSpan? _allocationSpan;
    private int _runeLength = -1;
    private int[]? _runeByteOffsets;
    private int _hashCode;
    private bool _hashCodeComputed;

    private PyString(byte[] utf8) : this(utf8, utf8.Length) { }

    private PyString(byte[] utf8, int byteLength, MemoryGovernor? governor = null, LythonSourceSpan? allocationSpan = null)
    {
        _utf8 = utf8;
        _byteLength = byteLength;
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
    }

    private PyString(byte[] utf8, MemoryGovernor governor, LythonSourceSpan? allocationSpan, bool prefunded = false)
        : this(utf8, utf8.Length, governor, allocationSpan)
    {
        if (!prefunded)
        {
            var bytes = EstimateApproximateBytes(utf8.Length);
            governor.Reserve(bytes, allocationSpan);
            governor.Commit(bytes);
        }
    }

    public static PyString Empty { get; } = new([]);

    public int Length
    {
        get
        {
            return _runeLength >= 0 ? _runeLength : (_runeLength = CountRunes(Utf8Span));

            static int CountRunes(ReadOnlySpan<byte> utf8)
            {
                var count = 0;
                for (var i = 0; i < utf8.Length;)
                {
                    i += GetRuneLength(utf8[i]);
                    count++;
                }

                return count;
            }
        }
    }

    /// <summary>
    /// Counts the UTF-16 code units the decoded CLR string needs. Astral runes
    /// (4-byte UTF-8 sequences) decode to a surrogate pair, so this exceeds
    /// <see cref="Length"/> whenever astral characters are present. Projection
    /// budgets must use this width: rune counts undercharge astral payloads.
    /// </summary>
    public int GetUtf16CodeUnitCount()
    {
        var units = 0;
        var bytes = Utf8Span;
        for (var i = 0; i < bytes.Length;)
        {
            var lead = bytes[i];
            if (lead < 0x80)
            {
                units += 1;
                i += 1;
            }
            else if (lead < 0xE0)
            {
                units += 1;
                i += 2;
            }
            else if (lead < 0xF0)
            {
                units += 1;
                i += 3;
            }
            else
            {
                units += 2;
                i += 4;
            }
        }

        return units;
    }

    private ReadOnlySpan<byte> Utf8Span => _utf8.AsSpan(0, _byteLength);

    public ReadOnlyMemory<byte> Utf8Bytes => _utf8.AsMemory(0, _byteLength);

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes) =>
        OwnershipSnapshot.Owned(OwnerMemoryGovernor, CommittedOwnedBytes, out chargeBytes);

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    internal static long EstimateApproximateBytes(int utf8Length) => 128L + utf8Length;

    public bool IsTruthy() => Length != 0;

    public static PyString FromString(string text)
    {
        return new PyString(Utf8.GetBytes(text));
    }

    public static PyString FromString(string text, MemoryGovernor governor)
        => FromString(text, governor, null);

    public static PyString FromString(string text, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        if (text.Length == 0)
        {
            return Empty;
        }

        var byteCount = Utf8.GetByteCount(text);
        var bytes = AllocateGovernedUtf8(byteCount, governor, allocationSpan);
        _ = Utf8.GetBytes(text.AsSpan(), bytes.AsSpan());
        return new PyString(bytes, governor, allocationSpan, prefunded: true);
    }

    internal static PyString FromOwnedUtf8(byte[] utf8)
    {
        return utf8.Length == 0 ? Empty : new PyString(utf8);
    }

    internal static PyString FromOwnedUtf8(byte[] utf8, MemoryGovernor governor)
        => FromOwnedUtf8(utf8, governor, null);

    internal static PyString FromOwnedUtf8(byte[] utf8, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        return utf8.Length == 0 ? Empty : new PyString(utf8, governor, allocationSpan);
    }

    internal static PyString FromOwnedUtf8(byte[] utf8, MemoryGovernor governor, LythonSourceSpan? allocationSpan,
        MemoryGovernor.TemporaryMemoryReservation storage, int? byteLength = null)
    {
        var length = byteLength ?? utf8.Length;
        if ((uint)length > (uint)utf8.Length) throw new ArgumentOutOfRangeException(nameof(byteLength));
        if (length == 0) return Empty;
        var result = new PyString(utf8, length, governor, allocationSpan);
        storage.Commit(EstimateApproximateBytes(utf8.Length));
        return result;
    }

    // Consume one fixed construction admission. The caller keeps its exact
    // reserved-byte balance until this method succeeds; no buffer escapes early.
    internal static PyString FromReservedUtf8(byte[] utf8, int byteLength,
        MemoryGovernor? governor, LythonSourceSpan? allocationSpan)
    {
        if ((uint)byteLength > (uint)utf8.Length) throw new ArgumentOutOfRangeException(nameof(byteLength));
        var charge = EstimateApproximateBytes(utf8.Length);
        if (byteLength == 0)
        {
            governor?.ReleaseReserved(charge);
            return Empty;
        }
        var result = new PyString(utf8, byteLength, governor, allocationSpan);
        governor?.Commit(charge);
        return result;
    }

    // The caller relinquishes this backing only after success. Its full capacity
    // is already committed to the same owner; only new string metadata is admitted.
    internal static PyString FromTransferredUtf8(byte[] utf8, int byteLength,
        MemoryGovernor? governor, LythonSourceSpan? allocationSpan)
    {
        if (byteLength <= 0 || byteLength > utf8.Length)
            throw new ArgumentOutOfRangeException(nameof(byteLength));
        var metadataBytes = EstimateApproximateBytes(0);
        governor?.Reserve(metadataBytes, allocationSpan);
        try
        {
            var result = new PyString(utf8, byteLength, governor, allocationSpan);
            governor?.Commit(metadataBytes);
            return result;
        }
        catch
        {
            governor?.ReleaseReserved(metadataBytes);
            throw;
        }
    }

    public static PyString FromUtf8(ReadOnlyMemory<byte> utf8)
    {
        if (utf8.Length == 0)
        {
            return Empty;
        }

        return TryGetOwnedArray(utf8, out var owned)
            ? new PyString(owned)
            : new PyString(utf8.ToArray());
    }

    public static PyString FromUtf8(ReadOnlyMemory<byte> utf8, MemoryGovernor governor)
        => FromUtf8(utf8, governor, null);

    public static PyString FromUtf8(ReadOnlyMemory<byte> utf8, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        if (utf8.Length == 0)
        {
            return Empty;
        }

        if (TryGetOwnedArray(utf8, out var owned))
        {
            return new PyString(owned, governor, allocationSpan);
        }

        return CopyFromUtf8(utf8.Span, governor, allocationSpan);
    }

    internal static PyString CopyFromUtf8(ReadOnlySpan<byte> utf8, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        if (utf8.IsEmpty) return Empty;
        var copy = AllocateGovernedUtf8(utf8.Length, governor, allocationSpan);
        utf8.CopyTo(copy);
        return new PyString(copy, governor, allocationSpan, prefunded: true);
    }

    public PyString Concat(PyString other)
    {
        var governor = _memoryGovernor ?? other._memoryGovernor;
        var allocationSpan = _allocationSpan ?? other._allocationSpan;
        return ConcatCore(other, governor, allocationSpan);
    }

    public PyString Concat(PyString other, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        => ConcatCore(other, governor, allocationSpan);

    private PyString ConcatCore(PyString other, MemoryGovernor? governor, LythonSourceSpan? allocationSpan)
    {
        var resultLength = CheckedByteLength(
            RuntimeMemoryEstimates.SaturatingAdd(_byteLength, other._byteLength),
            allocationSpan);
        var result = governor is null
            ? new byte[resultLength]
            : AllocateGovernedUtf8(resultLength, governor, allocationSpan);
        Buffer.BlockCopy(_utf8, 0, result, 0, _byteLength);
        Buffer.BlockCopy(other._utf8, 0, result, _byteLength, other._byteLength);
        return governor is null ? new PyString(result) : new PyString(result, governor, allocationSpan, prefunded: true);
    }

    public PyString Repeat(int count)
    {
        return RepeatCore(count, _memoryGovernor, _allocationSpan);
    }

    public PyString Repeat(int count, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        => RepeatCore(count, governor, allocationSpan);

    private PyString RepeatCore(int count, MemoryGovernor? governor, LythonSourceSpan? allocationSpan)
    {
        if (count <= 0 || _byteLength == 0)
        {
            return Empty;
        }

        var resultLength = CheckedByteLength(
            RuntimeMemoryEstimates.SaturatingMultiply(_byteLength, count),
            allocationSpan);
        var result = governor is null
            ? new byte[resultLength]
            : AllocateGovernedUtf8(resultLength, governor, allocationSpan);
        for (var i = 0; i < count; i++)
        {
            Buffer.BlockCopy(_utf8, 0, result, i * _byteLength, _byteLength);
        }

        return governor is null ? new PyString(result) : new PyString(result, governor, allocationSpan, prefunded: true);
    }

    public PyString Slice(IReadOnlyList<int> runeIndices)
    {
        if (runeIndices.Count == 0)
        {
            return Empty;
        }

        var totalBytes = 0L;
        foreach (var runeIndex in runeIndices)
        {
            totalBytes += GetRuneByteRange(runeIndex).Length;
        }

        var resultLength = CheckedByteLength(totalBytes, _allocationSpan);
        var result = _memoryGovernor is null
            ? new byte[resultLength]
            : AllocateGovernedUtf8(resultLength, _memoryGovernor, _allocationSpan);
        var offset = 0;
        foreach (var runeIndex in runeIndices)
        {
            var (start, length) = GetRuneByteRange(runeIndex);
            Buffer.BlockCopy(_utf8, start, result, offset, length);
            offset += length;
        }

        return _memoryGovernor is null ? new PyString(result) : new PyString(result, _memoryGovernor, _allocationSpan, prefunded: true);
    }

    public PyString Slice(PyIndexing.SliceBounds bounds)
    {
        var count = bounds.Count;
        if (count == 0)
        {
            return Empty;
        }

        if (bounds.Step == 1)
        {
            return SliceByByteRange(
                GetByteIndexForRuneBoundary(bounds.Start),
                GetByteIndexForRuneBoundary(bounds.End));
        }

        long totalBytes = 0;
        for (long runeIndex = bounds.Start; bounds.Step > 0 ? runeIndex < bounds.End : runeIndex > bounds.End; runeIndex += bounds.Step)
        {
            totalBytes += GetRuneByteRange((int)runeIndex).Length;
        }

        var resultLength = CheckedByteLength(totalBytes, _allocationSpan);
        var result = _memoryGovernor is null
            ? new byte[resultLength]
            : AllocateGovernedUtf8(resultLength, _memoryGovernor, _allocationSpan);
        var offset = 0;
        for (long runeIndex = bounds.Start; bounds.Step > 0 ? runeIndex < bounds.End : runeIndex > bounds.End; runeIndex += bounds.Step)
        {
            var (start, length) = GetRuneByteRange((int)runeIndex);
            Buffer.BlockCopy(_utf8, start, result, offset, length);
            offset += length;
        }

        return _memoryGovernor is null ? new PyString(result) : new PyString(result, _memoryGovernor, _allocationSpan, prefunded: true);
    }

    public PyString Index(int runeIndex)
    {
        var (start, length) = GetRuneByteRange(runeIndex);
        if (length == 1 && _utf8[start] < 0x80)
        {
            return AsciiCharacters[_utf8[start]];
        }

        var bytes = _memoryGovernor is null
            ? new byte[length]
            : AllocateGovernedUtf8(length, _memoryGovernor, _allocationSpan);
        Buffer.BlockCopy(_utf8, start, bytes, 0, length);
        return _memoryGovernor is null ? new PyString(bytes) : new PyString(bytes, _memoryGovernor, _allocationSpan, prefunded: true);
    }

    public IEnumerable<PyString> EnumerateRunes()
    {
        for (var byteIndex = 0; byteIndex < _byteLength;)
        {
            var runeLength = GetRuneLengthAtByteIndex(byteIndex);
            if (runeLength == 1)
            {
                yield return AsciiCharacters[_utf8[byteIndex]];
                byteIndex++;
                continue;
            }

            var bytes = _memoryGovernor is null
                ? new byte[runeLength]
                : AllocateGovernedUtf8(runeLength, _memoryGovernor, _allocationSpan);
            Buffer.BlockCopy(_utf8, byteIndex, bytes, 0, runeLength);
            yield return _memoryGovernor is null ? new PyString(bytes) : new PyString(bytes, _memoryGovernor, _allocationSpan, prefunded: true);
            byteIndex += runeLength;
        }
    }

    public string AsString()
    {
        // Decodes stay transient: callers overwhelmingly read once (parsing,
        // rendering, host writes) while equality and hashing work on UTF-8
        // bytes, so retaining a UTF-16 copy per string would pin megabytes
        // behind long-lived values for no hot-path gain. Persistent rune and
        // offset caches below keep their own charges.
        return Utf8.GetString(Utf8Span);
    }

    // Borrowed bounded working storage uses the same strict decoder as AsString.
    internal int CopyUtf16To(Span<char> destination) => Utf8.GetChars(Utf8Span, destination);

    internal long CommittedOwnedBytes => EstimateApproximateBytes(_utf8.Length)
        + (_memoryGovernor is not null && _runeByteOffsets is { } offsets ? 32L + 4L * offsets.Length : 0);

    public string AsString(int runeIndex)
    {
        var (start, length) = GetRuneByteRange(runeIndex);
        return Utf8.GetString(_utf8, start, length);
    }

    public bool StartsWith(PyString prefix)
    {
        var prefixBytes = prefix.Utf8Span;
        return prefixBytes.Length <= _byteLength &&
            Utf8Span[..prefixBytes.Length].SequenceEqual(prefixBytes);
    }

    public bool EndsWith(PyString suffix)
    {
        var suffixBytes = suffix.Utf8Span;
        return suffixBytes.Length <= _byteLength &&
            Utf8Span[^suffixBytes.Length..].SequenceEqual(suffixBytes);
    }

    public bool Contains(PyString part) => IndexOfBytes(Utf8Span, part.Utf8Span) >= 0;

    public int Find(PyString part)
    {
        var byteIndex = IndexOfBytes(Utf8Span, part.Utf8Span);
        return byteIndex < 0 ? -1 : ByteIndexToRuneIndex(byteIndex);
    }

    public int Count(PyString part)
    {
        if (part.Length == 0)
        {
            return Length + 1;
        }

        var count = 0;
        var offset = 0;
        while (true)
        {
            var found = IndexOfBytes(Utf8Span[offset..], part.Utf8Span);
            if (found < 0)
            {
                return count;
            }

            count++;
            offset += found + part._byteLength;
        }
    }

    public PyString Replace(PyString oldValue, PyString newValue, int count = -1,
        LythonRuntime.ExecutionContext? context = null, LythonSourceSpan? span = null)
    {
        if (count == 0) return this;
        context ??= PyStructuralGuard.AmbientContext;
        span ??= PyStructuralGuard.AmbientSpan ?? _allocationSpan;
        var governor = context?.MemoryGovernor ?? _memoryGovernor;
        context?.CheckExecution(span);
        var remaining = count < 0 ? long.MaxValue : count;
        if (oldValue._byteLength == 0)
        {
            long insertionCount = 1;
            var prefixEnd = 0;
            while (prefixEnd < _byteLength && insertionCount < remaining)
            {
                context?.CheckExecution(span);
                prefixEnd += GetRuneLengthAtByteIndex(prefixEnd);
                insertionCount++;
            }
            if (prefixEnd == _byteLength) _runeLength = (int)(insertionCount - 1);
            var capacity = CheckedByteLength(
                RuntimeMemoryEstimates.SaturatingAdd(
                    _byteLength,
                    RuntimeMemoryEstimates.SaturatingMultiply(insertionCount, newValue._byteLength)),
                span);
            var builder = governor is null
                ? new GovernedByteBuilder(capacity)
                : new GovernedByteBuilder(governor, span, capacity);
            try
            {
                builder.Append(newValue);
                for (var byteIndex = 0; byteIndex < prefixEnd;)
                {
                    context?.CheckExecution(span);
                    var runeLength = GetRuneLengthAtByteIndex(byteIndex);
                    builder.Append(_utf8.AsSpan(byteIndex, runeLength));
                    builder.Append(newValue);
                    byteIndex += runeLength;
                }
                builder.Append(Utf8Span[prefixEnd..]);
                return builder.ToPyString();
            }
            finally { builder.Release(); }
        }

        var result = governor is null
            ? new GovernedByteBuilder(_byteLength)
            : new GovernedByteBuilder(governor, span, _byteLength);
        try
        {
            var offset = 0;
            while (offset < _byteLength && remaining > 0)
            {
                context?.CheckExecution(span);
                var found = IndexOfBytes(Utf8Span[offset..], oldValue.Utf8Span);
                if (found < 0) break;
                result.Append(_utf8.AsSpan(offset, found));
                result.Append(newValue);
                offset += found + oldValue._byteLength;
                remaining--;
            }
            result.Append(Utf8Span[offset..]);
            return result.ToPyString();
        }
        finally { result.Release(); }
    }

    public PyString ToLowerInvariant()
        => PyStringOps.Lower(this);

    public PyString ToUpperInvariant()
        => PyStringOps.Upper(this);

    public override string ToString() => AsString();

    public bool Equals(PyString? other)
    {
        return other is not null && Utf8Span.SequenceEqual(other.Utf8Span);
    }

    public override bool Equals(object? obj) => obj is PyString other && Equals(other);

    public override int GetHashCode()
    {
        if (Volatile.Read(ref _hashCodeComputed))
        {
            return _hashCode;
        }

        var hash = new HashCode();
        hash.AddBytes(Utf8Span);

        var computed = hash.ToHashCode();
        _hashCode = computed;
        Volatile.Write(ref _hashCodeComputed, true);
        return computed;
    }

    public static int CompareOrdinal(PyString left, PyString right)
    {
        return left.Utf8Span.SequenceCompareTo(right.Utf8Span);
    }

    public object GetIndex(int index) => Index(index);

    public object GetSlice(IEnumerable<int> indices)
    {
        var materialized = indices as IReadOnlyList<int>;
        if (materialized is not null)
        {
            return Slice(materialized);
        }

        var list = new List<int>();
        foreach (var index in indices)
        {
            list.Add(index);
        }

        return Slice(list);
    }

    object IPySliceableValue.GetSlice(object? start, object? end, object? step, LythonSourceSpan span)
        => Slice(PyIndexing.NormalizeSliceBounds(Length, start, end, step, span));

    public IEnumerable<object> Iterate()
    {
        foreach (var rune in EnumerateRunes())
        {
            yield return rune;
        }
    }

    public int GetPyHashCode() => GetHashCode();

    public PyString RenderPython(PyRenderingContext context) => this;

    public PyString RenderInterpolated(PyRenderingContext context) => this;

    public int ByteIndexToRuneIndex(int byteIndex)
    {
        if (byteIndex <= 0)
        {
            return 0;
        }

        if (Length == _byteLength)
        {
            return Math.Min(byteIndex, _byteLength);
        }

        var offsets = GetRuneByteOffsets();
        var index = Array.BinarySearch(offsets, byteIndex);
        return index >= 0 ? index : ~index;
    }

    internal int GetByteIndexForRuneBoundary(int runeIndex)
    {
        if (runeIndex <= 0)
        {
            return 0;
        }

        var length = Length;
        if (runeIndex >= length)
        {
            return _byteLength;
        }

        return length == _byteLength ? runeIndex : GetRuneByteOffsets()[runeIndex];
    }

    internal int GetByteIndexAfterRunes(int startByte, int runeCount)
    {
        if (runeCount <= 0 || startByte >= _byteLength)
        {
            return Math.Min(startByte, _byteLength);
        }

        if (Length == _byteLength)
        {
            return (int)Math.Min((long)startByte + runeCount, _byteLength);
        }

        var offsets = GetRuneByteOffsets();
        var startRune = Array.BinarySearch(offsets, startByte);
        if (startRune < 0)
        {
            startRune = ~startRune;
        }

        var targetRune = (long)startRune + runeCount;
        if (targetRune < 0)
        {
            targetRune = 0;
        }
        else if (targetRune > offsets.Length - 1)
        {
            targetRune = offsets.Length - 1;
        }

        return offsets[(int)targetRune];
    }

    internal PyString SliceByByteRange(int startByte, int endByte)
    {
        if (startByte >= endByte)
        {
            return Empty;
        }

        var bytes = Utf8Span[startByte..endByte];
        var length = bytes.Length;
        var result = _memoryGovernor is null
            ? new byte[length]
            : AllocateGovernedUtf8(length, _memoryGovernor, _allocationSpan);
        bytes.CopyTo(result);
        return _memoryGovernor is null ? new PyString(result) : new PyString(result, _memoryGovernor, _allocationSpan, prefunded: true);
    }

    public static int IndexOfBytes(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
        => haystack.IndexOf(needle);

    private RuneByteRange GetRuneByteRange(int runeIndex)
    {
        if (runeIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runeIndex));
        }

        var length = Length;
        if (runeIndex >= length)
        {
            throw new ArgumentOutOfRangeException(nameof(runeIndex));
        }

        if (length == _byteLength)
        {
            return new RuneByteRange(runeIndex, 1);
        }

        var offsets = GetRuneByteOffsets();
        return new RuneByteRange(offsets[runeIndex], offsets[runeIndex + 1] - offsets[runeIndex]);
    }

    private int[] GetRuneByteOffsets()
    {
        if (_runeByteOffsets is not null)
        {
            return _runeByteOffsets;
        }

        // Cache capacity describes its own charge; no separate accumulator.
        var count = Length;
        var bytes = 32L + 4L * ((long)count + 1);
        _memoryGovernor?.Reserve(bytes, _allocationSpan);
        try
        {
            var offsets = new int[count + 1];
            var runeIndex = 0;
            for (var byteIndex = 0; byteIndex < _byteLength;)
            {
                offsets[runeIndex++] = byteIndex;
                byteIndex += GetRuneLengthAtByteIndex(byteIndex);
            }
            offsets[runeIndex] = _byteLength;
            _runeByteOffsets = offsets;
            _memoryGovernor?.Commit(bytes);
            return offsets;
        }
        catch
        {
            _memoryGovernor?.ReleaseReserved(bytes);
            throw;
        }
    }

    private static PyString[] CreateAsciiCharacters()
    {
        var characters = new PyString[128];
        for (var value = 0; value < characters.Length; value++)
        {
            characters[value] = new PyString([(byte)value]);
        }

        return characters;
    }

    private int GetRuneLengthAtByteIndex(int byteIndex) => GetRuneLength(_utf8[byteIndex]);

    private static int GetRuneLength(byte firstByte)
    {
        if ((firstByte & 0b1000_0000) == 0)
        {
            return 1;
        }

        if ((firstByte & 0b1110_0000) == 0b1100_0000)
        {
            return 2;
        }

        if ((firstByte & 0b1111_0000) == 0b1110_0000)
        {
            return 3;
        }

        return 4;
    }

    private static bool TryGetOwnedArray(ReadOnlyMemory<byte> utf8, out byte[] owned)
    {
        if (MemoryMarshal.TryGetArray(utf8, out ArraySegment<byte> segment) &&
            segment.Array is not null &&
            segment.Offset == 0 &&
            segment.Count == segment.Array.Length)
        {
            owned = segment.Array;
            return true;
        }

        owned = [];
        return false;
    }

    private static byte[] AllocateGovernedUtf8(int length, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        var charge = EstimateApproximateBytes(length);
        governor.Reserve(charge, allocationSpan);
        try
        {
            var storage = new byte[length];
            governor.Commit(charge);
            return storage;
        }
        catch
        {
            governor.ReleaseReserved(charge);
            throw;
        }
    }

    private static int CheckedByteLength(long length, LythonSourceSpan? span)
    {
        if (length > int.MaxValue)
        {
            throw RuntimeErrors.Runtime("string allocation is too large.", span);
        }

        return (int)length;
    }
}
