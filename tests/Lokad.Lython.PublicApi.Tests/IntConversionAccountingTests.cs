using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG01: int() renders its failure literal lazily like float(), so repeated
/// parsing never retains a repr per call. Failure texts are unchanged; only
/// the retained pressure disappears, which unblocks aggregating scans.
/// </summary>
public class IntConversionAccountingTests : LifetimeScenario
{
    protected override int Iterations(int stressIterations) => Math.Min(stressIterations, 1024);
    protected override long Budget(long stressBudget) => Math.Min(stressBudget, 131072);

    [Fact]
    public Task IntParsingBatchesReturnExecutionFunding()
        => AssertBatchCompletes($"""
            total = 0
            for i in range({BatchIterations(100000)}):
                total += int("12345")
            return total
            """, (12345L * BatchIterations(100000)).ToString(), Budget(2097152), Iterations(100000));

    [Fact]
    public async Task IntFailureTextsUnchanged()
    {
        var script = new LythonEngine().Compile(
            $"""
            results = []
            for bad in ["x", "  0xFF  ", "1__0"]:
                try:
                    int(bad)
                except ValueError as e:
                    results.append(str(e))
            try:
                int(b"0xFF")
            except ValueError as e:
                results.append(str(e))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "invalid literal for int() with base 10: 'x'",
            "invalid literal for int() with base 10: '  0xFF  '",
            "invalid literal for int() with base 10: '1__0'",
            "invalid literal for int() with base 10: b'0xFF'",
        };

        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CsvIntAggregationBatchesReturnSharedAccounting()
    {
        var rows = Iterations(100000);
        var batchSize = BatchIterations(100000);
        var budget = new LythonMemoryBudget(Budget(8388608) + 512 * 1024);
        using var script = new LythonEngine().Compile("""
            import csv
            with open('/big.csv') as stream:
                total = 0
                for row in csv.reader(stream):
                    total += int(row[0])
            return total
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(Budget(8388608)) };
        long syncTotal = 0, asyncTotal = 0;
        for (var offset = 0; offset < rows; offset += batchSize)
        {
            var count = Math.Min(batchSize, rows - offset);
            var content = new StringBuilder();
            for (var i = offset; i < offset + count; i++) content.Append(i).Append(',').Append('y', 50).Append('\n');
            var expected = new BigInteger((2L * offset + count - 1) * count / 2);
            var host = new MockLythonHost();
            host.SeedFile("/big.csv", content.ToString());
            using (var result = script.Run(host, options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.ReturnValue);
                syncTotal += (long)expected;
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            var asyncHost = new MockLythonHost();
            asyncHost.SeedFile("/big.csv", content.ToString());
            using (var result = await script.RunAsync(asyncHost, options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.ReturnValue);
                asyncTotal += (long)expected;
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        var golden = (long)rows * (rows - 1) / 2;
        Assert.Equal(golden, syncTotal);
        Assert.Equal(golden, asyncTotal);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
