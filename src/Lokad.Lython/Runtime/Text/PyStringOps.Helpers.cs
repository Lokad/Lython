using System.Numerics;
using System.Text;
using System.Text.Unicode;
using System.Globalization;

namespace Lokad.Lython.Runtime.Text;

internal readonly record struct NormalizedStringRange(int Start, int End);

internal static partial class PyStringOps
{
    internal static NormalizedStringRange NormalizeRange(int length, object? start, object? end)
    {
        var normalizedStart = NormalizeBound(start, length, defaultValue: 0);
        var normalizedEnd = NormalizeBound(end, length, defaultValue: length);

        // Empty ranges stay empty: callers treat end below start as a miss.
        return new NormalizedStringRange(normalizedStart, normalizedEnd);
    }

    private static PyString SliceTrimmed(PyString value, bool trimStart, bool trimEnd)
        => SliceTrimmed(value, trimStart, trimEnd, null);

    // MG06: trimming compares runes directly over the UTF-8 span and slices
    // once. Slicing a governed rune string per boundary character committed
    // durable scratch beside the result on padding-heavy inputs.
    private static PyString SliceTrimmed(PyString value, bool trimStart, bool trimEnd, PyString? chars)
    {
        if (chars is not null && chars.Utf8Span.IsEmpty) return value;

        HashSet<int>? trimRunes = null;
        if (chars is not null)
        {
            trimRunes = new HashSet<int>();
            var characters = chars.Utf8Span;
            for (var offset = 0; offset < characters.Length;)
            {
                PyStructuralGuard.NoteWork();
                if (Rune.DecodeFromUtf8(characters[offset..], out var trimRune, out var consumed) != System.Buffers.OperationStatus.Done)
                    throw new DecoderFallbackException("Invalid UTF-8 in strip characters.");
                trimRunes.Add(trimRune.Value);
                offset += consumed;
            }
        }

        var source = value.Utf8Span;
        var startByte = 0;
        var endByte = source.Length;

        if (trimStart)
        {
            while (startByte < endByte)
            {
                PyStructuralGuard.NoteWork();
                Rune.DecodeFromUtf8(source[startByte..], out var rune, out var runeLength);
                if (!ShouldTrim(rune, trimRunes))
                {
                    break;
                }

                startByte += runeLength;
            }
        }

        if (trimEnd)
        {
            while (endByte > startByte)
            {
                PyStructuralGuard.NoteWork();
                var runeStart = GetPreviousRuneStart(source, endByte);
                Rune.DecodeFromUtf8(source[runeStart..endByte], out var rune, out var _);
                if (!ShouldTrim(rune, trimRunes))
                {
                    break;
                }

                endByte = runeStart;
            }
        }

        // Shared constants have no individual governor. Admit a fresh slice
        // through the active execution rather than copying it unowned first.
        return startByte == 0 && endByte == source.Length
            ? value
            : SliceUtf8(value, startByte, endByte,
                value.OwnerMemoryGovernor ?? PyStructuralGuard.AmbientContext?.MemoryGovernor,
                PyStructuralGuard.AmbientSpan);
    }

    private static PyString MapCase(PyString value, CaseMapping mapping, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        context ??= PyStructuralGuard.AmbientContext;
        span ??= PyStructuralGuard.AmbientSpan;
        var source = value.Utf8Span;
        var capacity = CaseOutputCapacity(source, mapping, context, span);
        context?.CheckExecution(span);
        if (capacity == 0) return PyString.Empty;
        var governor = context?.MemoryGovernor ?? value.OwnerMemoryGovernor;
        var uncommittedBytes = PyString.EstimateApproximateBytes(capacity);
        governor?.Reserve(uncommittedBytes, span);
        try
        {
            // Admission can itself cancel the command. Keep the input funded and
            // check before allocating the one, conservative final backing.
            context?.CheckExecution(span);
            var output = PyString.CreateReservedUtf8(capacity, governor);
            var written = WriteCaseUtf8(source, output.UnpublishedUtf8, mapping, context, span);
            output.CompleteUnpublishedUtf8(written);
            context?.CheckExecution(span);
            governor?.Commit(uncommittedBytes);
            uncommittedBytes = 0;
            return output;
        }
        finally { if (uncommittedBytes != 0) governor?.ReleaseReserved(uncommittedBytes); }
    }

