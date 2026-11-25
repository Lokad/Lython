using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeDefinitionPairTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrivateCallKeepsRetainedCapturesFailuresAndDynamicOperands(bool asynchronous)
    {
        const string source = """
            def make(seed):
                offset = seed % 7
                def left(value):
                    return value + offset
                def alternate():
                    step = 7 // seed
                    return lambda value: value - step
                return left, alternate()

            class Dynamic:
                def __mod__(self, divisor):
                    print('mod', divisor)
                    return 1
                def __rfloordiv__(self, left):
                    with open('step.txt') as source:
                        print('divide', left)
                        return int(source.read())

            first, second = make(2)
            a, b = make(2)
            print(first(10), second(10), first is a, second is b, second.__qualname__)
            try:
                a, b = make(0)
            except ZeroDivisionError:
                print(a(10), b(10))
            dynamic_first, dynamic_second = make(Dynamic())
            print(dynamic_first(10), dynamic_second(10))
            """;
        var budget = new LythonMemoryBudget(768 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/step.txt", "4");
        var asyncHost = new DelayedLythonHost();
        asyncHost.SeedFile("/step.txt", "4");
        using (var result = asynchronous ? await script.RunAsync(asyncHost) : script.Run(syncHost))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("12 7 False False make.<locals>.alternate.<locals>.<lambda>\n12 7\nmod 7\ndivide 7\n11 6\n",
                result.StandardOutput);
            if (asynchronous) Assert.True(asyncHost.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

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
                def apply(value=seed % 13 + 1000):
                    return value
                other = lambda value=10 // seed: value
                return apply, other

            first, second = 'old first', 'old second'
            try:
                first, second = make(0)
            except ZeroDivisionError:
                print(first, second)
            first, second = make(2)
            kept = first()
            print(kept, second(), kept is first(), kept is first.__defaults__[0])
            first.__defaults__ = (kept + 1,)
            print(first(), kept)
            """;
        var budget = new LythonMemoryBudget(768 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("old first old second\n1002 5 True True\n1003 1002\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
