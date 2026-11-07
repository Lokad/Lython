using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PreparedFunctionDefaultsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedDefinitionsKeepDefaultsAndClosuresIndependent(bool asynchronous)
    {
        const string source = """
def make(seed):
    def call(value=seed, /, *extra, flag=seed, **named):
        return value, extra, flag, named, seed
    def stream(value=seed, *, flag=seed):
        yield value, flag, seed
    return call, stream, lambda value=seed, *, flag=seed: (value, flag, seed)
first, first_stream, first_lambda = make(2)
second, second_stream, second_lambda = make(7)
first.__defaults__ = (11,)
first.__kwdefaults__['flag'] = 13
first_stream.__kwdefaults__['flag'] = 17
first_lambda.__defaults__ = (19,)
print(first())
print(second(23, 29, value=31, flag=37))
print(list(first_stream()), list(second_stream()))
print(first_lambda(), second_lambda())
print(second.__defaults__, second.__kwdefaults__)
""";
        var budget = new LythonMemoryBudget(16 * 1024 * 1024);
        using (var compiled = new LythonEngine().Compile(source, budget))
        {
            Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics));
            var baseline = budget.ReservedBytes;
            for (var run = 0; run < 2; run++)
            {
                using (var result = asynchronous
                    ? await compiled.RunAsync(new MockLythonHost())
                    : compiled.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("(11, (), 13, {}, 2)\n(23, (29,), 37, {'value': 31}, 7)\n" +
                        "[(2, 17, 2)] [(7, 7, 7)]\n(19, 2, 2) (7, 7, 7)\n(7,) {'flag': 7}\n", result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
