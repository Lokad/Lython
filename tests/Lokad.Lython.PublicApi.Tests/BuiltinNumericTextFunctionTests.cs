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
return "|".join(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("zero|complex|3", result.ReturnValue);
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
    [InlineData("abs('x')\n", "TypeError", "numeric")]
    [InlineData("pow(2, 3, 0)\n", "ValueError", "cannot be 0")]
    [InlineData("bin(1.2)\n", "TypeError", "integer")]
    [InlineData("chr(1114112)\n", "ValueError", "range")]
    [InlineData("ord('ab')\n", "TypeError", "character")]
    [InlineData("format(1, 2)\n", "TypeError", "format_spec")]
    [InlineData("hash([])\n", "TypeError", "unhashable")]
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
}
