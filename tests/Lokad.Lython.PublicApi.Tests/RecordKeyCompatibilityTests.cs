using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RecordKeyCompatibilityTests
{
    // Independently executed under CPython3.13; protocol order, row mutation,
    // duplicate/reordered keys, reentrant hooks and actual CSV key aliases.
    private const string Source = """
        import csv
        import io
        import json

        rows = json.loads('[{"alpha":1,"beta":2},{"alpha":3,"beta":4},{"beta":5,"alpha":6},{"alpha":7,"alpha":8,"beta":9},{"alpha":10},{},{"alpha":11,"beta":12,"gamma":13}]')
        result = [[list(row.items()) for row in rows]]
        alias = rows[1]
        del rows[1]['alpha']
        rows[1]['beta'] = 40
        rows[1]['alpha'] = 30
        assert alias is rows[1] and rows[0]['alpha'] == 1
        del rows[0]['alpha']
        copy = rows[0].copy()
        result.append([list(rows[0].items()), list(rows[1].items()), list(copy.items())])
        result.append((rows[3].popitem(), list(rows[3].items())))
        rows[3].clear()
        rows[3]['z'] = 99
        result.append(list(rows[3].items()))
        result.append((rows[4].setdefault('alpha', 0), rows[4].setdefault('omega', 20), list(rows[4].items())))

        events = []
        class Key:
            def __hash__(self):
                events.append('hash')
                return hash('alpha')
            def __eq__(self, other):
                events.append('eq')
                return other == 'alpha'
        key = Key()
        record = json.loads('{"alpha":1,"beta":2}')
        result.append(record[key])
        record[key] = 7
        del record[key]
        result.append((events, list(record.items())))

        def integer(text):
            json.loads('{"nested":true,"extra":[]}')
            return int(text)
        result.append(json.loads('[{"alpha":1,"beta":2},{"alpha":3,"beta":4}]', parse_int=integer))
        result.append(json.loads('{"x":1,"x":2}', object_pairs_hook=list))

        names = ['a', 'b']
        reader = csv.DictReader(io.StringIO('1,2\n3,4\n5,6\n7,8\n'), fieldnames=names)
        first = next(reader)
        names[:] = ['b', 'a']
        second = next(reader)
        names[:] = ['a', 'a']
        third = next(reader)
        names[:] = ['x', 'y', 'z']
        fourth = next(reader)
        result.append([list(row.items()) for row in [first, second, third, fourth]])

        names = ['a' * 50, 'b' * 50]
        reader = csv.DictReader(io.StringIO('1,2\n3,4\n'), fieldnames=names)
        original = names[0]
        first = next(reader)
        names[0] = ''.join(['a' * 25, 'a' * 25])
        second = next(reader)
        assert next(iter(first)) is original
        assert next(iter(second)) is names[0]
        print(repr(result))
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MutableRecordsAndHooksPreservePythonOrderAndAliases(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(3 * 1024 * 1024);
        using var script = new LythonEngine().Compile(Source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("[[[('alpha', 1), ('beta', 2)], [('alpha', 3), ('beta', 4)], [('beta', 5), ('alpha', 6)], [('alpha', 8), ('beta', 9)], [('alpha', 10)], [], [('alpha', 11), ('beta', 12), ('gamma', 13)]], [[('beta', 2)], [('beta', 40), ('alpha', 30)], [('beta', 2)]], (('beta', 9), [('alpha', 8)]), [('z', 99)], (10, 20, [('alpha', 10), ('omega', 20)]), 1, (['hash', 'eq', 'hash', 'eq', 'hash', 'eq'], [('beta', 2)]), [{'alpha': 1, 'beta': 2}, {'alpha': 3, 'beta': 4}], [('x', 1), ('x', 2)], [[('a', '1'), ('b', '2')], [('b', '3'), ('a', '4')], [('a', '6')], [('x', '7'), ('y', '8'), ('z', None)]]]\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
