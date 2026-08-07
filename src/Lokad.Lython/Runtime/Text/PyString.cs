using System.Text;
using System.Runtime.InteropServices;

namespace Lokad.Lython.Runtime.Text;

internal sealed class PyString : IEquatable<PyString>, IPyTruthyValue, IPyIndexableValue, IPyIterableValue, IPyRenderableValue, IPyHashableValue, IPyGovernedValue, IPySizedValue
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly byte[] _utf8;
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly LythonSourceSpan? _allocationSpan;
    private int _runeLength = -1;
    private string? _decodedString;

    private PyString(byte[] utf8)
    {
        _utf8 = utf8;
    }

    private PyString(byte[] utf8, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        ChargeGovernedAllocation(governor, allocationSpan, EstimateApproximateBytes(utf8.Length));
        _utf8 = utf8;
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
    }

    public static PyString Empty { get; } = new([]);

    public int Length => _runeLength >= 0 ? _runeLength : (_runeLength = CountRunes(_utf8));

    public ReadOnlyMemory<byte> Utf8Bytes => _utf8;

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    internal static long EstimateApproximateBytes(int utf8Length) => 32L + utf8Length;

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
        return new PyString(bytes, governor, allocationSpan);
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

        var copy = AllocateGovernedUtf8(utf8.Length, governor, allocationSpan);
        utf8.Span.CopyTo(copy);
        return new PyString(copy, governor, allocationSpan);
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
            RuntimeMemoryEstimates.SaturatingAdd(_utf8.Length, other._utf8.Length),
            allocationSpan);
        var result = governor is null
            ? new byte[resultLength]
            : AllocateGovernedUtf8(resultLength, governor, allocationSpan);
        Buffer.BlockCopy(_utf8, 0, result, 0, _utf8.Length);
        Buffer.BlockCopy(other._utf8, 0, result, _utf8.Length, other._utf8.Length);
        return governor is null ? new PyString(result) : new PyString(result, governor, allocationSpan);
    }

    public PyString Repeat(int count)
    {
        return RepeatCore(count, _memoryGovernor, _allocationSpan);
    }

    public PyString Repeat(int count, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        => RepeatCore(count, governor, allocationSpan);

    private PyString RepeatCore(int count, MemoryGovernor? governor, LythonSourceSpan? allocationSpan)
    {
        if (count <= 0 || _utf8.Length == 0)
        {
            return Empty;
        }

        var resultLength = CheckedByteLength(
            RuntimeMemoryEstimates.SaturatingMultiply(_utf8.Length, count),
            allocationSpan);
        var result = governor is null
            ? new byte[resultLength]
            : AllocateGovernedUtf8(resultLength, governor, allocationSpan);
        for (var i = 0; i < count; i++)
        {
            Buffer.BlockCopy(_utf8, 0, result, i * _utf8.Length, _utf8.Length);
        }

        return governor is null ? new PyString(result) : new PyString(result, governor, allocationSpan);
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

        return _memoryGovernor is null ? new PyString(result) : new PyString(result, _memoryGovernor, _allocationSpan);
    }

    public PyString Index(int runeIndex)
    {
        var (start, length) = GetRuneByteRange(runeIndex);
        var bytes = _memoryGovernor is null
            ? new byte[length]
            : AllocateGovernedUtf8(length, _memoryGovernor, _allocationSpan);
        Buffer.BlockCopy(_utf8, start, bytes, 0, length);
        return _memoryGovernor is null ? new PyString(bytes) : new PyString(bytes, _memoryGovernor, _allocationSpan);
    }

    public IEnumerable<PyString> EnumerateRunes()
    {
        for (var byteIndex = 0; byteIndex < _utf8.Length;)
        {
            var runeLength = GetRuneLengthAtByteIndex(byteIndex);
            var bytes = _memoryGovernor is null
                ? new byte[runeLength]
                : AllocateGovernedUtf8(runeLength, _memoryGovernor, _allocationSpan);
            Buffer.BlockCopy(_utf8, byteIndex, bytes, 0, runeLength);
            yield return _memoryGovernor is null ? new PyString(bytes) : new PyString(bytes, _memoryGovernor, _allocationSpan);
            byteIndex += runeLength;
        }
    }

    public string AsString()
    {
        return _decodedString ??= Utf8.GetString(_utf8);
    }

    public string AsString(int runeIndex)
    {
        var (start, length) = GetRuneByteRange(runeIndex);
        return Utf8.GetString(_utf8, start, length);
    }

    public int RuneIndexFromUtf16Index(int utf16Index)
    {
        if (utf16Index <= 0)
        {
            return 0;
        }

        var runeIndex = 0;
        var consumedUtf16 = 0;
        for (var byteIndex = 0; byteIndex < _utf8.Length;)
        {
            Rune.DecodeFromUtf8(_utf8.AsSpan(byteIndex), out var rune, out var runeLength);
            if (consumedUtf16 >= utf16Index)
            {
                break;
            }

            consumedUtf16 += rune.Utf16SequenceLength;
            if (consumedUtf16 > utf16Index)
            {
                break;
            }

            runeIndex++;
            byteIndex += runeLength;
        }

        return runeIndex;
    }

    public bool StartsWith(PyString prefix)
    {
        var prefixBytes = prefix._utf8.AsSpan();
        return prefixBytes.Length <= _utf8.Length &&
            _utf8.AsSpan()[..prefixBytes.Length].SequenceEqual(prefixBytes);
    }

    public bool EndsWith(PyString suffix)
    {
        var suffixBytes = suffix._utf8.AsSpan();
        return suffixBytes.Length <= _utf8.Length &&
            _utf8.AsSpan()[^suffixBytes.Length..].SequenceEqual(suffixBytes);
    }

    public bool Contains(PyString part) => IndexOfBytes(_utf8, part._utf8) >= 0;

    public int Find(PyString part)
    {
        var byteIndex = IndexOfBytes(_utf8, part._utf8);
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
            var found = IndexOfBytes(_utf8.AsSpan()[offset..], part._utf8);
            if (found < 0)
            {
                return count;
            }

            count++;
            offset += found + part._utf8.Length;
        }
    }

    public PyString Replace(PyString oldValue, PyString newValue)
    {
        if (oldValue._utf8.Length == 0)
        {
            var insertionCount = _utf8.Length == 0 ? 1 : Length + 1;
            var capacity = CheckedByteLength(
                RuntimeMemoryEstimates.SaturatingAdd(
                    _utf8.Length,
                    RuntimeMemoryEstimates.SaturatingMultiply(insertionCount, newValue._utf8.Length)),
                _allocationSpan);
            var builder = _memoryGovernor is null
                ? new Utf8ValueBuilder(capacity)
                : new Utf8ValueBuilder(_memoryGovernor, _allocationSpan, capacity);
            builder.Append(newValue);
            foreach (var rune in EnumerateRunes())
            {
                builder.Append(rune);
                builder.Append(newValue);
            }

            return builder.ToPyString();
        }

        var result = _memoryGovernor is null
            ? new Utf8ValueBuilder(_utf8.Length)
            : new Utf8ValueBuilder(_memoryGovernor, _allocationSpan, _utf8.Length);
        var offset = 0;
        while (offset < _utf8.Length)
        {
            var found = IndexOfBytes(_utf8.AsSpan()[offset..], oldValue._utf8);
            if (found < 0)
            {
                result.Append(_utf8.AsSpan()[offset..]);
                break;
            }

            result.Append(_utf8.AsSpan()[offset..(offset + found)]);
            result.Append(newValue);
            offset += found + oldValue._utf8.Length;
        }

        return result.ToPyString();
    }

    public PyString ToLowerInvariant()
        => PyStringOps.Lower(this);

    public PyString ToUpperInvariant()
        => PyStringOps.Upper(this);

    public override string ToString() => AsString();

    public bool Equals(PyString? other)
    {
        return other is not null && _utf8.AsSpan().SequenceEqual(other._utf8);
    }

    public override bool Equals(object? obj) => obj is PyString other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var b in _utf8)
        {
            hash.Add(b);
        }

        return hash.ToHashCode();
    }

    public static int CompareOrdinal(PyString left, PyString right)
    {
        return left._utf8.AsSpan().SequenceCompareTo(right._utf8);
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

        var runeIndex = 0;
        for (var i = 0; i < byteIndex;)
        {
            i += GetRuneLength(_utf8[i]);
            runeIndex++;
        }

        return runeIndex;
    }

    internal int GetByteIndexForRuneBoundary(int runeIndex)
    {
        if (runeIndex <= 0)
        {
            return 0;
        }

        var currentRune = 0;
        for (var byteIndex = 0; byteIndex < _utf8.Length;)
        {
            if (currentRune == runeIndex)
            {
                return byteIndex;
            }

            byteIndex += GetRuneLength(_utf8[byteIndex]);
            currentRune++;
        }

        return _utf8.Length;
    }

    internal int GetByteIndexAfterRunes(int startByte, int runeCount)
    {
        if (runeCount <= 0 || startByte >= _utf8.Length)
        {
            return Math.Min(startByte, _utf8.Length);
        }

        var byteIndex = startByte;
        for (var remaining = runeCount; remaining > 0 && byteIndex < _utf8.Length; remaining--)
        {
            byteIndex += GetRuneLengthAtByteIndex(byteIndex);
        }

        return byteIndex;
    }

    internal PyString SliceByByteRange(int startByte, int endByte)
    {
        if (startByte >= endByte)
        {
            return Empty;
        }

        var length = endByte - startByte;
        var result = _memoryGovernor is null
            ? new byte[length]
            : AllocateGovernedUtf8(length, _memoryGovernor, _allocationSpan);
        Buffer.BlockCopy(_utf8, startByte, result, 0, result.Length);
        return _memoryGovernor is null ? new PyString(result) : new PyString(result, _memoryGovernor, _allocationSpan);
    }

    public static int IndexOfBytes(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        if (needle.Length == 0)
        {
            return 0;
        }

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.Slice(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }

        return -1;
    }

    private static int CountRunes(byte[] utf8)
    {
        var count = 0;
        for (var i = 0; i < utf8.Length;)
        {
            i += GetRuneLength(utf8[i]);
            count++;
        }

        return count;
    }

    private (int Start, int Length) GetRuneByteRange(int runeIndex)
    {
        if (runeIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runeIndex));
        }

        var currentRune = 0;
        for (var byteIndex = 0; byteIndex < _utf8.Length;)
        {
            var runeLength = GetRuneLengthAtByteIndex(byteIndex);
            if (currentRune == runeIndex)
            {
                return (byteIndex, runeLength);
            }

            byteIndex += runeLength;
            currentRune++;
        }

        throw new ArgumentOutOfRangeException(nameof(runeIndex));
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

    private static void ChargeGovernedAllocation(MemoryGovernor governor, LythonSourceSpan? allocationSpan, long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

        governor.Reserve(bytes, allocationSpan);
        governor.Commit(bytes);
    }

    private static byte[] AllocateGovernedUtf8(int length, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        governor.EnsureCanReserve(EstimateApproximateBytes(length), allocationSpan);
        return new byte[length];
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
