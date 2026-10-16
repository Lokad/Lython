using System.Numerics;
using System.Text;
using System.Text.Unicode;
using System.Globalization;

namespace Lokad.Lython.Runtime.Text;

internal static partial class PyStringOps
{
    public static bool IsLower(PyString value)
    {
        var foundCased = false;
        foreach (var rune in value.AsString().EnumerateRunes())
        {
            switch (GetRuneCase(rune))
            {
                case RuneCase.Lower:
                    foundCased = true;
                    break;
                case RuneCase.Upper:
                case RuneCase.Title:
                    return false;
                default:
                    break;
            }
        }

        return foundCased;
    }

    public static bool IsUpper(PyString value)
    {
        var foundCased = false;
        foreach (var rune in value.AsString().EnumerateRunes())
        {
            switch (GetRuneCase(rune))
            {
                case RuneCase.Upper:
                    foundCased = true;
                    break;
                case RuneCase.Lower:
                case RuneCase.Title:
                    return false;
                default:
                    break;
            }
        }

        return foundCased;
    }

    public static PyString Lower(PyString value) => MapCase(value, CaseMapping.Lower);

    public static PyString Upper(PyString value) => MapCase(value, CaseMapping.Upper);

    public static bool IsAlpha(PyString value)
    {
        return CheckAllRunes(value, static rune => Rune.IsLetter(rune));
    }

    public static bool IsAlnum(PyString value)
    {
        return CheckAllRunes(value, static rune =>
        {
            var category = Rune.GetUnicodeCategory(rune);
            return Rune.IsLetter(rune) || category is UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.LetterNumber
                or UnicodeCategory.OtherNumber;
        });
    }

    public static bool IsSpace(PyString value)
    {
        return CheckAllRunes(value, static rune => IsPythonWhitespace(rune));
    }

    public static PyString Capitalize(PyString value)
    {
        var source = value.Utf8Bytes.Span;
        if (source.Length == 0)
        {
            return PyString.Empty;
        }

        var builder = CreateBuilder(value, source.Length);
        try
        {
            var precedingCased = false;
            for (var offset = 0; offset < source.Length;)
            {
                PyStructuralGuard.NoteWork();
                Rune.DecodeFromUtf8(source[offset..], out var rune, out var consumed);
                AppendMappedRune(builder, rune, offset == 0 ? CaseMapping.Title : CaseMapping.Lower,
                    precedingCased, source[(offset + consumed)..]);
                if (!SigmaPropertyContains(rune.Value, SigmaIgnorableRanges))
                    precedingCased = SigmaPropertyContains(rune.Value, SigmaCasedRanges);
                offset += consumed;
            }
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }

    public static PyString SwapCase(PyString value)
    {
        var builder = CreateBuilder(value, value.Utf8Bytes.Length);
        try
        {
            var source = value.Utf8Bytes.Span;
            var precedingCased = false;
            for (var offset = 0; offset < source.Length;)
            {
                PyStructuralGuard.NoteWork();
                Rune.DecodeFromUtf8(source[offset..], out var rune, out var consumed);
                var kind = GetRuneCase(rune);
                if (kind is RuneCase.Upper or RuneCase.Lower)
                    AppendMappedRune(builder, rune, kind == RuneCase.Upper ? CaseMapping.Lower : CaseMapping.Upper,
                        precedingCased, source[(offset + consumed)..]);
                else builder.Append(rune);
                if (!SigmaPropertyContains(rune.Value, SigmaIgnorableRanges))
                    precedingCased = SigmaPropertyContains(rune.Value, SigmaCasedRanges);
                offset += consumed;
            }
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }

    public static PyString Title(PyString value)
    {
        var builder = CreateBuilder(value, value.Utf8Bytes.Length);
        try
        {
            var previousWasCased = false;
            var precedingCased = false;
            var source = value.Utf8Bytes.Span;
            for (var offset = 0; offset < source.Length;)
            {
                PyStructuralGuard.NoteWork();
                Rune.DecodeFromUtf8(source[offset..], out var rune, out var consumed);
                AppendMappedRune(builder, rune, previousWasCased ? CaseMapping.Lower : CaseMapping.Title,
                    precedingCased, source[(offset + consumed)..]);
                previousWasCased = SigmaPropertyContains(rune.Value, SigmaCasedRanges);
                if (!SigmaPropertyContains(rune.Value, SigmaIgnorableRanges))
                    precedingCased = previousWasCased;
                offset += consumed;
            }
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }

    public static PyString Replace(PyString value, PyString oldValue, PyString newValue, int count)
    {
        if (count == 0)
        {
            return value;
        }

        if (count < 0)
        {
            return value.Replace(oldValue, newValue);
        }

        if (oldValue.Utf8Bytes.Length == 0)
        {
            var builder = CreateBuilder(value, value.Utf8Bytes.Length + ((Math.Min(value.Length + 1, count)) * newValue.Utf8Bytes.Length));
            var replacements = 0;
            if (replacements < count)
            {
                builder.Append(newValue);
                replacements++;
            }

            foreach (var rune in value.EnumerateRunes())
            {
                builder.Append(rune);
                if (replacements < count)
                {
                    builder.Append(newValue);
                    replacements++;
                }
            }

            return builder.ToPyStringAndRelease();
        }

        var result = CreateBuilder(value, value.Utf8Bytes.Length);
        var offset = 0;
        var replacementsCount = 0;
        var bytes = value.Utf8Bytes.Span;
        var needle = oldValue.Utf8Bytes.Span;
        while (offset < bytes.Length && replacementsCount < count)
        {
            var found = PyString.IndexOfBytes(bytes[offset..], needle);
            if (found < 0)
            {
                break;
            }

            result.Append(bytes[offset..(offset + found)]);
            result.Append(newValue);
            offset += found + needle.Length;
            replacementsCount++;
        }

        result.Append(bytes[offset..]);
        return result.ToPyStringAndRelease();
    }

}