    private static int CaseOutputCapacity(ReadOnlySpan<byte> source, CaseMapping mapping,
        LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        // ASCII keeps its width. Non-ASCII needs at most one managed Rune or a
        // generated full expansion; lower's only expansion fits one Rune's width.
        // Inspect bytes only, leaving all Unicode mapping/context work to one pass.
        var maximum = mapping == CaseMapping.Lower ? 4 : Math.Max(4, MaxCaseExpansionUtf8Bytes);
        long capacity = source.Length;
        for (var offset = 0; offset < source.Length;)
        {
            context?.CheckExecution(span);
            var end = offset + Math.Min(4096, source.Length - offset);
            while (offset < end)
            {
                var prefix = source[offset..end].IndexOfAnyExceptInRange((byte)0, (byte)0x7f);
                if (prefix < 0) { offset = end; break; }
                offset += prefix;
                var lead = source[offset];
                var width = lead < 0xe0 ? 2 : lead < 0xf0 ? 3 : 4;
                capacity += maximum - width;
                offset += width;
            }
        }
        return CheckCaseOutputCapacity(capacity, span);
    }

    private static int CheckCaseOutputCapacity(long capacity, LythonSourceSpan? span)
    {
        if (capacity > Array.MaxLength) throw RuntimeErrors.Memory("cased string exceeds supported capacity", span);
        return (int)capacity;
    }

    private static int WriteCaseUtf8(ReadOnlySpan<byte> source, Span<byte> output, CaseMapping mapping,
        LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        var written = 0;
        var previousWasCased = false;
        var nextCheck = 0;
        for (var offset = 0; offset < source.Length;)
        {
            if (offset >= nextCheck)
            {
                context?.CheckExecution(span);
                nextCheck = offset + Math.Min(4096, source.Length - offset);
            }
            if (source[offset] < 0x80)
            {
                var prefix = source[offset..nextCheck];
                var boundary = prefix.IndexOfAnyExceptInRange((byte)0, (byte)0x7f);
                if (boundary >= 0) prefix = prefix[..boundary];
                WriteAsciiCase(prefix, output.Slice(written, prefix.Length), mapping, offset, ref previousWasCased);
                written = checked(written + prefix.Length);
                offset += prefix.Length;
                continue;
            }
            _ = Rune.DecodeFromUtf8(source[offset..], out var rune, out var consumed);
            var selected = mapping switch
            {
                CaseMapping.Capitalize => offset == 0 ? CaseMapping.Title : CaseMapping.Lower,
                CaseMapping.Title => previousWasCased ? CaseMapping.Lower : CaseMapping.Title,
                CaseMapping.Swap => GetRuneCase(rune) switch
                {
                    RuneCase.Upper => CaseMapping.Lower,
                    RuneCase.Lower => CaseMapping.Upper,
                    _ => CaseMapping.Swap,
                },
                _ => mapping,
            };
            var mapped = MapCaseRune(rune, selected, source, offset, context, span, out var expansion);
            var count = expansion.IsEmpty ? mapped.Utf8SequenceLength : expansion.Length;
            if (expansion.IsEmpty) mapped.EncodeToUtf8(output[written..]);
            else expansion.CopyTo(output[written..]);
            written = checked(written + count);
            if (mapping == CaseMapping.Title)
                previousWasCased = SigmaPropertyContains(rune.Value, SigmaCasedRanges);
            offset += consumed;
        }
        return written;
    }

    private static void WriteAsciiCase(ReadOnlySpan<byte> input, Span<byte> output,
        CaseMapping mapping, int sourceOffset, ref bool previousWasCased)
    {
        if (mapping == CaseMapping.Lower) Ascii.ToLower(input, output, out _);
        else if (mapping == CaseMapping.Upper) Ascii.ToUpper(input, output, out _);
        else for (var index = 0; index < input.Length; index++)
        {
            var value = input[index];
            var isUpper = value >= (byte)'A' && value <= (byte)'Z';
            var isLower = value >= (byte)'a' && value <= (byte)'z';
            var lower = isUpper ? (byte)(value + 32) : value;
            var upper = isLower ? (byte)(value - 32) : value;
            output[index] = mapping switch
            {
                CaseMapping.Title => previousWasCased ? lower : upper,
                CaseMapping.Capitalize => sourceOffset == 0 && index == 0 ? upper : lower,
                CaseMapping.Swap => isUpper ? lower : upper,
                _ => throw new ArgumentOutOfRangeException(nameof(mapping)),
            };
            if (mapping == CaseMapping.Title) previousWasCased = isUpper || isLower;
        }
    }

    private static PyString CompleteOwnedString(PyString result, LythonRuntime.ExecutionContext? context,
        LythonSourceSpan? span, CancellationToken cancellationToken = default)
    {
        try { context?.CheckExecution(span); cancellationToken.ThrowIfCancellationRequested(); return result; }
        catch
        {
            // The final allocation can itself trigger host cancellation. This
            // output has no user yet; input aliases retain their own funding.
            result.OwnerMemoryGovernor?.RefundUnpublishedValue(result);
            throw;
        }
    }

