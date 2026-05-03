using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DecimalModuleFunctionTests
{
    [Fact]
    public void DecimalModule_Functions_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from decimal import Decimal, ROUND_DOWN

x = Decimal("2.5000")
vals = []
vals.append(f"{x + 2}")
vals.append(f"{x * 2}")
vals.append(f"{x / 2}")
vals.append(f"{Decimal('2.59').quantize(Decimal('0.1'), ROUND_DOWN)}")
vals.append(f"{x.normalize()}")
vals.append(f"{Decimal('4').sqrt()}")
vals.append(str(int(Decimal("1").exp() * 1000)))
vals.append(str(int(Decimal("10").ln() * 1000)))
vals.append(f"{Decimal('1000').log10()}")
vals.append(f"{Decimal('-2').copy_abs()}")
vals.append(f"{Decimal('2').copy_negate()}")
vals.append(f"{Decimal('2').copy_sign(Decimal('-1'))}")
vals.append(f"{Decimal('2.9').to_integral_value()}")
vals.append(str(Decimal("1.0") == 1))
vals.append(str(Decimal("3.0") > 2))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("4.5000|5.0000|1.2500|2.5|2.5|2|2718|2302|3|2|-2|-2|2|True|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DecimalModule_ConstantsAndRoundingModes_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from decimal import Decimal, ROUND_HALF_EVEN, ROUND_UP, InvalidOperation, DivisionByZero

vals = []
vals.append(f"{Decimal('2.55').quantize(Decimal('0.1'), ROUND_HALF_EVEN)}")
vals.append(f"{Decimal('2.51').quantize(Decimal('0.1'), ROUND_UP)}")
try:
    raise InvalidOperation("bad")
except InvalidOperation as ex:
    vals.append(ex.type)
    vals.append(ex.message)
try:
    raise DivisionByZero("zero")
except DivisionByZero as ex:
    vals.append(ex.type)
    vals.append(ex.message)
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("2.6|2.6|InvalidOperation|bad|DivisionByZero|zero", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
from decimal import Decimal
Decimal("1") / 0
""",
        "DivisionByZero",
        "division by zero")]
    [InlineData(
        """
from decimal import Decimal
Decimal("1") + 1.5
""",
        "TypeError",
        "Decimal and integer operands")]
    [InlineData(
        """
from decimal import Decimal
Decimal("2.5").quantize(Decimal("0.1"), "ROUND_SIDEWAYS")
""",
        "ValueError",
        "Unsupported decimal rounding mode")]
    public void DecimalModule_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure!.ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }
}
