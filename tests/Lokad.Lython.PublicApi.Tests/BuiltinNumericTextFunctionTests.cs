using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BuiltinNumericTextFunctionTests
{
    [Fact]
    public void ScalarConstructorsMatchPythonDefaultsBasesAndFloatText()
    {
        var result = new LythonEngine().Run(
            """
import math
return str(int()) + "|" + str(float()) + "|" + str(bool()) + "|" + str(int("101", 2)) + "|" + str(int("0xff", base=0)) + "|" + str(int("1_000")) + "|" + str(math.isinf(float("-inf"))) + "|" + str(math.isnan(float("nan"))) + "|" + str(float("1_000.5"))
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("0|0.0|False|5|255|1000|True|True|1000.5", result.ReturnValue);
    }

    [Fact]
    public void IntBaseAndLiteralDiagnosticsMatchPython()
    {
        var result = new LythonEngine().Run(
            """
values = []
try:
    int("1", "2")
except TypeError as e:
    values.append(str(e))
try:
    int("1", 2.0)
except TypeError as e:
    values.append(str(e))
try:
    int("1", None)
except TypeError as e:
    values.append(str(e))
try:
    int("1", True)
except ValueError as e:
    values.append(str(e))
try:
    int("010", 0)
except ValueError as e:
    values.append(str(e))
try:
    int("  0xFF  ")
except ValueError as e:
    values.append(str(e))
try:
    int(b"0xFF")
except ValueError as e:
    values.append(str(e))
values.append(str(int("10", 0)))
return "|".join(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            "'str' object cannot be interpreted as an integer|'float' object cannot be interpreted as an integer|'NoneType' object cannot be interpreted as an integer|int() base must be >= 2 and <= 36, or 0|invalid literal for int() with base 0: '010'|invalid literal for int() with base 10: '  0xFF  '|invalid literal for int() with base 10: b'0xFF'|10",
            result.ReturnValue);
    }
    [Fact]
    public void BytesConstructorAndOrdMatchPythonForms()
    {
        var result = new LythonEngine().Run(
            """
try:
    bytes("é")
except TypeError:
    missing_encoding = True
return str(missing_encoding) + "|" + repr(bytes(3)) + "|" + repr(bytes("é", "utf-8")) + "|" + str(ord(b"A"))
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|b'\\x00\\x00\\x00'|b'\\xc3\\xa9'|65", result.ReturnValue);
    }

    [Fact]
    public void MinAndMaxSupportPythonCallForms()
    {
        var result = new LythonEngine().Run(
            """
values = []
values.append(str(min(3, 1, 2)))
values.append(str(max("a", "bbb", "cc", key=len)))
values.append(str(min([], default=7)))
values.append(repr(max([], default=None)))
try:
    max(1, 2, default=0)
except TypeError:
    values.append("caught")
return "|".join(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("1|bbb|7|None|caught", result.ReturnValue);
    }

    [Fact]
    public void NumericComparisonsAreExactAndTreatNanAsUnordered()
    {
        var result = new LythonEngine().Run(
            """
import math
n = 9007199254740993
f = 9007199254740992.0
x = math.nan
return str(n == f) + "|" + str(n > f) + "|" + str(x == x) + "|" + str(x != x) + "|" + str(x < 1) + "|" + str(x <= 1) + "|" + str(x > 1) + "|" + str(x >= 1)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("False|True|False|True|False|False|False|False", result.ReturnValue);
    }

    [Fact]
    public void FloatRenderingUsesPythonSpellings()
    {
        var result = new LythonEngine().Run(
            """
import math
return str(1.0) + "|" + repr(-0.0) + "|" + str(1e20) + "|" + str(math.inf) + "|" + repr(-math.inf) + "|" + str(math.nan) + "|" + str([1.0, -0.0])
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("1.0|-0.0|1e+20|inf|-inf|nan|[1.0, -0.0]", result.ReturnValue);
    }

    [Fact]
    public void FloatingPowerRejectsPythonExceptionalCases()
    {
        var result = new LythonEngine().Run(
            """
values = []
try:
    0.0 ** -1
except ZeroDivisionError:
    values.append("zero")
try:
    (-1.0) ** 0.5
except TypeError:
    values.append("complex")
values.append(str(pow(2, -1, 5)))
try:
    pow(2, 3, 0)
except ValueError as e:
    values.append(str(e))
return "|".join(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("zero|complex|3|pow() 3rd argument cannot be 0", result.ReturnValue);
    }

    [Fact]
    public void DivisionAndModuloHandleOverflowAndInfinityLikePython()
    {
        var result = new LythonEngine().Run(
            """
import math
values = []
try:
    (10 ** 400) / 1
except OverflowError:
    values.append("overflow")
left = -1.0 % math.inf
right = 1.0 % -math.inf
values.append(str(math.isinf(left) and left > 0))
values.append(str(math.isinf(right) and right < 0))
values.append(str(1.0 % math.inf))
values.append(str(-1.0 % -math.inf))
return "|".join(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("overflow|True|True|1.0|-1.0", result.ReturnValue);
    }

    [Fact]
    public void BooleanBitwiseOperatorsPreserveBooleanResults()
    {
        var result = new LythonEngine().Run(
            """
x = True
x &= True
return str(True & True) + "|" + str(True | False) + "|" + str(True ^ False) + "|" + type(x).__name__
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True|True|bool", result.ReturnValue);
    }

    [Fact]
    public void LargeIntegerBaseFormattingPreservesDigitOrder()
    {
        var result = new LythonEngine().Run(
            """
value = (1 << 4096) + 15
text = hex(value)
return str(len(text)) + "|" + text[:3] + "|" + text[-1]
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("1027|0x1|f", result.ReturnValue);
    }

    [Fact]
    public void NumericAndTextBuiltins_MatchPythonShapedCoreBehavior()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from decimal import Decimal

class Box:
    pass

values = []
values.append(str(abs(-5)))
values.append(str(abs(-2.5)))
values.append(f"{abs(Decimal('-3.25'))}")
values.append(str(pow(2, 10)))
values.append(str(pow(2, 5, 7)))
values.append(str(pow(2, 3, -5)))
values.append(str(round(2.5)))
values.append(str(round(3.14159, 2)))
values.append(str(round(125, -1)))
values.append(f"{round(Decimal('1.25'), 1)}")
values.append(bin(-5))
values.append(oct(8))
values.append(hex(255))
values.append(chr(128512))
values.append(str(ord("😀")))
values.append(ascii("café"))
values.append(format(15, "#x"))
values.append(format("xy", ">4"))
values.append(format(2.5, ".1f"))
values.append(str(callable(len)))
values.append(str(callable(Box)))
values.append(str(callable(Box())))
values.append(str(hash("same") == hash("same")))

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("5|2.5|3.25|1024|4|-2|2|3.14|120|1.2|-0b101|0o10|0xff|😀|128512|'caf\\xe9'|0xf|  xy|2.5|True|True|False|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void RoundUsesPythonBinaryFloatSemanticsAndBoundsExtremeScaling()
    {
        var result = new LythonEngine().Run(
            """
from decimal import Decimal

return "|".join([
    str(round(2.675, 2)),
    str(round(-2.675, 2)),
    str(round(1.25, 1)),
    str(round(1.35, 1)),
    str(round(5e-324, 323)),
    str(round(5e-324, 324)),
    str(round(-2.675, -2147483648)),
    str(round(123, -2147483648)),
    f"{round(Decimal('1'), -2147483648)}",
])
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("2.67|-2.67|1.2|1.4|0.0|5e-324|-0.0|0|0", result.ReturnValue);
    }

    [Fact]
    public void RoundReportsFloatResultsOutsideBinary64Range()
    {
        var result = new LythonEngine().Run("round(1.7976931348623157e308, -308)\n", new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("OverflowError", result.Failure?.ExceptionType);
        Assert.Contains("too large", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("abs('x')\n", "TypeError", "bad operand type for abs()")]
    [InlineData("pow(2, 3, 0)\n", "ValueError", "cannot be 0")]
    [InlineData("bin(1.2)\n", "TypeError", "integer")]
    [InlineData("chr(1114112)\n", "ValueError", "range")]
    [InlineData("ord('ab')\n", "TypeError", "character")]
    [InlineData("format(1, 2)\n", "TypeError", "format() argument 2 must be str, not int")]
    [InlineData("hash([])\n", "TypeError", "unhashable type: 'list'")]
    [InlineData("hash({})\n", "TypeError", "unhashable type: 'dict'")]
    [InlineData("hash({1})\n", "TypeError", "unhashable type: 'set'")]
    [InlineData("from collections import defaultdict\nhash(defaultdict(int))\n", "TypeError", "unhashable type: 'collections.defaultdict'")]
    [InlineData("from collections import Counter\nhash(Counter())\n", "TypeError", "unhashable type: 'Counter'")]
    [InlineData("import math\nint(math.nan)\n", "ValueError", "NaN")]
    [InlineData("import math\nint(math.inf)\n", "OverflowError", "infinity")]
    [InlineData("import math\nround(math.nan)\n", "ValueError", "NaN")]
    [InlineData("import math\nround(math.inf)\n", "OverflowError", "infinity")]
    public void NumericAndTextBuiltins_ReportExplicitFailures(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure?.ExceptionType);
        Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
    }
    [Fact]
    public async Task NumericComparisonDundersAdvanceLikeCpython()
    {
        // int slots take the integer tower only (floats decline to the
        // reflected slot) while float slots take the whole tower; anything
        // else declines with NotImplemented on every dunder, ordering
        // included, exactly like CPython.
        var script = new LythonEngine().Compile("""
from decimal import Decimal
def call2(f, a, b):
    return f(a, b)
results = []
results.append(str((1).__eq__(1)))
results.append(str((1).__ne__(2)))
results.append(str((1).__lt__(2)))
results.append(str((1).__le__(1)))
results.append(str((1).__gt__(0)))
results.append(str((1).__ge__(2)))
results.append(str((1).__eq__(True)))
results.append(str((2).__eq__(2.0)))
results.append(str((1).__eq__(1.0)))
results.append(str((1).__ne__(1.5)))
results.append(str((1).__lt__(1.5)))
results.append(str((2).__gt__(1.5)))
results.append(str((2).__le__(2.0)))
results.append(str((1).__eq__("a")))
results.append(str((1).__ne__("a")))
results.append(str((1).__lt__("a")))
results.append(str((1).__ge__("a")))
results.append(str((1).__eq__(None)))
results.append(str((1).__eq__([1])))
results.append(str((1).__eq__({1: 2})))
results.append(str((10 ** 30).__eq__(10 ** 30)))
results.append(str((10 ** 30).__lt__(10 ** 30 + 1)))
results.append(str((2 ** 100).__eq__(2 ** 100)))
results.append(str((10 ** 30).__eq__(1e30)))
results.append(str((10 ** 30).__lt__(1e30)))
results.append(str(True.__eq__(1)))
results.append(str(True.__ne__(0)))
results.append(str(True.__lt__(2)))
results.append(str(False.__ge__(0)))
results.append(str(True.__eq__(1.0)))
results.append(str(True.__lt__(1.5)))
results.append(str(True.__eq__("a")))
results.append(str((1.5).__eq__(1.5)))
results.append(str((1.5).__ne__(2.0)))
results.append(str((1.5).__lt__(2)))
results.append(str((1.5).__le__(1.5)))
results.append(str((1.5).__gt__(1)))
results.append(str((1.5).__ge__(1.5)))
results.append(str((1.0).__eq__(1)))
results.append(str((1.5).__ne__(1)))
results.append(str((1.5).__lt__(2)))
results.append(str((2.0).__ge__(2)))
results.append(str((1.0).__eq__("a")))
results.append(str((1.5).__lt__("a")))
results.append(str((1.0).__eq__(None)))
results.append(str((1.0).__eq__([1])))
n = float("nan")
results.append(str(n.__eq__(n)))
results.append(str(n.__ne__(n)))
results.append(str(n.__lt__(1)))
results.append(str(n.__le__(n)))
results.append(str(n.__gt__(1)))
results.append(str(n.__ge__(n)))
results.append(str(float("inf").__gt__(10 ** 30)))
results.append(str(float("-inf").__lt__(1)))
results.append(str(float("inf").__eq__(float("inf"))))
results.append(str((1).__eq__(Decimal("1"))))
results.append(str((1.0).__eq__(Decimal("1"))))
results.append(str((1).__lt__(Decimal("1.5"))))
for (f, a, b) in [((1).__eq__, 1, 2), ((1).__ne__, 1, 2), ((1).__lt__, 1, 2), ((1).__le__, 1, 2), ((1).__gt__, 1, 2), ((1).__ge__, 1, 2), ((1.0).__eq__, 1, 2), ((1.0).__ne__, 1, 2), ((1.0).__lt__, 1, 2), ((1.0).__le__, 1, 2), ((1.0).__gt__, 1, 2), ((1.0).__ge__, 1, 2)]:
    try:
        call2(f, a, b)
    except TypeError as e:
        results.append(type(e).__name__)
        results.append(str(e))
results.append(str(hasattr(1, "__eq__")))
results.append(str(hasattr(1.0, "__lt__")))
results.append(str(hasattr(True, "__ge__")))
results.append(str(hasattr(1, "__rlt__")))
results.append(str(hasattr(1.0, "__req__")))
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
            "False",
            "True",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "True",
            "True",
            "True",
            "NotImplemented",
            "NotImplemented",
            "True",
            "True",
            "True",
            "True",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "True",
            "True",
            "True",
            "True",
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
            "False",
            "True",
            "False",
            "False",
            "False",
            "False",
            "True",
            "True",
            "True",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "TypeError",
            "Method 'int.__eq__' received too many positional arguments.",
            "TypeError",
            "Method 'int.__ne__' received too many positional arguments.",
            "TypeError",
            "Method 'int.__lt__' received too many positional arguments.",
            "TypeError",
            "Method 'int.__le__' received too many positional arguments.",
            "TypeError",
            "Method 'int.__gt__' received too many positional arguments.",
            "TypeError",
            "Method 'int.__ge__' received too many positional arguments.",
            "TypeError",
            "Method 'float.__eq__' received too many positional arguments.",
            "TypeError",
            "Method 'float.__ne__' received too many positional arguments.",
            "TypeError",
            "Method 'float.__lt__' received too many positional arguments.",
            "TypeError",
            "Method 'float.__le__' received too many positional arguments.",
            "TypeError",
            "Method 'float.__gt__' received too many positional arguments.",
            "TypeError",
            "Method 'float.__ge__' received too many positional arguments.",
            "True",
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
