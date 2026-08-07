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
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("2.5|2.5|2.5|2|3|3|[1, 2]|4000|4571|2000|2138", host.ReadText("/out.txt"));
    }

    [Fact]
    public void StatisticsModeHelpers_AcceptGenericHashableObservations()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import statistics

values = [
    statistics.multimode("abac"),
    statistics.mode("abac"),
    statistics.multimode([("x", 1), ("y", 2), ("x", 1)]),
    statistics.multimode([]),
]
try:
    statistics.mode([[1], [1]])
except TypeError as error:
    values.append(error.type)
with open("/out.txt", "w") as output:
    output.write("|".join(str(value) for value in values))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("['a']|a|[('x', 1)]|[]|TypeError", host.ReadText("/out.txt"));
    }

    [Fact]
    public void StatisticsAggregates_AcceptWeightsAndSuppliedCenters()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import statistics

values = [
    statistics.fmean([1, 2, 3], weights=[1, 1, 2]),
    statistics.pvariance([1, 2, 3], mu=2),
    statistics.variance([1, 2, 3], xbar=2),
    int(statistics.pstdev([1, 2, 3], mu=1) * 1000),
    int(statistics.stdev([1, 2, 3], xbar=1) * 1000),
]
with open("/out.txt", "w") as output:
    output.write("|".join(str(value) for value in values))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.Null(result.Failure);
        Assert.Equal("2.25|0.6666666666666666|1.0|1290|1581", host.ReadText("/out.txt"));
    }

    [Fact]
    public void StatisticsModule_ExpandedFunctions_HaveDirectCoverage()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import statistics
from decimal import Decimal

x = [1, 2, 3, 4, 5]
y = [2, 3, 5, 7, 11]
lr = statistics.linear_regression(x, y)
prop = statistics.linear_regression(x, y, proportional=True)
manual = statistics.LinearRegression(3, -2)
normal = statistics.NormalDist(100, 15)
sampled = statistics.NormalDist.from_samples([2, 4, 4, 4, 5, 5, 7, 9])
shifted = normal + statistics.NormalDist(10, 15)
scaled = -((statistics.NormalDist(1, 2) * 3) / 2)
cuts = statistics.quantiles([1, 2, 3, 4, 5, 6, 7, 8, 9], n=4)
inclusive = statistics.quantiles([1, 2, 3, 4, 5, 6, 7, 8, 9], n=4, method="inclusive")
normal_cuts = normal.quantiles(n=4)

