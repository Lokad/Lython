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
    public async Task DroppedSourcesRemainBoundedButRetainedSourcesReject(string retention, bool asynchronous)
    {
        var retain = retention == "all" ? "    held.append(reader)\n" :
            retention == "one" ? "    if i == 0:\n        held.append(reader)\n" : "";
        var source = "import csv\nheld = []\nfor i in range(5000):\n    reader = csv.reader(['café,😀'])\n" + retain;
        source += retention == "one" ? "return next(held[0])\n" : "return 5000\n";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.Empty(result.StandardOutput);
        Assert.Empty(result.StandardError);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
        if (retention == "all")
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.FailureAttribution?.Reason);
        }
        else
        {
            Assert.True(result.Success, result.Failure?.Message);
            if (retention == "one") Assert.Equal(new List<object?> { "café", "😀" }, result.ReturnValue);
            else Assert.Equal(new BigInteger(5000), result.ReturnValue);
        }
    }
}
