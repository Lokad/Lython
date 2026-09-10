using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDateTimeOps
{
    private static readonly int[] IsoDatePrefixLengths = [10, 8, 7];

    private static readonly LythonCallableSignature TimedeltaCallSignature = LythonCallableSignature.Create(
        "datetime.timedelta",
        ["days", "seconds", "microseconds", "milliseconds", "minutes", "hours", "weeks"],
        requiredCount: 0);
    private static readonly LythonCallableSignature DateCallSignature = LythonCallableSignature.Create("datetime.date", ["year", "month", "day"]);
    private static readonly LythonCallableSignature TimeCallSignature = LythonCallableSignature.Create(
        "datetime.time",
        ["hour", "minute", "second", "microsecond", "tzinfo", "fold"],
        requiredCount: 0);
    private static readonly LythonCallableSignature DateTimeCallSignature = LythonCallableSignature.Create(
        "datetime.datetime",
        ["year", "month", "day", "hour", "minute", "second", "microsecond", "tzinfo", "fold"],
        requiredCount: 3);
    private static readonly LythonCallableSignature TimezoneCallSignature = LythonCallableSignature.Create(
        "datetime.timezone",
        ["offset", "name"],
        requiredCount: 1);

    private static readonly DateTimeOffset UnixEpoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Constructed date/time values retain small fixed-size payloads; charge one
    // table slot per value once built (the expression evaluates first, so failed
    // constructions leak nothing). Member projections stay uncharged.
    private const long DateTimeValueBytes = 64;

    private static T OwnDateTimeValue<T>(T value, LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
    {
        context.MemoryGovernor.Reserve(DateTimeValueBytes, span);
        context.MemoryGovernor.Commit(DateTimeValueBytes);
        return value;
    }

    // Rendered date/time text allocates fresh strings on every access; adopt them
    // into the caller governor like converted scalar renders. Shared empties and
    // already-owned values pass through untouched.
    internal static object OwnDateTimeText(object result, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (result is not PyString text || governor is null || text.OwnerMemoryGovernor is not null || ReferenceEquals(text, PyString.Empty))
        {
            return result;
        }

        return PyString.FromString(text.AsString(), governor, span);
    }

    private static readonly Regex OffsetTextRegex = new(
        @"^(?<sign>[+-])(?<hour>\d{2})(?::?(?<minute>\d{2}))(?:(?::?)(?<second>\d{2})(?:[.,](?<fraction>\d{1,6}))?)?$",
        RegexOptions.CultureInvariant);
    private static readonly Regex CalendarDateRegex = new(
        @"^(?:(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})|(?<basicYear>\d{4})(?<basicMonth>\d{2})(?<basicDay>\d{2}))$",
        RegexOptions.CultureInvariant);
    private static readonly Regex WeekDateRegex = new(
        @"^(?:(?<year>\d{4})-W(?<week>\d{2})(?:-(?<weekday>\d))?|(?<basicYear>\d{4})W(?<basicWeek>\d{2})(?<basicWeekday>\d)?)$",
        RegexOptions.CultureInvariant);
    private static readonly Regex ExtendedTimeRegex = new(
        @"^(?<hour>\d{2})(?::(?<minute>\d{2})(?::(?<second>\d{2})(?:[.,](?<fraction>\d{1,6}))?)?)?$",
        RegexOptions.CultureInvariant);

    private sealed class TypeMemberCallable : LythonRuntime.ICallable
    {
        private readonly Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> _implementation;
        private readonly LythonCallableSignature _signature;

        public TypeMemberCallable(string name, Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> implementation)
            : this(implementation, LythonCallableSignature.Create(name))
        {
        }

        public TypeMemberCallable(string name, Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> implementation, string[] parameterNames)
            : this(implementation, LythonCallableSignature.Create(name, parameterNames))
        {
        }

        public TypeMemberCallable(string name, Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> implementation, string[] parameterNames, int requiredCount)
            : this(implementation, LythonCallableSignature.Create(name, parameterNames, requiredCount))
        {
        }

        private TypeMemberCallable(
            Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> implementation,
            LythonCallableSignature signature)
        {
            _implementation = implementation;
            _signature = signature;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var positional = CallBinder.BindNamedArguments(arguments, span, _signature, PythonCallableKind.Builtin);
            return _implementation(positional, span, context);
        }
    }

    public static readonly PyBuiltinRuntimeType TimedeltaType = new(
        "datetime.timedelta",
        CreateTimedelta,
        memberName => memberName switch
        {
            "min" => PyTimedelta.Min,
            "max" => PyTimedelta.Max,
            "resolution" => PyTimedelta.Resolution,
            _ => null
        });

    public static readonly PyBuiltinRuntimeType DateType = new(
        "datetime.date",
        CreateDate,
        memberName => memberName switch
        {
            "min" => new PyDate(DateOnly.MinValue),
            "max" => new PyDate(DateOnly.MaxValue),
            "resolution" => new PyTimedelta(TimeSpan.FromDays(1)),
            "fromordinal" => new TypeMemberCallable("datetime.date.fromordinal", DateFromOrdinal, ["ordinal"]),
            "fromisoformat" => new TypeMemberCallable("datetime.date.fromisoformat", DateFromIsoFormat, ["date_string"]),
            "fromisocalendar" => new TypeMemberCallable("datetime.date.fromisocalendar", DateFromIsoCalendar, ["year", "week", "day"]),
            "fromtimestamp" => new TypeMemberCallable("datetime.date.fromtimestamp", DateFromTimestamp, ["timestamp"]),
            "today" => new TypeMemberCallable("datetime.date.today", DateToday),
            _ => null
        });

    public static readonly PyBuiltinRuntimeType TimeType = new(
        "datetime.time",
        CreateTime,
        memberName => memberName switch
        {
            "min" => new PyTime(TimeOnly.MinValue),
            "max" => new PyTime(new TimeOnly(23, 59, 59, 999).Add(TimeSpan.FromTicks(9990))),
            "resolution" => new PyTimedelta(TimeSpan.FromTicks(10)),
            "fromisoformat" => new TypeMemberCallable("datetime.time.fromisoformat", TimeFromIsoFormat, ["time_string"]),
            _ => null
        });

    public static readonly PyBuiltinRuntimeType DateTimeType = new(
        "datetime.datetime",
        CreateDateTime,
        memberName => memberName switch
        {
            "min" => new PyDateTime(DateTime.MinValue),
            "max" => new PyDateTime(new DateTime(9999, 12, 31, 23, 59, 59, 999, DateTimeKind.Unspecified).AddTicks(9990)),
            "resolution" => new PyTimedelta(TimeSpan.FromTicks(10)),
            "combine" => new TypeMemberCallable("datetime.datetime.combine", DateTimeCombine, ["date", "time", "tzinfo"], 2),
            "fromordinal" => new TypeMemberCallable("datetime.datetime.fromordinal", DateTimeFromOrdinal, ["ordinal"]),
            "fromisoformat" => new TypeMemberCallable("datetime.datetime.fromisoformat", DateTimeFromIsoFormat, ["date_string"]),
            "fromisocalendar" => new TypeMemberCallable("datetime.datetime.fromisocalendar", DateTimeFromIsoCalendar, ["year", "week", "day"]),
            "fromtimestamp" => new TypeMemberCallable("datetime.datetime.fromtimestamp", DateTimeFromTimestamp, ["timestamp", "tz"], 1),
            "strptime" => new TypeMemberCallable("datetime.datetime.strptime", DateTimeStrptime, ["date_string", "format"]),
            "now" => new TypeMemberCallable("datetime.datetime.now", DateTimeNow, ["tz"], 0),
            "utcfromtimestamp" => new TypeMemberCallable("datetime.datetime.utcfromtimestamp", DateTimeUtcFromTimestamp, ["timestamp"]),
            "utcnow" => new TypeMemberCallable("datetime.datetime.utcnow", DateTimeUtcNow),
            _ => null
        });

    public static readonly PyBuiltinRuntimeType TzInfoType = new(
        "datetime.tzinfo",
        CreateTzInfo);

    public static readonly PyBuiltinRuntimeType TimezoneType = new(
        "datetime.timezone",
        CreateTimezone,
        memberName => memberName switch
        {
            "utc" => PyTimezone.Utc,
            _ => null
        });

    public static object CreateTimedelta(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var bound = CallBinder.BindNamedArguments(arguments, span, TimedeltaCallSignature, PythonCallableKind.Builtin);

        var days = GetReal(ArgAt(bound, 0), "datetime.timedelta", span);
        var seconds = GetReal(ArgAt(bound, 1), "datetime.timedelta", span);
        var microseconds = GetReal(ArgAt(bound, 2), "datetime.timedelta", span);
        var milliseconds = GetReal(ArgAt(bound, 3), "datetime.timedelta", span);
        var minutes = GetReal(ArgAt(bound, 4), "datetime.timedelta", span);
        var hours = GetReal(ArgAt(bound, 5), "datetime.timedelta", span);
        var weeks = GetReal(ArgAt(bound, 6), "datetime.timedelta", span);

        var totalMicroseconds =
            (decimal)weeks * 7m * 86_400_000_000m +
            (decimal)days * 86_400_000_000m +
            (decimal)hours * 3_600_000_000m +
            (decimal)minutes * 60_000_000m +
            (decimal)seconds * 1_000_000m +
            (decimal)milliseconds * 1_000m +
            (decimal)microseconds;

        try
        {
            return OwnDateTimeValue(new PyTimedelta(new BigInteger(Math.Round(totalMicroseconds, MidpointRounding.ToEven))), context, span);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", "timedelta is outside Python's supported day range", span, ex);
        }
    }

    public static object CreateDate(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var bound = CallBinder.BindNamedArguments(arguments, span, DateCallSignature, PythonCallableKind.Builtin);

        return OwnDateTimeValue(new PyDate(new DateOnly(
            GetInteger(ArgAt(bound, 0), "datetime.date", span),
            GetInteger(ArgAt(bound, 1), "datetime.date", span),
            GetInteger(ArgAt(bound, 2), "datetime.date", span))), context, span);
    }

    public static object CreateTime(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var bound = CallBinder.BindNamedArguments(arguments, span, TimeCallSignature, PythonCallableKind.Builtin);

        var fold = GetFold(ArgAt(bound, 5), "datetime.time", span);
        return OwnDateTimeValue(new PyTime(
            new TimeOnly(
                GetInteger(ArgAt(bound, 0), "datetime.time", span),
                GetInteger(ArgAt(bound, 1), "datetime.time", span),
                GetInteger(ArgAt(bound, 2), "datetime.time", span),
                GetInteger(ArgAt(bound, 3), "datetime.time", span) / 1000,
                GetInteger(ArgAt(bound, 3), "datetime.time", span) % 1000),
            GetTimezone(ArgAt(bound, 4), "datetime.time", span),
            fold), context, span);
    }

    public static object CreateDateTime(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var bound = CallBinder.BindNamedArguments(arguments, span, DateTimeCallSignature, PythonCallableKind.Builtin);

        var microsecond = GetInteger(ArgAt(bound, 6), "datetime.datetime", span);
        var fold = GetFold(ArgAt(bound, 8), "datetime.datetime", span);
        return OwnDateTimeValue(new PyDateTime(
            new DateTime(
                GetInteger(ArgAt(bound, 0), "datetime.datetime", span),
                GetInteger(ArgAt(bound, 1), "datetime.datetime", span),
                GetInteger(ArgAt(bound, 2), "datetime.datetime", span),
                GetInteger(ArgAt(bound, 3), "datetime.datetime", span),
                GetInteger(ArgAt(bound, 4), "datetime.datetime", span),
                GetInteger(ArgAt(bound, 5), "datetime.datetime", span),
                microsecond / 1000,
                DateTimeKind.Unspecified).AddTicks((microsecond % 1000) * 10L),
            GetTimezone(ArgAt(bound, 7), "datetime.datetime", span),
            fold), context, span);
    }

    public static object CreateTzInfo(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 0)
        {
            throw new LythonRuntimeException("TypeError", "datetime.tzinfo() expects no arguments.", span);
        }

        throw new LythonRuntimeException("NotImplementedError", "datetime.tzinfo is an abstract base; use datetime.timezone(...) for fixed-offset timezones.", span);
    }

    public static object CreateTimezone(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var bound = CallBinder.BindNamedArguments(arguments, span, TimezoneCallSignature, PythonCallableKind.Builtin);

        if (ArgAt(bound, 0) is not PyTimedelta delta)
        {
            throw new LythonRuntimeException("TypeError", "datetime.timezone(offset[, name]) expects a timedelta offset.", span);
        }

        if (delta.TotalMicroseconds <= -86_400_000_000 || delta.TotalMicroseconds >= 86_400_000_000)
        {
            throw new LythonRuntimeException("ValueError", "datetime.timezone(...) offset must be strictly between -24h and +24h.", span);
        }

        var name = ArgAt(bound, 1) switch
        {
            null or PyNone => null,
            _ when PyStringOps.TryAsString(ArgAt(bound, 1).RequireNotNull(), out var text) => text.AsString(),
            _ => throw new LythonRuntimeException("TypeError", "datetime.timezone(offset[, name]) expects name to be a string or None.", span)
        };

        return delta.TotalMicroseconds.IsZero && name is null
            ? PyTimezone.Utc
            : OwnDateTimeValue(new PyTimezone(delta.Value, name), context, span);
    }

    public static object DateFromIsoFormat(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
        {
            throw new LythonRuntimeException("TypeError", "datetime.date.fromisoformat(date_string) expects one string argument.", span);
        }

        try
        {
            return OwnDateTimeValue(new PyDate(ParseIsoDate(text.AsString())), context, span);
        }
        catch (FormatException)
        {
            throw new LythonRuntimeException("ValueError", $"Invalid isoformat string: '{text.AsString()}'", span);
        }
    }

    public static object DateFromOrdinal(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "datetime.date.fromordinal(ordinal) expects one integer argument.", span);
        }

        return OwnDateTimeValue(new PyDate(DateFromOrdinalValue(arguments[0], "datetime.date.fromordinal", span)), context, span);
    }

    public static object DateFromIsoCalendar(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 3)
        {
            throw new LythonRuntimeException("TypeError", "datetime.date.fromisocalendar(year, week, day) expects three integer arguments.", span);
        }

        return new PyDate(DateFromIsoCalendarValue(arguments[0], arguments[1], arguments[2], "datetime.date.fromisocalendar", span));
    }

    public static object DateFromTimestamp(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "datetime.date.fromtimestamp(timestamp) expects one argument.", span);
        }

        context.RegisterHostCall(span);
        var instant = DateTimeOffsetFromTimestamp(GetTimestamp(arguments[0], "datetime.date.fromtimestamp", span), span);
        return OwnDateTimeValue(new PyDate(DateOnly.FromDateTime(instant.ToOffset(context.Host.LocalNow.Offset).DateTime)), context, span);
    }

    public static object TimeFromIsoFormat(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
        {
            throw new LythonRuntimeException("TypeError", "datetime.time.fromisoformat(time_string) expects one string argument.", span);
        }

        try
        {
            return ParseTime(text.AsString());
        }
        catch (FormatException)
        {
            throw new LythonRuntimeException("ValueError", $"Invalid isoformat string: '{text.AsString()}'", span);
        }
    }

    public static object DateTimeFromIsoFormat(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.fromisoformat(date_string) expects one string argument.", span);
        }

        try
        {
            return ParseDateTime(text.AsString());
        }
        catch (FormatException)
        {
            throw new LythonRuntimeException("ValueError", $"Invalid isoformat string: '{text.AsString()}'", span);
        }
    }

    public static object DateTimeFromOrdinal(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.fromordinal(ordinal) expects one integer argument.", span);
        }

        return OwnDateTimeValue(new PyDateTime(DateFromOrdinalValue(arguments[0], "datetime.datetime.fromordinal", span).ToDateTime(TimeOnly.MinValue)), context, span);
    }

    public static object DateTimeFromIsoCalendar(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 3)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.fromisocalendar(year, week, day) expects three integer arguments.", span);
        }

        return OwnDateTimeValue(new PyDateTime(DateFromIsoCalendarValue(arguments[0], arguments[1], arguments[2], "datetime.datetime.fromisocalendar", span).ToDateTime(TimeOnly.MinValue)), context, span);
    }

    public static object DateTimeFromTimestamp(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.fromtimestamp(timestamp[, tz]) expects one or two arguments.", span);
        }

        var instant = DateTimeOffsetFromTimestamp(GetTimestamp(arguments[0], "datetime.datetime.fromtimestamp", span), span);
        if (arguments.Length == 1 || arguments[1] is PyNone)
        {
            context.RegisterHostCall(span);
            return OwnDateTimeValue(new PyDateTime(DateTime.SpecifyKind(instant.ToOffset(context.Host.LocalNow.Offset).DateTime, DateTimeKind.Unspecified)), context, span);
        }

        if (arguments[1] is not PyTimezone tz)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.fromtimestamp(timestamp[, tz]) expects tz to be a timezone or None.", span);
        }

        return OwnDateTimeValue(new PyDateTime(DateTime.SpecifyKind(instant.UtcDateTime + tz.Offset, DateTimeKind.Unspecified), tz), context, span);
    }

    public static object DateTimeUtcFromTimestamp(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.utcfromtimestamp(timestamp) expects one argument.", span);
        }

        return OwnDateTimeValue(new PyDateTime(DateTime.SpecifyKind(DateTimeOffsetFromTimestamp(GetTimestamp(arguments[0], "datetime.datetime.utcfromtimestamp", span), span).UtcDateTime, DateTimeKind.Unspecified)), context, span);
    }

    public static object DateTimeCombine(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        if (arguments.Length is < 2 or > 3)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.combine(date, time[, tzinfo]) expects two or three arguments.", span);
        }

        var date = arguments[0] switch
        {
            PyDate value => value.Value,
            PyDateTime value => DateOnly.FromDateTime(value.Value),
            _ => throw new LythonRuntimeException("TypeError", "datetime.datetime.combine(date, time[, tzinfo]) expects a date and a time.", span)
        };

        if (arguments[1] is not PyTime time)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.combine(date, time[, tzinfo]) expects a date and a time.", span);
        }

        var timezone = arguments.Length == 2
            ? time.TzInfo
            : arguments[2] switch
            {
                PyNone => null,
                PyTimezone tz => tz,
                _ => throw new LythonRuntimeException("TypeError", "datetime.datetime.combine(date, time[, tzinfo]) expects tzinfo to be a timezone or None.", span)
            };

        return OwnDateTimeValue(new PyDateTime(date.ToDateTime(time.Value), timezone, time.Fold), context, span);
    }

    public static object DateTimeStrptime(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 2 ||
            !PyStringOps.TryAsString(arguments[0], out var text) ||
            !PyStringOps.TryAsString(arguments[1], out var format))
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.strptime(date_string, format) expects two string arguments.", span);
        }

        try
        {
            return ParseStrptime(text.AsString(), format.AsString(), span);
        }
        catch (FormatException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
    }

    public static object DateToday(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = arguments;
        context.RegisterHostCall(span);
        return OwnDateTimeValue(new PyDate(DateOnly.FromDateTime(context.Host.LocalNow.DateTime)), context, span);
    }

    public static object DateTimeNow(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.now([tz]) expects zero or one argument.", span);
        }

        if (arguments.Length == 0 || arguments[0] is PyNone)
        {
            context.RegisterHostCall(span);
            var localNow = context.Host.LocalNow;
            return OwnDateTimeValue(new PyDateTime(DateTime.SpecifyKind(localNow.DateTime, DateTimeKind.Unspecified)), context, span);
        }

        if (arguments[0] is not PyTimezone tz)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.now(tz) expects tz to be a timezone or None.", span);
        }

        context.RegisterHostCall(span);
        var instant = context.Host.UtcNow.UtcDateTime + tz.Offset;
        return OwnDateTimeValue(new PyDateTime(DateTime.SpecifyKind(instant, DateTimeKind.Unspecified), tz), context, span);
    }

    public static object DateTimeUtcNow(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = arguments;
        context.RegisterHostCall(span);
        var utcNow = context.Host.UtcNow;
        return OwnDateTimeValue(new PyDateTime(DateTime.SpecifyKind(utcNow.UtcDateTime, DateTimeKind.Unspecified)), context, span);
    }

    public static PyIsoCalendarDate IsoCalendar(DateOnly date)
    {
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        return new PyIsoCalendarDate(
            ISOWeek.GetYear(dateTime),
            ISOWeek.GetWeekOfYear(dateTime),
            ((int)date.DayOfWeek + 6) % 7 + 1);
    }

    public static PyString CTime(DateOnly date)
        => CTime(date.ToDateTime(TimeOnly.MinValue));

    public static PyString CTime(DateTime dateTime)
    {
        var prefix = dateTime.ToString("ddd MMM", CultureInfo.InvariantCulture);
        var time = dateTime.ToString("HH:mm:ss yyyy", CultureInfo.InvariantCulture);
        return PyString.FromString($"{prefix} {dateTime.Day,2} {time}");
    }

    public static PyTuple TimeTuple(DateOnly date)
        => CreateTimeTuple(date, TimeOnly.MinValue, isDst: -1);

    public static PyTuple TimeTuple(DateTime dateTime)
        => TimeTuple(dateTime, -1);

    public static PyTuple TimeTuple(DateTime dateTime, int isDst)
        => CreateTimeTuple(DateOnly.FromDateTime(dateTime), TimeOnly.FromDateTime(dateTime), isDst);

    public static double Timestamp(PyDateTime dateTime, TimeSpan localOffset, LythonSourceSpan span)
    {
        try
        {
            var offset = dateTime.TzInfo?.Offset ?? localOffset;
            return (dateTime.Value.Ticks - offset.Ticks - UnixEpoch.UtcTicks) / (double)TimeSpan.TicksPerSecond;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
    }

    public static PyDateTime Astimezone(PyDateTime dateTime, PyTimezone? targetTimezone, TimeSpan localOffset, LythonSourceSpan span)
    {
        try
        {
            var sourceOffset = dateTime.TzInfo?.Offset ?? localOffset;
            var target = targetTimezone ?? new PyTimezone(localOffset);
            var instant = dateTime.Value - sourceOffset + target.Offset;
            return new PyDateTime(DateTime.SpecifyKind(instant, DateTimeKind.Unspecified), target, dateTime.Fold);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
    }

    public static object Add(object left, object right, LythonSourceSpan span)
    {
        return (left, right) switch
        {
            (PyTimedelta lhs, PyTimedelta rhs) => CreateTimedelta(lhs.TotalMicroseconds + rhs.TotalMicroseconds, span),
            (PyDate date, PyTimedelta delta) => new PyDate(date.Value.AddDays(GetDateDeltaDays(delta))),
            (PyTimedelta delta, PyDate date) => new PyDate(date.Value.AddDays(GetDateDeltaDays(delta))),
            (PyDateTime dateTime, PyTimedelta delta) => new PyDateTime(dateTime.Value + delta.Value, dateTime.TzInfo, dateTime.Fold),
            (PyTimedelta delta, PyDateTime dateTime) => new PyDateTime(dateTime.Value + delta.Value, dateTime.TzInfo, dateTime.Fold),
            _ => throw new LythonRuntimeException("TypeError", "Operands are not compatible with '+'.", span)
        };
    }

    public static object Subtract(object left, object right, LythonSourceSpan span)
    {
        return (left, right) switch
        {
            (PyTimedelta lhs, PyTimedelta rhs) => CreateTimedelta(lhs.TotalMicroseconds - rhs.TotalMicroseconds, span),
            (PyDate lhs, PyTimedelta rhs) => new PyDate(lhs.Value.AddDays(-GetDateDeltaDays(rhs))),
            (PyDate lhs, PyDate rhs) => new PyTimedelta(TimeSpan.FromDays(lhs.Value.DayNumber - rhs.Value.DayNumber)),
            (PyDateTime lhs, PyTimedelta rhs) => new PyDateTime(lhs.Value - rhs.Value, lhs.TzInfo, lhs.Fold),
            (PyDateTime lhs, PyDateTime rhs) => SubtractDateTimes(lhs, rhs, span),
            _ => throw new LythonRuntimeException("TypeError", "Operands are not compatible with '-'.", span)
        };
    }

    public static object Negate(object operand, LythonSourceSpan span)
    {
        return operand switch
        {
            PyTimedelta delta => CreateTimedelta(-delta.TotalMicroseconds, span),
            _ => throw new LythonRuntimeException("TypeError", "Operand is not numeric.", span)
        };
    }

    public static object Multiply(object left, object right, LythonSourceSpan span)
    {
        return (left, right) switch
        {
            (PyTimedelta delta, _) when TryGetScale(right, out var scale) => ScaleTimedelta(delta, scale, span),
            (_, PyTimedelta delta) when TryGetScale(left, out var scale) => ScaleTimedelta(delta, scale, span),
            _ => throw new LythonRuntimeException("TypeError", "Operands are not compatible with '*'.", span)
        };
    }

    public static object Divide(object left, object right, LythonSourceSpan span)
    {
        return (left, right) switch
        {
            (PyTimedelta delta, PyTimedelta other) => DivideTimedeltas(delta, other, span),
            (PyTimedelta delta, _) when TryGetScale(right, out var scale) => ScaleTimedelta(delta, 1.0 / scale, span, floor: false, checkZero: true),
            _ => throw new LythonRuntimeException("TypeError", "Operands are not compatible with '/'.", span)
        };
    }

    public static object FloorDivide(object left, object right, LythonSourceSpan span)
    {
        return (left, right) switch
        {
            (PyTimedelta delta, PyTimedelta other) => FloorDivideMicroseconds(delta.TotalMicroseconds, other.TotalMicroseconds, span),
            (PyTimedelta delta, _) when TryGetScale(right, out var scale) => ScaleTimedelta(delta, 1.0 / scale, span, floor: true, checkZero: true),
            _ => throw new LythonRuntimeException("TypeError", "Operands are not compatible with '//'.", span)
        };
    }

    public static object Modulo(object left, object right, LythonSourceSpan span)
    {
        if (left is PyTimedelta delta && right is PyTimedelta other)
        {
            return TimedeltaModulo(delta, other, span);
        }

        throw new LythonRuntimeException("TypeError", "Operands are not compatible with '%'.", span);
    }

    public static PyTuple DivMod(PyTimedelta left, PyTimedelta right, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var quotient = FloorDivide(left, right, span);
        var remainder = TimedeltaModulo(left, right, span);
        return PyTuple.FromOwnedArray([quotient, remainder], context.MemoryGovernor, span);
    }

    public static int Compare(object left, object right, LythonSourceSpan span)
    {
        return (left, right) switch
        {
            (PyTimedelta lhs, PyTimedelta rhs) => lhs.TotalMicroseconds.CompareTo(rhs.TotalMicroseconds),
            (PyDate lhs, PyDate rhs) => lhs.Value.CompareTo(rhs.Value),
            (PyTime lhs, PyTime rhs) => CompareTimes(lhs, rhs, span),
            (PyDateTime lhs, PyDateTime rhs) => CompareDateTimes(lhs, rhs, span),
            _ => throw new LythonRuntimeException("TypeError", "Values are not comparable.", span)
        };
    }

}
