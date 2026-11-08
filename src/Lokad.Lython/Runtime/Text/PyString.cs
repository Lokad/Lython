using System.Threading;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime.Text;

internal sealed class PyString : IEquatable<PyString>, IPyTruthyValue, IPyIndexableValue, IPySliceableValue, IPyIterableValue, IPyRenderableValue, IPyHashableValue, IPySizedValue, IPyOwnershipSnapshot
{

    private readonly record struct RuneByteRange(int Start, int Length);

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly PyString[] AsciiCharacters = CreateAsciiCharacters();

    internal const int InlineUtf8Capacity = 8;

    // A managed word gives the payload its natural alignment. All encoding is
    // through bounded byte spans; it assumes neither field layout nor endianness.
    [InlineArray(1)]
    private struct InlineUtf8 { private long _first; }

    private byte[]? _utf8;
    private InlineUtf8 _inlineUtf8;
    // Zero means adopted array storage. A positive capacity also funds any
    // later stable memory borrow; native span readers keep using inline bytes.
    private readonly byte _inlineCapacity;
    private int _byteLength;
    private readonly MemoryGovernor? _memoryGovernor;
    private int _runeLength = -1;
    private int[]? _runeByteOffsets;
    private int _hashCode;
    private bool _hashCodeComputed;

    private PyString(byte[] utf8) : this(utf8, utf8.Length) { }

    private PyString(byte[] utf8, int byteLength, MemoryGovernor? governor = null)
    {
        _utf8 = utf8;
        _byteLength = byteLength;
        _memoryGovernor = governor;
    }

    private PyString(ReadOnlySpan<byte> utf8, int capacity, MemoryGovernor? governor)
    {
        if (utf8.IsEmpty || utf8.Length > capacity || capacity > InlineUtf8Capacity)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        utf8.CopyTo(InlineBytes);
        _inlineCapacity = checked((byte)capacity);
        _byteLength = utf8.Length;
        _memoryGovernor = governor;
    }

    // Factory-private construction. The whole capacity is already reserved;
    // callers write it and commit before exposing this value to any reader.
    private PyString(int capacity, MemoryGovernor? governor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        if (capacity <= InlineUtf8Capacity) _inlineCapacity = (byte)capacity;
        else _utf8 = new byte[capacity];
        _byteLength = capacity;
        _memoryGovernor = governor;
    }

    internal static PyString CreateReservedUtf8(int capacity, MemoryGovernor? governor)
        => new(capacity, governor);

    // Only the constructing factory may borrow this span, before committing.
    private Span<byte> InlineBytes => MemoryMarshal.AsBytes((Span<long>)_inlineUtf8);

    internal Span<byte> UnpublishedUtf8 => _inlineCapacity != 0
        ? InlineBytes[.._inlineCapacity] : _utf8.AsSpan();

    internal PyString CompleteReservedUtf8(int byteLength,
        MemoryGovernor.TemporaryMemoryReservation? storage = null)
    {
        var capacity = _inlineCapacity != 0 ? _inlineCapacity : _utf8!.Length;
        if ((uint)byteLength > (uint)capacity) throw new ArgumentOutOfRangeException(nameof(byteLength));
        var charge = EstimateApproximateBytes(capacity);
        if (byteLength == 0)
        {
            if (storage is null) _memoryGovernor?.ReleaseReserved(charge);
            return Empty;
        }
        _byteLength = byteLength;
        if (storage is not null) storage.Commit(charge);
        else _memoryGovernor?.Commit(charge);
        return this;
    }

    public static PyString Empty { get; } = new([]);

    public int Length
    {
        get
        {
            if (_runeLength >= 0) return _runeLength;
            var context = PyStructuralGuard.AmbientContext;
            var span = PyStructuralGuard.AmbientSpan;
            context?.CheckExecution(span);
            return GetCharacterCount(context?.State.CancellationToken ?? default, span);
        }
    }

    internal int GetCharacterCount(CancellationToken cancellationToken, LythonSourceSpan? span)
        => _runeLength >= 0 ? _runeLength
            : (_runeLength = CountUtf8Units(false, cancellationToken, span));

    /// <summary>
    /// Counts the UTF-16 code units the decoded CLR string needs. Astral runes
    /// (4-byte UTF-8 sequences) decode to a surrogate pair, so this exceeds
    /// <see cref="Length"/> whenever astral characters are present. Projection
    /// budgets must use this width: rune counts undercharge astral payloads.
    /// </summary>
    public int GetUtf16CodeUnitCount(CancellationToken cancellationToken = default, LythonSourceSpan? span = null)
    {
        var context = PyStructuralGuard.AmbientContext;
        span ??= PyStructuralGuard.AmbientSpan;
        context?.CheckExecution(span);
        if (!cancellationToken.CanBeCanceled) cancellationToken = context?.State.CancellationToken ?? default;
        return CountUtf8Units(true, cancellationToken, span);
    }

