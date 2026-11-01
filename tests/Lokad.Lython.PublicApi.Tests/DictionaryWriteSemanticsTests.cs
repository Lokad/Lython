using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DictionaryWriteSemanticsTests
{
    private const string Source = """
        original = ('key', True)
        equal = ('key', 1)
        data = {original: 0, 'tail': 9}
        cursor = iter(data)
        for key in data:
            if key == original:
                data[equal] = 7
        print(next(cursor) is original, list(data.values()), list(data)[0] is original)
        del data[equal]
        data[equal] = 8
        print(list(data)[-1] is equal, list(data.values()))
        events = []
        armed = False
        data = {}
        class Key:
            def __init__(self, number):
                self.number = number
            def __hash__(self):
                events.append('hash:' + str(self.number))
                return 1
            def __eq__(self, other):
                global armed
                events.append('eq')
                if armed:
                    armed = False
                    data['side'] = 5
                return isinstance(other, Key)
        stored = Key(1)
        lookup = Key(2)
        data[stored] = 10
        events.clear()
        armed = True
        data[lookup] = 20
        print(events)
        print(list(data)[0] is stored, data['side'], data[stored])
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacementKeepsKeysViewsAndReentrantEffects(bool asynchronous)
    {
        // Independent CPython 3.13.2 golden; callback counts are Python effects.
        const string expected = "True [7, 9] True\nTrue [9, 8]\n['hash:2', 'eq']\nTrue 5 20\n";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(Source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using (var result = asynchronous
                ? await script.RunAsync(new MockLythonHost())
                : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
