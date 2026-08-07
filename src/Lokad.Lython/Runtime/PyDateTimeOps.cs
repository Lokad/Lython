using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class PyDateTimeOps
{
    private static readonly int[] IsoDatePrefixLengths = [10, 8, 7];

    private static readonly DateTimeOffset UnixEpoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
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
        private readonly string _name;
        private readonly string[]? _parameterNames;
        private readonly int _requiredCount;

        public TypeMemberCallable(string name, Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> implementation) : this(name, implementation, null, null) { }

        public TypeMemberCallable(string name, Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> implementation, string[]? parameterNames) : this(name, implementation, parameterNames, null) { }

        public TypeMemberCallable(string name, Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> implementation, string[]? parameterNames, int? requiredCount)
        {
            _name = name;
            _implementation = implementation;
            _parameterNames = parameterNames;
            _requiredCount = requiredCount ?? parameterNames?.Length ?? 0;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var positional = CallBinder.BindNamedArguments(arguments, span, _name, "Builtin", _parameterNames, _requiredCount);
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
        _ = context;
        var bound = CallBinder.BindNamedArguments(
            arguments,
            span,
            "datetime.timedelta",
            "Builtin",
            ["days", "seconds", "microseconds", "milliseconds", "minutes", "hours", "weeks"],
            0);

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
            return new PyTimedelta(new BigInteger(Math.Round(totalMicroseconds, MidpointRounding.ToEven)));
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", "timedelta is outside Python's supported day range", span, ex);
        }
    }

    public static object CreateDate(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        var bound = CallBinder.BindNamedArguments(
            arguments,
            span,
            "datetime.date",
            "Builtin",
            ["year", "month", "day"],
            3);

        return new PyDate(new DateOnly(
            GetInteger(ArgAt(bound, 0), "datetime.date", span),
            GetInteger(ArgAt(bound, 1), "datetime.date", span),
            GetInteger(ArgAt(bound, 2), "datetime.date", span)));
    }

    public static object CreateTime(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        var bound = CallBinder.BindNamedArguments(
            arguments,
            span,
            "datetime.time",
            "Builtin",
            ["hour", "minute", "second", "microsecond", "tzinfo", "fold"],
            0);

        var fold = GetFold(ArgAt(bound, 5), "datetime.time", span);
        return new PyTime(
            new TimeOnly(
                GetInteger(ArgAt(bound, 0), "datetime.time", span),
                GetInteger(ArgAt(bound, 1), "datetime.time", span),
                GetInteger(ArgAt(bound, 2), "datetime.time", span),
                GetInteger(ArgAt(bound, 3), "datetime.time", span) / 1000,
                GetInteger(ArgAt(bound, 3), "datetime.time", span) % 1000),
            GetTimezone(ArgAt(bound, 4), "datetime.time", span),
            fold);
    }

    public static object CreateDateTime(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        var bound = CallBinder.BindNamedArguments(
            arguments,
            span,
            "datetime.datetime",
            "Builtin",
            ["year", "month", "day", "hour", "minute", "second", "microsecond", "tzinfo", "fold"],
            3);

        var microsecond = GetInteger(ArgAt(bound, 6), "datetime.datetime", span);
        var fold = GetFold(ArgAt(bound, 8), "datetime.datetime", span);
        return new PyDateTime(
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
            fold);
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
        _ = context;
        var bound = CallBinder.BindNamedArguments(
            arguments,
            span,
            "datetime.timezone",
            "Builtin",
            ["offset", "name"],
            1);

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
            : new PyTimezone(delta.Value, name);
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
            return new PyDate(ParseIsoDate(text.AsString()));
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

        return new PyDate(DateFromOrdinalValue(arguments[0], "datetime.date.fromordinal", span));
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
        return new PyDate(DateOnly.FromDateTime(instant.ToOffset(context.Host.LocalNow.Offset).DateTime));
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

        return new PyDateTime(DateFromOrdinalValue(arguments[0], "datetime.datetime.fromordinal", span).ToDateTime(TimeOnly.MinValue));
    }

    public static object DateTimeFromIsoCalendar(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 3)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.fromisocalendar(year, week, day) expects three integer arguments.", span);
        }

        return new PyDateTime(DateFromIsoCalendarValue(arguments[0], arguments[1], arguments[2], "datetime.datetime.fromisocalendar", span).ToDateTime(TimeOnly.MinValue));
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
            return new PyDateTime(DateTime.SpecifyKind(instant.ToOffset(context.Host.LocalNow.Offset).DateTime, DateTimeKind.Unspecified));
        }

        if (arguments[1] is not PyTimezone tz)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.fromtimestamp(timestamp[, tz]) expects tz to be a timezone or None.", span);
        }

        return new PyDateTime(DateTime.SpecifyKind(instant.UtcDateTime + tz.Offset, DateTimeKind.Unspecified), tz);
    }

    public static object DateTimeUtcFromTimestamp(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.utcfromtimestamp(timestamp) expects one argument.", span);
        }

        return new PyDateTime(DateTime.SpecifyKind(DateTimeOffsetFromTimestamp(GetTimestamp(arguments[0], "datetime.datetime.utcfromtimestamp", span), span).UtcDateTime, DateTimeKind.Unspecified));
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

        return new PyDateTime(date.ToDateTime(time.Value), timezone, time.Fold);
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
        return new PyDate(DateOnly.FromDateTime(context.Host.LocalNow.DateTime));
    }

    public static object DateTimeNow(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length > 1)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.now([tz]) expects zero or one argument.", span);
        }

        context.RegisterHostCall(span);
        var localNow = context.Host.LocalNow;

        if (arguments.Length == 0 || arguments[0] is PyNone)
        {
            return new PyDateTime(DateTime.SpecifyKind(localNow.DateTime, DateTimeKind.Unspecified));
        }

        if (arguments[0] is not PyTimezone tz)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.now(tz) expects tz to be a timezone or None.", span);
        }

        var instant = context.Host.UtcNow.UtcDateTime + tz.Offset;
        return new PyDateTime(DateTime.SpecifyKind(instant, DateTimeKind.Unspecified), tz);
    }

    public static object DateTimeUtcNow(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = arguments;
        context.RegisterHostCall(span);
        var utcNow = context.Host.UtcNow;
        return new PyDateTime(DateTime.SpecifyKind(utcNow.UtcDateTime, DateTimeKind.Unspecified));
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
        return new PyTuple([quotient, remainder], context.MemoryGovernor, span);
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

    public static string FormatOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        offset = offset.Duration();
        var text = $"{sign}{offset.Hours:00}:{offset.Minutes:00}";
        if (offset.Seconds != 0 || offset.Microseconds != 0)
        {
            text += $":{offset.Seconds:00}";
        }

        if (offset.Microseconds != 0)
        {
            text += $".{offset.Microseconds:000000}";
        }

        return text;
    }

    public static string FormatIsoTime(TimeOnly value, string timespec)
    {
        return timespec switch
        {
            "auto" => value.Microsecond == 0
                ? value.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                : value.ToString("HH:mm:ss.ffffff", CultureInfo.InvariantCulture),
            "hours" => value.ToString("HH", CultureInfo.InvariantCulture),
            "minutes" => value.ToString("HH:mm", CultureInfo.InvariantCulture),
            "seconds" => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            "milliseconds" => value.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
            "microseconds" => value.ToString("HH:mm:ss.ffffff", CultureInfo.InvariantCulture),
            _ => throw new LythonRuntimeException("ValueError", "Unknown timespec value.", default)
        };
    }

    public static PyString FormatValue(object value, PyString format, LythonSourceSpan span)
    {
        if (format.Length == 0)
        {
            return value switch
            {
                PyDate date => date.IsoFormat(),
                PyTime time => time.IsoFormat(),
                PyDateTime dateTime => dateTime.IsoFormat(" "),
                _ => PyString.FromString(string.Empty)
            };
        }

        return value switch
        {
            PyDate date => Strftime(date.Value, format, span),
            PyTime time => Strftime(time.Value, time.TzInfo, format, span),
            PyDateTime dateTime => Strftime(dateTime.Value, dateTime.TzInfo, format, span),
            _ => throw new LythonRuntimeException("TypeError", "__format__ expects a date, time, or datetime value.", span)
        };
    }

    public static PyString Strftime(DateOnly value, PyString format, LythonSourceSpan span)
        => PyString.FromString(FormatStrftime(value.ToDateTime(TimeOnly.MinValue), null, format.AsString(), span));

    public static PyString Strftime(TimeOnly value, PyTimezone? timezone, PyString format, LythonSourceSpan span)
    {
        var dateTime = new DateTime(1900, 1, 1, value.Hour, value.Minute, value.Second, value.Millisecond, DateTimeKind.Unspecified)
            .AddTicks(value.Ticks % TimeSpan.TicksPerMillisecond);
        return PyString.FromString(FormatStrftime(dateTime, timezone, format.AsString(), span));
    }

    public static PyString Strftime(DateTime value, PyTimezone? timezone, PyString format, LythonSourceSpan span)
        => PyString.FromString(FormatStrftime(value, timezone, format.AsString(), span));

    public static string FormatStrftime(DateTime value, PyTimezone? timezone, string format, LythonSourceSpan span)
        => FormatStrftime(value, timezone, format, span, null, null);

    public static string FormatStrftime(DateTime value, PyTimezone? timezone, string format, LythonSourceSpan span, int? weekdayOverride)
        => FormatStrftime(value, timezone, format, span, weekdayOverride, null);

    public static string FormatStrftime(
        DateTime value,
        PyTimezone? timezone,
        string format,
        LythonSourceSpan span,
        int? weekdayOverride,
        int? yearDayOverride)
    {
        var mondayBasedWeekday = weekdayOverride ?? ((int)value.DayOfWeek + 6) % 7;
        var yearDay = yearDayOverride ?? value.DayOfYear;
        var displayWeekday = new DateTime(2024, 1, 1).AddDays(mondayBasedWeekday);
        var builder = new StringBuilder(format.Length * 2);
        for (var i = 0; i < format.Length; i++)
        {
            var ch = format[i];
            if (ch != '%')
            {
                builder.Append(ch);
                continue;
            }

            if (i + 1 >= format.Length)
            {
                throw new LythonRuntimeException("ValueError", "strftime format string cannot end with '%'.", span);
            }

            var directive = format[++i];
            switch (directive)
            {
                case '%':
                    builder.Append('%');
                    break;
                case 'Y':
                    builder.Append(value.Year.ToString("0000", CultureInfo.InvariantCulture));
                    break;
                case 'y':
                    builder.Append((value.Year % 100).ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'm':
                    builder.Append(value.Month.ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'd':
                    builder.Append(value.Day.ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'H':
                    builder.Append(value.Hour.ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'I':
                    var hour12 = value.Hour % 12;
                    builder.Append((hour12 == 0 ? 12 : hour12).ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'p':
                    builder.Append(value.Hour < 12 ? "AM" : "PM");
                    break;
                case 'M':
                    builder.Append(value.Minute.ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'S':
                    builder.Append(value.Second.ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'f':
                    builder.Append(value.Microsecond.ToString("000000", CultureInfo.InvariantCulture));
                    break;
                case 'z':
                    builder.Append(timezone is null ? string.Empty : FormatCompactOffset(timezone.Offset));
                    break;
                case 'Z':
                    builder.Append(timezone?.Name ?? string.Empty);
                    break;
                case 'a':
                    builder.Append(displayWeekday.ToString("ddd", CultureInfo.InvariantCulture));
                    break;
                case 'A':
                    builder.Append(displayWeekday.ToString("dddd", CultureInfo.InvariantCulture));
                    break;
                case 'b':
                case 'h':
                    builder.Append(value.ToString("MMM", CultureInfo.InvariantCulture));
                    break;
                case 'B':
                    builder.Append(value.ToString("MMMM", CultureInfo.InvariantCulture));
                    break;
                case 'j':
                    builder.Append(yearDay.ToString("000", CultureInfo.InvariantCulture));
                    break;
                case 'w':
                    builder.Append(((mondayBasedWeekday + 1) % 7).ToString(CultureInfo.InvariantCulture));
                    break;
                case 'u':
                    builder.Append((mondayBasedWeekday + 1).ToString(CultureInfo.InvariantCulture));
                    break;
                case 'U':
                    builder.Append(WeekNumber(yearDay, mondayBasedWeekday, sundayFirst: true).ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'W':
                    builder.Append(WeekNumber(yearDay, mondayBasedWeekday, sundayFirst: false).ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'G':
                    builder.Append(ISOWeek.GetYear(value).ToString("0000", CultureInfo.InvariantCulture));
                    break;
                case 'V':
                    builder.Append(ISOWeek.GetWeekOfYear(value).ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'c':
                    builder.Append(displayWeekday.ToString("ddd", CultureInfo.InvariantCulture));
                    builder.Append(' ');
                    builder.Append(value.ToString("MMM", CultureInfo.InvariantCulture));
                    builder.Append(' ');
                    builder.Append(value.Day.ToString(CultureInfo.InvariantCulture).PadLeft(2, ' '));
                    builder.Append(value.ToString(" HH:mm:ss yyyy", CultureInfo.InvariantCulture));
                    break;
                case 'x':
                    builder.Append(value.ToString("MM/dd/yy", CultureInfo.InvariantCulture));
                    break;
                case 'X':
                    builder.Append(value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
                    break;
                default:
                    throw new LythonRuntimeException("ValueError", $"strftime directive '%{directive}' is not supported in Lython yet.", span);
            }
        }

        return builder.ToString();
    }

    public static bool MatchesBuiltinType(string typeName, object value)
    {
        return typeName switch
        {
            "datetime.timedelta" => value is PyTimedelta,
            "datetime.date" => value is PyDate,
            "datetime.time" => value is PyTime,
            "datetime.datetime" => value is PyDateTime,
            "datetime.timezone" => value is PyTimezone,
            "datetime.tzinfo" => value is PyTimezone,
            _ => false
        };
    }

    private static object SubtractDateTimes(PyDateTime left, PyDateTime right, LythonSourceSpan span)
    {
        if ((left.TzInfo is null) != (right.TzInfo is null))
        {
            throw new LythonRuntimeException("TypeError", "Cannot mix naive and timezone-aware datetimes.", span);
        }

        return left.TzInfo is null
            ? new PyTimedelta(left.Value - right.Value)
            : new PyTimedelta(new BigInteger((left.ToUtcTicks() - right.ToUtcTicks()) / 10));
    }

    private static int CompareDateTimes(PyDateTime left, PyDateTime right, LythonSourceSpan span)
    {
        if ((left.TzInfo is null) != (right.TzInfo is null))
        {
            throw new LythonRuntimeException("TypeError", "Cannot compare naive and timezone-aware datetimes.", span);
        }

        return left.TzInfo is null
            ? left.Value.CompareTo(right.Value)
            : left.ToUtcTicks().CompareTo(right.ToUtcTicks());
    }

    private static int CompareTimes(PyTime left, PyTime right, LythonSourceSpan span)
    {
        if ((left.TzInfo is null) != (right.TzInfo is null))
        {
            throw new LythonRuntimeException("TypeError", "Cannot compare naive and timezone-aware times.", span);
        }

        if (left.TzInfo is null)
        {
            return left.Value.CompareTo(right.Value);
        }

        var leftAdjusted = AdjustTimeTicks(left.Value, left.TzInfo.RequireNotNull().Offset);
        var rightAdjusted = AdjustTimeTicks(right.Value, right.TzInfo.RequireNotNull().Offset);
        return leftAdjusted.CompareTo(rightAdjusted);
    }

    public static bool DateTimeEquals(PyDateTime left, PyDateTime right)
    {
        if ((left.TzInfo is null) != (right.TzInfo is null))
        {
            return false;
        }

        return left.TzInfo is null
            ? left.Value == right.Value
            : left.ToUtcTicks() == right.ToUtcTicks();
    }

    public static bool TimeEquals(PyTime left, PyTime right)
    {
        if ((left.TzInfo is null) != (right.TzInfo is null))
        {
            return false;
        }

        if (left.TzInfo is null)
        {
            return left.Value == right.Value;
        }

        return AdjustTimeTicks(left.Value, left.TzInfo.RequireNotNull().Offset) == AdjustTimeTicks(right.Value, right.TzInfo.RequireNotNull().Offset);
    }

    public static long AdjustTimeTicks(TimeOnly value, TimeSpan offset)
    {
        var ticks = (value.Ticks - offset.Ticks) % TimeSpan.TicksPerDay;
        return ticks < 0 ? ticks + TimeSpan.TicksPerDay : ticks;
    }

    private static int WeekNumber(DateTime value, DayOfWeek firstDay)
    {
        var first = new DateTime(value.Year, 1, 1);
        var offset = ((int)firstDay - (int)first.DayOfWeek + 7) % 7;
        var firstWeekStart = first.AddDays(offset);
        if (value.Date < firstWeekStart)
        {
            return 0;
        }

        return ((value.Date - firstWeekStart).Days / 7) + 1;
    }

    private static int WeekNumber(int yearDay, int mondayBasedWeekday, bool sundayFirst)
    {
        var zeroBasedDay = yearDay - 1;
        var currentWeekday = sundayFirst ? (mondayBasedWeekday + 1) % 7 : mondayBasedWeekday;
        var firstDayWeekday = (currentWeekday - zeroBasedDay % 7 + 7) % 7;
        var firstWeekStart = (7 - firstDayWeekday) % 7;
        return zeroBasedDay < firstWeekStart ? 0 : 1 + (zeroBasedDay - firstWeekStart) / 7;
    }

    private static string FormatCompactOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        offset = offset.Duration();
        var text = $"{sign}{(int)offset.TotalHours:00}{offset.Minutes:00}";
        if (offset.Seconds != 0 || offset.Microseconds != 0)
        {
            text += $"{offset.Seconds:00}";
        }

        if (offset.Microseconds != 0)
        {
            text += $".{offset.Microseconds:000000}";
        }

        return text;
    }

    public static PyDateTime ParseStrptime(string text, string format, LythonSourceSpan span)
    {
        format = ExpandCompositeStrptimeDirectives(format);
        var pattern = new StringBuilder(format.Length * 3);
        var groups = new Dictionary<char, string>();
        var groupIndex = 0;
        pattern.Append('^');

        for (var i = 0; i < format.Length; i++)
        {
            var ch = format[i];
            if (char.IsWhiteSpace(ch))
            {
                while (i + 1 < format.Length && char.IsWhiteSpace(format[i + 1]))
                {
                    i++;
                }

                pattern.Append(@"\s+");
                continue;
            }

            if (ch != '%')
            {
                pattern.Append(Regex.Escape(ch.ToString()));
                continue;
            }

            if (i + 1 >= format.Length)
            {
                throw new LythonRuntimeException("ValueError", "strptime format string cannot end with '%'.", span);
            }

            var directive = format[++i];
            if (directive == '%')
            {
                pattern.Append('%');
                continue;
            }

            var groupName = "g" + groupIndex.ToString(CultureInfo.InvariantCulture);
            groupIndex++;
            groups[directive] = groupName;
            pattern.Append("(?<").Append(groupName).Append('>').Append(StrptimeDirectivePattern(directive, span)).Append(')');
        }

        pattern.Append('$');
        var match = Regex.Match(text, pattern.ToString(), RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            throw new FormatException("time data does not match format.");
        }

        string? Capture(char directive)
            => groups.TryGetValue(directive, out var group) ? match.Groups[group].Value : null;

        static int ParseInt(string? value, int fallback)
            => string.IsNullOrEmpty(value) ? fallback : int.Parse(value, CultureInfo.InvariantCulture);

        var year = ParseYear(Capture('Y'), Capture('y'));
        var month = ParseMonth(Capture('m'), Capture('b'), Capture('B'));
        var day = ParseInt(Capture('d'), 1);

        if (Capture('G') is not null || Capture('V') is not null || Capture('u') is not null)
        {
            if (Capture('G') is null || Capture('V') is null || Capture('u') is null)
            {
                throw new FormatException("ISO year, week, and weekday directives must be used together.");
            }

            var isoDate = DateFromIsoCalendarParts(
                int.Parse(Capture('G').RequireNotNull(), CultureInfo.InvariantCulture),
                int.Parse(Capture('V').RequireNotNull(), CultureInfo.InvariantCulture),
                int.Parse(Capture('u').RequireNotNull(), CultureInfo.InvariantCulture));
            year = isoDate.Year;
            month = isoDate.Month;
            day = isoDate.Day;
        }
        else if (Capture('j') is { } dayOfYearText)
        {
            var dayOfYear = int.Parse(dayOfYearText, CultureInfo.InvariantCulture);
            var date = new DateTime(year, 1, 1).AddDays(dayOfYear - 1);
            if (date.Year != year)
            {
                throw new FormatException("day of year out of range.");
            }

            month = date.Month;
            day = date.Day;
        }

        var hour = ParseInt(Capture('H'), 0);
        if (Capture('I') is { } hour12Text)
        {
            hour = int.Parse(hour12Text, CultureInfo.InvariantCulture) % 12;
            if (string.Equals(Capture('p'), "PM", StringComparison.OrdinalIgnoreCase))
            {
                hour += 12;
            }
        }

        var minute = ParseInt(Capture('M'), 0);
        var second = ParseInt(Capture('S'), 0);
        var microsecond = ParseMicrosecond(Capture('f'));
        var value = new DateTime(year, month, day, hour, minute, second, microsecond / 1000, DateTimeKind.Unspecified)
            .AddTicks((microsecond % 1000) * 10L);

        var timezone = ParseStrptimeTimezone(Capture('z'), Capture('Z'));
        return new PyDateTime(value, timezone);
    }

    private static string ExpandCompositeStrptimeDirectives(string format)
    {
        var expanded = new StringBuilder(format.Length);
        for (var index = 0; index < format.Length; index++)
        {
            if (format[index] != '%' || index + 1 >= format.Length)
            {
                expanded.Append(format[index]);
                continue;
            }

            var directive = format[index + 1];
            switch (directive)
            {
                case '%':
                    expanded.Append("%%");
                    index++;
                    break;
                case 'c':
                    expanded.Append("%a %b %d %H:%M:%S %Y");
                    index++;
                    break;
                case 'x':
                    expanded.Append("%m/%d/%y");
                    index++;
                    break;
                case 'X':
                    expanded.Append("%H:%M:%S");
                    index++;
                    break;
                default:
                    expanded.Append('%').Append(directive);
                    index++;
                    break;
            }
        }

        return expanded.ToString();
    }

    private static string StrptimeDirectivePattern(char directive, LythonSourceSpan span)
    {
        return directive switch
        {
            'Y' => @"\d{1,4}",
            'y' => @"\d{2}",
            'm' => @"\d{1,2}",
            'd' => @"\d{1,2}",
            'H' => @"\d{1,2}",
            'I' => @"\d{1,2}",
            'p' => @"AM|PM|am|pm",
            'M' => @"\d{1,2}",
            'S' => @"\d{1,2}",
            'f' => @"\d{1,6}",
            'z' => @"Z|[+-]\d{2}:?\d{2}",
            'Z' => @"[A-Za-z_][A-Za-z0-9_+-]*",
            'a' or 'A' => @"[A-Za-z]+",
            'b' or 'h' or 'B' => @"[A-Za-z]+",
            'j' => @"\d{1,3}",
            'w' => @"\d",
            'u' => @"\d",
            'U' or 'W' => @"\d{1,2}",
            'G' => @"\d{1,4}",
            'V' => @"\d{1,2}",
            _ => throw new LythonRuntimeException("ValueError", $"strptime directive '%{directive}' is not supported in Lython yet.", span)
        };
    }

    private static int ParseYear(string? yearText, string? shortYearText)
    {
        if (!string.IsNullOrEmpty(yearText))
        {
            return int.Parse(yearText, CultureInfo.InvariantCulture);
        }

        if (string.IsNullOrEmpty(shortYearText))
        {
            return 1900;
        }

        var shortYear = int.Parse(shortYearText, CultureInfo.InvariantCulture);
        return shortYear <= 68 ? 2000 + shortYear : 1900 + shortYear;
    }

    private static int ParseMonth(string? monthText, string? abbreviatedName, string? fullName)
    {
        if (!string.IsNullOrEmpty(monthText))
        {
            return int.Parse(monthText, CultureInfo.InvariantCulture);
        }

        var name = abbreviatedName ?? fullName;
        if (string.IsNullOrEmpty(name))
        {
            return 1;
        }

        for (var i = 1; i <= 12; i++)
        {
            var date = new DateTime(2000, i, 1);
            if (string.Equals(name, date.ToString("MMM", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, date.ToString("MMMM", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        throw new FormatException("month name is not recognized.");
    }

    private static int ParseMicrosecond(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return int.Parse(text.PadRight(6, '0'), CultureInfo.InvariantCulture);
    }

    private static PyTimezone? ParseStrptimeTimezone(string? offsetText, string? nameText)
    {
        if (!string.IsNullOrEmpty(offsetText))
        {
            if (!TryParseOffsetText(offsetText, out var offset))
            {
                throw new FormatException("timezone offset is not recognized.");
            }

            return new PyTimezone(offset);
        }

        return nameText is not null &&
            (string.Equals(nameText, "UTC", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(nameText, "GMT", StringComparison.OrdinalIgnoreCase))
            ? PyTimezone.Utc
            : null;
    }

    private static PyTime ParseTime(string text)
    {
        text = NormalizeIsoText(text);
        if (text.StartsWith('T'))
        {
            text = text[1..];
        }

        if (text.EndsWith("Z", StringComparison.Ordinal))
        {
            text = text[..^1] + "+00:00";
        }

        if (TryParseTrailingOffset(text, out var body, out var offset))
        {
            return new PyTime(ParseIsoTime(body), new PyTimezone(offset));
        }

        return new PyTime(ParseIsoTime(text));
    }

    private static PyDateTime ParseDateTime(string text)
    {
        text = NormalizeIsoText(text);
        if (text.EndsWith("Z", StringComparison.Ordinal))
        {
            text = text[..^1] + "+00:00";
        }

        if (TryParseTrailingOffset(text, out var body, out var offset))
        {
            return ParseIsoDateTime(body, new PyTimezone(offset));
        }

        return ParseIsoDateTime(text, null);
    }

    private static DateOnly ParseIsoDate(string text)
    {
        var calendarMatch = CalendarDateRegex.Match(text);
        if (calendarMatch.Success)
        {
            var basic = calendarMatch.Groups["basicYear"].Success;
            var year = ParseIsoComponent(calendarMatch, basic ? "basicYear" : "year");
            var month = ParseIsoComponent(calendarMatch, basic ? "basicMonth" : "month");
            var day = ParseIsoComponent(calendarMatch, basic ? "basicDay" : "day");
            try
            {
                return new DateOnly(year, month, day);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new FormatException("Invalid ISO calendar date.", ex);
            }
        }

        var weekMatch = WeekDateRegex.Match(text);
        if (weekMatch.Success)
        {
            var basic = weekMatch.Groups["basicYear"].Success;
            var year = ParseIsoComponent(weekMatch, basic ? "basicYear" : "year");
            var week = ParseIsoComponent(weekMatch, basic ? "basicWeek" : "week");
            var weekdayGroup = weekMatch.Groups[basic ? "basicWeekday" : "weekday"];
            var weekday = weekdayGroup.Success
                ? int.Parse(weekdayGroup.Value, CultureInfo.InvariantCulture)
                : 1;
            if (weekday is < 1 or > 7 || week is < 1 or > 53)
            {
                throw new FormatException("Invalid ISO week date.");
            }

            try
            {
                var dayOfWeek = weekday == 7 ? DayOfWeek.Sunday : (DayOfWeek)weekday;
                var value = DateOnly.FromDateTime(ISOWeek.ToDateTime(year, week, dayOfWeek));
                if (ISOWeek.GetYear(value.ToDateTime(TimeOnly.MinValue)) != year ||
                    ISOWeek.GetWeekOfYear(value.ToDateTime(TimeOnly.MinValue)) != week)
                {
                    throw new FormatException("Invalid ISO week date.");
                }

                return value;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new FormatException("Invalid ISO week date.", ex);
            }
        }

        throw new FormatException("Invalid ISO date string.");
    }

    private static PyDateTime ParseIsoDateTime(string text, PyTimezone? timezone)
    {
        foreach (var dateLength in IsoDatePrefixLengths)
        {
            if (text.Length < dateLength)
            {
                continue;
            }

            DateOnly date;
            try
            {
                date = ParseIsoDate(text[..dateLength]);
            }
            catch (FormatException)
            {
                continue;
            }

            if (text.Length == dateLength)
            {
                return new PyDateTime(date.ToDateTime(TimeOnly.MinValue), timezone);
            }

            if (text[dateLength] is not ('T' or ' '))
            {
                continue;
            }

            var time = ParseIsoTime(text[(dateLength + 1)..]);
            return new PyDateTime(date.ToDateTime(time), timezone);
        }

        throw new FormatException("Invalid ISO datetime string.");
    }

    private static TimeOnly ParseIsoTime(string text)
    {
        var match = ExtendedTimeRegex.Match(text);
        int hour;
        int minute;
        int second;
        int microsecond;
        if (match.Success)
        {
            hour = ParseIsoComponent(match, "hour");
            minute = match.Groups["minute"].Success ? ParseIsoComponent(match, "minute") : 0;
            second = match.Groups["second"].Success ? ParseIsoComponent(match, "second") : 0;
            microsecond = ParseIsoFraction(match.Groups["fraction"]);
        }
        else
        {
            var fractionSeparator = text.IndexOfAny(['.', ',']);
            var digits = fractionSeparator < 0 ? text : text[..fractionSeparator];
            var fraction = fractionSeparator < 0 ? string.Empty : text[(fractionSeparator + 1)..];
            if (digits.Length is not (2 or 4 or 6) || !digits.All(char.IsAsciiDigit) ||
                fraction.Length > 6 || fraction.Any(ch => !char.IsAsciiDigit(ch)) ||
                fraction.Length > 0 && digits.Length != 6)
            {
                throw new FormatException("Invalid ISO time string.");
            }

            hour = int.Parse(digits[..2], CultureInfo.InvariantCulture);
            minute = digits.Length >= 4 ? int.Parse(digits.Substring(2, 2), CultureInfo.InvariantCulture) : 0;
            second = digits.Length == 6 ? int.Parse(digits.Substring(4, 2), CultureInfo.InvariantCulture) : 0;
            microsecond = fraction.Length == 0 ? 0 : int.Parse(fraction.PadRight(6, '0'), CultureInfo.InvariantCulture);
        }

        try
        {
            return new TimeOnly(hour, minute, second, microsecond / 1000, microsecond % 1000);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new FormatException("Invalid ISO time string.", ex);
        }
    }

    private static int ParseIsoComponent(Match match, string groupName)
        => int.Parse(match.Groups[groupName].Value, CultureInfo.InvariantCulture);

    private static int ParseIsoFraction(Group group)
        => group.Success ? int.Parse(group.Value.PadRight(6, '0'), CultureInfo.InvariantCulture) : 0;

    private static bool TryParseTrailingOffset(string text, out string body, out TimeSpan offset)
    {
        for (var i = text.Length - 1; i > 0; i--)
        {
            if (text[i] is not ('+' or '-'))
            {
                continue;
            }

            if (TryParseOffsetText(text[i..], out offset))
            {
                body = text[..i];
                return true;
            }
        }

        body = text;
        offset = default;
        return false;
    }

    private static bool TryParseOffsetText(string text, out TimeSpan offset)
    {
        if (string.Equals(text, "Z", StringComparison.OrdinalIgnoreCase))
        {
            offset = TimeSpan.Zero;
            return true;
        }

        var match = OffsetTextRegex.Match(text);
        if (!match.Success)
        {
            offset = default;
            return false;
        }

        var hours = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
        var minutes = int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture);
        var seconds = match.Groups["second"].Success
            ? int.Parse(match.Groups["second"].Value, CultureInfo.InvariantCulture)
            : 0;
        var microseconds = match.Groups["fraction"].Success
            ? int.Parse(match.Groups["fraction"].Value.PadRight(6, '0'), CultureInfo.InvariantCulture)
            : 0;
        if (hours > 23 || minutes > 59 || seconds > 59)
        {
            offset = default;
            return false;
        }

        var sign = match.Groups["sign"].Value == "-" ? -1L : 1L;
        var ticks = (hours * 3600L + minutes * 60L + seconds) * TimeSpan.TicksPerSecond + microseconds * 10L;
        offset = TimeSpan.FromTicks(sign * ticks);
        return true;
    }

    private static string NormalizeIsoText(string text)
        => text.Replace(',', '.');

    private static PyTuple CreateTimeTuple(DateOnly date, TimeOnly time, int isDst)
    {
        return new PyTuple([
            new BigInteger(date.Year),
            new BigInteger(date.Month),
            new BigInteger(date.Day),
            new BigInteger(time.Hour),
            new BigInteger(time.Minute),
            new BigInteger(time.Second),
            new BigInteger(((int)date.DayOfWeek + 6) % 7),
            new BigInteger(date.DayOfYear),
            new BigInteger(isDst)
        ]);
    }

    private static DateOnly DateFromOrdinalValue(object value, string owner, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(value, out var ordinal))
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(ordinal) expects an integer ordinal.", span);
        }

        if (ordinal < BigInteger.One || ordinal > new BigInteger(DateOnly.MaxValue.DayNumber + 1))
        {
            throw new LythonRuntimeException("ValueError", $"{owner}(ordinal) ordinal is out of range.", span);
        }

        return DateOnly.FromDayNumber((int)ordinal - 1);
    }

    private static DateOnly DateFromIsoCalendarValue(object yearValue, object weekValue, object dayValue, string owner, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(yearValue, out var year) ||
            !Numbers.PyNumberOps.TryAsInteger(weekValue, out var week) ||
            !Numbers.PyNumberOps.TryAsInteger(dayValue, out var day))
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(year, week, day) expects integer fields.", span);
        }

        try
        {
            return DateFromIsoCalendarParts((int)year, (int)week, (int)day);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
    }

    private static DateOnly DateFromIsoCalendarParts(int year, int week, int day)
    {
        var dayOfWeek = day switch
        {
            1 => DayOfWeek.Monday,
            2 => DayOfWeek.Tuesday,
            3 => DayOfWeek.Wednesday,
            4 => DayOfWeek.Thursday,
            5 => DayOfWeek.Friday,
            6 => DayOfWeek.Saturday,
            7 => DayOfWeek.Sunday,
            _ => throw new ArgumentOutOfRangeException(nameof(day), "ISO weekday must be in 1..7.")
        };

        return DateOnly.FromDateTime(ISOWeek.ToDateTime(year, week, dayOfWeek));
    }

    private static double GetTimestamp(object value, string owner, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsNumber(value, out var number))
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(timestamp) expects a real number.", span);
        }

        var timestamp = number.ToDouble();
        if (!double.IsFinite(timestamp))
        {
            throw new LythonRuntimeException("ValueError", $"{owner}(timestamp) timestamp is out of range.", span);
        }

        return timestamp;
    }

    private static DateTimeOffset DateTimeOffsetFromTimestamp(double timestamp, LythonSourceSpan span)
    {
        try
        {
            var ticks = checked((long)Math.Round(timestamp * TimeSpan.TicksPerSecond, MidpointRounding.ToEven));
            ticks -= ticks % 10;
            return UnixEpoch.AddTicks(ticks);
        }
        catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException)
        {
            throw new LythonRuntimeException("ValueError", "timestamp out of range.", span);
        }
    }

    private static double GetReal(object? value, string owner, LythonSourceSpan span)
    {
        if (value is null or PyNone)
        {
            return 0.0;
        }

        if (!Numbers.PyNumberOps.TryAsNumber(value, out var number))
        {
            throw new LythonRuntimeException("TypeError", $"{owner} expects real numbers.", span);
        }

        return number.ToDouble();
    }

    private static int GetInteger(object? value, string owner, LythonSourceSpan span)
    {
        if (value is null or PyNone)
        {
            return 0;
        }

        if (!Numbers.PyNumberOps.TryAsInteger(value, out var integer))
        {
            throw new LythonRuntimeException("TypeError", $"{owner} expects integer fields.", span);
        }

        return (int)integer;
    }

    private static PyTimezone? GetTimezone(object? value, string owner, LythonSourceSpan span)
    {
        return value switch
        {
            null or PyNone => null,
            PyTimezone timezone => timezone,
            _ => throw new LythonRuntimeException("TypeError", $"{owner} only supports timezone values created by datetime.timezone(...).", span)
        };
    }

    private static int GetFold(object? value, string owner, LythonSourceSpan span)
    {
        var fold = GetInteger(value, owner, span);
        if (fold is not 0 and not 1)
        {
            throw new LythonRuntimeException("ValueError", $"{owner} fold must be either 0 or 1.", span);
        }

        return fold;
    }

    private static object? ArgAt(object[] arguments, int index) => index < arguments.Length ? arguments[index] : null;

    private static bool TryGetScale(object value, out double scale)
    {
        if (Numbers.PyNumberOps.TryAsNumber(value, out var number))
        {
            scale = number.ToDouble();
            return true;
        }

        scale = 0.0;
        return false;
    }

    private static PyTimedelta ScaleTimedelta(PyTimedelta delta, double scale, LythonSourceSpan span)
        => ScaleTimedelta(delta, scale, span, false, false);

    private static PyTimedelta ScaleTimedelta(PyTimedelta delta, double scale, LythonSourceSpan span, bool floor)
        => ScaleTimedelta(delta, scale, span, floor, false);

    private static PyTimedelta ScaleTimedelta(PyTimedelta delta, double scale, LythonSourceSpan span, bool floor, bool checkZero)
    {
        if (checkZero && double.IsInfinity(scale))
        {
            throw new LythonRuntimeException("ValueError", "division by zero", span);
        }

        var scaledMicroseconds = (double)delta.TotalMicroseconds * scale;
        var roundedMicroseconds = floor
            ? Math.Floor(scaledMicroseconds)
            : Math.Round(scaledMicroseconds, MidpointRounding.ToEven);
        try
        {
            return CreateTimedelta(new BigInteger(roundedMicroseconds), span);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", "timedelta is outside Python's supported day range", span, ex);
        }
    }

    private static double DivideTimedeltas(PyTimedelta left, PyTimedelta right, LythonSourceSpan span)
    {
        if (right.TotalMicroseconds.IsZero)
        {
            throw new LythonRuntimeException("ValueError", "division by zero", span);
        }

        return (double)left.TotalMicroseconds / (double)right.TotalMicroseconds;
    }

    private static PyTimedelta TimedeltaModulo(PyTimedelta left, PyTimedelta right, LythonSourceSpan span)
    {
        var quotient = FloorDivideMicroseconds(left.TotalMicroseconds, right.TotalMicroseconds, span);
        return CreateTimedelta(left.TotalMicroseconds - quotient * right.TotalMicroseconds, span);
    }

    private static BigInteger FloorDivideMicroseconds(BigInteger left, BigInteger right, LythonSourceSpan span)
    {
        if (right.IsZero)
        {
            throw new LythonRuntimeException("ValueError", "integer division or modulo by zero", span);
        }

        var quotient = BigInteger.DivRem(left, right, out var remainder);
        if (!remainder.IsZero && left.Sign != right.Sign)
        {
            quotient -= BigInteger.One;
        }

        return quotient;
    }

    private static PyTimedelta CreateTimedelta(BigInteger totalMicroseconds, LythonSourceSpan span)
    {
        try
        {
            return new PyTimedelta(totalMicroseconds);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", "timedelta is outside Python's supported day range", span, ex);
        }
    }

    private static int GetDateDeltaDays(PyTimedelta delta)
        => (int)delta.Days;
}