    private static Rune MapCaseRune(Rune rune, CaseMapping mapping,
        ReadOnlySpan<byte> source, int offset, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span,
        out ReadOnlySpan<byte> expansion)
    {
        expansion = default;
        if (mapping == CaseMapping.Swap) return rune;
        if (mapping == CaseMapping.Lower)
        {
            if (rune.Value == 0x0130) { expansion = "i\u0307"u8; return rune; }
            return rune.Value == 0x03A3
                ? new Rune(HasPrecedingSigmaCasedRune(source[..offset], context, span)
                    && !HasFollowingSigmaCasedRune(source[(offset + rune.Utf8SequenceLength)..], context, span) ? 0x03C2 : 0x03C3)
                : PreservePython313Case(rune, Rune.ToLowerInvariant(rune));
        }
        expansion = GetCaseExpansion(rune, mapping);
        if (!expansion.IsEmpty) return rune;
        return mapping == CaseMapping.Title ? new Rune(rune.Value switch
        {
            // Unicode's four titlecase digraph triples have distinct title and
            // uppercase scalars. Full multi-scalar mappings stay in the table.
            0x01C4 or 0x01C5 or 0x01C6 => 0x01C5,
            0x01C7 or 0x01C8 or 0x01C9 => 0x01C8,
            0x01CA or 0x01CB or 0x01CC => 0x01CB,
            0x01F1 or 0x01F2 or 0x01F3 => 0x01F2,
            _ => PreservePython313Case(rune, Rune.ToUpperInvariant(rune)).Value,
        }) : PreservePython313Case(rune, Rune.ToUpperInvariant(rune));
    }

    private static Rune PreservePython313Case(Rune original, Rune mapped)
    {
        // Platform Unicode updates can add pairs involving characters that
        // Python 3.13 leaves unchanged, including an already-assigned partner.
        return original != mapped && (IsUnassignedInPython313(original.Value) || IsUnassignedInPython313(mapped.Value))
            ? original : mapped;
    }

    private static bool CheckAllRunes(PyString value, Func<Rune, bool> predicate,
        LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        var sawRune = false;
        var source = value.Utf8Span;
        context ??= PyStructuralGuard.AmbientContext;
        span ??= PyStructuralGuard.AmbientSpan;
        for (var byteIndex = 0; byteIndex < source.Length;)
        {
            context?.CheckExecution(span);
            // Keep the hot scalar loop free of cancellation bookkeeping while
            // checking the current execution before every small byte chunk.
            var end = byteIndex + Math.Min(64, source.Length - byteIndex);
            while (byteIndex < end)
            {
                Rune.DecodeFromUtf8(source[byteIndex..], out var rune, out var runeLength);
                sawRune = true;
                if (!predicate(rune))
                {
                    return false;
                }

                byteIndex += runeLength;
            }
        }

        return sawRune;
    }

    private static bool ShouldTrim(Rune rune, HashSet<int>? trimRunes)
    {
        return trimRunes is null
            ? IsPythonWhitespace(rune)
            : trimRunes.Contains(rune.Value);
    }

