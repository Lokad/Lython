using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvLongFieldLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedQuotedUnicodeFieldsSurviveLaterReadsAndReaderCleanup(bool asynchronous)
    {
        const string source = """
            import csv
            with open('/rows.csv',newline='') as stream:
                reader=csv.reader(stream)
                first=next(reader)
                kept=first[1]
                first=None
                second=next(reader)
                assert next(reader,None) is None
            reader=None
            return kept,second
            """;
        var first = new[] { "source/path/first.txt", "Résumé😀 \"quoted\"\nfirst detail", "first_tail_field" };
        var second = new[] { "source/path/second.txt", new string('z', 20 * 1024) + "é😀", "second_tail_field" };
        var data = Row(first) + Row(second);
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            ILythonHost host;
            if (asynchronous)
            {
                var delayed = new DelayedLythonHost();
                delayed.SeedFile("/rows.csv", data);
                host = delayed;
            }
            else
            {
                var synchronous = new MockLythonHost();
                synchronous.SeedFile("/rows.csv", data);
                host = synchronous;
            }
            using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                var values = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue);
                Assert.Equal(first[1], values[0]);
                Assert.Equal(second.Cast<object?>(), Assert.IsAssignableFrom<IReadOnlyList<object?>>(values[1]));
                Assert.True(budget.ReservedBytes > baseline);
                if (asynchronous) Assert.True(((DelayedLythonHost)host).CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static string Row(IEnumerable<string> fields)
        => string.Join(",", fields.Select(value => "\"" + value.Replace("\"", "\"\"") + "\"")) + "\n";
}