    private int CountUtf8Units(bool utf16, CancellationToken cancellationToken, LythonSourceSpan? span)
    {
        var source = Utf8Span;
        var units = 0;
        // Count both widths without decoding or creating a CLR string. Keep
        // cancellation ranges on scalar boundaries, deferring at most three bytes.
        for (var offset = 0; offset < source.Length;)
        {
            if (cancellationToken.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
            var end = offset + Math.Min(4096, source.Length - offset);
            while (end < source.Length && (source[end] & 0xC0) == 0x80) end--;
            var count = 0; // A bounded chunk cannot overflow this subtotal.
            while (offset < end)
            {
                var width = GetRuneLength(source[offset]);
                count += utf16 && width == 4 ? 2 : 1;
                offset += width;
            }
            units = checked(units + count);
        }
        if (cancellationToken.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
        return units;
    }

    internal ReadOnlySpan<byte> Utf8Span => _inlineCapacity != 0
        ? MemoryMarshal.AsBytes((ReadOnlySpan<long>)_inlineUtf8)[.._byteLength]
        : _utf8.AsSpan(0, _byteLength);

    internal int ByteLength => _byteLength;

    public ReadOnlyMemory<byte> Utf8Bytes => Volatile.Read(ref _utf8) is { } bytes
        ? bytes.AsMemory(0, _byteLength) : MaterializeInlineMemory();

    private ReadOnlyMemory<byte> MaterializeInlineMemory()
    {
        // Initial 128B metadata admission covers this at-most-eight-byte
        // array in addition to the wrapper. This prepaid data view also
        // stays available to native cleanup after command refusal.
        lock (this)
        {
            if (_utf8 is not { } bytes)
            {
                bytes = Utf8Span.ToArray();
                Volatile.Write(ref _utf8, bytes);
            }
            return bytes.AsMemory(0, _byteLength);
        }
    }

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes) =>
        OwnershipSnapshot.Owned(OwnerMemoryGovernor, CommittedOwnedBytes, out chargeBytes);

    internal static long EstimateApproximateBytes(int utf8Length) => 128L + utf8Length;

    public bool IsTruthy() => _byteLength != 0;

    public static PyString FromString(string text)
    {
        if (text.Length == 0) return Empty;
        if (text.Length <= InlineUtf8Capacity && Utf8.GetByteCount(text) is <= InlineUtf8Capacity and var length)
        {
            Span<byte> bytes = stackalloc byte[InlineUtf8Capacity];
            _ = Utf8.GetBytes(text.AsSpan(), bytes);
            return new PyString(bytes[..length], length, null);
        }
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
        if (byteCount <= InlineUtf8Capacity)
        {
            Span<byte> inline = stackalloc byte[InlineUtf8Capacity];
            _ = Utf8.GetBytes(text.AsSpan(), inline);
            return CopyFromUtf8(inline[..byteCount], governor, allocationSpan);
        }
        var bytes = AllocateGovernedUtf8(byteCount, governor, allocationSpan);
        _ = Utf8.GetBytes(text.AsSpan(), bytes.AsSpan());
        return new PyString(bytes, bytes.Length, governor);
    }

    internal static PyString FromOwnedUtf8(byte[] utf8)
    {
        return utf8.Length == 0 ? Empty : new PyString(utf8);
    }

    internal static PyString FromOwnedUtf8(byte[] utf8, MemoryGovernor governor)
        => FromOwnedUtf8(utf8, governor, null);

    internal static PyString FromOwnedUtf8(byte[] utf8, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        if (utf8.Length == 0) return Empty;
        var bytes = EstimateApproximateBytes(utf8.Length);
        governor.Reserve(bytes, allocationSpan);
        try
        {
            var result = new PyString(utf8, utf8.Length, governor);
            governor.Commit(bytes);
            return result;
        }
        catch { governor.ReleaseReserved(bytes); throw; }
    }

    internal static PyString FromOwnedUtf8(byte[] utf8, MemoryGovernor governor, LythonSourceSpan? allocationSpan,
        MemoryGovernor.TemporaryMemoryReservation storage, int? byteLength = null)
    {
        var length = byteLength ?? utf8.Length;
        if ((uint)length > (uint)utf8.Length) throw new ArgumentOutOfRangeException(nameof(byteLength));
        if (length == 0) return Empty;
        var result = new PyString(utf8, length, governor);
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
        var result = new PyString(utf8, byteLength, governor);
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
            var result = new PyString(utf8, byteLength, governor);
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
            : CopyFromUtf8(utf8.Span, null, null);
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
            return FromOwnedUtf8(owned, governor, allocationSpan);
        }

        return CopyFromUtf8(utf8.Span, governor, allocationSpan);
    }

