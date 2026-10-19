using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringKeyCollisionTests
{
    [Theory]
    [InlineData(false, "float('nan')")]
    [InlineData(true, "float('nan')")]
    [InlineData(false, "complex(float('nan'), 0)")]
    [InlineData(true, "complex(float('nan'), 0)")]
    public async Task NonReflexiveKeysUseIdentityWithoutChangingValueEquality(bool asynchronous, string expression)
    {
        var source = "key=" + expression + "\nother=" + expression + "\n" + """
table = {key: 7}
print(table[key], table.get(key), key in table, other in table)
table[key] = 9
print(len(table), table[key], key == key)
values = {key, key, other}
print(len(values), key in values, other in values)
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(x => x.Message)));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("7 7 True False\n1 9 False\n2 True True\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnicodeKeysAndReentrantProtocolCollisionsKeepTheirBehavior(bool asynchronous)
    {
        const string source = """
text = 'café😀'
seen = []
nested = {text: 7}
class Key:
    def __hash__(self):
        return hash(text)
    def __eq__(self, other):
        seen.append(nested[''.join(['ca', 'fé', '😀'])])
        return other == text
table = {Key(): 23}
print(table[''.join(['café', '😀'])])
print(seen)
numbers = {True: 'first', 1.0: 'second'}
print(len(numbers), numbers[1])
nan = float('nan')
print(nan == nan)
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(x => x.Message)));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("23\n[7]\n1 second\nFalse\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
