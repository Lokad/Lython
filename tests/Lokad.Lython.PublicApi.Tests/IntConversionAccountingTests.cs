using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG01: int() renders its failure literal lazily like float(), so repeated
/// parsing never retains a repr per call. Failure texts are unchanged; only
/// the retained pressure disappears, which unblocks aggregating scans.
/// </summary>
public sealed class IntConversionAccountingTests
{
    [Fact]
    public async Task RepeatedIntParsingStaysBounded()
    {
        // 100k parses used to commit ~13MB of retained reprs; only transient
        // scratch remains, so a 2MB budget fits with margin.
        var script = new LythonEngine().Compile(
            """
            total = 0
            i = 0
            while i < 100000:
                total = total + int("12345")
                i = i + 1
            return total
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 2097152 };

        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(1234500000), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(1234500000), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task IntFailureTextsUnchanged()
    {
        var script = new LythonEngine().Compile(
            """
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
    public async Task AggregatingScanWithIntStaysBounded()
    {
        // The calibration shape: per-row int() parsing plus accumulation over
        // 100k rows (~5.7MB) under the same 8MB budget as the counting scan.
        var content = new StringBuilder();
        for (var i = 0; i < 100000; i++)
        {
            content.Append(i).Append(',').Append('y', 50).Append('\n');
        }

        var script = new LythonEngine().Compile(
            """
            import csv
            with open("/big.csv") as f:
                total = 0
                for row in csv.reader(f):
                    total = total + int(row[0])
            return total
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 8388608 };

        var host = new MockLythonHost();
        host.SeedFile("/big.csv", content.ToString());
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(4999950000), sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedFile("/big.csv", content.ToString());
        var asyncResult = await script.RunAsync(host2, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(4999950000), asyncResult.ReturnValue);
    }
}
