using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DecimalPolicyBoundaryTests
{
    [Theory]
    [InlineData("Emin", -9, -999999)]
    [InlineData("Emax", 9, 999999)]
    [InlineData("capitals", 0, 1)]
    [InlineData("clamp", 1, 0)]
    public async Task UnsupportedSettingsFailBeforeReplacingOrMutatingTheContext(string name, int value, int supported)
    {
        using var script = new LythonEngine().Compile($$"""
            import decimal as d
            original = d.getcontext()
            try:
                setattr(original, '{{name}}', {{value}})
            except NotImplementedError:
                pass
            else:
                raise AssertionError('unsupported setting accepted')
            assert getattr(original, '{{name}}') == {{supported}}
            current = original
            try:
                current = d.Context({{name}}={{value}})
            except NotImplementedError:
                pass
            else:
                raise AssertionError('unsupported construction accepted')
            assert current is original and d.getcontext() is original
            assert getattr(original.copy(), '{{name}}') == {{supported}}
            with d.localcontext(original) as child:
                assert getattr(child, '{{name}}') == {{supported}}
            assert d.getcontext() is original
            """);
        await VerifyBothApis(script);
    }

    [Theory]
    [InlineData("Emin", -1000000)]
    [InlineData("Emax", 1000000)]
    [InlineData("capitals", 2)]
    [InlineData("clamp", 2)]
    public async Task InvalidRangesRetainTheirValueErrors(string name, int value)
    {
        using var script = new LythonEngine().Compile($$"""
            import decimal as d
            original = d.getcontext()
            before = getattr(original, '{{name}}')
            try:
                setattr(original, '{{name}}', {{value}})
            except ValueError:
                pass
            else:
                raise AssertionError('invalid setting accepted')
            assert getattr(original, '{{name}}') == before
            try:
                d.Context({{name}}={{value}})
            except ValueError:
                pass
            else:
                raise AssertionError('invalid construction accepted')
            """);
        await VerifyBothApis(script);
    }

    [Fact]
    public async Task SupportedDefaultsRemainReadableAndAccepted()
    {
        using var script = new LythonEngine().Compile("""
            import decimal as d
            c = d.Context(Emin=-999999, Emax=999999, capitals=1, clamp=0)
            c.Emin = -999999
            c.Emax = 999999
            c.capitals = 1
            c.clamp = 0
            assert (c.Emin,c.Emax,c.capitals,c.clamp) == (-999999,999999,1,0)
            """);
        await VerifyBothApis(script);
    }

    private static async Task VerifyBothApis(LythonCompiledScript script)
    {
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                       : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Empty(result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
