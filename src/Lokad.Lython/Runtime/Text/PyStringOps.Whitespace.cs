using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Lokad.Lython.Runtime.Text;

internal static partial class PyStringOps
{
    // Python 3.13's 29 whitespace scalars. Keeping this small inventory explicit
    // avoids BCL Unicode-version drift and includes Python's four C0 separators.
    internal static bool IsPythonWhitespace(Rune rune) => rune.Value <= 0x7F
        ? IsAsciiWhitespace((byte)rune.Value)
        : rune.Value is 0x85 or 0xA0 or 0x1680 or >= 0x2000 and <= 0x200A or
            0x2028 or 0x2029 or 0x202F or 0x205F or 0x3000;

    private static bool IsAsciiWhitespace(byte value)
        => value is >= 0x09 and <= 0x0D or >= 0x1C and <= 0x20;

    private static readonly SearchValues<byte> AsciiWhitespaceCharacters = CreateAsciiCharacters(whitespace: true);
    private static readonly SearchValues<byte> AsciiWordCharacters = CreateAsciiCharacters(whitespace: false);
    private static readonly SearchValues<byte> LineBreakCandidates = SearchValues.Create(
        (ReadOnlySpan<byte>)[(byte)'\n', (byte)'\r', (byte)'\v', (byte)'\f', 0x1c, 0x1d, 0x1e, 0xc2, 0xe2]);

    private static SearchValues<byte> CreateAsciiCharacters(bool whitespace)
    {
        Span<byte> characters = stackalloc byte[128];
        var count = 0;
        for (var value = 0; value < 128; value++)
            if (IsAsciiWhitespace((byte)value) == whitespace) characters[count++] = (byte)value;
        return SearchValues.Create(characters[..count]);
    }

    // The caller has classified the first (or last) ASCII byte. Stop at a
    // category change or any UTF-8 byte, leaving Unicode decoding to the caller.
    private static int AsciiWhitespaceRunLength(ReadOnlySpan<byte> chunk, bool whitespace, bool reverse = false)
    {
        var characters = whitespace ? AsciiWhitespaceCharacters : AsciiWordCharacters;
        if (reverse) return chunk.Length - 1 - chunk[..^1].LastIndexOfAnyExcept(characters);
        var boundary = chunk[1..].IndexOfAnyExcept(characters);
        return boundary < 0 ? chunk.Length : boundary + 1;
    }

