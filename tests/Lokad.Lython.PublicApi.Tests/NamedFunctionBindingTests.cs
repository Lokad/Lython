using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NamedFunctionBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NamedCallsKeepLiveDefaultsAndReboundCallables(bool asynchronous)
    {
        const string source = """
def choose(first, /, second=3, *, extra=5):
    return [first, second, extra]
first = choose(1, extra=9)
choose.__defaults__ = (7,)
choose.__kwdefaults__ = {'extra': 11}
second = choose(2, second=4)
original = choose
def other(*args, **kwargs):
    return [list(args), kwargs]
choose = other
third = choose(5, extra=13)
choose = original
last = choose(6, extra=17)
return [first, second, third, last]
""";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            for (var run = 0; run < 2; run++)
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    var values = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue);
                    Assert.Equal(4, values.Count);
                    Assert.Equal(new object?[] { new BigInteger(1), new BigInteger(3), new BigInteger(9) }, Sequence(values[0]));
                    Assert.Equal(new object?[] { new BigInteger(2), new BigInteger(4), new BigInteger(11) }, Sequence(values[1]));
                    var rebound = Sequence(values[2]);
                    Assert.Equal(new BigInteger(5), Assert.Single(Sequence(rebound[0])));
                    var keywords = Assert.IsAssignableFrom<IReadOnlyDictionary<object, object?>>(rebound[1]);
                    Assert.Equal(new BigInteger(13), keywords["extra"]);
                    Assert.Equal(new object?[] { new BigInteger(6), new BigInteger(7), new BigInteger(17) }, Sequence(values[3]));
                    Assert.True(budget.ReservedBytes > baseline);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DecoratedTargetsAndSuspendedBodiesKeepCanonicalBinding(bool asynchronous)
    {
        const string source = """
def wrap(original):
    def replacement(*args, **kwargs):
        return original(*args, **kwargs)
    return replacement
@wrap
def decorated(value, *, suffix='?'):
    return value + suffix
def read(path, *, suffix='?'):
    with open(path) as source:
        return source.read() + suffix
return [decorated('value', suffix='!'), read('marker.txt', suffix='.')]
""";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var host = new MockLythonHost(); host.SeedFile("/marker.txt", "retained");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/marker.txt", "retained");
            using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new object?[] { "value!", "retained." }, Sequence(result.ReturnValue));
                if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialNamedCallsAwaitProtocolDefaultsBeforeEnteringTheBody(bool asynchronous)
    {
        const string source = """
class Key:
    def __hash__(self): return hash('suffix')
    def __eq__(self, other):
        with open('default.txt') as source:
            return source.read() == other
def target(value, *, suffix='?'):
    return value + suffix
target.__kwdefaults__ = {Key(): '!'}
return target(value='body')
""";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var host = new MockLythonHost(); host.SeedFile("/default.txt", "suffix");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/default.txt", "suffix");
            using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("body!", result.ReturnValue);
                if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static IReadOnlyList<object?> Sequence(object? value)
        => Assert.IsAssignableFrom<IReadOnlyList<object?>>(value);
}
