using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DecimalSignalBoundaryTests
{
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
