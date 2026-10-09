using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IntegerByteOrderTests
{
    [Theory]
    [InlineData("to", false)]
    [InlineData("to", true)]
    [InlineData("from", false)]
    [InlineData("from", true)]
    public async Task OmittedByteOrderUsesPythonBigEndianDefault(string direction, bool asyncMode)
    {
        var source = direction == "to" ? """
return [(0x010203).to_bytes(3), (-2).to_bytes(2, signed=True),
        (0x010203).to_bytes(3, 'little'), (0x010203).to_bytes(3, 'big')]
""" : """
return [int.from_bytes(b'\x01\x02'), int.from_bytes(b'\xff\xfe', signed=True),
        int.from_bytes(b'\x01\x02', 'little'), int.from_bytes(b'\x01\x02', 'big')]
""";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var result = asyncMode ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        var returned = Assert.IsType<List<object?>>(result.ReturnValue);
        if (direction == "to")
        {
            var expected = new byte[][] { [1, 2, 3], [255, 254], [3, 2, 1], [1, 2, 3] };
            Assert.Equal(expected.Length, returned.Count);
            for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], Assert.IsType<byte[]>(returned[i]));
        }
        else
        {
            Assert.Equal(new object?[] { new BigInteger(258), new BigInteger(-2), new BigInteger(513), new BigInteger(258) }, returned);
        }
    }
}
