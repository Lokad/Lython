using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class MathModuleFunctionTests
{
    [Fact]
    public void MathModule_DispatchesRoundingAndIndexProtocols()
    {
        var result = new LythonEngine().Run(
            """
import math

class Rounded:
    def __ceil__(self): return 7
    def __floor__(self): return 6
    def __trunc__(self): return 5

class Indexed:
    def __index__(self): return 5

rounded = Rounded()
indexed = Indexed()
print(math.ceil(rounded), math.floor(rounded), math.trunc(rounded))
print(math.factorial(indexed), math.isqrt(indexed))
print(math.comb(indexed, 2), math.perm(indexed, 2))
print(math.gcd(indexed, 10), math.lcm(indexed, 10))
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("7 6 5\n120 2\n10 20\n5 10\n", result.StandardOutput);
    }

    [Fact]
    public void SensitiveMathFunctionsPreserveSmallInputsAndSpecialValues()
    {
        var result = new LythonEngine().Run(
            """
import math
return str(math.expm1(1e-16) != 0.0) + "|" + str(math.log1p(1e-16) != 0.0) + "|" + str(math.erf(0.0) == 0.0) + "|" + str(math.erfc(0.0) == 1.0) + "|" + str(math.erf(math.inf) == 1.0) + "|" + str(math.erfc(-math.inf) == 2.0)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True|True|True|True|True", result.ReturnValue);
    }

    [Theory]
    [InlineData("math.sqrt(9)", "3.0")]
    [InlineData("math.exp(1)", "2.718281828459045")]
    [InlineData("math.log(8, 2)", "3.0")]
    [InlineData("math.log10(1000)", "3.0")]
    [InlineData("math.log2(8)", "3.0")]
    [InlineData("math.sin(0)", "0.0")]
    [InlineData("math.cos(0)", "1.0")]
    [InlineData("math.tan(0)", "0.0")]
    [InlineData("math.asin(1)", "1.5707963267948966")]
    [InlineData("math.acos(1)", "0.0")]
    [InlineData("math.atan(1)", "0.7853981633974483")]
    [InlineData("math.atan2(1, 1)", "0.7853981633974483")]
    [InlineData("math.sinh(0)", "0.0")]
    [InlineData("math.cosh(0)", "1.0")]
    [InlineData("math.tanh(0)", "0.0")]
    [InlineData("math.floor(1.9)", "1")]
    [InlineData("math.ceil(1.1)", "2")]
    [InlineData("math.fabs(-1.5)", "1.5")]
    [InlineData("math.trunc(1.9)", "1")]
    [InlineData("math.degrees(math.pi)", "180.0")]
    [InlineData("math.radians(180)", "3.141592653589793")]
    [InlineData("math.isfinite(math.inf)", "False")]
    [InlineData("math.isinf(math.inf)", "True")]
    [InlineData("math.isnan(math.nan)", "True")]
    [InlineData("math.pow(2, 3)", "8.0")]
    [InlineData("math.hypot(3, 4)", "5.0")]
    [InlineData("math.hypot(2, 3, 6)", "7.0")]
    [InlineData("math.hypot()", "0.0")]
    [InlineData("math.fmod(7, 4)", "3.0")]
    [InlineData("math.copysign(2, -1)", "-2.0")]
    [InlineData("math.isclose(1, 1.0000000001)", "True")]
    [InlineData("math.prod([2, 3], start = 4)", "24")]
    [InlineData("math.fsum([0.1, 0.2, 0.3])", "0.6")]
    [InlineData("math.fsum([1e100, 1.0, -1e100])", "1.0")]
    [InlineData("math.isnan(math.fsum([math.nan]))", "True")]
    [InlineData("math.factorial(6)", "720")]
    [InlineData("math.factorial(True)", "1")]
    [InlineData("math.gcd(48, 18, -30)", "6")]
    [InlineData("math.gcd()", "0")]
    [InlineData("math.lcm(4, 6, 10)", "60")]
    [InlineData("math.lcm()", "1")]
    [InlineData("math.lcm(False, 3)", "0")]
    [InlineData("math.comb(5, 2)", "10")]
    [InlineData("math.comb(3, 5)", "0")]
    [InlineData("math.perm(5, 2)", "20")]
    [InlineData("math.perm(5)", "120")]
    [InlineData("math.perm(3, 5)", "0")]
    [InlineData("math.isqrt(10)", "3")]
    [InlineData("math.isqrt(10 ** 40)", "100000000000000000000")]
    [InlineData("math.dist([0, 0], [3, 4])", "5.0")]
    [InlineData("math.frexp(8)", "(0.5, 4)")]
    [InlineData("math.frexp(math.inf)", "(inf, 0)")]
    [InlineData("math.ldexp(0.5, 4)", "8.0")]
    [InlineData("math.modf(-1.25)", "(-0.25, -1.0)")]
    [InlineData("math.remainder(7, 4)", "-1.0")]
    [InlineData("math.nextafter(1, 2) > 1", "True")]
    [InlineData("math.nextafter(1, 2, steps = 0) == 1", "True")]
    [InlineData("math.nextafter(1, 2, steps = 2) > math.nextafter(1, 2)", "True")]
    [InlineData("math.nextafter(0, -1) < 0", "True")]
    [InlineData("math.ulp(1) > 0 and math.ulp(1) < 0.000000000000001", "True")]
    [InlineData("math.ulp(math.inf)", "inf")]
    [InlineData("math.exp2(3)", "8.0")]
    [InlineData("math.expm1(1) > 1.718 and math.expm1(1) < 1.719", "True")]
    [InlineData("math.log1p(math.e - 1) > 0.999 and math.log1p(math.e - 1) < 1.001", "True")]
    [InlineData("math.cbrt(-8)", "-2.0")]
    [InlineData("math.erf(1) > 0.842 and math.erf(1) < 0.843", "True")]
    [InlineData("math.erfc(1) > 0.157 and math.erfc(1) < 0.158", "True")]
    [InlineData("math.gamma(5) > 23.999 and math.gamma(5) < 24.001", "True")]
    [InlineData("math.lgamma(5) > 3.17 and math.lgamma(5) < 3.18", "True")]
    [InlineData("math.fma(2, 3, 4)", "10.0")]
    [InlineData("math.sumprod([1, 2, 3], [4, 5, 6])", "32")]
    [InlineData("math.sumprod([1.0, 2.0], [3, 4])", "11.0")]
    public void MathModule_Functions_HaveDirectCoverage(string expression, string expected)
    {
        Assert.Equal(expected, EvaluateToString(expression));
    }

    [Theory]
    [InlineData("math.pi > 3 and math.pi < 4", "True")]
    [InlineData("math.e > 2 and math.e < 3", "True")]
    [InlineData("math.tau > 6 and math.tau < 7", "True")]
    [InlineData("math.isinf(math.inf)", "True")]
    [InlineData("math.isnan(math.nan)", "True")]
    public void MathModule_Constants_HaveDirectCoverage(string expression, string expected)
    {
        Assert.Equal(expected, EvaluateToString(expression));
    }

    [Theory]
    [InlineData("math.factorial(-1)", "ValueError", "non-negative")]
    [InlineData("math.factorial(3.0)", "compile", "expects an integer argument")]
    [InlineData("math.comb(5, -1)", "ValueError", "non-negative")]
    [InlineData("math.isqrt(-1)", "ValueError", "non-negative")]
    [InlineData("math.dist([1, 2], [1])", "ValueError", "same number of dimensions")]
    [InlineData("math.remainder(1, 0)", "ValueError", "math domain error")]
    [InlineData("math.log1p(-1)", "ValueError", "math domain error")]
    [InlineData("math.gamma(0)", "ValueError", "math domain error")]
    [InlineData("math.fmod(math.inf, 1)", "ValueError", "math domain error")]
    [InlineData("math.exp(1000)", "OverflowError", "math range error")]
    [InlineData("math.fma(math.inf, 0, 1)", "ValueError", "invalid operation in fma")]
    [InlineData("math.sumprod([1, 2], [3])", "ValueError", "same length")]
    [InlineData("math.prod([1], 2)", "compile", "start=1")]
    [InlineData("math.nextafter(1, 2, -1)", "compile", "steps")]
    [InlineData("math.nextafter(1, 2, steps = -1)", "ValueError", "non-negative")]
    [InlineData("math.floor(math.nan)", "ValueError", "NaN")]
    [InlineData("math.floor(math.inf)", "OverflowError", "infinity")]
    [InlineData("math.ceil(math.nan)", "ValueError", "NaN")]
    [InlineData("math.trunc(-math.inf)", "OverflowError", "infinity")]
    [InlineData("math.fsum([math.inf, -math.inf])", "ValueError", "-inf + inf")]
    [InlineData("math.fsum([1e308, 1e308])", "OverflowError", "intermediate overflow")]
    public void MathModule_Failures_ArePythonShaped(string expression, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(
            $$"""
import math
{{expression}}
""",
            new MockLythonHost());

        Assert.False(result.Success);
        if (exceptionType == "compile")
        {
            Assert.Null(result.Failure);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(messageFragment, StringComparison.Ordinal));
        }
        else
        {
            Assert.NotNull(result.Failure);
            Assert.Equal(exceptionType, result.Failure.RequireNotNull().ExceptionType);
            Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MathModule_StaticContracts_CoverNewCallShapes()
    {
        var valid = new LythonEngine().Run(
            """
import math
m, e = math.frexp(8)
frac, whole = math.modf(1.25)
total = math.prod([2, 3], start = 4)
distance = math.dist([0, 0], [3, 4])
step = math.nextafter(1, 2, steps = 2)
return str((m, e, frac, whole, total, distance, step > 1))
""",
            new MockLythonHost());

        Assert.True(valid.Success, valid.Failure?.Message);
        Assert.Equal("(0.5, 4, 0.25, 1.0, 24, 5.0, True)", Assert.IsType<string>(valid.ReturnValue));

        var invalid = new LythonEngine().Run(
            """
import math
math.factorial()
math.factorial(3.0)
math.prod([1], 2)
math.nextafter(1, 2, 3)
math.ldexp(1, 2.5)
math.fma(1, 2)
math.sumprod([1])
""",
            new MockLythonHost());

        Assert.False(invalid.Success);
        Assert.Null(invalid.Failure);
        Assert.True(invalid.Diagnostics.Count(d => d.Code is "LA3151" or "LA3158") >= 7);
    }

    private static string EvaluateToString(string expression)
    {
        var source = $$"""
import math
return str({{expression}})
""";

        var result = new LythonEngine().Run(source, new MockLythonHost());

        var diagnostics = string.Join(" | ", result.Diagnostics.Select(d => $"{d.Code}:{d.Message}"));
        Assert.True(result.Success, result.Failure?.Message ?? diagnostics);
        return Assert.IsType<string>(result.ReturnValue);
    }
}