    internal static int LastIndexOfBytes(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
        => haystack.LastIndexOf(needle);

    private static PyString SliceRange(PyString value, int start, int end)
    {
        if (start >= end)
        {
            return PyString.Empty;
        }

        var startByte = value.GetByteIndexForRuneBoundary(start);
        var endByte = value.GetByteIndexForRuneBoundary(end);
        return value.SliceByByteRange(startByte, endByte);
    }

    private static int NormalizeBound(object? bound, int length, int defaultValue)
    {
        if (bound is null || ReferenceEquals(bound, PyNone.Instance))
        {
            return defaultValue;
        }

        BigInteger integer;
        if (bound is bool flag)
        {
            integer = flag ? BigInteger.One : BigInteger.Zero;
        }
        else if (bound is not BigInteger big)
        {
            throw new InvalidOperationException("slice bounds must be integers or None");
        }
        else
        {
            integer = big;
        }

        if (integer < int.MinValue)
        {
            return 0;
        }

        if (integer > int.MaxValue)
        {
            return length;
        }

        var value = (int)integer;
        if (value < 0)
        {
            value += length;
        }

        if (value < 0)
        {
            return 0;
        }

        return value > length ? length : value;
    }

    private static PyString Pad(PyString value, int width, PyString fillChar, PadMode mode,
        MemoryGovernor? governor, LythonSourceSpan? span, CancellationToken cancellationToken)
    {
        if (fillChar.Length != 1)
        {
            throw new InvalidOperationException("The fill character must be exactly one character long");
        }

        if (width <= value.Length) return value;
        cancellationToken.ThrowIfCancellationRequested();
        var padding = width - value.Length;
        var left = mode switch
        {
            PadMode.Left => 0,
            PadMode.Right => padding,
            PadMode.Center => padding / 2 + ((padding & width & 1) == 1 ? 1 : 0),
            _ => 0
        };

        return PadUtf8(value.Utf8Span, fillChar.Utf8Span, left, padding - left,
            governor, span, cancellationToken);
    }

    // Format precision and width count scalars, independently of UTF-8 byte size.
    // The subject stays borrowed until the single padded/truncated result exists.
    internal static PyString FormatStringValue(PyString value, int? width, int? precision, Rune fill,
        char alignment, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        context.CheckExecution(span);
        if (precision is null && width is null or 0) return value;
        var source = value.Utf8Span;
        var token = context.State.CancellationToken;
        var (end, length) = FormattedStringBounds(source, precision, token);
        var padding = Math.Max(0, (width ?? 0) - length);
        if (padding == 0)
        {
            if (end == source.Length) return value;
            return CompleteOwnedString(PyString.CopyFromUtf8(source[..end], context.MemoryGovernor, span), context, span, token);
        }

        // Python format ^ places the odd extra fill on the right. str.center's
        // parity rule above is intentionally different.
        var left = alignment switch { '<' => 0, '>' => padding, '^' => padding / 2, _ => throw new InvalidOperationException("Invalid string alignment.") };
        Span<byte> fillBytes = stackalloc byte[4];
        var fillLength = fill.EncodeToUtf8(fillBytes);
        return PadUtf8(source[..end], fillBytes[..fillLength], left, padding - left,
            context.MemoryGovernor, span, token, context);
    }

    internal static void AppendFormattedStringValue(GovernedByteBuilder output, PyString value, int? width,
        int? precision, Rune fill, char alignment, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        context.CheckExecution(span);
        if (precision is null && width is null or 0) { output.Append(value); return; }
        var source = value.Utf8Span;
        var token = context.State.CancellationToken;
        var (end, length) = FormattedStringBounds(source, precision, token);
        var padding = Math.Max(0, (width ?? 0) - length);
        var left = alignment switch { '<' => 0, '>' => padding, '^' => padding / 2, _ => throw new InvalidOperationException("Invalid string alignment.") };
        Span<byte> fillBytes = stackalloc byte[4];
        var fillLength = fill.EncodeToUtf8(fillBytes);
        var bytes = checked((long)end + (long)padding * fillLength);
        if (bytes > Array.MaxLength) throw RuntimeErrors.Memory("padded string exceeds supported capacity", span);
        var destination = output.GetWriteWindow((int)bytes).Span;
        WritePaddedUtf8(destination, source[..end], fillBytes[..fillLength], left, token);
        context.CheckExecution(span);
        output.Advance((int)bytes);
    }

    private static (int End, int Length) FormattedStringBounds(ReadOnlySpan<byte> source, int? precision, CancellationToken token)
    {
        var end = 0;
        var length = 0;
        var limit = precision ?? int.MaxValue;
        while (end < source.Length && length < limit)
        {
            if ((length & 1023) == 0) token.ThrowIfCancellationRequested();
            Rune.DecodeFromUtf8(source[end..], out _, out var consumed);
            end += consumed;
            length++;
        }
        return (end, length);
    }

    private static PyString PadUtf8(ReadOnlySpan<byte> value, ReadOnlySpan<byte> fill, int left, int right,
        MemoryGovernor? governor, LythonSourceSpan? span, CancellationToken cancellationToken,
        LythonRuntime.ExecutionContext? context = null)
    {
        var bytes = checked((long)value.Length + ((long)left + right) * fill.Length);
        if (bytes > Array.MaxLength) throw RuntimeErrors.Memory("padded string exceeds supported capacity", span);
        var uncommittedBytes = PyString.EstimateApproximateBytes((int)bytes);
        governor?.Reserve(uncommittedBytes, span);
        try
        {
            context?.CheckExecution(span);
            cancellationToken.ThrowIfCancellationRequested();
            var output = new byte[(int)bytes];
            WritePaddedUtf8(output, value, fill, left, cancellationToken);
            context?.CheckExecution(span);
            cancellationToken.ThrowIfCancellationRequested();
            var result = PyString.FromReservedUtf8(output, output.Length, governor, span);
            uncommittedBytes = 0;
            return CompleteOwnedString(result, context, span, cancellationToken);
        }
        finally { if (uncommittedBytes != 0) governor?.ReleaseReserved(uncommittedBytes); }
    }

    private static void WritePaddedUtf8(Span<byte> output, ReadOnlySpan<byte> value, ReadOnlySpan<byte> fill,
        int left, CancellationToken cancellationToken)
    {
        var leftBytes = checked(left * fill.Length);
        FillPadding(output[..leftBytes], fill, cancellationToken);
        for (var offset = 0; offset < value.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(4096, value.Length - offset);
            value.Slice(offset, count).CopyTo(output.Slice(leftBytes + offset, count));
            offset += count;
        }
        FillPadding(output[(leftBytes + value.Length)..], fill, cancellationToken);
    }

    internal static void FillPadding(Span<byte> destination, ReadOnlySpan<byte> fill, CancellationToken cancellationToken)
    {
        if (fill.Length == 1)
        {
            for (var offset = 0; offset < destination.Length;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(4096, destination.Length - offset);
                destination.Slice(offset, count).Fill(fill[0]);
                offset += count;
            }
            return;
        }
        for (var offset = 0; offset < destination.Length; offset += fill.Length)
        {
            if ((offset & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
            fill.CopyTo(destination[offset..]);
        }
    }

    private static PyString SliceByByteRange(PyString value, int startByte, int endByte)
    {
        return SliceUtf8(value, startByte, endByte, value.OwnerMemoryGovernor, PyStructuralGuard.AmbientSpan);
    }

    private static GovernedByteBuilder CreateBuilder(PyString value)
        => CreateBuilder(value, 0);

    private static GovernedByteBuilder CreateBuilder(PyString value, int capacity)
        => value.OwnerMemoryGovernor is null
            ? new GovernedByteBuilder(capacity)
            : new GovernedByteBuilder(value.OwnerMemoryGovernor, PyStructuralGuard.AmbientSpan, capacity);

    private static GovernedByteBuilder CreateBuilder(MemoryGovernor? governor, LythonSourceSpan? span)
        => CreateBuilder(governor, span, 0);

    private static GovernedByteBuilder CreateBuilder(MemoryGovernor? governor, LythonSourceSpan? span, int capacity)
        => governor is null ? new GovernedByteBuilder(capacity) : new GovernedByteBuilder(governor, span, capacity);

    private static PyString FromString(string text, MemoryGovernor? governor, LythonSourceSpan? span)
        => governor is null ? PyString.FromString(text) : PyString.FromString(text, governor, span);

    private static PyString SliceUtf8(PyString value, int startByte, int endByte, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (startByte >= endByte)
        {
            return PyString.Empty;
        }

        return governor is null || ReferenceEquals(governor, value.OwnerMemoryGovernor)
            ? value.SliceByByteRange(startByte, endByte)
            : PyString.CopyFromUtf8(value.Utf8Span[startByte..endByte], governor, span);
    }

    private static int GetPreviousRuneStart(ReadOnlySpan<byte> source, int endByte)
    {
        var byteIndex = endByte - 1;
        while (byteIndex > 0 && (source[byteIndex] & 0b1100_0000) == 0b1000_0000)
        {
            byteIndex--;
        }

        return byteIndex;
    }

    internal static bool TryGetLineBreakByteLength(ReadOnlySpan<byte> source, int byteIndex, out int lineBreakLength)
    {
        var current = source[byteIndex];
        switch (current)
        {
            case (byte)'\n':
            case (byte)'\v':
            case (byte)'\f':
            case 0x1C:
            case 0x1D:
            case 0x1E:
                lineBreakLength = 1;
                return true;
            case (byte)'\r':
                lineBreakLength = byteIndex + 1 < source.Length && source[byteIndex + 1] == (byte)'\n' ? 2 : 1;
                return true;
            case 0xC2 when byteIndex + 1 < source.Length && source[byteIndex + 1] == 0x85:
                lineBreakLength = 2;
                return true;
            case 0xE2 when byteIndex + 2 < source.Length && source[byteIndex + 1] == 0x80 &&
                           (source[byteIndex + 2] == 0xA8 || source[byteIndex + 2] == 0xA9):
                lineBreakLength = 3;
                return true;
            default:
                lineBreakLength = 0;
                return false;
        }
    }

    private enum PadMode
    {
        Left,
        Right,
        Center
    }

    private enum CaseMapping
    {
        Lower,
        Upper,
        Title,
        Capitalize,
        Swap
    }
}
