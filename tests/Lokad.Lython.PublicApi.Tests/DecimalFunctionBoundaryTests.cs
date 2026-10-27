using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DecimalFunctionBoundaryTests
{
    [Theory]
    [InlineData("int(d.Decimal(1).exp()*1000)")]
    [InlineData("d.Decimal(-1).exp()")]
    [InlineData("int(d.Decimal(10).ln()*1000)")]
    [InlineData("d.Decimal(2).ln()")]
    [InlineData("d.Decimal(2).log10()")]
    [InlineData("d.Decimal(0).ln()")]
    [InlineData("d.Decimal(0).log10()")]
    public async Task UnsupportedTranscendentalRequestsFailExplicitlyWithoutInventingSignals(string expression)
    {
        using var script = new LythonEngine().Compile($$"""
            import decimal as d
            c=d.getcontext()
            try:
                {{expression}}
            except NotImplementedError:
                pass
            else:
                raise AssertionError('unsupported transcendental accepted')
            assert not any(c.flags.values())
            """);
        await Verify(script);
    }

    [Fact]
    public async Task UnrepresentableSqrtScaleFailsWithoutASecondRound()
    {
        using var script = new LythonEngine().Compile("""
            import decimal as d
            c=d.getcontext()
            for trapped in [True,False]:
                c.traps[d.InvalidOperation]=trapped
                try:
                    d.Decimal('2E-28').sqrt()
                except d.InvalidOperation:
                    pass
                else:
                    raise AssertionError('unrepresentable sqrt accepted')
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
