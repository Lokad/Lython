using System.Globalization;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IntegerRenderingTests
{
    public static TheoryData<string, string> Numbers => new()
    {
        { "0", "0" },
        { "1", "1" },
        { "-1", "-1" },
        { "2147483647", "2147483647" },
        { "-2147483648", "-2147483648" },
        { "9223372036854775807", "9223372036854775807" },
        { "-9223372036854775808", "-9223372036854775808" },
        { "9223372036854775808", "9223372036854775808" },
        { "-9223372036854775809", "-9223372036854775809" },
        { "18446744073709551615", "18446744073709551615" },
        { "-18446744073709551616", "-18446744073709551616" },
        { "2**256", "115792089237316195423570985008687907853269984665640564039457584007913129639936" },
        { "-(2**256)", "-115792089237316195423570985008687907853269984665640564039457584007913129639936" },
        { "10**1000+7", "1" + new string('0', 999) + "7" },
    };

    [Theory]
    [MemberData(nameof(Numbers))]
    public async Task OrdinaryIntegerTextAndContainerReprAreExact(string expression, string expected)
    {
        var source = "value=" + expression + "\nprint(str(value),repr(value),f'{value}')\nprint(value)\nprint([value],(value,))";
        var stdout = expected + " " + expected + " " + expected + "\n" + expected + "\n[" + expected + "] (" + expected + ",)\n";
        await AssertBoth(source, stdout);
    }

    [Fact]
    public async Task RenderingKeepsNativeAndCustomPrecedence()
        => await AssertBoth("class Value:\n def __str__(self):return 'custom-str'\n def __repr__(self):return 'custom-repr'\nvalue=Value()\nprint(str(value),repr(value),f'{value}',True,False,None,1.5)",
            "custom-str custom-repr custom-str True False None 1.5\n");

    [Fact]
    public async Task DecimalIntegerTextUsesInvariantSigns()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NegativeSign = "minus";
            CultureInfo.CurrentCulture = culture;
            await AssertBoth("print(str(-123456),repr(-123456),f'{-123456}',-123456)",
                "-123456 -123456 -123456 -123456\n");
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    private static async Task AssertBoth(string source, string expected)
    {
        using var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await compiled.RunAsync(new MockLythonHost()) : compiled.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