    internal static PyList SplitCasedWhitespace(PyString value, bool upper,
        MemoryGovernor governor, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => SplitCore(value, -1, false, governor, span, context,
            upper ? CaseMapping.Upper : CaseMapping.Lower);

    // Only compiler-private immediate iteration may consume this representation.
    // Admit every eventual word before the first loop effect, just as eager split
    // does. The execution owner retains unused capacity until its normal cleanup.
    internal static CasedWhitespaceIteration PrepareCasedWhitespaceIteration(PyString value, bool upper,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        context.CheckExecution(span);
        var mapping = upper ? CaseMapping.Upper : CaseMapping.Lower;
        CasedWhitespaceIteration? prepared = context.MemoryGovernor.RentWordBuffer(span);
        var previousScratchCapacity = prepared.EncodedCapacity;
        try
        {
            var source = value.Utf8Span;
            var state = new WhitespaceRangeState(-1, mapping);
            // The first length needs four bytes. Each later length uses
            // space already covered by at least one skipped whitespace scalar.
            var capacity = source.IsEmpty ? 0
                : CheckCaseOutputCapacity(sizeof(int) + (long)source.Length * state.MaximumCaseWidth, span);
            var encoded = prepared.PrepareEncoded(capacity, context, span);
            context.CheckExecution(span);
            var used = 0;
            long outputBytes = 0;
            while (state.Position < source.Length)
            {
                state.Written = used + sizeof(int);
                if (!TryReadWhitespaceRange(source, ref state, context, span, out var range, encoded)) break;
                BinaryPrimitives.WriteInt32LittleEndian(encoded[used..], range.Capacity);
                used = range.End;
                outputBytes = checked(outputBytes + PyString.EstimateApproximateBytes(range.Capacity));
            }

            // Prepay every eventual immutable word before any loop effect.
            context.MemoryGovernor.Reserve(outputBytes, span);
            try
            {
                context.CheckExecution(span);
                var result = prepared;
                result.Initialize(context, span, used);
                context.MemoryGovernor.Commit(outputBytes);
                prepared = null;
                return result;
            }
            catch { context.MemoryGovernor.ReleaseReserved(outputBytes); throw; }
        }
        finally
        {
            if (prepared is not null)
            {
                // Keep earlier reusable capacity, but never retain newly grown
                // working bytes from a failed scan. No cursor escaped.
                try
                {
                    if (prepared.EncodedCapacity > previousScratchCapacity)
                        prepared.ReleaseEncoded();
                }
                finally { context.MemoryGovernor.ReturnWordBuffer(prepared); }
            }
        }
    }

    internal sealed class CasedWhitespaceIteration : ExecutableTemporaryValue
    {
        private readonly MemoryGovernor _owner;
        private byte[] _encoded = [];
        internal int EncodedCapacity => _encoded.Length;
        private LythonRuntime.ExecutionContext? _context;
        private LythonSourceSpan? _span;
        private int _length;
        private int _position;
        private bool _loaned = true;

        internal const long HeaderBytes = 128;
        internal long StorageBytes => HeaderBytes + EncodedStorageBytes(EncodedCapacity);

        internal CasedWhitespaceIteration(MemoryGovernor owner) => _owner = owner;

        internal void ReleaseStorage()
        {
            try { ReleaseEncoded(); }
            finally { _owner.Release(HeaderBytes); }
        }

        private static long EncodedStorageBytes(int capacity) => capacity == 0 ? 0 : 32L + capacity;

        internal Span<byte> PrepareEncoded(int capacity, LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
        {
            if (capacity > _encoded.Length)
            {
                // Only a new line can replace scratch. Prior cached bytes have
                // no user; reserve overlap, but never copy them into the new line.
                var bytes = EncodedStorageBytes(capacity);
                using var replacement = _owner.ReserveTemporary(bytes, span);
                context.CheckExecution(span);
                var encoded = new byte[capacity];
                replacement.Commit(bytes);
                var previous = _encoded;
                _encoded = encoded;
                _owner.Release(EncodedStorageBytes(previous.Length));
            }
            return _encoded.AsSpan(0, capacity);
        }

        internal void ReleaseEncoded()
        {
            var previous = _encoded;
            _encoded = [];
            _owner.Release(EncodedStorageBytes(previous.Length));
        }

        internal void BeginLoan() => _loaned = true;

        internal bool EndLoan()
        {
            if (!_loaned) return false;
            _loaned = false;
            _context = null;
            _span = null;
            _length = _position = 0;
            return true;
        }

        internal void Initialize(LythonRuntime.ExecutionContext context, LythonSourceSpan span, int length)
        {
            _context = context;
            _span = span;
            _length = length;
            _position = 0;
        }

        internal PyIterationResult MoveNext()
        {
            // ForNext already checks the active execution before entering this
            // private cursor. The shared writer checks inside long work;
            // publication retains the ordinary binding/next-opcode check.
            if (_position == _length) return PyIterationResult.End;
            var range = ReadWordRange();
            return PyIterationResult.Yield(CreateSplitFragment(_encoded.AsSpan(), range, null, _owner, _span, _context));
        }

        private (int Start, int End, int Capacity) ReadWordRange()
        {
            var start = _position + sizeof(int);
            var length = BinaryPrimitives.ReadInt32LittleEndian(_encoded.AsSpan(_position));
            _position = start + length;
            return (start, _position, length);
        }

        internal MemoryGovernor MemoryGovernor => _owner;

        // Only the closed native histogram segment borrows this value. Other
        // consumers keep MoveNext and receive their ordinary immutable strings.
        internal bool TryReadWord(out CompactWord word)
        {
            word = default;
            if (_position == _length) return false;
            var range = ReadWordRange();
            if (range.Capacity > PyString.InlineUtf8Capacity)
                word.Value = CreateSplitFragment(_encoded.AsSpan(), range, null,
                    _owner, _span, _context);
            else
            {
                word.Length = range.Capacity;
                _encoded.AsSpan()[range.Start..range.End].CopyTo((Span<byte>)word.Utf8);
            }
            return true;
        }

        // The eager preflight already funds this entire value. Publication is
        // also native unwind work: no admission, callback or execution check may
        // replace the pending failure while restoring the last Python binding.
        internal object Materialize(ref CompactWord word)
        {
            if (word.Value is not null) return word.Value;
            var value = PyString.CreateReservedUtf8(word.Length, _owner);
            CompactWordBytes(ref word).CopyTo(value.UnpublishedUtf8);
            value.CompleteUnpublishedUtf8(word.Length);
            word.Value = value;
            return value;
        }

        public override void Dispose()
            => _owner.ReturnWordBuffer(this);
    }

    [System.Runtime.CompilerServices.InlineArray(PyString.InlineUtf8Capacity)]
    internal struct CompactWordUtf8 { private byte _first; }

    internal struct CompactWord
    {
        internal object? Value;
        internal CompactWordUtf8 Utf8;
        internal int Length;
    }

    internal static ReadOnlySpan<byte> CompactWordBytes(ref CompactWord word)
        => ((ReadOnlySpan<byte>)word.Utf8)[..word.Length];

    private static PyList SplitCore(PyString value, int maxSplit, bool reverse,
        MemoryGovernor? governor, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context,
        CaseMapping? casing = null, PyString? separator = null)
    {
        return BuildSplitResult(value, value.Utf8Span, separator, new(value.ByteLength, maxSplit, reverse, false, false, casing),
            casing, governor, span, context)!;
    }

    private static PyList SplitLineRanges(PyString value, bool keepEnds, MemoryGovernor? governor,
        LythonSourceSpan? span, LythonRuntime.ExecutionContext? context)
        => BuildSplitResult(value, value.Utf8Span, null, new(value.ByteLength, -1, false, true, keepEnds, null),
            null, governor, span, context)!;

    internal static PyList? SplitForUnpack(PyString value, PyString? separator, int maxSplit,
        Span<LythonRuntime.ExecutableSlot> unpacked, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => BuildSplitResult(value, value.Utf8Span, separator, new(value.ByteLength, maxSplit, false, false, false, null),
            null, context.MemoryGovernor, span, context, unpacked);

    internal static PyList? SplitUtf8Prefix(PyString value, int end, PyString separator,
        Span<LythonRuntime.ExecutableSlot> unpacked, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => BuildSplitResult(value, value.Utf8Span[..end], separator, new(end, -1, false, false, false, null),
            null, context.MemoryGovernor, span, context, unpacked);

    internal static PyString JoinTrimmedSplit(PyString value, PyString delimiter, PyString separator,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        using var ambient = PyStructuralGuard.PushAmbient(context, span);
        var source = value.Utf8Span;
        var split = delimiter.Utf8Span;
        var join = separator.Utf8Span;
        var length = ScanTrimmedSplit(source, split, join, default, context, span);
        if (length == 0) return PyString.Empty;

        // The input and both separators are immutable. Replay their ranges
        // directly into admitted final storage after the host admission call.
        var bytes = PyString.EstimateApproximateBytes(length);
        using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
        context.CheckExecution(span);
        var result = PyString.CreateReservedUtf8(length, context.MemoryGovernor);
        _ = ScanTrimmedSplit(source, split, join, result.UnpublishedUtf8, context, span);
        context.CheckExecution(span);
        storage.Commit(bytes);
        return result;
    }

    private static int ScanTrimmedSplit(ReadOnlySpan<byte> source, ReadOnlySpan<byte> delimiter,
        ReadOnlySpan<byte> separator, Span<byte> output, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var scan = new SplitScanState(source.Length, -1, false, false, false, null);
        var first = true;
        var length = 0;
        while (TryReadSplitRange(source, delimiter, ref scan, context, span, out var range))
        {
            context.CheckExecution(span);
            if (!first) length = AppendJoinedBytes(separator, output, length, context, span);
            var end = TrimUtf8End(source, range.Start, range.End);
            length = AppendJoinedBytes(source[range.Start..end], output, length, context, span);
            first = false;
        }
        return length;
    }

    private static int AppendJoinedBytes(ReadOnlySpan<byte> source, Span<byte> output, int length,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (source.Length > Array.MaxLength - length) throw RuntimeErrors.Memory("joined string exceeds supported capacity", span);
        if (!output.IsEmpty) CopyJoinBytes(source, output.Slice(length, source.Length), context, span);
        return length + source.Length;
    }

    internal static void CopyJoinBytes(ReadOnlySpan<byte> source, Span<byte> output,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        while (source.Length > 4096)
        {
            context.CheckExecution(span);
            source[..4096].CopyTo(output);
            source = source[4096..];
            output = output[4096..];
        }
        source.CopyTo(output);
    }

    private static PyList? BuildSplitResult(PyString value, ReadOnlySpan<byte> source, PyString? separator, SplitScanState scan,
        CaseMapping? casing, MemoryGovernor? governor, LythonSourceSpan? span,
        LythonRuntime.ExecutionContext? context, Span<LythonRuntime.ExecutableSlot> unpacked = default)
    {
        governor ??= value.OwnerMemoryGovernor;
        span ??= PyStructuralGuard.AmbientSpan;
        context ??= PyStructuralGuard.AmbientContext;
        governor?.ThrowIfAdmissionFailed();
        context?.CheckExecution(span);
        if (separator is { ByteLength: 0 }) throw new InvalidOperationException("empty separator");
        var delimiter = separator is null ? default : separator.Utf8Span;
        var sizing = scan;
        long outputBytes = 0, sharedPayload = 0;
        (int Start, int End, int Capacity) first = default;
        var bulkCount = TryCountBulkSplit(source, delimiter, scan, context, span, out var count,
            out var payloadBytes, out var wholeSource);
        if (!bulkCount)
        {
            while (TryReadSplitRange(source, delimiter, ref sizing, context, span, out var range))
            {
                context?.CheckExecution(span);
                if (count == 0) first = range;
                count = checked(count + 1);
                if (range.Capacity != 0)
                    outputBytes = checked(outputBytes + PyString.EstimateApproximateBytes(range.Capacity));
                if (range.Capacity > PyString.InlineUtf8Capacity)
                    sharedPayload = checked(sharedPayload + range.Capacity);
            }
            wholeSource = count == 1 && first.Start == 0 && first.End == source.Length;
        }

        // Deterministic scanning establishes exact final capacity before any
        // allocation. Replay directly into it; no temporary range table or loan.
        var reuseWhole = casing is null && wholeSource && source.Length == value.ByteLength &&
            ReferenceEquals(governor, value.OwnerMemoryGovernor);
        var borrow = !reuseWhole && casing is null && value.CanBorrowUtf8Ranges(governor);
        var sharedHeader = borrow && (bulkCount ? source.Length > PyString.InlineUtf8Capacity : sharedPayload != 0)
            ? value.Utf8SharingHeaderBytes : 0;
        if (bulkCount)
        {
            // Every nonempty part needs at most one shell. Shared large parts
            // copy no payload; short parts copy at most their inline capacity.
            // Empty/whole-source parts and unused headroom are refunded after
            // exact final construction, without relinquishing retained storage.
            outputBytes = reuseWhole ? 0 : checked(Math.Min(count, payloadBytes) * PyString.EstimateApproximateBytes(0)
                + (borrow ? Math.Min(payloadBytes, (long)count * PyString.InlineUtf8Capacity) : payloadBytes));
        }
        else if (reuseWhole && first.Capacity != 0)
            outputBytes -= PyString.EstimateApproximateBytes(first.Capacity);
        else if (borrow) outputBytes -= sharedPayload;
        var writeOperands = !unpacked.IsEmpty && count == unpacked.Length;
        var resultBytes = writeOperands ? 0 : PyList.EstimateCapacityBytes(count);
        using var output = governor?.ReserveTemporary(checked(resultBytes + outputBytes + sharedHeader), span);
        context?.CheckExecution(span);
        var parts = writeOperands ? null : count == 0 ? Array.Empty<object>() : new object[count];
        var index = 0;
        var headerCommitted = false;
        try
        {
            while (TryReadSplitRange(source, delimiter, ref scan, context, span, out var range))
            {
                context?.CheckExecution(span);
                object fragment;
                if (reuseWhole) fragment = value;
                else
                {
                    var bytes = range.Capacity == 0 ? 0 : PyString.EstimateApproximateBytes(range.Capacity);
                    if (borrow && range.Capacity > PyString.InlineUtf8Capacity)
                    {
                        if (!headerCommitted)
                        {
                            // Fund surviving source backing before its first view;
                            // admission reentry may already have established it.
                            output?.Commit(value.Utf8SharingHeaderBytes);
                            headerCommitted = true;
                        }
                        fragment = value.CreateReservedUtf8Range(range.Start, range.Capacity, governor!);
                        bytes -= range.Capacity;
                    }
                    else fragment = CreateSplitFragment(source, range, casing, governor, span, context);
                    resultBytes = checked(resultBytes + bytes);
                }
                var destination = scan.Reverse ? count - index - 1 : index;
                if (parts is null) unpacked[destination] = LythonRuntime.ExecutableSlot.FromObject(fragment);
                else parts[destination] = fragment;
                index++;
            }
            var result = parts is null ? null : PyList.FromTransferredArray(parts, governor, span);
            context?.CheckExecution(span);
            output?.Commit(resultBytes);
            return result;
        }
        catch
        {
            // Speculative operands are private. Clear them before refunding the
            // reservation for their uncommitted storage.
            if (writeOperands) unpacked.Clear();
            throw;
        }
    }

    private static PyString CreateSplitFragment(ReadOnlySpan<byte> source,
        (int Start, int End, int Capacity) range, CaseMapping? casing,
        MemoryGovernor? governor, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context)
    {
        if (range.Capacity == 0) return PyString.Empty;
        var fragment = PyString.CreateReservedUtf8(range.Capacity, governor);
        var bytes = fragment.UnpublishedUtf8;
        var written = bytes.Length;
        if (casing is { } mapping)
        {
            // Whitespace is case-stable and breaks final-sigma context.
            written = WriteCaseUtf8(source[range.Start..range.End], bytes, mapping, context, span);
        }
        else
            for (var copied = 0; copied < bytes.Length;)
            {
                context?.CheckExecution(span);
                var chunk = Math.Min(4096, bytes.Length - copied);
                source.Slice(range.Start + copied, chunk).CopyTo(bytes.Slice(copied, chunk));
                copied += chunk;
            }
        fragment.CompleteUnpublishedUtf8(written);
        return fragment;
    }


    private struct WhitespaceRangeState(int maxSplit, CaseMapping? casing)
    {
        internal int Position;
        internal int Written;
        internal int NextCheck;
        internal int Remaining = maxSplit < 0 ? int.MaxValue : maxSplit;
        internal readonly CaseMapping? Mapping = casing;
        internal readonly int MaximumCaseWidth = casing is null ? 0
            : casing == CaseMapping.Lower ? 4 : Math.Max(4, MaxCaseExpansionUtf8Bytes);
    }

    // One forward scanner serves sizing/materialization and native consumption.
    // State carries only positions and case bounds, never a borrowed byte span.
    private static bool TryReadWhitespaceRange(ReadOnlySpan<byte> source, ref WhitespaceRangeState state,
        LythonRuntime.ExecutionContext? context, LythonSourceSpan? span,
        out (int Start, int End, int Capacity) range, Span<byte> encodedBytes = default)
    {
        var outputStart = state.Written;
        var writeCased = !encodedBytes.IsEmpty;
        var start = -1;
        long extraCapacity = 0;
        while (state.Position < source.Length)
        {
            if (state.Position >= state.NextCheck)
            {
                context?.CheckExecution(span);
                state.NextCheck = state.Position + Math.Min(64, source.Length - state.Position);
            }
            var position = state.Position;
            int width;
            Rune rune = default;
            bool whitespace;
            if (source[position] < 0x80)
            {
                whitespace = IsAsciiWhitespace(source[position]);
                width = AsciiWhitespaceRunLength(source[position..state.NextCheck], whitespace);
            }
            else
            {
                Rune.DecodeFromUtf8(source[position..], out rune, out width);
                whitespace = IsPythonWhitespace(rune);
                if (!whitespace && state.MaximumCaseWidth != 0)
                    extraCapacity += state.MaximumCaseWidth - width;
            }
            if (whitespace)
            {
                if (start >= 0)
                {
                    state.Position += width;
                    state.Remaining--;
                    range = writeCased ? (outputStart, state.Written, state.Written - outputStart)
                        : (start, position, state.MaximumCaseWidth == 0 ? position - start
                            : CheckCaseOutputCapacity(position - start + extraCapacity, span));
                    return true;
                }
            }
            else if (start < 0)
            {
                start = position;
                if (state.Remaining == 0)
                {
                    state.Position = source.Length;
                    range = (start, source.Length, source.Length - start);
                    return true;
                }
            }
            if (!whitespace && writeCased)
            {
                var mapping = state.Mapping!.Value;
                if (source[position] < 0x80)
                {
                    var output = encodedBytes.Slice(state.Written, width);
                    var previousWasCased = false;
                    WriteAsciiCase(source.Slice(position, width), output, mapping, position, ref previousWasCased);
                    state.Written += width;
                }
                else
                {
                    var output = encodedBytes.Slice(state.Written, state.MaximumCaseWidth);
                    state.Written += WriteCaseRune(rune, mapping, source, position, width, output, context, span);
                }
            }
            state.Position += width;
        }
        if (start >= 0)
        {
            range = writeCased ? (outputStart, state.Written, state.Written - outputStart)
                : (start, source.Length, state.MaximumCaseWidth == 0 ? source.Length - start
                    : CheckCaseOutputCapacity(source.Length - start + extraCapacity, span));
            return true;
        }
        range = default;
        return false;
    }

}
