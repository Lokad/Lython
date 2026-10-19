using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeConsumerStreamingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task NativeCsvAggregationBatchesPreserveWorkingAndRetainedStorage(bool retain, bool asynchronous)
        => VerifyNativeBatches(retain, asynchronous, retain ? 5000 : 256);

    [Theory]
    [Trait("Category", "Stress")]
    [InlineData(false)]
    [InlineData(true)]
    public Task SustainedNativeCsvAggregationBatches(bool asynchronous)
        => VerifyNativeBatches(false, asynchronous, 5000);

    private static async Task VerifyNativeBatches(bool retain, bool asynchronous, int totalRows)
    {
        // Each finite execution keeps sum active through generator pulls and
        // awaits. Host batches relinquish working storage; retained rows deny.
        var source = "retain = " + (retain ? "True" : "False") + "\n" + """
import csv
kept = []
def amounts(rows):
    for row in rows:
        if retain:
            kept.append(row)
        yield int(row[1])
with open('/input.csv') as file:
    total = sum(amounts(csv.reader(file)))
with open('/report.txt', 'w') as report:
    report.write(str(total) + '\n')
print(total)
""";
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        var baseline = budget.ReservedBytes;
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var completed = 0;
        for (var offset = 0; offset < totalRows; offset += retain ? totalRows : 32)
        {
            var rows = retain ? totalRows : Math.Min(32, totalRows - offset);
            var input = string.Concat(Enumerable.Repeat("red,1,ok\n", rows));
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            ILythonHost host = (ILythonHost?)delayed ?? mock!;
            if (delayed is not null) delayed.SeedFile("/input.csv", input);
            else mock!.SeedFile("/input.csv", input);
            var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(262144) };
            using var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
            if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
            Assert.Empty(result.StandardError);
            if (retain)
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
                Assert.False(await host.ExistsAsync("/report.txt", CancellationToken.None));
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal($"{rows}\n", result.StandardOutput);
                Assert.Equal($"{rows}\n", delayed is not null ? delayed.ReadText("/report.txt") : mock!.ReadText("/report.txt"));
            }
            result.Dispose();
            Assert.Equal(baseline, budget.ReservedBytes);
            completed += rows;
        }
        Assert.Equal(totalRows, completed);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
