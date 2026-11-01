using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DictionaryOrderingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TupleTypesKeepDistinctHashableIdentitiesInGrownMappings(bool asynchronous)
    {
        const string source = """
            from collections import namedtuple
            import time
            first = namedtuple('Point', 'x')
            second = namedtuple('Point', 'x')
            data = {str(i): i for i in range(8)}
            for index, cls in enumerate([first, second, time.struct_time]):
                data[cls] = index
            print([data[cls] for cls in [first, second, time.struct_time]])
            print(len({first, second, time.struct_time}), hash(first) == hash(first))
            del data[first]
            data[first] = 99
            print(list(data)[-1] is first, data[second], data[first])
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("[0, 1, 2]\n3 True\nTrue 1 99\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task ReinsertedKeysMoveToTheEndForViewsCopiesSerializationAndCalls()
    {
        // Frozen CPython 3.13 golden across the former small-storage boundary.
        const string source = """
            import json
            for size in [5, 6, 7, 8, 20]:
                data = {str(i): i for i in range(size)}
                keys = data.keys()
                data.pop('2')
                data['2'] = 99
                expected = [str(i) for i in range(size) if i != 2] + ['2']
                print(size, list(data) == expected, list(keys) == expected,
                      list(data.values()) == [i for i in range(size) if i != 2] + [99],
                      list(reversed(data)) == list(reversed(expected)))
                print(list(dict(data).items()) == list(data.items()), json.dumps(data).endswith('"2": 99}'))
                def keyword_order(**kwargs):
                    return list(kwargs)
                print(keyword_order(**data) == expected, data.popitem() == ('2', 99))
            """;
        const string expected = "5 True True True True\nTrue True\nTrue True\n6 True True True True\nTrue True\nTrue True\n7 True True True True\nTrue True\nTrue True\n8 True True True True\nTrue True\nTrue True\n20 True True True True\nTrue True\nTrue True\n";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
