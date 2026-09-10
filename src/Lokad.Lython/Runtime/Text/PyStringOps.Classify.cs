using System.Globalization;
using System.Text;

namespace Lokad.Lython.Runtime.Text;

internal static partial class PyStringOps
{
    // Exception tables for Unicode binary properties with no BCL equivalent,
    // derived empirically (CPython 3.13 predicates versus .NET categories over
    // BMP plus astral planes 1-3, so reviewer diffs stay mechanical). Category
    // rules stay exact; version drift in either runtime can only add members
    // to these sets, never invalidate the rules.
    // Other_Uppercase (roman numerals, enclosed capitals): 120 code points in 5 ranges.
    private static readonly (int Lo, int Hi)[] UpperExceptionRanges = [
        (0x2160, 0x216F),
        (0x24B6, 0x24CF),
        (0x1F130, 0x1F149),
        (0x1F150, 0x1F169),
        (0x1F170, 0x1F189),
    ];
    // Other_Lowercase (modifier letters,ª/º): 311 code points in 28 ranges.
    private static readonly (int Lo, int Hi)[] LowerExceptionRanges = [
        (0xAA, 0xAA),
        (0xBA, 0xBA),
        (0x2B0, 0x2B8),
        (0x2C0, 0x2C1),
        (0x2E0, 0x2E4),
        (0x345, 0x345),
        (0x37A, 0x37A),
        (0x10FC, 0x10FC),
        (0x1D2C, 0x1D6A),
        (0x1D78, 0x1D78),
        (0x1D9B, 0x1DBF),
        (0x2071, 0x2071),
        (0x207F, 0x207F),
        (0x2090, 0x209C),
        (0x2170, 0x217F),
        (0x24D0, 0x24E9),
        (0x2C7C, 0x2C7D),
        (0xA69C, 0xA69D),
        (0xA770, 0xA770),
        (0xA7F2, 0xA7F4),
        (0xA7F8, 0xA7F9),
        (0xAB5C, 0xAB5F),
        (0xAB69, 0xAB69),
        (0x10780, 0x10780),
        (0x10783, 0x10785),
        (0x10787, 0x107B0),
        (0x107B2, 0x107BA),
        (0x1E030, 0x1E06D),
    ];
    // Numeric_Type=Digit non-decimals (superscripts, enclosed): 128 code points in 20 ranges.
    private static readonly (int Lo, int Hi)[] DigitExceptionRanges = [
        (0xB2, 0xB3),
        (0xB9, 0xB9),
        (0x1369, 0x1371),
        (0x19DA, 0x19DA),
        (0x2070, 0x2070),
        (0x2074, 0x2079),
        (0x2080, 0x2089),
        (0x2460, 0x2468),
        (0x2474, 0x247C),
        (0x2488, 0x2490),
        (0x24EA, 0x24EA),
        (0x24F5, 0x24FD),
        (0x24FF, 0x24FF),
        (0x2776, 0x277E),
        (0x2780, 0x2788),
        (0x278A, 0x2792),
        (0x10A40, 0x10A43),
        (0x10E60, 0x10E68),
        (0x11052, 0x1105A),
        (0x1F100, 0x1F10A),
    ];
    // kPrimaryNumeric ideographs (no BCL equivalent): 91 code points in 82 ranges.
    private static readonly (int Lo, int Hi)[] NumericIdeographRanges = [
        (0x3405, 0x3405),
        (0x3483, 0x3483),
        (0x382A, 0x382A),
        (0x3B4D, 0x3B4D),
        (0x4E00, 0x4E00),
        (0x4E03, 0x4E03),
        (0x4E07, 0x4E07),
        (0x4E09, 0x4E09),
        (0x4E24, 0x4E24),
        (0x4E5D, 0x4E5D),
        (0x4E8C, 0x4E8C),
        (0x4E94, 0x4E94),
        (0x4E96, 0x4E96),
        (0x4EAC, 0x4EAC),
        (0x4EBF, 0x4EC0),
        (0x4EDF, 0x4EDF),
        (0x4EE8, 0x4EE8),
        (0x4F0D, 0x4F0D),
        (0x4F70, 0x4F70),
        (0x4FE9, 0x4FE9),
        (0x5006, 0x5006),
        (0x5104, 0x5104),
        (0x5146, 0x5146),
        (0x5169, 0x5169),
        (0x516B, 0x516B),
        (0x516D, 0x516D),
        (0x5341, 0x5341),
        (0x5343, 0x5345),
        (0x534C, 0x534C),
        (0x53C1, 0x53C4),
        (0x56DB, 0x56DB),
        (0x58F1, 0x58F1),
        (0x58F9, 0x58F9),
        (0x5E7A, 0x5E7A),
        (0x5EFE, 0x5EFF),
        (0x5F0C, 0x5F0E),
        (0x5F10, 0x5F10),
        (0x62D0, 0x62D0),
        (0x62FE, 0x62FE),
        (0x634C, 0x634C),
        (0x67D2, 0x67D2),
        (0x6D1E, 0x6D1E),
        (0x6F06, 0x6F06),
        (0x7396, 0x7396),
        (0x767E, 0x767E),
        (0x7695, 0x7695),
        (0x79ED, 0x79ED),
        (0x8086, 0x8086),
        (0x842C, 0x842C),
        (0x8CAE, 0x8CAE),
        (0x8CB3, 0x8CB3),
        (0x8D30, 0x8D30),
        (0x920E, 0x920E),
        (0x94A9, 0x94A9),
        (0x9621, 0x9621),
        (0x9646, 0x9646),
        (0x964C, 0x964C),
        (0x9678, 0x9678),
        (0x96F6, 0x96F6),
        (0xF96B, 0xF96B),
        (0xF973, 0xF973),
        (0xF978, 0xF978),
        (0xF9B2, 0xF9B2),
        (0xF9D1, 0xF9D1),
        (0xF9D3, 0xF9D3),
        (0xF9FD, 0xF9FD),
        (0x20001, 0x20001),
        (0x20064, 0x20064),
        (0x200E2, 0x200E2),
        (0x20121, 0x20121),
        (0x2092A, 0x2092A),
        (0x20983, 0x20983),
        (0x2098C, 0x2098C),
        (0x2099C, 0x2099C),
        (0x20AEA, 0x20AEA),
        (0x20AFD, 0x20AFD),
        (0x20B19, 0x20B19),
        (0x22390, 0x22390),
        (0x22998, 0x22998),
        (0x23B1B, 0x23B1B),
        (0x2626D, 0x2626D),
        (0x2F890, 0x2F890),
    ];
    // Other_ID_Start: 4 code points in 3 ranges.
    private static readonly (int Lo, int Hi)[] IdentifierStartRanges = [
        (0x1885, 0x1886),
        (0x2118, 0x2118),
        (0x212E, 0x212E),
    ];
    // Other_ID_Continue: 18 code points in 9 ranges.
    private static readonly (int Lo, int Hi)[] IdentifierContinueRanges = [
        (0xB7, 0xB7),
        (0x387, 0x387),
        (0x1369, 0x1371),
        (0x19DA, 0x19DA),
        (0x200C, 0x200D),
        (0x2118, 0x2118),
        (0x212E, 0x212E),
        (0x30FB, 0x30FB),
        (0xFF65, 0xFF65),
    ];
    private static bool InRanges((int Lo, int Hi)[] ranges, int value)
    {
        foreach (var (lo, hi) in ranges)
        {
            if ((uint)(value - lo) <= (uint)(hi - lo))
            {
                return true;
            }
        }

        return false;
    }

