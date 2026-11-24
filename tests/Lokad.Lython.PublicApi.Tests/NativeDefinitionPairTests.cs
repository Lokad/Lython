using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeDefinitionPairTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallablePairKeepsIndependentDefaultsAndExactResults(bool asynchronous)
    {
        const string source = """
            def make(seed):
                def apply(value, offset=seed % 7, /, *, factor=seed % 5 + 1, enabled=seed % 3 != 0):
                    return (value + offset) * factor if enabled else value
                alternate = lambda value, offset=(seed + 1) % 7, /, *, factor=2: (value - offset) * factor
                return apply, alternate
            def nested(seed):
                left, right = make(seed)
                return left, right

            first, second = make(307)
            other_first, other_second = nested(307)
            kept = make(307)
            print(first(5), second(5), first is other_first, second is other_second)
            defaults = first.__kwdefaults__
            defaults['factor'] = 2
            print(first(5), other_first(5), kept[0](5), defaults is first.__kwdefaults__)
            print(first.__defaults__, second.__defaults__)
            """;
        var budget = new LythonMemoryBudget(768 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("33 10 False False\n22 33 33 True\n(6,) (0,)\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SecondDefinitionFailureLeavesUnpackTargetsIntact(bool asynchronous)
    {
        const string source = """
            def make(seed):
                def apply(value=seed % 13):
                    return value
                other = lambda value=10 // seed: value
                return apply, other

            first, second = 'old first', 'old second'
            try:
                first, second = make(0)
            except ZeroDivisionError:
                print(first, second)
            first, second = make(2)
            print(first(), second())
            """;
        var budget = new LythonMemoryBudget(768 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("old first old second\n2 5\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
