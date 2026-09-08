using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TimeModuleFunctionTests
{
    [Fact]
    public void WallClockFunctionsUseTheHostUtcAndFixedLocalValues()
    {
        var host = new MockLythonHost
        {
            UtcNow = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero),
            LocalNow = new DateTimeOffset(2024, 1, 2, 4, 4, 5, TimeSpan.FromHours(1)),
        };

        var result = new LythonEngine().Run(
            """
import time
from time import timezone, altzone, daylight, tzname

utc = time.gmtime()
local = time.localtime()
values = [
    str(time.time()),
    str(time.time_ns()),
    str(utc),
    str(local),
    time.ctime(),
    str(time.mktime(local)),
    str(timezone),
    str(altzone),
    str(daylight),
    str(tzname),
    utc.tm_zone,
    str(utc.tm_gmtoff),
    local.tm_zone,
    str(local.tm_gmtoff),
]
return "|".join(values)
""",
            host);

        Assert.True(result.Success, Describe(result));
        Assert.Equal(
            "1704164645.0|1704164645000000000|time.struct_time(tm_year=2024, tm_mon=1, tm_mday=2, tm_hour=3, tm_min=4, tm_sec=5, tm_wday=1, tm_yday=2, tm_isdst=0)|time.struct_time(tm_year=2024, tm_mon=1, tm_mday=2, tm_hour=4, tm_min=4, tm_sec=5, tm_wday=1, tm_yday=2, tm_isdst=0)|Tue Jan  2 04:04:05 2024|1704164645.0|-3600|-3600|0|('UTC+01:00', 'UTC+01:00')|GMT|0|UTC+01:00|3600",
            result.ReturnValue);
    }

    [Fact]
    public void EpochConversionsAndFormattingUseTimeTupleSlots()
    {
        var result = new LythonEngine().Run(
            """
import time

utc = time.gmtime(0)
local = time.localtime(0)
inconsistent = (2024, 1, 2, 3, 4, 5, 6, 300, -1)
values = [
    str(utc),
    str(local),
    time.ctime(0),
    time.asctime(inconsistent),
    time.strftime("%Y-%m-%d %H:%M:%S %a %A %j %w %u %U %W %c %x %X", inconsistent),
    time.strftime("%z %Z", utc),
]
return "|".join(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(
            "time.struct_time(tm_year=1970, tm_mon=1, tm_mday=1, tm_hour=0, tm_min=0, tm_sec=0, tm_wday=3, tm_yday=1, tm_isdst=0)|time.struct_time(tm_year=1970, tm_mon=1, tm_mday=1, tm_hour=1, tm_min=0, tm_sec=0, tm_wday=3, tm_yday=1, tm_isdst=0)|Thu Jan  1 01:00:00 1970|Sun Jan  2 03:04:05 2024|2024-01-02 03:04:05 Sun Sunday 300 0 7 43 42 Sun Jan  2 03:04:05 2024 01/02/24 03:04:05|+0000 GMT",
            result.ReturnValue);
    }

    [Fact]
    public void StructTimeIsTupleCompatibleWithNamedAndHiddenFields()
    {
        var result = new LythonEngine().Run(
            """
import time

t = time.struct_time((2024, 1, 2, 3, 4, 5, 1, 2, -1, "X", 3600))
year, month, day, hour, minute, second, weekday, yearday, isdst = t
checks = [
    isinstance(t, tuple),
    isinstance(t, time.struct_time),
    len(t) == 9,
    t[0:3] == (2024, 1, 2),
    tuple(t) == (2024, 1, 2, 3, 4, 5, 1, 2, -1),
    t == tuple(t),
    t.tm_year == year,
    t.tm_mon == month,
    t.tm_mday == day,
    t.tm_zone == "X",
    t.tm_gmtoff == 3600,
    t.count(2) == 2,
    t.index(5) == 5,
    t.n_fields == 11,
    t.n_sequence_fields == 9,
    t.n_unnamed_fields == 0,
    time.struct_time.n_fields == 11,
]
return str(t) + "|" + str(checks)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(
            "time.struct_time(tm_year=2024, tm_mon=1, tm_mday=2, tm_hour=3, tm_min=4, tm_sec=5, tm_wday=1, tm_yday=2, tm_isdst=-1)|[True, True, True, True, True, True, True, True, True, True, True, True, True, True, True, True, True]",
            result.ReturnValue);
    }

    [Fact]
    public void StrptimeReusesTheDeterministicDatetimeParser()
    {
        var result = new LythonEngine().Run(
            """
import time

plain = time.strptime("2024-01-02 03:04:05", "%Y-%m-%d %H:%M:%S")
defaulted = time.strptime("Tue Jan 02 03:04:05 2024")
day = time.strptime("2024-002", "%Y-%j")
composite = time.strptime("Tue Jan 02 03:04:05 2024", "%c")
offset = time.strptime("2024-01-02 +0200", "%Y-%m-%d %z")
return "|".join([
    str(plain),
    str(defaulted),
    str(day),
    str(composite),
    str(offset.tm_gmtoff),
    str(offset.tm_zone is None),
    time.strftime("%Y/%m/%d %H:%M:%S", plain),
])
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(
            "time.struct_time(tm_year=2024, tm_mon=1, tm_mday=2, tm_hour=3, tm_min=4, tm_sec=5, tm_wday=1, tm_yday=2, tm_isdst=-1)|time.struct_time(tm_year=2024, tm_mon=1, tm_mday=2, tm_hour=3, tm_min=4, tm_sec=5, tm_wday=1, tm_yday=2, tm_isdst=-1)|time.struct_time(tm_year=2024, tm_mon=1, tm_mday=2, tm_hour=0, tm_min=0, tm_sec=0, tm_wday=1, tm_yday=2, tm_isdst=-1)|time.struct_time(tm_year=2024, tm_mon=1, tm_mday=2, tm_hour=3, tm_min=4, tm_sec=5, tm_wday=1, tm_yday=2, tm_isdst=-1)|7200|True|2024/01/02 03:04:05",
            result.ReturnValue);
    }

    [Fact]
    public void InvalidTimesAndAmbientTimezoneMutationFailExplicitly()
    {
        var result = new LythonEngine().Run(
            """
import time

values = []
for action in [
    lambda: time.gmtime("zero"),
    lambda: time.gmtime(float("nan")),
    lambda: time.gmtime(float("inf")),
    lambda: time.asctime(None),
    lambda: time.strftime("%Y", None),
    lambda: time.mktime((2024, 13, 1, 0, 0, 0, 0, 1, -1)),
    lambda: time.struct_time((1, 2)),
    lambda: time.struct_time(tuple(range(12))),
    lambda: time.strptime("not-a-date", "%Y-%m-%d"),
    lambda: time.tzset(),
]:
    try:
        action()
    except Exception as ex:
        values.append(ex.type)
return "|".join(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(
            "TypeError|ValueError|OverflowError|TypeError|TypeError|ValueError|TypeError|TypeError|ValueError|NotImplementedError",
            result.ReturnValue);
    }

    [Fact]
    public void StaticContractsRecognizeTimeAndItsCallShapes()
    {
        var valid = new LythonEngine().Compile(
            """
import time
from time import gmtime, strftime

now = time.time()
stamp = time.time_ns()
parts = gmtime(0)
text = strftime("%Y", parts)
parsed = time.strptime("2024", "%Y")
custom = time.struct_time((2024, 1, 1, 0, 0, 0, 0, 1, -1))
""");
        var invalid = new LythonEngine().Compile(
            """
import time
time.time(1)
time.gmtime(1, 2)
time.mktime()
time.strftime()
time.strptime()
time.tzset(1)
time.struct_time()
""");

        Assert.True(valid.IsValid, string.Join(" | ", valid.Diagnostics.Select(d => d.Message)));
        Assert.False(invalid.IsValid);
        Assert.Equal(7, invalid.Diagnostics.Count(d => d.Code == "LA3151"));
    }

    [Fact]
    public void StructTimeProjectsAsItsNineElementTuple()
    {
        var result = new LythonEngine().Run("import time\nreturn time.gmtime(0)\n", new MockLythonHost());

        Assert.True(result.Success, Describe(result));
        Assert.Equal(
            new object?[]
            {
                new System.Numerics.BigInteger(1970),
                System.Numerics.BigInteger.One,
                System.Numerics.BigInteger.One,
                System.Numerics.BigInteger.Zero,
                System.Numerics.BigInteger.Zero,
                System.Numerics.BigInteger.Zero,
                new System.Numerics.BigInteger(3),
                System.Numerics.BigInteger.One,
                System.Numerics.BigInteger.Zero,
            },
            Assert.IsType<object?[]>(result.ReturnValue));
    }

    private static string Describe(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message));
}

