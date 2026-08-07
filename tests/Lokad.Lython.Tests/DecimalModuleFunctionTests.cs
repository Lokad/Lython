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
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("4.5000|5.0000|1.2500|2.5|2.5|2|2718|2302|3|2|-2|-2|3|True|True", host.ReadText("/out.txt"));
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
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("2.6|2.6|InvalidOperation|bad|DivisionByZero|zero", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DecimalModule_ContextsAndTuples_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from decimal import Decimal, DecimalTuple, Context, getcontext, setcontext, localcontext, ROUND_DOWN, ROUND_HALF_UP

vals = []
ctx = getcontext()
vals.append(str(ctx.prec))
ctx.rounding = ROUND_DOWN
vals.append(f"{Decimal('2.9').to_integral_value()}")
setcontext(Context(prec=9, rounding=ROUND_HALF_UP))
vals.append(str(getcontext().prec))
vals.append(f"{Decimal('2.5').to_integral_value()}")
with localcontext(Context(rounding=ROUND_DOWN)) as local:
    vals.append(str(local.prec))
    vals.append(f"{Decimal('2.9').to_integral_value()}")
vals.append(f"{Decimal('2.5').to_integral_value()}")
t = Decimal('12.30').as_tuple()
vals.append(str(t.sign))
vals.append(f"{t.digits}")
vals.append(str(t.exponent))
vals.append(f"{Decimal(DecimalTuple(0, (1, 2, 3), -2))}")
vals.append(f"{getcontext().copy().create_decimal('4.50')}")
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("28|2|9|3|28|2|3|0|(1, 2, 3, 0)|-2|1.23|4.50", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DecimalModule_ActiveContextControlsRoundingOperations()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from decimal import Decimal, ROUND_DOWN, ROUND_UP, getcontext

context = getcontext()
context.rounding = ROUND_DOWN
values = [
    str(round(Decimal("1.29"), 1)),
    str(round(Decimal("129"), -1)),
    str(round(Decimal("1.9"))),
    str(Decimal("1.29").quantize(Decimal("0.1"))),
    str(Decimal("1.9").to_integral_value(context=None)),
    str(Decimal("1.21").to_integral_value(ROUND_UP)),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("1.2|120|2|1.2|1|2", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DecimalModule_ExpandedMethodsAndRoundingModes_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from decimal import Decimal, ROUND_CEILING, ROUND_FLOOR, ROUND_HALF_UP, ROUND_HALF_DOWN, ROUND_05UP

vals = []
vals.append(f"{Decimal('2.55').quantize(Decimal('0.1'), ROUND_HALF_UP)}")
vals.append(f"{Decimal('2.55').quantize(Decimal('0.1'), ROUND_HALF_DOWN)}")
vals.append(f"{Decimal('-2.51').quantize(Decimal('0.1'), ROUND_FLOOR)}")
vals.append(f"{Decimal('2.51').quantize(Decimal('0.1'), ROUND_CEILING)}")
vals.append(f"{Decimal('1.51').quantize(Decimal('0.1'), ROUND_05UP)}")
vals.append(str(Decimal('123.45').adjusted()))
vals.append(f"{Decimal('2').compare(Decimal('3'))}")
vals.append(f"{Decimal('1.0').compare_total(Decimal('1.00'))}")
vals.append(str(Decimal('1').is_nan()))
vals.append(str(Decimal('1').is_infinite()))
vals.append(str(Decimal('1').is_finite()))
vals.append(str(Decimal('0').is_zero()))
vals.append(str(Decimal('-2').is_signed()))
vals.append(f"{Decimal('123.45').to_eng_string()}")
vals.append(f"{Decimal('12.3').scaleb(2)}")
vals.append(f"{Decimal('12.3').shift(2)}")
vals.append(f"{Decimal('12.3').rotate(1)}")
vals.append(str(Decimal('1.2').same_quantum(Decimal('3.4'))))
vals.append(str(Decimal('1.2').same_quantum(Decimal('3.45'))))
vals.append(f"{Decimal('10').remainder_near(Decimal('6'))}")
vals.append(f"{Decimal('-2').min(Decimal('3'))}")
vals.append(f"{Decimal('-2').max(Decimal('3'))}")
vals.append(f"{Decimal('-2').min_mag(Decimal('3'))}")
vals.append(f"{Decimal('-2').max_mag(Decimal('3'))}")
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("2.6|2.5|-2.6|2.6|1.6|2|-1|1|False|False|True|True|True|123.45|1.23E+3|1230.0|31.2|True|False|-2|-2|3|-2|3", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DecimalModule_RetainsRepresentableExponentAndQuantumMetadata()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import copy
from decimal import Decimal, DecimalTuple

x = Decimal("1E+3")
values = [
    str(x),
    repr(x),
    str(x.as_tuple()),
    str(copy.copy(x)),
    str(x.copy_abs()),
    str(x + Decimal("0E+2")),
    str(x * Decimal("1.0")),
    str(Decimal(DecimalTuple(0, (1, 2, 3, 0), -2))),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("1E+3|Decimal('1E+3')|DecimalTuple(sign=0, digits=(1,), exponent=3)|1E+3|1E+3|1.0E+3|1.0E+3|12.30", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DecimalModule_StaticDiagnosticsCoverExpandedSurface()
    {
        var valid = new LythonEngine().Compile(
            """
from decimal import Decimal, DecimalTuple, Context, getcontext, setcontext, localcontext, ROUND_DOWN

ctx = getcontext()
ctx.prec
ctx.rounding = ROUND_DOWN
setcontext(Context(prec=9, rounding=ROUND_DOWN))
with localcontext(Context()) as local:
    local.copy()
d = Decimal("1.25")
d.quantize(Decimal("0.1"), ROUND_DOWN)
d.as_tuple().digits
d.adjusted()
d.compare(Decimal("2"))
d.is_finite()
d.scaleb(2)
Decimal(DecimalTuple(0, (1, 2), -1))
""");

        Assert.True(valid.IsValid, string.Join(" | ", valid.Diagnostics.Select(d => d.Code + ":" + d.Message)));

        var invalid = new LythonEngine().Compile(
            """
from decimal import Decimal, Context

Decimal(1, 2, 3)
Decimal("1").as_tuple(1)
Decimal("1").bogus()
Context().copy(1)
""");

        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Diagnostics, d => d.Code == "LA3151" && d.Message.Contains("decimal.Decimal", StringComparison.Ordinal));
        Assert.Contains(invalid.Diagnostics, d => d.Code == "LA3156" && d.Message.Contains("Decimal.as_tuple", StringComparison.Ordinal));
        Assert.Contains(invalid.Diagnostics, d => d.Code == "LA3113" && d.Message.Contains("bogus", StringComparison.Ordinal));
        Assert.Contains(invalid.Diagnostics, d => d.Code == "LA3156" && d.Message.Contains("Context.copy", StringComparison.Ordinal));
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
    [InlineData(
        """
from decimal import Decimal
Decimal("NaN")
""",
        "InvalidOperation",
        "NaN, sNaN, and Infinity")]
    [InlineData(
        """
from decimal import Context
Context(prec=100)
""",
        "ValueError",
        "prec")]
    [InlineData(
        """
from decimal import DecimalTuple
DecimalTuple(0, (1, 12), -1)
""",
        "ValueError",
        "digits")]
    public void DecimalModule_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }
}
