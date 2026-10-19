using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TextReaderAbandonmentTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void CompleteTextJobBatchesReleaseOwnersAndRejectRetainedReaders(bool close, bool retain)
    {
        var original = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", "text-summary.py")).Replace("\r\n", "\n");
        var lines = original.Split('\n');
        if (!close)
        {
            var opening = Array.IndexOf(lines, "with open('input.txt') as source:");
            Assert.True(opening >= 0);
            lines[opening] = "source = open('input.txt')";
            for (var i = opening + 1; i < lines.Length && lines[i].StartsWith("    ", StringComparison.Ordinal); i++)
                lines[i] = lines[i][4..];
        }
        var source = "kept = []\ndef job():\n" + string.Join('\n', lines.Select(line => " " + line)) +
            "\n return source\nfor i in range(500):\n reader = job()\n" + (retain ? " kept.append(reader)\n" : "") + " reader = None\nreturn i\n";
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(retain ? source : source.Replace("range(500)", "range(count)"), budget);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new MockLythonHost(); host.SeedFile("/input.txt", "Alpha café 😀\n");
        if (retain)
        {
            using var result = script.Run(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(262144) });
            Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        }
        else
        {
            var output = new System.Text.StringBuilder();
            for (var start = 0; start < 500; start += 16)
            {
                var count = Math.Min(16, 500 - start);
                using (var result = script.Run(host, new LythonRunOptions
                {
                    MemoryBudget = new LythonMemoryBudget(262144),
                    Globals = new Dictionary<string, object?> { ["count"] = count }
                }))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(new BigInteger(count - 1), result.ReturnValue);
                    Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
                    output.Append(result.StandardOutput);
                    Assert.Equal("alpha=1\ncafé=1\n😀=1\n", host.ReadText("/report.txt"));
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            Assert.Equal(string.Concat(Enumerable.Repeat("3\n", 500)), output.ToString());
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
