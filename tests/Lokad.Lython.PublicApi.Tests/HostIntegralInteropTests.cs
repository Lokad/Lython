using System.Globalization;
using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class HostIntegralInteropTests
{
    public static IEnumerable<object[]> IntegralKinds()
    {
        foreach (var kind in new[] { "sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong" })
            foreach (var asyncMode in new[] { false, true })
                yield return [kind, asyncMode];
    }

    [Theory]
    [MemberData(nameof(IntegralKinds))]
    public async Task HostIntegersUsePythonIntegerSemantics(string kind, bool asyncMode)
    {
        var value = Integer(kind, "3");
        var nested = new List<object?> { new List<object?> { value } };
        var options = new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["n"] = value, ["zero"] = Integer(kind, "0"), ["nested"] = nested, ["alias"] = nested },
        };
        var script = new LythonEngine().Compile("""
assert list(range(n)) == [0, 1, 2]
assert [10, 20, 30, 40][n] == 40
assert [10, 20, 30, 40][zero:n] == [10, 20, 30]
assert n + 2 == 5 and 2 + n == 5 and -n == -3
assert n * 2 == 6 and n // 2 == 1 and n ** 2 == 9 and n & 1 == 1
assert n == 3 and n == 3.0 and n != 4 and n < 4
assert hash(n) == hash(3)
assert {n: 'host'}[3] == 'host' and {3: 'guest'}[n] == 'guest'
assert len({n, 3, 3.0}) == 1 and len({(n,): 1, (3,): 2}) == 1
assert bool(n) and not bool(zero)
assert isinstance(n, int) and type(n) is int and n.__class__ is int
assert n.__eq__(3) and (3).__eq__(n) and (3.0).__eq__(n)
assert n in range(5) and range(5).index(n) == 3 and range(5).index(zero) == 0
assert 'x' * n == 'xxx' and [1].__mul__(n) == [1, 1, 1]
assert str(n) == '3' and repr(n) == '3' and format(n, '04d') == '0003'
assert n.bit_length() == 2 and int(n) == 3 and float(n) == 3.0
assert bytes(n) == b'\x00\x00\x00'
assert b'\x01\x02\x03'.find(n, zero, n) == 2
assert (1).to_bytes(n, 'big') == b'\x00\x00\x01'
import itertools
assert list(itertools.repeat('x', n)) == ['x', 'x', 'x']
assert nested is alias and nested[0][0] + 1 == 4
import math
assert math.factorial(n) == 6
import json
assert json.dumps([n, zero]) == '[3, 0]'
assert json.dumps({n: 'x'}) == '{"3": "x"}'
match n:
    case int():
        pass
    case _:
        raise AssertionError('integer class pattern did not match')
assert 'A'.translate({65: n + 64}) == 'C'
assert 'A'.translate({65: n}) == '\x03'
import functools
@functools.singledispatch
def describe(value):
    return 'other'
@describe.register(int)
def describe_integer(value):
    return 'integer'
assert describe(n) == 'integer'
@functools.lru_cache(typed=True)
def identify(value):
    return value
identify(n)
identify(3)
assert identify.cache_info().hits == 1
return [n, nested[0][0], zero]
""");
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var result = asyncMode ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, $"{result.Failure?.ExceptionType}: {result.Failure?.Message}; span={result.Failure?.Span}");
        var returned = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new[] { value, value, Integer(kind, "0") }, returned);
        Assert.All(returned, item => Assert.Equal(value.GetType(), item!.GetType()));
    }

    public static IEnumerable<object[]> IntegralBounds()
    {
        var bounds = new (string Kind, string Minimum, string Maximum)[]
        {
            ("sbyte", "-128", "127"), ("byte", "0", "255"),
            ("short", "-32768", "32767"), ("ushort", "0", "65535"),
            ("int", "-2147483648", "2147483647"), ("uint", "0", "4294967295"),
            ("long", "-9223372036854775808", "9223372036854775807"),
            ("ulong", "0", "18446744073709551615"),
        };
        foreach (var (kind, minimum, maximum) in bounds)
            foreach (var text in new[] { minimum, maximum })
                foreach (var asyncMode in new[] { false, true })
                    yield return [kind, text, asyncMode];
    }

    [Theory]
    [MemberData(nameof(IntegralBounds))]
    public async Task IntegralBoundsStayExactAndRoundTripTheirClrTypes(string kind, string text, bool asyncMode)
    {
        var value = Integer(kind, text);
        var expected = BigInteger.Parse(text, CultureInfo.InvariantCulture);
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["n"] = value } };
        var script = new LythonEngine().Compile($$"""
assert n == {{text}} and hash(n) == hash({{text}})
assert { {{text}}: 'value'}[n] == 'value'
assert str(n) == '{{text}}'
return [n, n + 1, n - 1, -n, n * n, n == {{text}}, n < {{text}} + 1]
""");
        Assert.True(script.IsValid);
        var result = asyncMode ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        var returned = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(value.GetType(), returned[0]!.GetType());
        Assert.Equal(new object?[] { value, expected + 1, expected - 1, -expected, expected * expected, true, true }, returned);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OtherHostScalarTypesStillRoundTripWithoutNumericCoercion(bool asyncMode)
    {
        var options = new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["single"] = 1.25f, ["decimal_value"] = 1.25m },
        };
        var script = new LythonEngine().Compile("""
rejected = []
for value in [single, decimal_value]:
    try:
        value + 1
    except TypeError:
        rejected.append(True)
    else:
        rejected.append(False)
return [single, decimal_value, rejected]
""");
        var result = asyncMode ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        var returned = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(1.25f, Assert.IsType<float>(returned[0]));
        Assert.Equal(1.25m, Assert.IsType<decimal>(returned[1]));
        Assert.Equal(new object?[] { true, true }, Assert.IsType<List<object?>>(returned[2]));
    }

    private static object Integer(string kind, string text) => kind switch
    {
        "sbyte" => sbyte.Parse(text, CultureInfo.InvariantCulture),
        "byte" => byte.Parse(text, CultureInfo.InvariantCulture),
        "short" => short.Parse(text, CultureInfo.InvariantCulture),
        "ushort" => ushort.Parse(text, CultureInfo.InvariantCulture),
        "int" => int.Parse(text, CultureInfo.InvariantCulture),
        "uint" => uint.Parse(text, CultureInfo.InvariantCulture),
        "long" => long.Parse(text, CultureInfo.InvariantCulture),
        "ulong" => ulong.Parse(text, CultureInfo.InvariantCulture),
        _ => throw new ArgumentException("Unknown integer kind.", nameof(kind)),
    };
}
