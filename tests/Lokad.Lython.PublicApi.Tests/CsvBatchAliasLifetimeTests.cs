using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvBatchAliasLifetimeTests
{
    public static IEnumerable<object[]> ModesAndRetention()
    {
        foreach (var retention in new[] { "drop", "rows", "fields", "first" })
            foreach (var asynchronous in new[] { false, true })
                yield return [retention, asynchronous];
    }

    [Theory]
    [MemberData(nameof(ModesAndRetention))]
    public async Task MediatedBatchScansKeepLiveAliasesFunded(string retention, bool asynchronous)
    {
        var body = retention switch
        {
            "rows" => " rows = list(reader)\nreturn len(rows)\n",
            "fields" => " fields = []\n for row in reader:\n  fields.append(row[0])\nreturn len(fields)\n",
            "first" => " first = None\n total = 0\n for row in reader:\n  if first is None:\n   first = row[0]\n  total += 1\nreturn total == 4000 and first == 'a'\n",
            _ => " total = 0\n for row in reader:\n  total += 1\nreturn total\n"
        };
        var retained = retention is "rows" or "fields";
        var count = retained ? 4000 : 16;
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile("import csv\nwith open('/rows.csv', 'r') as stream:\n reader = csv.reader(stream)\n" + body.Replace("4000", count.ToString()), budget);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var text = string.Concat(Enumerable.Repeat("a,b,c,d,e,f\n", count));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        for (var batch = 0; batch < (retained ? 1 : 8); batch++)
        {
            LythonExecutionResult result;
            if (asynchronous)
            {
                var host = new DelayedLythonHost();
                host.SeedFile("/rows.csv", text);
                result = await script.RunAsync(host, options);
                Assert.True(host.CompletedAsynchronously > 0);
            }
            else
            {
                var host = new MockLythonHost();
                host.SeedFile("/rows.csv", text);
                result = script.Run(host, options);
            }
            Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
            if (retention is "rows" or "fields")
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(retention == "first" ? true : (object)new BigInteger(count), result.ReturnValue);
            }
            result.Dispose();
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
