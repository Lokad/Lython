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

    private static PyString MapCase(PyString value, CaseMapping mapping)
    {
        var builder = CreateBuilder(value, value.Utf8Bytes.Length);
        try
        {
            var source = value.Utf8Bytes.Span;
            var precedingCased = false;
            for (var offset = 0; offset < source.Length;)
            {
                PyStructuralGuard.NoteWork();
                _ = Rune.DecodeFromUtf8(source[offset..], out var rune, out var consumed);
                AppendMappedRune(builder, rune, mapping, precedingCased, source[(offset + consumed)..]);
                if (mapping == CaseMapping.Lower && !SigmaPropertyContains(rune.Value, SigmaIgnorableRanges))
                    precedingCased = SigmaPropertyContains(rune.Value, SigmaCasedRanges);
                offset += consumed;
            }
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }

    private static void AppendMappedRune(
        GovernedByteBuilder builder, Rune rune, CaseMapping mapping,
        bool precedingCased, ReadOnlySpan<byte> following)
    {
        if (mapping == CaseMapping.Lower)
        {
            if (rune.Value == 0x0130) builder.AppendString("i\u0307");
            else builder.Append(rune.Value == 0x03A3
                ? new Rune(precedingCased && !HasFollowingSigmaCasedRune(following) ? 0x03C2 : 0x03C3)
                : Rune.ToLowerInvariant(rune));
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
            _ => Rune.ToUpperInvariant(rune).Value,
        }) : Rune.ToUpperInvariant(rune));
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

        var right = padding - left;
        var bytes = checked((long)value.Utf8Bytes.Length + (long)padding * fillChar.Utf8Bytes.Length);
        if (bytes > int.MaxValue) throw RuntimeErrors.Memory("padded string exceeds supported capacity", span);
        var builder = CreateBuilder(governor, span, (int)bytes);
        try
        {
            AppendRepeated(builder, fillChar, left, cancellationToken);
            builder.Append(value);
            AppendRepeated(builder, fillChar, right, cancellationToken);
            return builder.ToPyString();
        }
        finally { builder.Release(); }
    }

    private static void AppendRepeated(GovernedByteBuilder builder, PyString value, int count, CancellationToken cancellationToken)
    {
        for (var i = 0; i < count; i++)
        {
            if ((i & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
            builder.Append(value);
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
        Title
    }
}