    private enum RuneCase
    {
        Uncased,
        Upper,
        Lower,
        Title,
    }

    private static RuneCase GetRuneCase(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.UppercaseLetter || InRanges(UpperExceptionRanges, rune.Value))
        {
            return RuneCase.Upper;
        }

        if (category is UnicodeCategory.LowercaseLetter || InRanges(LowerExceptionRanges, rune.Value))
        {
            return RuneCase.Lower;
        }

        if (category is UnicodeCategory.TitlecaseLetter)
        {
            return RuneCase.Title;
        }

        return RuneCase.Uncased;
    }

    public static bool IsAscii(PyString value)
        => CheckAllRunes(value, static rune => rune.Value < 128);

    public static bool IsDecimal(PyString value)
        => CheckAllRunes(value, static rune => Rune.GetUnicodeCategory(rune) == UnicodeCategory.DecimalDigitNumber);

    public static bool IsDigit(PyString value)
        => CheckAllRunes(value, IsDigitRune);

    internal static bool IsDigitRune(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        return category == UnicodeCategory.DecimalDigitNumber
            || InRanges(DigitExceptionRanges, rune.Value);
    }

    public static bool IsNumeric(PyString value)
        => CheckAllRunes(value, static rune =>
        {
            var category = Rune.GetUnicodeCategory(rune);
            return category is UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.LetterNumber
                or UnicodeCategory.OtherNumber
                || InRanges(NumericIdeographRanges, rune.Value);
        });

    public static bool IsPrintable(PyString value)
    {
        if (value.AsString().Length == 0)
        {
            return true;
        }

        return CheckAllRunes(value, static rune =>
        {
            if (rune.Value == 0x20)
            {
                return true;
            }

            return Rune.GetUnicodeCategory(rune) is not UnicodeCategory.Control
                and not UnicodeCategory.Format
                and not UnicodeCategory.Surrogate
                and not UnicodeCategory.PrivateUse
                and not UnicodeCategory.OtherNotAssigned
                and not UnicodeCategory.LineSeparator
                and not UnicodeCategory.ParagraphSeparator
                and not UnicodeCategory.SpaceSeparator;
        });
    }

    public static bool IsIdentifier(PyString value)
    {
        var first = true;
        foreach (var rune in value.AsString().EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (first)
            {
                if (!IsIdentifierStart(rune, category))
                {
                    return false;
                }

                first = false;
            }
            else if (!IsIdentifierContinue(rune, category))
            {
                return false;
            }
        }

        return !first;
    }

    private static bool IsIdentifierStart(Rune rune, UnicodeCategory category)
        => category is UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter
            or UnicodeCategory.LetterNumber
            || rune.Value == '_'
            || InRanges(IdentifierStartRanges, rune.Value);

    private static bool IsIdentifierContinue(Rune rune, UnicodeCategory category)
        => IsIdentifierStart(rune, category)
            || category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.ConnectorPunctuation
            || InRanges(IdentifierContinueRanges, rune.Value);

    public static bool IsTitle(PyString value)
    {
        var previousIsCased = false;
        var foundCased = false;
        foreach (var rune in value.AsString().EnumerateRunes())
        {
            switch (GetRuneCase(rune))
            {
                case RuneCase.Upper:
                case RuneCase.Title:
                    if (previousIsCased)
                    {
                        return false;
                    }

                    previousIsCased = true;
                    foundCased = true;
                    break;
                case RuneCase.Lower:
                    if (!previousIsCased)
                    {
                        return false;
                    }

                    previousIsCased = true;
                    foundCased = true;
                    break;
                default:
                    previousIsCased = false;
                    break;
            }
        }

        return foundCased;
    }
}
