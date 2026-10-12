using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeConsumerStreamingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CsvAggregationWithinOneNativeCallPreservesLiveMemoryProtection(bool retain, bool asynchronous)
    {
        // sum remains active across every generator iteration and host await.
        // A rule that pins all allocations until native-call return would
        // incorrectly turn this dropped-data job into cumulative accounting.
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
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var input = string.Concat(Enumerable.Repeat("red,1,ok\n", 5000));
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        ILythonHost host = (ILythonHost?)delayed ?? mock!;
        if (delayed is not null) delayed.SeedFile("/input.csv", input);
        else mock!.SeedFile("/input.csv", input);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
        Assert.Empty(result.StandardError);
        if (retain)
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.False(await host.ExistsAsync("/report.txt", CancellationToken.None));
        }
        else
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("5000\n", result.StandardOutput);
            Assert.Equal("5000\n", delayed is not null ? delayed.ReadText("/report.txt") : mock!.ReadText("/report.txt"));
        }
    }
}
