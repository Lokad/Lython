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
        if (chars is not null && chars.Utf8Bytes.IsEmpty) return value;

        HashSet<int>? trimRunes = null;
        if (chars is not null)
        {
            trimRunes = new HashSet<int>();
            var characters = chars.Utf8Bytes.Span;
            for (var offset = 0; offset < characters.Length;)
            {
                PyStructuralGuard.NoteWork();
                if (Rune.DecodeFromUtf8(characters[offset..], out var trimRune, out var consumed) != System.Buffers.OperationStatus.Done)
                    throw new DecoderFallbackException("Invalid UTF-8 in strip characters.");
                trimRunes.Add(trimRune.Value);
                offset += consumed;
            }
        }

        var source = value.Utf8Bytes.Span;
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
                value.AllocationSpan ?? PyStructuralGuard.AmbientSpan);
    }

    private static PyString MapCase(PyString value, CaseMapping mapping, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        context ??= PyStructuralGuard.AmbientContext;
        span ??= PyStructuralGuard.AmbientSpan ?? value.AllocationSpan;
        var builder = CreateBuilder(context?.MemoryGovernor ?? value.OwnerMemoryGovernor, span, value.Utf8Bytes.Length);
        try
        {
            var source = value.Utf8Bytes.Span;
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
                    offset += AppendAsciiCasePrefix(builder, source[offset..nextCheck], mapping, offset, ref previousWasCased);
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
                if (selected == CaseMapping.Swap) builder.Append(rune);
                else AppendMappedRune(builder, rune, selected, source, offset, context, span);
                if (mapping == CaseMapping.Title)
                    previousWasCased = SigmaPropertyContains(rune.Value, SigmaCasedRanges);
                offset += consumed;
            }
            context?.CheckExecution(span);
            return CompleteOwnedString(builder.ToPyStringAndRelease(), context, span);
        }
        finally { builder.Release(); }
    }

    private static int AppendAsciiCasePrefix(GovernedByteBuilder builder, ReadOnlySpan<byte> source,
        CaseMapping mapping, int sourceOffset, ref bool previousWasCased)
    {
        // The caller bounds work between checks. This prefix is nonempty;
        // Unicode retains its scalar/context path over the original subject.
        var boundary = source.IndexOfAnyExceptInRange((byte)0, (byte)0x7f);
        var count = boundary < 0 ? source.Length : boundary;
        var input = source[..count];
        var output = builder.GetWriteWindow(count).Span;
        for (var index = 0; index < input.Length; index++)
        {
            var value = input[index];
            var isUpper = value >= (byte)'A' && value <= (byte)'Z';
            var isLower = value >= (byte)'a' && value <= (byte)'z';
            var lower = isUpper ? (byte)(value + 32) : value;
            var upper = isLower ? (byte)(value - 32) : value;
            output[index] = mapping switch
            {
                CaseMapping.Lower => lower,
                CaseMapping.Upper => upper,
                CaseMapping.Title => previousWasCased ? lower : upper,
                CaseMapping.Capitalize => sourceOffset == 0 && index == 0 ? upper : lower,
                CaseMapping.Swap => isUpper ? lower : upper,
                _ => throw new ArgumentOutOfRangeException(nameof(mapping)),
            };
            if (mapping == CaseMapping.Title) previousWasCased = isUpper || isLower;
        }
        builder.Advance(count);
        return count;
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

    private static void AppendMappedRune(
        GovernedByteBuilder builder, Rune rune, CaseMapping mapping,
        ReadOnlySpan<byte> source, int offset, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        if (mapping == CaseMapping.Lower)
        {
            if (rune.Value == 0x0130) builder.AppendString("i\u0307");
            else builder.Append(rune.Value == 0x03A3
                ? new Rune(HasPrecedingSigmaCasedRune(source[..offset], context, span)
                    && !HasFollowingSigmaCasedRune(source[(offset + rune.Utf8SequenceLength)..], context, span) ? 0x03C2 : 0x03C3)
                : PreservePython313Case(rune, Rune.ToLowerInvariant(rune)));
        }
        else if (GetCaseExpansion(rune, mapping) is { } expansion) builder.AppendString(expansion);
        else builder.Append(mapping == CaseMapping.Title ? new Rune(rune.Value switch
        {
            // Unicode's four titlecase digraph triples have distinct title and
            // uppercase scalars. Full multi-scalar mappings stay in the table.
            0x01C4 or 0x01C5 or 0x01C6 => 0x01C5,
            0x01C7 or 0x01C8 or 0x01C9 => 0x01C8,
            0x01CA or 0x01CB or 0x01CC => 0x01CB,
            0x01F1 or 0x01F2 or 0x01F3 => 0x01F2,
            _ => PreservePython313Case(rune, Rune.ToUpperInvariant(rune)).Value,
        }) : PreservePython313Case(rune, Rune.ToUpperInvariant(rune)));
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
        var source = value.Utf8Bytes.Span;
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

        return PadUtf8(value.Utf8Bytes.Span, fillChar.Utf8Bytes.Span, left, padding - left,
            governor, span, cancellationToken);
    }

    // Format precision and width count scalars, independently of UTF-8 byte size.
    // The subject stays borrowed until the single padded/truncated result exists.
    internal static PyString FormatStringValue(PyString value, int? width, int? precision, Rune fill,
        char alignment, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        context.CheckExecution(span);
        if (precision is null && width is null or 0) return value;
        var source = value.Utf8Bytes.Span;
        var end = 0;
        var length = 0;
        var limit = precision ?? int.MaxValue;
        var token = context.State.CancellationToken;
        while (end < source.Length && length < limit)
        {
            if ((length & 1023) == 0) token.ThrowIfCancellationRequested();
            Rune.DecodeFromUtf8(source[end..], out _, out var consumed);
            end += consumed;
            length++;
        }
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

    private static PyString PadUtf8(ReadOnlySpan<byte> value, ReadOnlySpan<byte> fill, int left, int right,
        MemoryGovernor? governor, LythonSourceSpan? span, CancellationToken cancellationToken,
        LythonRuntime.ExecutionContext? context = null)
    {
        var bytes = checked((long)value.Length + ((long)left + right) * fill.Length);
        if (bytes > Array.MaxLength) throw RuntimeErrors.Memory("padded string exceeds supported capacity", span);
        using var storage = governor?.ReserveTemporary(PyString.EstimateApproximateBytes((int)bytes), span);
        context?.CheckExecution(span);
        cancellationToken.ThrowIfCancellationRequested();
        var output = new byte[(int)bytes];
        var leftBytes = checked(left * fill.Length);
        FillPadding(output.AsSpan(0, leftBytes), fill, cancellationToken);
        for (var offset = 0; offset < value.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(4096, value.Length - offset);
            value.Slice(offset, count).CopyTo(output.AsSpan(leftBytes + offset, count));
            offset += count;
        }
        FillPadding(output.AsSpan(leftBytes + value.Length), fill, cancellationToken);
        context?.CheckExecution(span);
        cancellationToken.ThrowIfCancellationRequested();
        var result = governor is null
            ? PyString.FromOwnedUtf8(output)
            : PyString.FromOwnedUtf8(output, governor, span, storage!);
        return CompleteOwnedString(result, context, span, cancellationToken);
    }

    private static void FillPadding(Span<byte> destination, ReadOnlySpan<byte> fill, CancellationToken cancellationToken)
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
        return SliceUtf8(value, startByte, endByte, value.OwnerMemoryGovernor, value.AllocationSpan);
    }

    private static GovernedByteBuilder CreateBuilder(PyString value)
        => CreateBuilder(value, 0);

    private static GovernedByteBuilder CreateBuilder(PyString value, int capacity)
        => value.OwnerMemoryGovernor is null
            ? new GovernedByteBuilder(capacity)
            : new GovernedByteBuilder(value.OwnerMemoryGovernor, value.AllocationSpan, capacity);

    private static GovernedByteBuilder CreateBuilder(MemoryGovernor? governor, LythonSourceSpan? span)
        => CreateBuilder(governor, span, 0);

    private static GovernedByteBuilder CreateBuilder(MemoryGovernor? governor, LythonSourceSpan? span, int capacity)
        => governor is null ? new GovernedByteBuilder(capacity) : new GovernedByteBuilder(governor, span, capacity);

    private static PyString FromString(string text, MemoryGovernor? governor, LythonSourceSpan? span)
        => governor is null ? PyString.FromString(text) : PyString.FromString(text, governor, span);

    private static PyString FromUtf8(byte[] utf8, MemoryGovernor? governor, LythonSourceSpan? span)
        => governor is null ? PyString.FromUtf8(utf8) : PyString.FromUtf8(utf8, governor, span);

    private static PyString SliceUtf8(PyString value, int startByte, int endByte, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (startByte >= endByte)
        {
            return PyString.Empty;
        }

        return governor is null || ReferenceEquals(governor, value.OwnerMemoryGovernor)
            ? value.SliceByByteRange(startByte, endByte)
            : CopyUtf8Slice(value, startByte, endByte, governor, span);
    }

    private static PyString CopyUtf8Slice(PyString value, int startByte, int endByte, MemoryGovernor governor, LythonSourceSpan? span)
    {
        var length = endByte - startByte;
        using var preflight10543 = governor.ReserveTemporary(PyString.EstimateApproximateBytes(length), span);
        var copy = value.Utf8Bytes.Span[startByte..endByte].ToArray();
        return FromUtf8(copy, governor, span);
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