    internal static PyString CopyFromUtf8(ReadOnlySpan<byte> utf8, MemoryGovernor? governor, LythonSourceSpan? allocationSpan)
    {
        if (utf8.IsEmpty) return Empty;
        if (utf8.Length <= InlineUtf8Capacity)
        {
            var charge = EstimateApproximateBytes(utf8.Length);
            governor?.Reserve(charge, allocationSpan);
            try
            {
                PyStructuralGuard.AmbientContext?.CheckExecution(allocationSpan);
                var result = new PyString(utf8, utf8.Length, governor);
                governor?.Commit(charge);
                return result;
            }
            catch { governor?.ReleaseReserved(charge); throw; }
        }
        var copy = governor is null ? new byte[utf8.Length]
            : AllocateGovernedUtf8(utf8.Length, governor, allocationSpan);
        utf8.CopyTo(copy);
        return new PyString(copy, copy.Length, governor);
    }

    public PyString Concat(PyString other)
    {
        var governor = _memoryGovernor ?? other._memoryGovernor;
        var allocationSpan = PyStructuralGuard.AmbientSpan;
        return ConcatCore(other, governor, allocationSpan);
    }

    public PyString Concat(PyString other, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        => ConcatCore(other, governor, allocationSpan);

    private PyString ConcatCore(PyString other, MemoryGovernor? governor, LythonSourceSpan? allocationSpan)
    {
        var resultLength = CheckedByteLength(
            RuntimeMemoryEstimates.SaturatingAdd(_byteLength, other._byteLength),
            allocationSpan);
        if (resultLength <= InlineUtf8Capacity)
        {
            Span<byte> inline = stackalloc byte[InlineUtf8Capacity];
            Utf8Span.CopyTo(inline);
            other.Utf8Span.CopyTo(inline[_byteLength..]);
            return CopyFromUtf8(inline[..resultLength], governor, allocationSpan);
        }
        var result = governor is null
            ? new byte[resultLength]
            : AllocateGovernedUtf8(resultLength, governor, allocationSpan);
        Utf8Span.CopyTo(result);
        other.Utf8Span.CopyTo(result.AsSpan(_byteLength));
        return new PyString(result, result.Length, governor);
    }

    public PyString Repeat(int count)
    {
        return RepeatCore(count, _memoryGovernor, PyStructuralGuard.AmbientSpan);
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
        if (resultLength <= InlineUtf8Capacity)
        {
            Span<byte> inline = stackalloc byte[InlineUtf8Capacity];
            for (var i = 0; i < count; i++) Utf8Span.CopyTo(inline[(i * _byteLength)..]);
            return CopyFromUtf8(inline[..resultLength], governor, allocationSpan);
        }
        var result = governor is null
            ? new byte[resultLength]
            : AllocateGovernedUtf8(resultLength, governor, allocationSpan);
        for (var i = 0; i < count; i++)
        {
            Utf8Span.CopyTo(result.AsSpan(i * _byteLength));
        }

        return new PyString(result, result.Length, governor);
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

        var resultLength = CheckedByteLength(totalBytes, PyStructuralGuard.AmbientSpan);
        Span<byte> inline = stackalloc byte[InlineUtf8Capacity];
        var result = resultLength <= InlineUtf8Capacity ? null : _memoryGovernor is null
            ? new byte[resultLength]
            : AllocateGovernedUtf8(resultLength, _memoryGovernor, PyStructuralGuard.AmbientSpan);
        var destination = result is null ? inline[..resultLength] : result.AsSpan();
        var offset = 0;
        foreach (var runeIndex in runeIndices)
        {
            var (start, length) = GetRuneByteRange(runeIndex);
            Utf8Span.Slice(start, length).CopyTo(destination[offset..]);
            offset += length;
        }

        return result is null ? CopyFromUtf8(destination, _memoryGovernor, PyStructuralGuard.AmbientSpan)
            : new PyString(result, result.Length, _memoryGovernor);
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

        var resultLength = CheckedByteLength(totalBytes, PyStructuralGuard.AmbientSpan);
        Span<byte> inline = stackalloc byte[InlineUtf8Capacity];
        var result = resultLength <= InlineUtf8Capacity ? null : _memoryGovernor is null
            ? new byte[resultLength]
            : AllocateGovernedUtf8(resultLength, _memoryGovernor, PyStructuralGuard.AmbientSpan);
        var destination = result is null ? inline[..resultLength] : result.AsSpan();
        var offset = 0;
        for (long runeIndex = bounds.Start; bounds.Step > 0 ? runeIndex < bounds.End : runeIndex > bounds.End; runeIndex += bounds.Step)
        {
            var (start, length) = GetRuneByteRange((int)runeIndex);
            Utf8Span.Slice(start, length).CopyTo(destination[offset..]);
            offset += length;
        }

        return result is null ? CopyFromUtf8(destination, _memoryGovernor, PyStructuralGuard.AmbientSpan)
            : new PyString(result, result.Length, _memoryGovernor);
    }

    public PyString Index(int runeIndex)
    {
        var (start, length) = GetRuneByteRange(runeIndex);
        if (length == 1 && Utf8Span[start] < 0x80)
        {
            return AsciiCharacters[Utf8Span[start]];
        }
        return CopyFromUtf8(Utf8Span.Slice(start, length), _memoryGovernor, PyStructuralGuard.AmbientSpan);
    }

    public IEnumerable<PyString> EnumerateRunes()
    {
        for (var byteIndex = 0; byteIndex < _byteLength;)
        {
            var runeLength = GetRuneLengthAtByteIndex(byteIndex);
            if (runeLength == 1)
            {
                yield return AsciiCharacters[Utf8Span[byteIndex]];
                byteIndex++;
                continue;
            }

            yield return CopyFromUtf8(Utf8Span.Slice(byteIndex, runeLength), _memoryGovernor, PyStructuralGuard.AmbientSpan);
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

    internal long CommittedOwnedBytes => EstimateApproximateBytes(_inlineCapacity != 0 ? _inlineCapacity : _utf8!.Length)
        + (_memoryGovernor is not null && _runeByteOffsets is { } offsets ? 32L + 4L * offsets.Length : 0);

    public string AsString(int runeIndex)
    {
        var (start, length) = GetRuneByteRange(runeIndex);
        return Utf8.GetString(Utf8Span.Slice(start, length));
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
        span ??= PyStructuralGuard.AmbientSpan;
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
                    builder.Append(Utf8Span.Slice(byteIndex, runeLength));
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
                result.Append(Utf8Span.Slice(offset, found));
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

        return ComputeHash(default, null);
    }

    internal int GetHashCode(CancellationToken token, LythonSourceSpan? span)
        => Volatile.Read(ref _hashCodeComputed) ? _hashCode : ComputeHash(token, span);

    private int ComputeHash(CancellationToken token, LythonSourceSpan? span)
    {
        var computed = HashUtf8(Utf8Span, span: span, cancellationToken: token);
        _hashCode = computed;
        Volatile.Write(ref _hashCodeComputed, true);
        return computed;
    }

    internal static int HashUtf8(ReadOnlySpan<byte> bytes, LythonRuntime.ExecutionContext? context = null,
        LythonSourceSpan? span = null, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
        var hash = new HashCode();
        for (var offset = 0; offset < bytes.Length;)
        {
            context?.CheckExecution(span);
            if (cancellationToken.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
            var count = Math.Min(4096, bytes.Length - offset);
            hash.AddBytes(bytes.Slice(offset, count));
            offset += count;
        }
        return hash.ToHashCode();
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

        return CopyFromUtf8(Utf8Span[startByte..endByte], _memoryGovernor, PyStructuralGuard.AmbientSpan);
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
        var context = PyStructuralGuard.AmbientContext;
        var span = PyStructuralGuard.AmbientSpan;
        var count = Length;
        var bytes = 32L + 4L * ((long)count + 1);
        _memoryGovernor?.Reserve(bytes, span);
        try
        {
            // Admission can invoke the host. Keep a cancelled construction
            // private and refund its reservation before allocating the cache.
            context?.CheckExecution(span);
            var offsets = new int[count + 1];
            var runeIndex = 0;
            for (var byteIndex = 0; byteIndex < _byteLength;)
            {
                if ((runeIndex & 1023) == 0) context?.CheckExecution(span);
                offsets[runeIndex++] = byteIndex;
                byteIndex += GetRuneLengthAtByteIndex(byteIndex);
            }
            offsets[runeIndex] = _byteLength;
            context?.CheckExecution(span);
            _memoryGovernor?.Commit(bytes);
            return _runeByteOffsets = offsets;
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

    private int GetRuneLengthAtByteIndex(int byteIndex) => GetRuneLength(Utf8Span[byteIndex]);

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
