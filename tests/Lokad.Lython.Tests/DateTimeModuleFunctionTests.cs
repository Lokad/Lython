using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DateTimeModuleFunctionTests
{
    [Fact]
    public void DateTime_DisplayAndRepresentationFollowDistinctPythonContracts()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import datetime

delta = datetime.timedelta(days=1, seconds=2, microseconds=3)
aware_time = datetime.time(1, 2, tzinfo=datetime.timezone.utc, fold=1)
aware_datetime = datetime.datetime(2024, 1, 2, tzinfo=datetime.timezone.utc, fold=1)
values = [
    str(delta),
    repr(delta),
    repr(aware_time),
    repr(aware_datetime),
    str(datetime.datetime(2024, 1, 2, 3, 4, 5)),
    format(datetime.datetime(2024, 1, 2, 3, 4, 5), ""),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            "1 day, 0:00:02.000003|datetime.timedelta(days=1, seconds=2, microseconds=3)|datetime.time(1, 2, tzinfo=datetime.timezone.utc, fold=1)|datetime.datetime(2024, 1, 2, 0, 0, fold=1, tzinfo=datetime.timezone.utc)|2024-01-02 03:04:05|2024-01-02 03:04:05",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void DateTime_TimezonePreservesSubMinuteOffsets()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import datetime

zone = datetime.timezone(datetime.timedelta(seconds=30, microseconds=1))
values = [
    str(zone.utcoffset(None)),
    datetime.time(1, tzinfo=zone).isoformat(),
    datetime.time.fromisoformat("01:00:00+00:00:30.000001").isoformat(),
    datetime.datetime(2024, 1, 1, tzinfo=zone).strftime("%z"),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("0:00:30.000001|01:00:00+00:00:30.000001|01:00:00+00:00:30.000001|+000030.000001", host.ReadText("/out.txt"));
    }

    [Fact]
    public void DateTime_IsoParsingAcceptsBasicWeekAndLeadingTimeForms()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import datetime

values = [
    str(datetime.date.fromisoformat("2024-W01-2")),
    str(datetime.date.fromisoformat("2024W012")),
    str(datetime.date.fromisoformat("20240102")),
    str(datetime.datetime.fromisoformat("20240102T030405")),
    str(datetime.datetime.fromisoformat("2024-W01-2T030405")),
    str(datetime.time.fromisoformat("T03:04:05")),
    str(datetime.time.fromisoformat("030405")),
]
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("2024-01-02|2024-01-02|2024-01-02|2024-01-02 03:04:05|2024-01-02 03:04:05|03:04:05|03:04:05", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("datetime.MINYEAR", "1")]
    [InlineData("datetime.MAXYEAR", "9999")]
    [InlineData("datetime.timedelta(days=1, seconds=2, microseconds=3).days", "1")]
    [InlineData("datetime.timedelta(days=1, seconds=2, microseconds=3).seconds", "2")]
    [InlineData("datetime.timedelta(days=1, seconds=2, microseconds=3).microseconds", "3")]
    [InlineData("datetime.timedelta(days=1, seconds=2, microseconds=3).total_seconds()", "86402.000003")]
    [InlineData("datetime.timedelta.min.days", "-999999999")]
    [InlineData("datetime.timedelta.max.days", "999999999")]
    [InlineData("datetime.timedelta.max.microseconds", "999999")]
    [InlineData("datetime.timedelta.resolution.microseconds", "1")]
    [InlineData("datetime.timedelta(days=-1, microseconds=1).days", "-1")]
    [InlineData("(datetime.timedelta(seconds=5) % datetime.timedelta(seconds=2)).total_seconds()", "1.0")]
    [InlineData("divmod(datetime.timedelta(seconds=5), datetime.timedelta(seconds=2))[0]", "2")]
    [InlineData("divmod(datetime.timedelta(seconds=5), datetime.timedelta(seconds=2))[1].total_seconds()", "1.0")]
    [InlineData("(-datetime.timedelta(days=1)).days", "-1")]
    [InlineData("(datetime.timedelta(microseconds=3) * 0.5).microseconds", "2")]
    [InlineData("(datetime.timedelta(microseconds=3) / 2).microseconds", "2")]
    [InlineData("(datetime.timedelta(microseconds=3) // 2).microseconds", "1")]
    [InlineData("datetime.UTC is datetime.timezone.utc", "True")]
    [InlineData("isinstance(datetime.UTC, datetime.tzinfo)", "True")]
    [InlineData("datetime.date.min.isoformat()", "0001-01-01")]
    [InlineData("datetime.date.max.isoformat()", "9999-12-31")]
    [InlineData("datetime.date.resolution.days", "1")]
    [InlineData("datetime.date(2024, 1, 2).year", "2024")]
    [InlineData("datetime.date(2024, 1, 2).month", "1")]
    [InlineData("datetime.date(2024, 1, 2).day", "2")]
    [InlineData("datetime.date(2024, 1, 2).weekday()", "1")]
    [InlineData("datetime.date(2024, 1, 2).isoweekday()", "2")]
    [InlineData("datetime.date(2024, 1, 2).isocalendar().week", "1")]
    [InlineData("datetime.date(2024, 1, 2).isocalendar()[2]", "2")]
    [InlineData("datetime.date(2024, 1, 2).toordinal()", "738887")]
    [InlineData("datetime.date.fromordinal(738887).isoformat()", "2024-01-02")]
    [InlineData("datetime.date.fromisocalendar(2024, 1, 2).isoformat()", "2024-01-02")]
    [InlineData("datetime.date(2024, 1, 2).timetuple()[7]", "2")]
    [InlineData("datetime.date(2024, 1, 2).ctime()", "Tue Jan  2 00:00:00 2024")]
    [InlineData("datetime.date(2024, 1, 2).isoformat()", "2024-01-02")]
    [InlineData("datetime.date(2024, 1, 2).strftime(\"%Y/%m/%d\")", "2024/01/02")]
    [InlineData("datetime.date(2024, 1, 2).__format__(\"%Y/%m/%d\")", "2024/01/02")]
    [InlineData("datetime.date(2024, 1, 2).replace(year=2025).isoformat()", "2025-01-02")]
    [InlineData("(datetime.date(2024, 1, 2) + datetime.timedelta(microseconds=-1)).isoformat()", "2024-01-01")]
    [InlineData("datetime.time().isoformat()", "00:00:00")]
    [InlineData("datetime.time.min.isoformat()", "00:00:00")]
    [InlineData("datetime.time.max.isoformat()", "23:59:59.999999")]
    [InlineData("datetime.time.resolution.microseconds", "1")]
    [InlineData("datetime.time(7, 8, 9, 10).hour", "7")]
    [InlineData("datetime.time(7, 8, 9, 10).minute", "8")]
    [InlineData("datetime.time(7, 8, 9, 10).second", "9")]
    [InlineData("datetime.time(7, 8, 9, 10).microsecond", "10")]
    [InlineData("datetime.time(7, 8, 9, 10).tzinfo", "None")]
    [InlineData("datetime.time(7, 8, tzinfo=datetime.timezone(datetime.timedelta(hours=2))).utcoffset().total_seconds()", "7200.0")]
    [InlineData("datetime.time(7, 8, tzinfo=datetime.timezone(datetime.timedelta(hours=2), \"X\")).tzname()", "X")]
    [InlineData("datetime.time(7, 8, tzinfo=datetime.timezone.utc).dst()", "None")]
    [InlineData("datetime.time(7, 8, 9, 10).isoformat()", "07:08:09.000010")]
    [InlineData("datetime.time(7, 8, 9, 10).isoformat(timespec=\"milliseconds\")", "07:08:09.000")]
    [InlineData("datetime.time(7, 8, 9, 123456).isoformat(timespec=\"minutes\")", "07:08")]
    [InlineData("datetime.time(7, 8, 9, 10).__format__(\"%H:%M:%S.%f\")", "07:08:09.000010")]
    [InlineData("datetime.time(7, 8, 9, 10, tzinfo=datetime.timezone.utc).strftime(\"%H:%M:%S%z\")", "07:08:09+0000")]
    [InlineData("datetime.time(7, 8, 9, 10).replace(hour=1, microsecond=20).isoformat()", "01:08:09.000020")]
    [InlineData("datetime.time(1, fold=1).fold", "1")]
    [InlineData("datetime.time(1, fold=1).replace(fold=0).fold", "0")]
    [InlineData("datetime.time.fromisoformat(\"07:08:09Z\").isoformat()", "07:08:09+00:00")]
    [InlineData("datetime.time.fromisoformat(\"07:08:09,123456+0000\").isoformat()", "07:08:09.123456+00:00")]
    [InlineData("datetime.datetime.min.isoformat()", "0001-01-01T00:00:00")]
    [InlineData("datetime.datetime.max.isoformat()", "9999-12-31T23:59:59.999999")]
    [InlineData("datetime.datetime.resolution.microseconds", "1")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).year", "2024")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).month", "1")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).day", "2")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).hour", "3")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).minute", "4")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).second", "5")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).microsecond", "6")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).tzinfo", "None")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).weekday()", "1")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).isoweekday()", "2")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).isocalendar().year", "2024")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).toordinal()", "738887")]
    [InlineData("datetime.datetime.fromordinal(738887).isoformat()", "2024-01-02T00:00:00")]
    [InlineData("datetime.datetime.fromisocalendar(2024, 1, 2).isoformat()", "2024-01-02T00:00:00")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).timetuple()[3]", "3")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).ctime()", "Tue Jan  2 03:04:05 2024")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).date().isoformat()", "2024-01-02")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).time().isoformat()", "03:04:05.000006")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6, tzinfo=datetime.timezone.utc).time().tzinfo", "None")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6, tzinfo=datetime.timezone.utc).timetz().isoformat()", "03:04:05.000006+00:00")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).isoformat()", "2024-01-02T03:04:05.000006")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).isoformat(sep=\" \", timespec=\"seconds\")", "2024-01-02 03:04:05")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).__format__(\"%Y-%m-%d %H:%M:%S.%f\")", "2024-01-02 03:04:05.000006")]
    [InlineData("f\"{datetime.datetime(2024, 1, 2, 3, 4, 5, 6):%Y-%m-%d %H:%M:%S.%f}\"", "2024-01-02 03:04:05.000006")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).strftime(\"%Y-%m-%d %H:%M:%S\")", "2024-01-02 03:04:05")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6, tzinfo=datetime.timezone(datetime.timedelta(hours=2), \"X\")).strftime(\"%Y-%m-%d %H:%M:%S.%f %z %Z %G-W%V-%u %%\")", "2024-01-02 03:04:05.000006 +0200 X 2024-W01-2 %")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6).replace(year=2025, hour=9).isoformat()", "2025-01-02T09:04:05.000006")]
    [InlineData("datetime.datetime(2024, 1, 1, fold=1).replace(fold=0).fold", "0")]
    [InlineData("datetime.datetime.combine(datetime.date(2024, 1, 2), datetime.time(3, 4, 5, 6, tzinfo=datetime.timezone.utc)).isoformat()", "2024-01-02T03:04:05.000006+00:00")]
    [InlineData("datetime.datetime.combine(datetime.date(2024, 1, 2), datetime.time(3, 4, 5, 6, tzinfo=datetime.timezone.utc), tzinfo=None).isoformat()", "2024-01-02T03:04:05.000006")]
    [InlineData("datetime.datetime.fromtimestamp(0, datetime.timezone.utc).isoformat()", "1970-01-01T00:00:00+00:00")]
    [InlineData("datetime.datetime.fromtimestamp(timestamp=0, tz=datetime.UTC).isoformat()", "1970-01-01T00:00:00+00:00")]
    [InlineData("datetime.datetime.utcfromtimestamp(0).isoformat()", "1970-01-01T00:00:00")]
    [InlineData("datetime.datetime(1970, 1, 1, tzinfo=datetime.timezone.utc).timestamp()", "0.0")]
    [InlineData("datetime.datetime(2024, 1, 1, 12, tzinfo=datetime.timezone(datetime.timedelta(hours=2))) == datetime.datetime(2024, 1, 1, 10, tzinfo=datetime.UTC)", "True")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6, tzinfo=datetime.timezone(datetime.timedelta(hours=2), \"X\")).utcoffset().total_seconds()", "7200.0")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6, tzinfo=datetime.timezone(datetime.timedelta(hours=2), \"X\")).tzname()", "X")]
    [InlineData("datetime.datetime(2024, 1, 2, 3, 4, 5, 6, tzinfo=datetime.timezone.utc).dst()", "None")]
    [InlineData("datetime.timezone.utc.utcoffset(None).total_seconds()", "0.0")]
    [InlineData("datetime.timezone.utc.tzname(None)", "UTC")]
    [InlineData("datetime.timezone.utc.dst(None)", "None")]
    [InlineData("datetime.timezone.utc", "datetime.timezone.utc")]
    [InlineData("datetime.timezone(datetime.timedelta(hours=2))", "datetime.timezone(+02:00)")]
    [InlineData("datetime.date.fromisoformat(\"2024-02-03\").isoformat()", "2024-02-03")]
    [InlineData("datetime.time.fromisoformat(\"07:08:09+00:00\").isoformat()", "07:08:09+00:00")]
    [InlineData("datetime.datetime.fromisoformat(\"2024-01-02T03:04:05+00:00\").isoformat()", "2024-01-02T03:04:05+00:00")]
    [InlineData("datetime.datetime.fromisoformat(\"2024-01-02 03:04:05,123456+0000\").isoformat()", "2024-01-02T03:04:05.123456+00:00")]
    [InlineData("datetime.datetime.strptime(\"2024-01-02 03:04:05\", \"%Y-%m-%d %H:%M:%S\").isoformat()", "2024-01-02T03:04:05")]
    [InlineData("datetime.datetime.strptime(\"2024-01-02 03:04:05.000006 +0200\", \"%Y-%m-%d %H:%M:%S.%f %z\").isoformat()", "2024-01-02T03:04:05.000006+02:00")]
    [InlineData("datetime.datetime.strptime(\"2024-W01-2\", \"%G-W%V-%u\").isoformat()", "2024-01-02T00:00:00")]
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
        Assert.Equal("2024-04-05T07:07:08+01:00", EvaluateToString("datetime.datetime.now(tz=datetime.timezone(datetime.timedelta(hours=1))).isoformat()", host));
        Assert.Equal("1970-01-01", EvaluateToString("datetime.date.fromtimestamp(0).isoformat()", host));
        Assert.Equal("1970-01-01T02:00:00", EvaluateToString("datetime.datetime.fromtimestamp(0).isoformat()", host));
        Assert.Equal("0.0", EvaluateToString("datetime.datetime(1970, 1, 1, 2).timestamp()", host));
        Assert.Equal("2024-01-01T10:00:00+00:00", EvaluateToString("datetime.datetime(2024, 1, 1, 12, tzinfo=datetime.timezone(datetime.timedelta(hours=2))).astimezone(datetime.UTC).isoformat()", host));
        Assert.Equal("2024-01-01T10:00:00+00:00", EvaluateToString("datetime.datetime(2024, 1, 1, 12).astimezone(datetime.UTC).isoformat()", host));
        Assert.Equal("2024-01-01T12:00:00+02:00", EvaluateToString("datetime.datetime(2024, 1, 1, 10, tzinfo=datetime.UTC).astimezone().isoformat()", host));
    }

    [Fact]
    public void DateTimeModule_StaticContractsCoverExpandedSurface()
    {
        var valid = new LythonEngine().Run(
            """
import datetime
delta = datetime.timedelta(days=1, seconds=2)
d = datetime.date.fromisocalendar(2024, 1, 2)
t = datetime.time.fromisoformat("03:04:05+00:00").replace(fold=1)
dt = datetime.datetime.combine(d, t, tzinfo=datetime.UTC).astimezone(datetime.UTC)
out = [
    str(delta.total_seconds()),
    d.replace(year=2025).isoformat(),
    str(t.fold),
    dt.isoformat(timespec="seconds"),
    datetime.timezone.utc.tzname(None),
]
return "|".join(out)
""",
            new MockLythonHost());

        Assert.True(valid.Success, valid.Failure?.Message);
        Assert.Equal("86402.0|2025-01-02|1|2024-01-02T03:04:05+00:00|UTC", Assert.IsType<string>(valid.ReturnValue));

        var invalid = new LythonEngine().Run(
            """
import datetime
datetime.date(2024, 1)
datetime.date.fromisocalendar(2024, 1)
datetime.datetime.now(tz=datetime.UTC, extra=1)
datetime.time(1).isoformat("seconds", "extra")
datetime.datetime(2024, 1, 1).replace(unknown=1)
datetime.timezone.utc.tzname()
datetime.timedelta(days=1).total_seconds(1)
""",
            new MockLythonHost());

        Assert.False(invalid.Success);
        Assert.Null(invalid.Failure);
        Assert.True(invalid.Diagnostics.Count(d => d.Code is "LA3151" or "LA3162") >= 7);
    }

    [Theory]
    [InlineData("datetime.date(2024, 1, 2).isocalendar(1)", "expects no arguments")]
    [InlineData("datetime.date.fromordinal(0)", "ordinal is out of range")]
    [InlineData("datetime.datetime.fromtimestamp(\"x\")", "expects a real number")]
    [InlineData("datetime.timezone.utc.utcoffset()", "expects one argument")]
    [InlineData("datetime.time(1, fold=2)", "fold must be either 0 or 1")]
    [InlineData("datetime.time(1).isoformat(timespec=\"centuries\")", "Unknown timespec")]
    [InlineData("datetime.datetime(2024, 1, 2).strftime(\"%Q\")", "directive '%Q'")]
    public void DateTimeModule_NearMisses_FailPrecisely(string expression, string messageFragment)
    {
        var source = $$"""
import datetime
{{expression}}
""";

        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        if (result.Failure is null)
        {
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(messageFragment, StringComparison.Ordinal));
        }
        else
        {
            Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
        }
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
