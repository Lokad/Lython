using System.Buffers.Binary;
using System.Security.Cryptography;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class HtmlNumericInventoryTests
{
    [Fact]
    public void MatchesPythonNumericReferenceConversionAcrossEveryCodePoint()
    {
        // Signed LE output scalars; -1 denotes a removed reference. Include
        // surrogates, every noncharacter, and the first out-of-range value.
        var values = new byte[0x110001 * 4];
        for (var scalar = 0; scalar <= 0x110000; scalar++)
            BinaryPrimitives.WriteInt32LittleEndian(values.AsSpan(scalar * 4, 4),
                LythonRuntime.HtmlModule.NormalizeNumericReference(scalar));
        Assert.Equal("A08C9164B7FB6BECE180DCE8E97CB02ECDCF1284ACC794C002B98E2D5EB88522", Convert.ToHexString(SHA256.HashData(values)));
    }
}
