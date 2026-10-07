using System.Globalization;
using System.Text;

namespace Lokad.Lython.Runtime.Text;

internal static partial class PyStringOps
{
    // Python 3.13 uses Unicode 15.1. These 47 intervals are unassigned there
    // but printable in .NET 10's newer category table. The complete scalar
    // inventory test detects any further runtime/category version drift.
    private static readonly (int Lo, int Hi)[] Python313UnassignedPrintableRanges = [
        (0x897, 0x897),
        (0x1B4E, 0x1B4F),
        (0x1B7F, 0x1B7F),
        (0x1C89, 0x1C8A),
        (0x2427, 0x2429),
        (0x31E4, 0x31E5),
        (0xA7CB, 0xA7CD),
        (0xA7DA, 0xA7DC),
        (0x105C0, 0x105F3),
        (0x10D40, 0x10D65),
        (0x10D69, 0x10D85),
        (0x10D8E, 0x10D8F),
        (0x10EC2, 0x10EC4),
        (0x10EFC, 0x10EFC),
        (0x11380, 0x11389),
        (0x1138B, 0x1138B),
        (0x1138E, 0x1138E),
        (0x11390, 0x113B5),
        (0x113B7, 0x113C0),
        (0x113C2, 0x113C2),
        (0x113C5, 0x113C5),
        (0x113C7, 0x113CA),
        (0x113CC, 0x113D5),
        (0x113D7, 0x113D8),
        (0x113E1, 0x113E2),
        (0x116D0, 0x116E3),
        (0x11BC0, 0x11BE1),
        (0x11BF0, 0x11BF9),
        (0x11F5A, 0x11F5A),
        (0x13460, 0x143FA),
        (0x16100, 0x16139),
        (0x16D40, 0x16D79),
        (0x18CFF, 0x18CFF),
        (0x1CC00, 0x1CCF9),
        (0x1CD00, 0x1CEB3),
        (0x1E5D0, 0x1E5FA),
        (0x1E5FF, 0x1E5FF),
        (0x1F8B2, 0x1F8BB),
        (0x1F8C0, 0x1F8C1),
        (0x1FA89, 0x1FA89),
        (0x1FA8F, 0x1FA8F),
        (0x1FABE, 0x1FABE),
        (0x1FAC6, 0x1FAC6),
        (0x1FADC, 0x1FADC),
        (0x1FADF, 0x1FADF),
        (0x1FAE9, 0x1FAE9),
        (0x1FBCB, 0x1FBEF),
    ];

    internal static bool IsPrintableRune(Rune rune)
    {
        if (rune.IsAscii) return rune.Value is >= 0x20 and < 0x7f;
        if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control
            or UnicodeCategory.Format or UnicodeCategory.Surrogate
            or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned
            or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
            or UnicodeCategory.SpaceSeparator)
            return false;

        var lo = 0;
        var hi = Python313UnassignedPrintableRanges.Length - 1;
        while (lo <= hi)
        {
            var middle = lo + (hi - lo) / 2;
            var range = Python313UnassignedPrintableRanges[middle];
            if (rune.Value < range.Lo) hi = middle - 1;
            else if (rune.Value > range.Hi) lo = middle + 1;
            else return false;
        }
        return true;
    }
}
