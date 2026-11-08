using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RecordFieldLookupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedFieldReadsObserveIndependentRowsMutationsAndProtocols(bool asynchronous)
    {
        const string source = """
            import json, csv
            def read(row, key):
                for _ in range(1):
                    return row[key]
            rows = json.loads('[{"a":1,"b":2},{"a":3,"b":4},{"a":5,"b":6}]')
            assert [read(row, 'a') for row in rows] == [1,3,5]
            observed = []
            for row in rows:
                observed.append(row['a'])
                row['a'] += 10
            assert observed == [1,3,5]
            for row in rows:
                row['a'] -= 10
            rows[1]['a'] = 30
            assert read(rows[0], 'a') == 1
            assert read(rows[1], 'a') == 30
            del rows[1]['a']
            try:
                read(rows[1], 'a')
                assert False
            except KeyError as error:
                assert error.args == ('a',)
            rows[1]['a'] = 31
            rows[2]['extra'] = 9
            assert read(rows[1], 'a') == 31
            assert read(rows[2], 'a') == 5
            assert list(rows[1]) == ['b','a']
            for key in ['a','b','aA'.lower()[0],'b']:
                assert read(rows[0], key) == {'a':1,'b':2}[key]
            rows[0].clear()
            rows[0]['b'] = 20
            rows[0]['a'] = 10
            assert read(rows[0], 'a') == 10
            assert read(rows[0], 'b') == 20
            records = list(csv.DictReader(['a,b\n','one,two\n','three,four\n']))
            assert [read(row, 'b') for row in records] == ['two','four']
            records[1]['b'] = 'changed'
            assert read(records[0], 'b') == 'two'
            assert read(records[1], 'b') == 'changed'
            class Mapping:
                def __getitem__(self, key):
                    with open('/value.txt') as stream:
                        return stream.read() + key
            assert read(Mapping(), 'a') == 'valuea'
            return 'record-fields-ok'
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics));
            var baseline = budget.ReservedBytes;
            for (var run = 0; run < 2; run++)
            {
                var immediate = new MockLythonHost();
                var delayed = new DelayedLythonHost();
                immediate.SeedFile("/value.txt", "value");
                delayed.SeedFile("/value.txt", "value");
                using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(immediate))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("record-fields-ok", result.ReturnValue);
                    if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
                    Assert.True(budget.ReservedBytes > baseline);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
