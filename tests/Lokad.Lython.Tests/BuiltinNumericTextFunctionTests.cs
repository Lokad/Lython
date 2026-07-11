using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class BuiltinNumericTextFunctionTests
{
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

    [Theory]
    [InlineData("abs('x')\n", "TypeError", "numeric")]
    [InlineData("pow(2, -1, 5)\n", "ValueError", "non-negative")]
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
        Assert.Equal(exceptionType, result.Failure!.ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }
}
