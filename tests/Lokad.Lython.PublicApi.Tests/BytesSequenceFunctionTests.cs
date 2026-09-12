using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BytesSequenceFunctionTests
{
    [Fact]
    public void BytesConcatenationAndRepetition_MatchCpython()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import operator
vals = []
vals.append(str(b'ab' + b'cd'))
vals.append(str(b'ab' * 3))
vals.append(str(2 * b'ab'))
vals.append(str(b'ab' * 0))
vals.append(str(b'' + b''))
vals.append(str(b'ab' * True))
grown = b'ab'
grown += b'cd'
vals.append(str(grown))
scaled = b'ab'
scaled *= 2
vals.append(str(scaled))
vals.append(str(operator.add(b'd', b'e')))
vals.append(str(operator.mul(b'ab', 3)))
vals.append(str(operator.concat(b'd', b'e')))
vals.append(str(operator.iconcat(b'd', b'e')))
vals.append(str(97 in b'abc'))
vals.append(str(98 in b'abc'))
vals.append(str(b'bc' in b'abc'))
vals.append(str(b'' in b'abc'))
vals.append(str(True in b'\x01'))
vals.append(str(b'a' < b'b'))
vals.append(str(b'b' > b'ab'))
vals.append(str(b'a' <= b'a'))
vals.append(str(b'B' < b'a'))
vals.append(str(operator.contains(b'abc', b'a')))
vals.append(str(operator.lt(b'a', b'b')))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("b'abcd'|b'ababab'|b'abab'|b''|b''|b'ab'|b'abcd'|b'abab'|b'de'|b'ababab'|b'de'|b'de'|True|True|True|True|True|True|True|True|True|True|True", host.ReadText("/out.txt"));
    }
}
