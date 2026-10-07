using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class PythonLineBoundaryInventoryTests
{
    [Fact]
    public void MatchesTheCompletePython313ScalarInventory()
    {
        // CPython 3.13.16 chr(c).splitlines() == [''] over all code points.
        HashSet<int> expected = [10, 11, 12, 13, 28, 29, 30, 133, 8232, 8233];
        Span<byte> encoded = stackalloc byte[4];
        for (var scalar = 0; scalar <= 0x10FFFF; scalar++)
        {
            if (!Rune.TryCreate(scalar, out var rune)) continue;
            var written = rune.EncodeToUtf8(encoded);
            var boundary = PyStringOps.TryGetLineBreakByteLength(encoded[..written], 0, out var length);
            Assert.Equal(expected.Contains(scalar), boundary);
            Assert.Equal(boundary ? written : 0, length);
        }
    }
}
