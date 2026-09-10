using System.Numerics;
using System.Text;
using System.Text.Unicode;
using System.Globalization;

namespace Lokad.Lython.Runtime.Text;

internal static partial class PyStringOps
{
    public static bool IsLower(PyString value)
    {
        return CheckCased(value, expectLower: true);
    }

    public static bool IsUpper(PyString value)
    {
        return CheckCased(value, expectLower: false);
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
        return CheckAllRunes(value, static rune => Rune.IsWhiteSpace(rune));
    }

    public static PyString Capitalize(PyString value)
    {
        var source = value.Utf8Bytes.Span;
        if (source.Length == 0)
        {
            return PyString.Empty;
        }

        var builder = CreateBuilder(value, source.Length);
        var byteIndex = 0;
        var first = true;
        while (byteIndex < source.Length)
        {
            Rune.DecodeFromUtf8(source[byteIndex..], out var rune, out var runeLength);
            builder.AppendString(MapCase(rune, first ? CaseMapping.Title : CaseMapping.Lower));
            first = false;
            byteIndex += runeLength;
        }

        return builder.ToPyStringAndRelease();
    }

    public static PyString SwapCase(PyString value)
    {
        var builder = CreateBuilder(value, value.Utf8Bytes.Length);
        var source = value.Utf8Bytes.Span;
        for (var byteIndex = 0; byteIndex < source.Length;)
        {
            Rune.DecodeFromUtf8(source[byteIndex..], out var rune, out var runeLength);
            var text = rune.ToString();
            var lower = MapCase(rune, CaseMapping.Lower);
            var upper = MapCase(rune, CaseMapping.Upper);
            builder.AppendString(text == lower && lower != upper ? upper : text == upper && lower != upper ? lower : text);
            byteIndex += runeLength;
        }

        return builder.ToPyStringAndRelease();
    }

    public static PyString Title(PyString value)
    {
        var builder = CreateBuilder(value, value.Utf8Bytes.Length);
        var previousWasCased = false;
        var source = value.Utf8Bytes.Span;
        for (var byteIndex = 0; byteIndex < source.Length;)
        {
            Rune.DecodeFromUtf8(source[byteIndex..], out var rune, out var runeLength);
            var text = rune.ToString();
            var lower = MapCase(rune, CaseMapping.Lower);
            var upper = MapCase(rune, CaseMapping.Upper);
            var isCased = lower != upper;
            if (!isCased)
            {
                builder.AppendString(text);
                previousWasCased = false;
            }
            else
            {
                builder.AppendString(previousWasCased ? lower : MapCase(rune, CaseMapping.Title));
                previousWasCased = true;
            }

            byteIndex += runeLength;
        }

        return builder.ToPyStringAndRelease();
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
