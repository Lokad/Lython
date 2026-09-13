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
    [Fact]
    public async Task BytesComparisonDundersAdvanceLikeCpython()
    {
        // bytes slots take bytes only and decline everything else with NotImplemented on every dunder, ordering included, exactly like CPython.
        var script = new LythonEngine().Compile("""
def call2(f, a, b):
    return f(a, b)
results = []
results.append(str(b"a".__eq__(b"a")))
results.append(str(b"a".__ne__(b"b")))
results.append(str(b"a".__lt__(b"b")))
results.append(str(b"a".__le__(b"a")))
results.append(str(b"b".__gt__(b"a")))
results.append(str(b"ab".__ge__(b"ab")))
results.append(str(b"a".__eq__("a")))
results.append(str(b"a".__ne__("a")))
results.append(str(b"a".__lt__("a")))
results.append(str(b"a".__ge__(1)))
results.append(str(b"a".__eq__(None)))
results.append(str(b"a".__eq__([97])))
results.append(str(b"".__eq__(b"")))
results.append(str(b"".__lt__(b"a")))
results.append(str(b"a".__lt__(b"ab")))
results.append(str(b"ab".__gt__(b"a")))
results.append(str(b"\xff".__gt__(b"\x00")))
for (f, a, b) in [(b"a".__eq__, 1, 2), (b"a".__ne__, 1, 2), (b"a".__lt__, 1, 2), (b"a".__le__, 1, 2), (b"a".__gt__, 1, 2), (b"a".__ge__, 1, 2)]:
    try:
        call2(f, a, b)
    except TypeError as e:
        results.append(type(e).__name__)
        results.append(str(e))
results.append(str(hasattr(b"a", "__eq__")))
results.append(str(hasattr(b"a", "__lt__")))
results.append(str(hasattr(b"a", "__rlt__")))
results.append(str(hasattr(b"a", "__req__")))
return results
""");
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "True",
            "True",
            "True",
            "True",
            "True",
            "TypeError",
            "Method 'bytes.__eq__' received too many positional arguments.",
            "TypeError",
            "Method 'bytes.__ne__' received too many positional arguments.",
            "TypeError",
            "Method 'bytes.__lt__' received too many positional arguments.",
            "TypeError",
            "Method 'bytes.__le__' received too many positional arguments.",
            "TypeError",
            "Method 'bytes.__gt__' received too many positional arguments.",
            "TypeError",
            "Method 'bytes.__ge__' received too many positional arguments.",
            "True",
            "True",
            "False",
            "False",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
