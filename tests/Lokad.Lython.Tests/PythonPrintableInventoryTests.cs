using System.Security.Cryptography;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class PythonPrintableInventoryTests
{
    [Fact]
    public void MatchesPython313PrintabilityAcrossAllScalars()
    {
        // SHA256 of CPython 3.13.16's isprintable bits, indexed by code point.
        // Surrogate bits remain zero because Lython strings are scalar UTF-8.
        var bits = new byte[0x110000 / 8];
        for (var scalar = 0; scalar <= 0x10FFFF; scalar++)
            if (Rune.TryCreate(scalar, out var rune) && PyStringOps.IsPrintableRune(rune))
                bits[scalar / 8] |= (byte)(1 << (scalar % 8));
        Assert.Equal("4B22A82D45B544B6F2B8BB8DA28D436D980EB7243A8BFFF2C9E8D45F20EAF686", Convert.ToHexString(SHA256.HashData(bits)));
    }
}
