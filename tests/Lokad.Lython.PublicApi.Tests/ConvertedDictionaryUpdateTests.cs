using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ConvertedDictionaryUpdateTests
{
    [Fact]
    public async Task PreservesGetValueAndCapturedConverterAcrossDynamicFieldRead()
    {
        const string source = """
            convert = int
            def update(totals, name, row):
                totals[name] = totals.get(name, 0) + convert(row['quantity'])

            totals = {}
            update(totals, 'service', {'quantity': '1000'})
            retained = totals['service']
            update(totals, 'service', {'quantity': '25'})
            print(retained, retained is totals['service'], totals['service'])

            def offset(text):
                with open('delta.txt') as f:
                    return int(text) + int(f.read())

            class Row:
                def __getitem__(self, key):
                    global convert
                    convert = offset
                    totals['service'] = 50000
                    with open('delta.txt') as f:
                        return f.read()

            update(totals, 'service', Row())
            print(totals['service'])
            update(totals, 'service', {'quantity': '3'})
            print(totals['service'])

            convert = int
            try:
                update(totals, 'service', {'quantity': 'bad'})
            except ValueError:
                print(totals['service'])
            try:
                update(totals, 'service', {})
            except KeyError:
                print(totals['service'])
            update(totals, 'service', {'quantity': '1000000000000000000000000'})
            print(totals['service'])
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var sync = new MockLythonHost();
            sync.SeedFile("/delta.txt", "7");
            using (var result = script.Run(sync)) Check(result);
            Assert.Equal(baseline, budget.ReservedBytes);

            var delayed = new DelayedLythonHost();
            delayed.SeedFile("/delta.txt", "7");
            using (var result = await script.RunAsync(delayed)) Check(result);
            Assert.True(delayed.CompletedAsynchronously > 0);
            Assert.Equal(2, delayed.ClosedFileCount);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);

        static void Check(LythonExecutionResult result)
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("1000 False 1025\n1032\n1042\n1042\n1042\n1000000000000000000001042\n", result.StandardOutput);
        }
    }
}
