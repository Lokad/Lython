using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class StatisticsModuleFunctionTests
{
    [Fact]
    public void StatisticsModule_Functions_HaveDirectCoverage()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import statistics

vals = []
vals.append(str(statistics.mean([1, 2, 3, 4])))
vals.append(str(statistics.fmean([1, 2, 3, 4])))
vals.append(str(statistics.median([1, 4, 2, 3])))
vals.append(str(statistics.median_low([1, 2, 3, 4])))
vals.append(str(statistics.median_high([1, 2, 3, 4])))
vals.append(str(statistics.mode([3, 1, 3, 2, 2, 3])))
vals.append(str(statistics.multimode([1, 2, 1, 2, 3])))
vals.append(str(int(statistics.pvariance([2, 4, 4, 4, 5, 5, 7, 9]) * 1000)))
vals.append(str(int(statistics.variance([2, 4, 4, 4, 5, 5, 7, 9]) * 1000)))
vals.append(str(int(statistics.pstdev([2, 4, 4, 4, 5, 5, 7, 9]) * 1000)))
vals.append(str(int(statistics.stdev([2, 4, 4, 4, 5, 5, 7, 9]) * 1000)))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("2.5|2.5|2.5|2|3|3|[1, 2]|4000|4571|2000|2138", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
import statistics
statistics.mean([])
""",
        "StatisticsError",
        "at least one data point")]
    [InlineData(
        """
import statistics
statistics.variance([1])
""",
        "StatisticsError",
        "at least two data points")]
    [InlineData(
        """
import statistics
statistics.mean(["x"])
""",
        "TypeError",
        "real number")]
    public void StatisticsModule_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure!.ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }
}
