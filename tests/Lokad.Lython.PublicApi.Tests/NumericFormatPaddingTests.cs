using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NumericFormatPaddingTests
{
    // Independent CPython 3.13.2 goldens. Width can be exceeded when an added
    // grouping separator is needed to include the last padding zero.
    [Theory]
    [InlineData("1234", "010,d", "00,001,234")]
    [InlineData("0", "04,d", "0,000")]
    [InlineData("1234", "0=10,d", "00,001,234")]
    [InlineData("1234", "0>10,d", "000001,234")]
    [InlineData("1234", "🐱=10,d", "🐱🐱🐱🐱🐱1,234")]
    [InlineData("1.25", "020,.6f", "0,000,000,001.250000")]
    [InlineData("-1.25", "+020_.6F", "-0_000_000_001.250000")]
    [InlineData("1234.5", "020,.2E", "0,000,000,001.23E+03")]
    [InlineData("-0.0", "010,.0f", "-0,000,000")]
    [InlineData("1.25", "010,.0e", "00,001e+00")]
    public async Task GroupingIncludesSignAwarePaddingAcrossEntryPoints(string value, string spec, string expected)
    {
        var source = "value=" + value + "\nreturn format(value,'" + spec + "')+'|'+('{0:" + spec
            + "}').format(value)+'|'+f'{value:" + spec + "}'";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous
                    ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(string.Join('|', expected, expected, expected), result.ReturnValue);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
