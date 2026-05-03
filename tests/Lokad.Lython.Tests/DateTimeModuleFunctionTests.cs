using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DateTimeModuleFunctionTests
{
    [Theory]
    [InlineData("datetime.MINYEAR", "1")]
    [InlineData("datetime.MAXYEAR", "9999")]
    [InlineData("datetime.timedelta(days=1, seconds=2, microseconds=3).days", "1")]
    [InlineData("datetime.timedelta(days=1, seconds=2, microseconds=3).seconds", "2")]
    [InlineData("datetime.timedelta(days=1, seconds=2, microseconds=3).microseconds", "3")]
    [InlineData("datetime.timedelta(days=1, seconds=2, microseconds=3).total_seconds()", "86402.000003")]
    [InlineData("datetime.date(2024, 1, 2).year", "2024")]
    [InlineData("datetime.date(2024, 1, 2).month", "1")]
    [InlineData("datetime.date(2024, 1, 2).day", "2")]
    [InlineData("datetime.date(2024, 1, 2).weekday()", "1")]
    [InlineData("datetime.date(2024, 1, 2).isoweekday()", "2")]
    [InlineData("datetime.date(2024, 1, 2).isoformat()", "2024-01-02")]
    [InlineData("datetime.date(2024, 1, 2).strftime(\"%Y/%m/%d\")", "2024/01/02")]
    [InlineData("datetime.time(7, 8, 9, 10).hour", "7")]
    [InlineData("datetime.time(7, 8, 9, 10).minute", "8")]
    [InlineData("datetime.time(7, 8, 9, 10).second", "9")]
    [InlineData("datetime.time(7, 8, 9, 10).microsecond", "10")]
    [InlineData("datetime.time(7, 8, 9, 10).tzinfo", "None")]
    [InlineData("datetime.time(7, 8, 9, 10).isoformat()", "07:08:09.000010")]
    [InlineData("datetime.time(7, 8, 9, 10, tzinfo=datetime.timezone.utc).strftime(\"%H:%M:%S%z\")", "07:08:09+00:00")]
    [InlineData("datetime.time(7, 8, 9, 10).replace(hour=1, microsecond=20).isoformat()", "01:08:09.000020")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).year", "2024")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).month", "1")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).day", "2")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).hour", "3")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).minute", "4")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).second", "5")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).microsecond", "6")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).tzinfo", "None")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).date().isoformat()", "2024-01-02")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).time().isoformat()", "03:04:05.000006")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).isoformat()", "2024-01-02T03:04:05.000006")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).strftime(\"%Y-%m-%d %H:%M:%S\")", "2024-01-02 03:04:05")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).replace(year=2025, hour=9).isoformat()", "2025-01-02T09:04:05.000006")]
    [InlineData("datetime.timezone.utc", "datetime.timezone.utc")]
    [InlineData("datetime.timezone(datetime.timedelta(hours=2))", "datetime.timezone(+02:00)")]
    [InlineData("datetime.date.fromisoformat(\"2024-02-03\").isoformat()", "2024-02-03")]
    [InlineData("datetime.time.fromisoformat(\"07:08:09+00:00\").isoformat()", "07:08:09+00:00")]
    [InlineData("datetime.datetime.fromisoformat(\"2024-01-02T03:04:05+00:00\").isoformat()", "2024-01-02T03:04:05+00:00")]
    [InlineData("datetime.datetime.strptime(\"2024-01-02 03:04:05\", \"%Y-%m-%d %H:%M:%S\").isoformat()", "2024-01-02T03:04:05")]
    public void DateTimeModule_FunctionsAndMethods_HaveDirectCoverage(string expression, string expected)
    {
        Assert.Equal(expected, EvaluateToString(expression));
    }

    [Fact]
    public void DateTimeModule_HostClockFunctions_HaveDirectCoverage()
    {
        var host = new MockLythonHost
        {
            UtcNow = new DateTimeOffset(2024, 4, 5, 6, 7, 8, TimeSpan.Zero),
            LocalNow = new DateTimeOffset(2024, 4, 5, 8, 7, 8, TimeSpan.FromHours(2))
        };

        Assert.Equal("2024-04-05", EvaluateToString("datetime.date.today().isoformat()", host));
        Assert.Equal("2024-04-05T08:07:08", EvaluateToString("datetime.datetime.now().isoformat()", host));
        Assert.Equal("2024-04-05T06:07:08", EvaluateToString("datetime.datetime.utcnow().isoformat()", host));
        Assert.Equal("2024-04-05T07:07:08+01:00", EvaluateToString("datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=1))).isoformat()", host));
    }

    private static string EvaluateToString(string expression, MockLythonHost? host = null)
    {
        var source = $$"""
import datetime
return str({{expression}})
""";

        var result = new LythonEngine().Run(source, host ?? new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        return Assert.IsType<string>(result.ReturnValue);
    }
}
