using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class PythonWhitespaceInventoryTests
{
    [Fact]
    public void MatchesTheCompletePython313ScalarInventory()
    {
        // Independently derived from CPython 3.13.16 chr(c).isspace() over
        // every code point, rather than the BCL category used by the old code.
        HashSet<int> expected = [9, 10, 11, 12, 13, 28, 29, 30, 31, 32, 133, 160, 5760, 8192, 8193, 8194, 8195, 8196, 8197, 8198, 8199, 8200, 8201, 8202, 8232, 8233, 8239, 8287, 12288];
        for (var scalar = 0; scalar <= 0x10FFFF; scalar++)
        {
            if (!Rune.TryCreate(scalar, out var rune)) continue;
            Assert.Equal(expected.Contains(scalar), PyStringOps.IsPythonWhitespace(rune));
        }
    }
}
