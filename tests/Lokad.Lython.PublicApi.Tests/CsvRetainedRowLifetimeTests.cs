using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvRetainedRowLifetimeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RowsOutliveCollectedReaderAndReclaimAfterDropping(bool dictionary, bool asynchronous)
    {
        var reader = dictionary ? "DictReader" : "reader";
        var script = new LythonEngine().Compile($$"""
            import csv
            completed=0
            for index in range(8):
                with open('/csv') as file:
                    rows=list(csv.{{reader}}(file))
                assert len(rows)=={{(dictionary ? 1000 : 1001)}}
                print('retained')
                rows=None
                file=None
                completed+=1
            return completed
            """);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var host = new MockLythonHost();
        host.SeedFile("/csv", "a,b,c,d,e,f\n" + string.Concat(Enumerable.Repeat("1,2,3,4,5,6\n", 1000)));
        host.OnStandardOutputWrite = () =>
        {
            if (!host.CapturedStandardOutput().EndsWith("retained\n", StringComparison.Ordinal)) return;
            // Collect the reader while its returned rows remain guest-retained.
            // Subsequent CSV pulls reconcile the source registry on its normal cadence.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        };
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 3L * 1024 * 1024 };
        var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(8), result.ReturnValue);
        Assert.Equal(string.Concat(Enumerable.Repeat("retained\n", 8)), result.StandardOutput);
        Assert.True(result.PeakExecutionMemoryBytes <= options.MaxExecutionMemoryBytes!.Value.Bytes);
        Assert.True(host.MaxRangeBytesServed <= 16384);
    }
}
