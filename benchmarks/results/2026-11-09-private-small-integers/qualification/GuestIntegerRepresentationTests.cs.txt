using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GuestIntegerRepresentationTests
{
    [Theory]
    [InlineData("2147483647 + 1", "2147483648")]
    [InlineData("-2147483648 - 1", "-2147483649")]
    [InlineData("(2147483647 + 1) - 1", "2147483647")]
    [InlineData("(-2147483648 - 1) + 1", "-2147483648")]
    public async Task ArithmeticPromotesExactlyAndProjectsGuestIntegers(string expression, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile("return " + expression, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous
                    ? await script.RunAsync(new MockLythonHost())
                    : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(BigInteger.Parse(expected), Assert.IsType<BigInteger>(result.ReturnValue));
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task IntegerAliasesAndMixedNumericKeysKeepPythonSemantics()
    {
        const string source = """
            value = int('123456789')
            aliases = [int(value), +value, abs(value), round(value),
                       value.__int__(), value.__index__(),
                       value.numerator, value.real, value.conjugate()]
            print(all(item is value for item in aliases))
            values = {value: 'integer', 123456789.0: 'float'}
            print(len(values), values[value], value == 123456789.0,
                  hash(value) == hash(123456789.0))
            return value
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous
                ? await script.RunAsync(new MockLythonHost())
                : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True\n1 float True True\n", result.StandardOutput);
            Assert.Equal(new BigInteger(123456789), Assert.IsType<BigInteger>(result.ReturnValue));
        }
    }
}
