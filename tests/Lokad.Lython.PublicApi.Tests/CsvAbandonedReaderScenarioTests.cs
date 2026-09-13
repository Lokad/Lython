using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG01: abandoning readers mid-scan stays usable: after hundreds of dropped
/// partial scans, a live scan still completes with correct rows.
/// </summary>
public sealed class CsvAbandonedReaderScenarioTests
{
    [Fact]
    public async Task AbandonedScansStayUsable()
    {
        var script = new LythonEngine().Compile("""
            import csv
            lines = ["a,b,c,d"] * 2000
            i = 0
            while i < 200:
                r = csv.reader(lines)
                n = 0
                for row in r:
                    n = n + 1
                    if n >= 50:
                        break
                i = i + 1
            total = 0
            for row in csv.reader(lines):
                total = total + 1
            return total
            """);
        Assert.True(script.IsValid);
        var expected = new BigInteger(2000);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
