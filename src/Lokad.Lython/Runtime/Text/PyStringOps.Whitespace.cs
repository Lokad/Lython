using System.Text;

namespace Lokad.Lython.Runtime.Text;

internal static partial class PyStringOps
{
    // Python 3.13's 29 whitespace scalars. Keeping this small inventory explicit
    // avoids BCL Unicode-version drift and includes Python's four C0 separators.
    internal static bool IsPythonWhitespace(Rune rune) => rune.Value is
        >= 0x09 and <= 0x0D or >= 0x1C and <= 0x20 or
        0x85 or 0xA0 or 0x1680 or >= 0x2000 and <= 0x200A or
        0x2028 or 0x2029 or 0x202F or 0x205F or 0x3000;
}
