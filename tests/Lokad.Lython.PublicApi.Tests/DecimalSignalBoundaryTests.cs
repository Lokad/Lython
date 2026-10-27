using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DecimalSignalBoundaryTests
{
    [Theory]
    [InlineData("d.Decimal('0E+999999').scaleb(1)")]
    [InlineData("d.Decimal('0E-1000026').scaleb(-1)")]
    public async Task UnsupportedZeroClampingRejectsExplicitly(string expression)
    {
        using var script = new LythonEngine().Compile($$"""
            import decimal as d
            try:
                {{expression}}
            except NotImplementedError:
                pass
            else:
                raise AssertionError('unsupported zero clamping accepted')
            """);
        await Verify(script);
    }

    [Theory]
    [InlineData("d.Decimal('0') ** -1")]
    [InlineData("d.Decimal('-0.00') ** -3")]
    public async Task ZeroNegativePowerRejectsInfinityWithoutInventingASignal(string expression)
    {
        using var script = new LythonEngine().Compile($$"""
            import decimal as d
            c=d.getcontext()
            try:
                {{expression}}
            except NotImplementedError:
                pass
            else:
                raise AssertionError('unsupported infinity accepted')
            assert not any(c.flags.values())
            """);
        await Verify(script);
    }

    [Theory]
    [InlineData("d.Decimal(1)/d.Decimal(0)", false, true)]
    [InlineData("d.Decimal(0)/d.Decimal(0)", true, false)]
    [InlineData("d.Decimal(1)//d.Decimal(0)", false, true)]
    [InlineData("d.Decimal(1)%d.Decimal(0)", true, false)]
    [InlineData("divmod(d.Decimal(1),d.Decimal(0))", true, true)]
    [InlineData("d.Decimal(12)//d.Decimal(1)", true, false)]
    [InlineData("d.Decimal(12).quantize(d.Decimal(1))", true, false)]
    [InlineData("d.Decimal(12).remainder_near(d.Decimal(1))", true, false)]
    [InlineData("d.Decimal(1).remainder_near(d.Decimal(0))", true, false)]
    [InlineData("d.Decimal(0).logb()", false, true)]
    [InlineData("d.Decimal(1).scaleb(d.Decimal('2.0'))", true, false)]
    [InlineData("d.Decimal(1).shift(2)", true, false)]
    [InlineData("d.Decimal(1).rotate(2)", true, false)]
    [InlineData("d.Decimal(-1).sqrt()", true, false)]
    [InlineData("d.Decimal(-1).ln()", true, false)]
    [InlineData("d.Decimal(-1).log10()", true, false)]
    public async Task UntrappedNonfiniteResultsFailExplicitlyWithTheirSignals(string expression, bool invalid, bool divide)
    {
        using var script = new LythonEngine().Compile($$"""
            import decimal as d
            c=d.getcontext();c.prec=1
            c.traps[d.InvalidOperation]=False
            c.traps[d.DivisionByZero]=False
            try:
                {{expression}}
            except NotImplementedError:
                pass
            else:
                raise AssertionError('unsupported nonfinite result accepted')
            assert c.flags[d.InvalidOperation] is {{(invalid ? "True" : "False")}}
            assert c.flags[d.DivisionByZero] is {{(divide ? "True" : "False")}}
            c.traps[d.InvalidOperation]=True
            c.traps[d.DivisionByZero]=True
            c.clear_flags()
            assert d.Decimal(1)%d.Decimal(1)==0
            assert not any(c.flags.values())
            """);
        await Verify(script);
    }

    [Theory]
    [InlineData("round(d.Decimal(1), -2147483648)")]
    [InlineData("round(d.Decimal(5), 29)")]
    [InlineData("d.Decimal(1).quantize(d.Decimal('1E-29'))")]
    public async Task UnsupportedQuantizeScaleFailsInsteadOfReturningAnAliasOrFabricatedZero(string expression)
    {
        using var script = new LythonEngine().Compile($$"""
            import decimal as d
            value=d.Decimal(1)
            try:
                {{expression}}
            except d.InvalidOperation:
                pass
            else:
                raise AssertionError('unsupported quantize scale accepted')
            assert str(value)=='1'
            """);
        await Verify(script);
    }

    [Theory]
    [InlineData("c.traps[d.Inexact] = 1", "c.traps[d.Inexact] = False", "TypeError")]
    [InlineData("del c.flags[d.Inexact]", "c.flags[d.Inexact] = False", "KeyError")]
    public async Task MalformedDirectEditsFailOnUseAndCanBeRepaired(string damage, string repair, string error)
    {
        using var script = new LythonEngine().Compile($$"""
            import decimal as d
            c=d.getcontext()
            {{damage}}
            try:
                d.Decimal('1')+d.Decimal('2')
            except {{error}}:
                pass
            else:
                raise AssertionError('malformed signal map accepted')
            {{repair}}
            assert d.Decimal('1')+d.Decimal('2') == d.Decimal('3')
            """);
        await Verify(script);
    }

    [Fact]
    public async Task InvalidAssignmentLeavesTheLiveDestinationUntouched()
    {
        using var script = new LythonEngine().Compile("""
            import decimal as d
            c=d.getcontext()
            flags=c.flags
            replacement=dict(flags)
            del replacement[d.Inexact]
            replacement['other']=True
            try:
                c.flags=replacement
            except KeyError:
                pass
            else:
                raise AssertionError('invalid signal inventory accepted')
            assert c.flags is flags and len(flags)==9 and flags[d.Inexact] is False
            assert d.Decimal('1')+d.Decimal('2') == d.Decimal('3')
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
