using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DecimalConversionBoundaryTests
{
    [Theory]
    [InlineData("c.create_decimal('1E-29')")]
    [InlineData("c.create_decimal('1.234567890123456789012345678901')")]
    [InlineData("c.create_decimal_from_float(0.0001)")]
    public async Task UnrepresentableConversionFailsInsteadOfRoundingTwice(string expression)
    {
        using var script = new LythonEngine().Compile($$"""
            import decimal as d
            c=d.Context(prec=28)
            original=d.Decimal('1.25')
            value=original
            try:
                value={{expression}}
            except d.InvalidOperation:
                pass
            else:
                raise AssertionError('unrepresentable Decimal conversion accepted')
            assert value is original and str(original)=='1.25'
            assert str(c.create_decimal('0.125'))=='0.125'
            """);
        await Verify(script);
    }

    [Fact]
    public async Task QuietInvalidConversionReportsItsUnsupportedNonfiniteResult()
    {
        using var script = new LythonEngine().Compile("""
            import decimal as d
            c=d.Context()
            c.traps[d.InvalidOperation]=False
            try:
                c.create_decimal('broken')
            except NotImplementedError:
                pass
            else:
                raise AssertionError('untrapped invalid conversion accepted')
            assert c.flags[d.InvalidOperation] is True
            """);
        await Verify(script);
    }

    private static async Task Verify(LythonCompiledScript script)
    {
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                       : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
                Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
