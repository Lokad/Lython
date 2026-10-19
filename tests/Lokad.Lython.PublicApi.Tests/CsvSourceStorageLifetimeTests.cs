using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvSourceStorageLifetimeTests
{
    [Theory]
    [InlineData("drop", false)]
    [InlineData("one", false)]
    [InlineData("all", false)]
    [InlineData("drop", true)]
    [InlineData("one", true)]
    [InlineData("all", true)]
    public async Task SourceBatchesReturnFundingAndRetainedSourcesReject(string retention, bool asynchronous)
    {
        var retain = retention == "all" ? "    held.append(reader)\n" :
            retention == "one" ? "    if i == 0:\n        held.append(reader)\n" : "";
        var source = "import csv\nheld = []\nfor i in range(5000):\n    reader = csv.reader(['café,😀'])\n" + retain;
        source += retention == "one" ? "return next(held[0])\n" : "return 5000\n";
        var retained = retention == "all";
        var count = retained ? 5000 : 16;
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile(source.Replace("5000", count.ToString()), budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(262144) };
        for (var batch = 0; batch < (retained ? 1 : 8); batch++)
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.Empty(result.StandardOutput);
            Assert.Empty(result.StandardError);
            Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
            if (retention == "all")
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                if (retention == "one") Assert.Equal(new List<object?> { "café", "😀" }, result.ReturnValue);
                else Assert.Equal(new BigInteger(count), result.ReturnValue);
            }
            result.Dispose();
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
