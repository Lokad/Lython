using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TextReaderAbandonmentTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void CompleteTextJobsReclaimDroppedReadersAndRejectRetainedReaders(bool close, bool retain)
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
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var host = new MockLythonHost(); host.SeedFile("/input.txt", "Alpha café 😀\n");
        var result = script.Run(host, new LythonRunOptions { MaxExecutionMemoryBytes = 262144 });
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
        if (retain)
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        }
        else
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new BigInteger(499), result.ReturnValue);
            Assert.Equal(string.Concat(Enumerable.Repeat("3\n", 500)), result.StandardOutput);
            Assert.Equal("alpha=1\ncafé=1\n😀=1\n", host.ReadText("/report.txt"));
        }
    }
}
