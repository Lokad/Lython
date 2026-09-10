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
        if (normalizedEnd < normalizedStart)
        {
            normalizedEnd = normalizedStart;
        }

        return new NormalizedStringRange(normalizedStart, normalizedEnd);
    }

    private static PyString SliceTrimmed(PyString value, bool trimStart, bool trimEnd)
        => SliceTrimmed(value, trimStart, trimEnd, null);

    private static PyString SliceTrimmed(PyString value, bool trimStart, bool trimEnd, PyString? chars)
    {
        HashSet<PyString>? trimChars = chars is null ? null : new HashSet<PyString>(chars.EnumerateRunes());
        var source = value.Utf8Bytes.Span;
        var startByte = 0;
        var endByte = source.Length;

        if (trimStart)
        {
            while (startByte < endByte)
            {
                Rune.DecodeFromUtf8(source[startByte..], out _, out var runeLength);
                var rune = SliceUtf8(value, startByte, startByte + runeLength, value.OwnerMemoryGovernor, value.AllocationSpan);
                if (!ShouldTrim(rune, trimChars))
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
                var runeStart = GetPreviousRuneStart(source, endByte);
                var rune = SliceUtf8(value, runeStart, endByte, value.OwnerMemoryGovernor, value.AllocationSpan);
                if (!ShouldTrim(rune, trimChars))
                {
                    break;
                }

                endByte = runeStart;
            }
        }

        return SliceUtf8(value, startByte, endByte, value.OwnerMemoryGovernor, value.AllocationSpan);
    }

    private static PyString MapCase(PyString value, CaseMapping mapping)
    {
        var builder = CreateBuilder(value, value.Utf8Bytes.Length);
        foreach (var rune in value.AsString().EnumerateRunes())
        {
            builder.AppendString(MapCase(rune, mapping));
        }

        return builder.ToPyStringAndRelease();
    }

    private static string MapCase(Rune rune, CaseMapping mapping)
    {
        if (mapping == CaseMapping.Lower && rune.Value == 0x0130)
        {
            return "i\u0307";
        }

        if (mapping is CaseMapping.Upper or CaseMapping.Title)
        {
            var special = (rune.Value, mapping) switch
            {
                (0x00DF, CaseMapping.Upper) => "SS",
                (0x00DF, CaseMapping.Title) => "Ss",
                (0x0149, _) => "\u02BCN",
                (0x01F0, _) => "J\u030C",
                (0x0390, _) => "\u0399\u0308\u0301",
                (0x03B0, _) => "\u03A5\u0308\u0301",
                (0x0587, CaseMapping.Upper) => "\u0535\u0552",
                (0x0587, CaseMapping.Title) => "\u0535\u0582",
                (0xFB00, CaseMapping.Upper) => "FF",
                (0xFB00, CaseMapping.Title) => "Ff",
                (0xFB01, CaseMapping.Upper) => "FI",
                (0xFB01, CaseMapping.Title) => "Fi",
                (0xFB02, CaseMapping.Upper) => "FL",
                (0xFB02, CaseMapping.Title) => "Fl",
                (0xFB03, CaseMapping.Upper) => "FFI",
                (0xFB03, CaseMapping.Title) => "Ffi",
                (0xFB04, CaseMapping.Upper) => "FFL",
                (0xFB04, CaseMapping.Title) => "Ffl",
                (0xFB05 or 0xFB06, CaseMapping.Upper) => "ST",
                (0xFB05 or 0xFB06, CaseMapping.Title) => "St",
                (0xFB13, CaseMapping.Upper) => "\u0544\u0546",
                (0xFB13, CaseMapping.Title) => "\u0544\u0576",
                (0xFB14, CaseMapping.Upper) => "\u0544\u0535",
                (0xFB14, CaseMapping.Title) => "\u0544\u0565",
                (0xFB15, CaseMapping.Upper) => "\u0544\u053B",
                (0xFB15, CaseMapping.Title) => "\u0544\u056B",
                (0xFB16, CaseMapping.Upper) => "\u054E\u0546",
                (0xFB16, CaseMapping.Title) => "\u054E\u0576",
                (0xFB17, CaseMapping.Upper) => "\u0544\u053D",
                (0xFB17, CaseMapping.Title) => "\u0544\u056D",
                _ => null
            };
            if (special is not null)
            {
                return special;
            }
        }

        var text = rune.ToString();
        return mapping switch
        {
            CaseMapping.Lower => text.ToLowerInvariant(),
            CaseMapping.Upper => text.ToUpperInvariant(),
            CaseMapping.Title => text.ToUpperInvariant(),
            _ => text
        };
    }

    private static bool CheckAllRunes(PyString value, Func<Rune, bool> predicate)
    {
        var sawRune = false;
        var source = value.Utf8Bytes.Span;
        for (var byteIndex = 0; byteIndex < source.Length;)
        {
            Rune.DecodeFromUtf8(source[byteIndex..], out var rune, out var runeLength);
            sawRune = true;
            if (!predicate(rune))
            {
                return false;
            }

            byteIndex += runeLength;
        }

        return sawRune;
    }

    private static bool ShouldTrim(PyString rune, HashSet<PyString>? trimChars)
    {
        return trimChars is null
            ? Rune.IsWhiteSpace(DecodeSingleRune(rune))
            : trimChars.Contains(rune);
    }

    private static int LastIndexOfBytes(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
        => haystack.LastIndexOf(needle);

    private static int LastFind(PyString value, PyString needle)
    {
        var byteIndex = LastIndexOfBytes(value.Utf8Bytes.Span, needle.Utf8Bytes.Span);
        if (byteIndex < 0)
        {
            return -1;
        }

        if (needle.Length == 0)
        {
            return value.Length;
        }

        return value.ByteIndexToRuneIndex(byteIndex);
    }

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

    private static PyString Pad(PyString value, int width, PyString fillChar, PadMode mode)
    {
        if (fillChar.Length != 1)
        {
            throw new InvalidOperationException("The fill character must be exactly one character long");
        }

        var padding = width - value.Length;
        if (padding <= 0)
        {
            return value;
        }

        var left = mode switch
        {
            PadMode.Left => 0,
            PadMode.Right => padding,
            PadMode.Center => padding / 2,
            _ => 0
        };

        var right = padding - left;
        var builder = CreateBuilder(value);
        AppendRepeated(builder, fillChar, left);
        builder.Append(value);
        AppendRepeated(builder, fillChar, right);
        return builder.ToPyStringAndRelease();
    }

    private static void AppendRepeated(GovernedByteBuilder builder, PyString value, int count)
    {
        for (var i = 0; i < count; i++)
        {
            builder.Append(value);
        }
    }

    private static PyString SliceByByteRange(PyString value, int startByte, int endByte)
    {
        return SliceUtf8(value, startByte, endByte, value.OwnerMemoryGovernor, value.AllocationSpan);
    }

    private static Rune DecodeSingleRune(PyString rune)
    {
        Rune.DecodeFromUtf8(rune.Utf8Bytes.Span, out var decoded, out _);
        return decoded;
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
        governor.EnsureCanReserve(PyString.EstimateApproximateBytes(length), span);
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

    private static bool TryGetLineBreakByteLength(ReadOnlySpan<byte> source, int byteIndex, out int lineBreakLength)
    {
        var current = source[byteIndex];
        switch (current)
        {
            case (byte)'\n':
            case (byte)'\v':
            case (byte)'\f':
                lineBreakLength = 1;
                return true;
            case (byte)'\r':
                lineBreakLength = byteIndex + 1 < source.Length && source[byteIndex + 1] == (byte)'\n' ? 2 : 1;
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
