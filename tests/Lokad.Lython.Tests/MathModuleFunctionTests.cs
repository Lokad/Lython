using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class MathModuleFunctionTests
{
    [Theory]
    [InlineData("math.sqrt(9)", "3")]
    [InlineData("math.exp(1)", "2.718281828459045")]
    [InlineData("math.log(8, 2)", "3")]
    [InlineData("math.log10(1000)", "3")]
    [InlineData("math.log2(8)", "3")]
    [InlineData("math.sin(0)", "0")]
    [InlineData("math.cos(0)", "1")]
    [InlineData("math.tan(0)", "0")]
    [InlineData("math.asin(1)", "1.5707963267948966")]
    [InlineData("math.acos(1)", "0")]
    [InlineData("math.atan(1)", "0.7853981633974483")]
    [InlineData("math.atan2(1, 1)", "0.7853981633974483")]
    [InlineData("math.sinh(0)", "0")]
    [InlineData("math.cosh(0)", "1")]
    [InlineData("math.tanh(0)", "0")]
    [InlineData("math.floor(1.9)", "1")]
    [InlineData("math.ceil(1.1)", "2")]
    [InlineData("math.fabs(-1.5)", "1.5")]
    [InlineData("math.trunc(1.9)", "1")]
    [InlineData("math.degrees(math.pi)", "180")]
    [InlineData("math.radians(180)", "3.141592653589793")]
    [InlineData("math.isfinite(math.inf)", "False")]
    [InlineData("math.isinf(math.inf)", "True")]
    [InlineData("math.isnan(math.nan)", "True")]
    [InlineData("math.pow(2, 3)", "8")]
    [InlineData("math.hypot(3, 4)", "5")]
    [InlineData("math.fmod(7, 4)", "3")]
    [InlineData("math.copysign(2, -1)", "-2")]
    [InlineData("math.isclose(1, 1.0000000001)", "True")]
    [InlineData("math.prod([2, 3], 4)", "24")]
    [InlineData("math.fsum([0.1, 0.2, 0.3])", "0.6")]
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

    private static string EvaluateToString(string expression)
    {
        var source = $$"""
import math
return str({{expression}})
""";

        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        return Assert.IsType<string>(result.ReturnValue);
    }
}