vals = []
vals.append(str(int((statistics.harmonic_mean([40, 60]) + 0.0001) * 1000)))
vals.append(str(int(statistics.harmonic_mean([40, 60], weights=[5, 30]) * 1000)))
vals.append(str(int(statistics.geometric_mean([54, 24, 36]) * 1000)))
vals.append(str(int(statistics.geometric_mean([1, 0]) * 1000)))
vals.append(str(statistics.median_grouped([52, 52, 53, 54], interval=1)))
vals.append(str(int(cuts[0] * 10)) + ":" + str(int(cuts[1] * 10)) + ":" + str(int(cuts[2] * 10)))
vals.append(str(int(inclusive[0] * 10)) + ":" + str(int(inclusive[1] * 10)) + ":" + str(int(inclusive[2] * 10)))
vals.append(str(statistics.quantiles([1], n=4)))
vals.append(str(int(statistics.covariance(x, y) * 1000)))
vals.append(str(int(statistics.correlation(x, y) * 1000000)))
vals.append(str(int(lr.slope * 1000)) + ":" + str(int(lr.intercept * 1000)) + ":" + str(lr._fields) + ":" + str(lr[0] == lr.slope) + ":" + str(lr.count(lr.slope)) + ":" + str(lr.index(lr.intercept)))
vals.append(str(int(prop.slope * 1000)) + ":" + str(int(prop.intercept * 1000)))
vals.append(str(lr._replace(intercept=2)))
vals.append(str(manual))
vals.append(str(int(normal.mean)) + ":" + str(int(normal.median)) + ":" + str(int(normal.mode)) + ":" + str(int(normal.stdev)) + ":" + str(int(normal.variance)))
vals.append(str(int(normal.pdf(100) * 1000000)))
vals.append(str(int(normal.cdf(100) * 1000)))
vals.append(str(int(normal.inv_cdf(0.5))))
vals.append(str(normal.zscore(130)))
vals.append(str(int(normal.overlap(statistics.NormalDist(110, 15)) * 1000)))
vals.append(str(int(normal_cuts[0] * 1000)) + ":" + str(int(normal_cuts[1] * 1000)) + ":" + str(int(normal_cuts[2] * 1000)))
vals.append(str(int(sampled.mean * 1000)) + ":" + str(int(sampled.stdev * 1000)) + ":" + str(int(sampled.variance * 1000)))
vals.append(str(int(shifted.mean)) + ":" + str(int(shifted.stdev * 1000)))
vals.append(str(int(scaled.mean * 1000)) + ":" + str(int(scaled.stdev * 1000)))
vals.append(str(isinstance(normal, statistics.NormalDist)))
vals.append(str(statistics.NormalDist(1, 2).samples(2, seed=123) == statistics.NormalDist(1, 2).samples(2, seed=123)))
vals.append(str(int(statistics.geometric_mean([Decimal("2"), Decimal("8")]) * 1000)))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            "48000|56000|36000|0|52.5|25:50:75|30:50:70|[1, 1, 1]|5500|972271|2200:-1000:('slope', 'intercept'):True:1:1|1927:0|LinearRegression(slope=2.2, intercept=2)|LinearRegression(slope=3, intercept=-2)|100:100:100:15:225|26596|500|100|2.0|738|89882:100000:110117|5000:2138:4571|110:21213|-1500:3000|True|True|4000",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void StatisticsModule_StaticContractsCoverExpandedSurface()
    {
        var valid = new LythonEngine().Run(
            """
import statistics

lr = statistics.linear_regression([1, 2, 3], [2, 4, 6])
normal = statistics.NormalDist(1, 2) + 3
out = [
    str(statistics.quantiles([1, 2, 3], n=2)[0]),
    str(lr._replace(intercept=0).intercept),
    str(int(normal.pdf(4) * 1000)),
    str(len(normal.samples(2, seed=1))),
]
return "|".join(out)
""",
            new MockLythonHost());

        Assert.True(
            valid.Success,
            valid.Failure?.Message ?? string.Join(" | ", valid.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.Equal("2|0.0|199|2", Assert.IsType<string>(valid.ReturnValue));

        var invalid = new LythonEngine().Run(
            """
import statistics

statistics.quantiles(1)
statistics.quantiles([1], n="x")
statistics.linear_regression([1], [2], proportional="yes")
statistics.LinearRegression("x", 1)
statistics.NormalDist("x")
statistics.NormalDist.from_samples(1)
statistics.NormalDist(0, 1).pdf()
statistics.NormalDist(0, 1).samples()
""",
            new MockLythonHost());

        Assert.False(invalid.Success);
        Assert.Null(invalid.Failure);
        Assert.True(
            invalid.Diagnostics.Count(d => d.Code is "LA3158" or "LA3163") >= 8,
            string.Join(" | ", invalid.Diagnostics.Select(d => d.Code + ":" + d.Message)));
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
    [InlineData(
        """
import statistics
statistics.harmonic_mean([-1, 2])
""",
        "StatisticsError",
        "negative values")]
    [InlineData(
        """
import statistics
statistics.harmonic_mean([1, 2], weights=[0, 0])
""",
        "StatisticsError",
        "Weighted sum must be positive")]
    [InlineData(
        """
import statistics
statistics.geometric_mean([1, -2])
""",
        "StatisticsError",
        "negative values")]
    [InlineData(
        """
import statistics
statistics.quantiles([1], n=0)
""",
        "ValueError",
        "n >= 1")]
    [InlineData(
        """
import statistics
statistics.quantiles([1], method="bogus")
""",
        "ValueError",
        "Unknown method")]
    [InlineData(
        """
import statistics
statistics.covariance([1, 2], [1])
""",
        "StatisticsError",
        "same number")]
    [InlineData(
        """
import statistics
statistics.correlation([1, 1], [2, 3])
""",
        "StatisticsError",
        "constant")]
    [InlineData(
        """
import statistics
statistics.linear_regression([1, 1], [2, 3])
""",
        "StatisticsError",
        "x is constant")]
    [InlineData(
        """
import statistics
statistics.NormalDist(0, -1)
""",
        "StatisticsError",
        "sigma must be non-negative")]
    [InlineData(
        """
import statistics
statistics.NormalDist(0, 0).pdf(0)
""",
        "StatisticsError",
        "sigma is zero")]
    [InlineData(
        """
import statistics
statistics.NormalDist(0, 0).zscore(1)
""",
        "StatisticsError",
        "sigma is zero")]
    [InlineData(
        """
import statistics
statistics.NormalDist().inv_cdf(0)
""",
        "StatisticsError",
        "0.0 < p < 1.0")]
    [InlineData(
        """
import statistics
statistics.kde([1, 2])
""",
        "NotImplementedError",
        "unsupported")]
    public void StatisticsModule_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }
}
