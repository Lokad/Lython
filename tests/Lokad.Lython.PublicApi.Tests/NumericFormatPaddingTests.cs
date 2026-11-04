using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NumericFormatPaddingTests
{
    // Independent CPython 3.13.2 goldens. Width can be exceeded when an added
    // grouping separator is needed to include the last padding zero.
    [Theory]
    [InlineData("1234", "010,d", "00,001,234")]
    [InlineData("0", "04,d", "0,000")]
    [InlineData("1", "02,d", "01")]
    [InlineData("1", "03,d", "001")]
    [InlineData("-1", "04,d", "-001")]
    [InlineData("1.25", "06,.2f", "001.25")]
    [InlineData("1.25", "06,.3g", "001.25")]
    [InlineData("decimal.Decimal('1.25')", "06,.2f", "001.25")]
    [InlineData("1234", "0=10,d", "00,001,234")]
    [InlineData("1234", "0>10,d", "000001,234")]
    [InlineData("1234", "🐱=10,d", "🐱🐱🐱🐱🐱1,234")]
    [InlineData("1.25", "020,.6f", "0,000,000,001.250000")]
    [InlineData("-1.25", "+020_.6F", "-0_000_000_001.250000")]
    [InlineData("1234.5", "020,.2E", "0,000,000,001.23E+03")]
    [InlineData("-0.0", "010,.0f", "-0,000,000")]
    [InlineData("1.25", "010,.0e", "00,001e+00")]
    [InlineData("1.25", "020,.6g", "0,000,000,000,001.25")]
    [InlineData("-1.25", "+020_.6g", "-0_000_000_000_001.25")]
    [InlineData("1.25", "020,.2%", "0,000,000,000,125.00%")]
    [InlineData("1.25", "020,", "0,000,000,000,001.25")]
    [InlineData("decimal.Decimal('1.25')", "020,.2f", "0,000,000,000,001.25")]
    [InlineData("decimal.Decimal('-0')", "010,.0f", "-0,000,000")]
    [InlineData("decimal.Decimal('1.20E+6')", "+020,.2E", "+0,000,000,001.20E+6")]
    [InlineData("float('inf')", "020,g", "00000000000000000inf")]
    [InlineData("float('nan')", "020_.2f", "00000000000000000nan")]
    [InlineData("complex(1234.5,-2.5)", "🌙^32,.2f", "🌙🌙🌙🌙🌙🌙🌙🌙🌙1,234.50-2.50j🌙🌙🌙🌙🌙🌙🌙🌙🌙")]
    [InlineData("complex(-0.0,2.0)", ">20", "             (-0+2j)")]
    [InlineData("complex(0.0,-2.0)", "+15", "            -2j")]
    [InlineData("complex(1,2)", "#.0E", "1.E+00+2.E+00j")]
    public Task GroupingIncludesSignAwarePaddingAcrossEntryPoints(string value, string spec, string expected)
        => AssertFormats(value, spec, expected);

    [Fact]
    public async Task LargeLegacyBodyPreservesGroupsAndUnicodePadding()
    {
        // Independent CPython 3.13.2 goldens for >1,024-byte bodies/fill.
        var expected = string.Concat(Enumerable.Repeat("🐈", 475)) + "1.25" + new string('0', 2045)
            + string.Concat(Enumerable.Repeat("🐈", 476));
        await AssertFormats("1.25", "🐈^#3000,.2048g", expected);
        await AssertFormats("1.25", "04096,.6g", "0" + string.Concat(Enumerable.Repeat(",000", 1022)) + ",001.25");
    }

    private static async Task AssertFormats(string value, string spec, string expected)
    {
        var source = "import decimal\nvalue=" + value + "\nreturn format(value,'" + spec + "')+'|'+('{0:" + spec
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
