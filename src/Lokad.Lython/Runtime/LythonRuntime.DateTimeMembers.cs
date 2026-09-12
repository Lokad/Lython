using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class TimedeltaMembers
    {
        public static bool TryGetMember(PyTimedelta delta, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "days" => delta.Days,
                "seconds" => delta.Seconds,
                "microseconds" => delta.Microseconds,
                "total_seconds" => BoundCallable.CreateNoArguments(
                    delta,
                    "timedelta.total_seconds",
                    static (receiver, _, _) => receiver.TotalSeconds()),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal static class DateMembers
    {
        public static bool TryGetMember(PyDate date, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "year" => date.Year,
                "month" => date.Month,
                "day" => date.Day,
                "weekday" => BoundCallable.CreateNoArguments(date, "date.weekday", static (receiver, _, _) => new BigInteger(receiver.Weekday())),
                "isoweekday" => BoundCallable.CreateNoArguments(date, "date.isoweekday", static (receiver, _, _) => new BigInteger(receiver.IsoWeekday())),
                "isocalendar" => BoundCallable.CreateNoArguments(date, "date.isocalendar", static (receiver, span, context) => PyDateTimeOps.IsoCalendar(receiver.Value, context, span)),
                "toordinal" => BoundCallable.CreateNoArguments(date, "date.toordinal", static (receiver, _, _) => receiver.ToOrdinal()),
                "timetuple" => BoundCallable.CreateNoArguments(date, "date.timetuple", static (receiver, span, context) => PyDateTimeOps.TimeTuple(receiver.Value, context, span)),
                "ctime" => BoundCallable.CreateNoArguments(date, "date.ctime", static (receiver, span, context) => PyDateTimeOps.OwnDateTimeText(PyDateTimeOps.CTime(receiver.Value), context.MemoryGovernor, span)),
                "isoformat" => BoundCallable.CreateNoArguments(date, "date.isoformat", static (receiver, span, context) => PyDateTimeOps.OwnDateTimeText(receiver.IsoFormat(), context.MemoryGovernor, span)),
                "__format__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", "date.__format__(format_spec) expects one string argument.", span);
                    }

                    return PyDateTimeOps.OwnDateTimeText(PyDateTimeOps.FormatValue(date, format, span), context.MemoryGovernor, span);
                }, "date.__format__", ["format_spec"]),
                "strftime" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", "date.strftime(format) expects one string argument.", span);
                    }

                    return PyDateTimeOps.OwnDateTimeText(PyDateTimeOps.Strftime(date.Value, format, span), context.MemoryGovernor, span);
                }, "date.strftime", ["format"]),
                "replace" => BoundCallable.CreateWithPresence((bound, span, context) =>
                {
                    var arguments = bound.Values;
                    bool IsAssigned(int index) => index < bound.Assigned.Length && bound.Assigned[index];
                    int year, month, day;
                    try
                    {
                        year = ReplacementInt(arguments, 0, (int)date.Year, span, context, IsAssigned(0));
                        month = ReplacementInt(arguments, 1, (int)date.Month, span, context, IsAssigned(1));
                        day = ReplacementInt(arguments, 2, (int)date.Day, span, context, IsAssigned(2));
                    }
                    catch (OverflowException ex)
                    {
                        throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C int", span, ex);
                    }

                    if (year < 1 || year > 9999)
                    {
                        throw new LythonRuntimeException("ValueError", $"year {year} is out of range", span);
                    }

                    if (month < 1 || month > 12)
                    {
                        throw new LythonRuntimeException("ValueError", "month must be in 1..12", span);
                    }

                    if (day < 1 || day > DateTime.DaysInMonth(year, month))
                    {
                        throw new LythonRuntimeException("ValueError", "day is out of range for month", span);
                    }

                    return PyDateTimeOps.OwnDateTimeValue(new PyDate(new DateOnly(year, month, day)), context, span);
                }, "date.replace", ["year", "month", "day"], 0),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal static class TimeMembers
    {
        public static bool TryGetMember(PyTime time, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "hour" => time.Hour,
                "minute" => time.Minute,
                "second" => time.Second,
                "microsecond" => time.Microsecond,
                "tzinfo" => time.TzInfo is null ? PyNone.Instance : time.TzInfo,
                "fold" => new BigInteger(time.Fold),
                "utcoffset" => BoundCallable.CreateNoArguments(
                    time,
                    "time.utcoffset",
                    static (receiver, span, context) => receiver.TzInfo is null ? (object)PyNone.Instance : PyDateTimeOps.OwnDateTimeValue(new PyTimedelta(receiver.TzInfo.Offset), context, span)),
                "tzname" => BoundCallable.CreateNoArguments(
                    time,
                    "time.tzname",
                    static (receiver, span, context) => receiver.TzInfo is null ? PyNone.Instance : PyDateTimeOps.OwnDateTimeText(PyString.FromString(receiver.TzInfo.Name), context.MemoryGovernor, span)),
                "dst" => BoundCallable.CreateNoArguments(time, "time.dst", static (_, _, _) => PyNone.Instance),
                "isoformat" => BoundCallable.CreateWithPresence((bound, span, context) =>
                {
                    if (bound.Values.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "time.isoformat([timespec]) expects zero or one argument.", span);
                    }

                    var timespec = GetTimespec(ArgAt(bound.Values, 0), 1, span, context, IsAssigned(bound, 0));
                    return PyDateTimeOps.OwnDateTimeText(time.IsoFormat(timespec), context.MemoryGovernor, span);
                }, "time.isoformat", ["timespec"], 0),
                "__format__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", "time.__format__(format_spec) expects one string argument.", span);
                    }

                    return PyDateTimeOps.OwnDateTimeText(PyDateTimeOps.FormatValue(time, format, span), context.MemoryGovernor, span);
                }, "time.__format__", ["format_spec"]),
                "strftime" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", "time.strftime(format) expects one string argument.", span);
                    }

                    return PyDateTimeOps.OwnDateTimeText(PyDateTimeOps.Strftime(time.Value, time.TzInfo, format, span), context.MemoryGovernor, span);
                }, "time.strftime", ["format"]),
                "replace" => BoundCallable.CreateWithPresence((bound, span, context) =>
                {
                    var arguments = bound.Values;
                    bool IsAssigned(int index) => index < bound.Assigned.Length && bound.Assigned[index];
                    int hour, minute, second, microsecond, fold;
                    try
                    {
                        hour = ReplacementInt(arguments, 0, (int)time.Hour, span, context, IsAssigned(0));
                        minute = ReplacementInt(arguments, 1, (int)time.Minute, span, context, IsAssigned(1));
                        second = ReplacementInt(arguments, 2, (int)time.Second, span, context, IsAssigned(2));
                        microsecond = ReplacementInt(arguments, 3, (int)time.Microsecond, span, context, IsAssigned(3));
                        fold = ReplacementInt(arguments, 5, time.Fold, span, context, IsAssigned(5));
                    }
                    catch (OverflowException ex)
                    {
                        throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C int", span, ex);
                    }

                    if (hour < 0 || hour > 23)
                    {
                        throw new LythonRuntimeException("ValueError", "hour must be in 0..23", span);
                    }

                    if (minute < 0 || minute > 59)
                    {
                        throw new LythonRuntimeException("ValueError", "minute must be in 0..59", span);
                    }

                    if (second < 0 || second > 59)
                    {
                        throw new LythonRuntimeException("ValueError", "second must be in 0..59", span);
                    }

                    if (microsecond < 0 || microsecond > 999999)
                    {
                        throw new LythonRuntimeException("ValueError", "microsecond must be in 0..999999", span);
                    }

                    if (fold is not 0 and not 1)
                    {
                        throw new LythonRuntimeException("ValueError", "fold must be either 0 or 1", span);
                    }

                    return PyDateTimeOps.OwnDateTimeValue(new PyTime(
                        new TimeOnly(hour, minute, second, microsecond / 1000, microsecond % 1000),
                        ReplacementTimezone(arguments, 4, time.TzInfo, "time.replace", span, IsAssigned(4)),
                        fold), context, span);
                }, "time.replace", ["hour", "minute", "second", "microsecond", "tzinfo", "fold"], 0),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    internal static class DateTimeMembers
    {
        public static bool TryGetMember(PyDateTime dateTime, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "year" => dateTime.Year,
                "month" => dateTime.Month,
                "day" => dateTime.Day,
                "hour" => dateTime.Hour,
                "minute" => dateTime.Minute,
                "second" => dateTime.Second,
                "microsecond" => dateTime.Microsecond,
                "tzinfo" => dateTime.TzInfo is null ? PyNone.Instance : dateTime.TzInfo,
                "fold" => new BigInteger(dateTime.Fold),
                "date" => BoundCallable.CreateNoArguments(dateTime, "datetime.date", static (receiver, span, context) => PyDateTimeOps.OwnDateTimeValue(receiver.DatePart(), context, span)),
                "time" => BoundCallable.CreateNoArguments(dateTime, "datetime.time", static (receiver, span, context) => PyDateTimeOps.OwnDateTimeValue(receiver.NaiveTimePart(), context, span)),
                "timetz" => BoundCallable.CreateNoArguments(dateTime, "datetime.timetz", static (receiver, span, context) => PyDateTimeOps.OwnDateTimeValue(receiver.TimePart(), context, span)),
                "weekday" => BoundCallable.CreateNoArguments(dateTime, "datetime.weekday", static (receiver, _, _) => new BigInteger(receiver.DatePart().Weekday())),
                "isoweekday" => BoundCallable.CreateNoArguments(dateTime, "datetime.isoweekday", static (receiver, _, _) => new BigInteger(receiver.DatePart().IsoWeekday())),
                "isocalendar" => BoundCallable.CreateNoArguments(dateTime, "datetime.isocalendar", static (receiver, span, context) => PyDateTimeOps.IsoCalendar(receiver.DatePart().Value, context, span)),
                "toordinal" => BoundCallable.CreateNoArguments(dateTime, "datetime.toordinal", static (receiver, _, _) => receiver.ToOrdinal()),
                "timetuple" => BoundCallable.CreateNoArguments(
                    dateTime,
                    "datetime.timetuple",
                    static (receiver, span, context) => PyDateTimeOps.TimeTuple(receiver.Value, -1, context, span)),
                "utctimetuple" => BoundCallable.CreateNoArguments(dateTime, "datetime.utctimetuple", static (receiver, span, context) =>
                {
                    var utcValue = receiver.TzInfo is null
                        ? receiver.Value
                        : new DateTime(receiver.ToUtcTicks(), DateTimeKind.Unspecified);
                    return PyDateTimeOps.TimeTuple(utcValue, 0, context, span);
                }),
                "ctime" => BoundCallable.CreateNoArguments(dateTime, "datetime.ctime", static (receiver, span, context) => PyDateTimeOps.OwnDateTimeText(PyDateTimeOps.CTime(receiver.Value), context.MemoryGovernor, span)),
                "timestamp" => BoundCallable.CreateNoArguments(dateTime, "datetime.timestamp", static (receiver, span, context) =>
                {
                    var localOffset = TimeSpan.Zero;
                    if (receiver.TzInfo is null)
                    {
                        context.RegisterHostCall(span);
                        localOffset = context.Host.LocalNow.Offset;
                    }

                    return PyDateTimeOps.Timestamp(receiver, localOffset, span);
                }),
                "utcoffset" => BoundCallable.CreateNoArguments(
                    dateTime,
                    "datetime.utcoffset",
                    static (receiver, span, context) => receiver.TzInfo is null ? (object)PyNone.Instance : PyDateTimeOps.OwnDateTimeValue(new PyTimedelta(receiver.TzInfo.Offset), context, span)),
                "tzname" => BoundCallable.CreateNoArguments(
                    dateTime,
                    "datetime.tzname",
                    static (receiver, span, context) => receiver.TzInfo is null ? PyNone.Instance : PyDateTimeOps.OwnDateTimeText(PyString.FromString(receiver.TzInfo.Name), context.MemoryGovernor, span)),
                "dst" => BoundCallable.CreateNoArguments(dateTime, "datetime.dst", static (_, _, _) => PyNone.Instance),
                "astimezone" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.astimezone([tz]) expects zero or one argument.", span);
                    }

                    var targetTimezone = ArgAt(arguments, 0) switch
                    {
                        null or PyNone => null,
                        PyTimezone tz => tz,
                        _ => throw new LythonRuntimeException("TypeError", "datetime.astimezone(tz) expects tz to be a timezone or None.", span)
                    };

                    context.RegisterHostCall(span);
                    return PyDateTimeOps.OwnDateTimeValue(PyDateTimeOps.Astimezone(dateTime, targetTimezone, context.Host.LocalNow.Offset, span), context, span);
                }, "datetime.astimezone", ["tz"], 0),
                "isoformat" => BoundCallable.CreateWithPresence((bound, span, context) =>
                {
                    if (bound.Values.Length > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.isoformat([sep][, timespec]) expects zero to two arguments.", span);
                    }

                    var separator = GetSeparator(ArgAt(bound.Values, 0), span, context, IsAssigned(bound, 0));
                    var timespec = GetTimespec(ArgAt(bound.Values, 1), 2, span, context, IsAssigned(bound, 1));
                    return PyDateTimeOps.OwnDateTimeText(dateTime.IsoFormat(separator, timespec), context.MemoryGovernor, span);
                }, "datetime.isoformat", ["sep", "timespec"], 0),
                "__format__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.__format__(format_spec) expects one string argument.", span);
                    }

                    return PyDateTimeOps.OwnDateTimeText(PyDateTimeOps.FormatValue(dateTime, format, span), context.MemoryGovernor, span);
                }, "datetime.__format__", ["format_spec"]),
                "strftime" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.strftime(format) expects one string argument.", span);
                    }

                    return PyDateTimeOps.OwnDateTimeText(PyDateTimeOps.Strftime(dateTime.Value, dateTime.TzInfo, format, span), context.MemoryGovernor, span);
                }, "datetime.strftime", ["format"]),
                "replace" => BoundCallable.CreateWithPresence((bound, span, context) =>
                {
                    var arguments = bound.Values;
                    bool IsAssigned(int index) => index < bound.Assigned.Length && bound.Assigned[index];
                    int year, month, day, hour, minute, second, microsecond, fold;
                    try
                    {
                        year = ReplacementInt(arguments, 0, (int)dateTime.Year, span, context, IsAssigned(0));
                        month = ReplacementInt(arguments, 1, (int)dateTime.Month, span, context, IsAssigned(1));
                        day = ReplacementInt(arguments, 2, (int)dateTime.Day, span, context, IsAssigned(2));
                        hour = ReplacementInt(arguments, 3, (int)dateTime.Hour, span, context, IsAssigned(3));
                        minute = ReplacementInt(arguments, 4, (int)dateTime.Minute, span, context, IsAssigned(4));
                        second = ReplacementInt(arguments, 5, (int)dateTime.Second, span, context, IsAssigned(5));
                        microsecond = ReplacementInt(arguments, 6, (int)dateTime.Microsecond, span, context, IsAssigned(6));
                        fold = ReplacementInt(arguments, 8, dateTime.Fold, span, context, IsAssigned(8));
                    }
                    catch (OverflowException ex)
                    {
                        throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C int", span, ex);
                    }

                    if (year < 1 || year > 9999)
                    {
                        throw new LythonRuntimeException("ValueError", $"year {year} is out of range", span);
                    }

                    if (month < 1 || month > 12)
                    {
                        throw new LythonRuntimeException("ValueError", "month must be in 1..12", span);
                    }

                    if (day < 1 || day > DateTime.DaysInMonth(year, month))
                    {
                        throw new LythonRuntimeException("ValueError", "day is out of range for month", span);
                    }

                    if (hour < 0 || hour > 23)
                    {
                        throw new LythonRuntimeException("ValueError", "hour must be in 0..23", span);
                    }

                    if (minute < 0 || minute > 59)
                    {
                        throw new LythonRuntimeException("ValueError", "minute must be in 0..59", span);
                    }

                    if (second < 0 || second > 59)
                    {
                        throw new LythonRuntimeException("ValueError", "second must be in 0..59", span);
                    }

                    if (microsecond < 0 || microsecond > 999999)
                    {
                        throw new LythonRuntimeException("ValueError", "microsecond must be in 0..999999", span);
                    }

                    if (fold is not 0 and not 1)
                    {
                        throw new LythonRuntimeException("ValueError", "fold must be either 0 or 1", span);
                    }

                    return PyDateTimeOps.OwnDateTimeValue(new PyDateTime(
                        new DateTime(year, month, day, hour, minute, second, microsecond / 1000, DateTimeKind.Unspecified).AddTicks((microsecond % 1000) * 10L),
                        ReplacementTimezone(arguments, 7, dateTime.TzInfo, "datetime.replace", span, IsAssigned(7)),
                        fold), context, span);
                }, "datetime.replace", ["year", "month", "day", "hour", "minute", "second", "microsecond", "tzinfo", "fold"], 0),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

    }

    internal static class TimezoneMembers
    {
        public static bool TryGetMember(PyTimezone timezone, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "utcoffset" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "timezone.utcoffset(dt) expects one argument.", span);
                    }

                    return PyDateTimeOps.OwnDateTimeValue(new PyTimedelta(timezone.Offset), context, span);
                }, "timezone.utcoffset", ["dt"]),
                "tzname" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "timezone.tzname(dt) expects one argument.", span);
                    }

                    return PyDateTimeOps.OwnDateTimeText(PyString.FromString(timezone.Name), context.MemoryGovernor, span);
                }, "timezone.tzname", ["dt"]),
                "dst" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "timezone.dst(dt) expects one argument.", span);
                    }

                    return PyNone.Instance;
                }, "timezone.dst", ["dt"]),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    private static int ToInt(object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        // Replacement fields coerce through __index__ like CPython; remaining
        // rejections name the original type instead of the factory.
        var coerced = LythonRuntime.CoerceIndexProtocol(value, context, span);
        if (coerced is bool flag)
        {
            return flag ? 1 : 0;
        }

        if (coerced is BigInteger integer)
        {
            return (int)integer;
        }

        throw new LythonRuntimeException("TypeError", "'" + UnboundTypeMethod.PythonTypeName(value, context) + "' object cannot be interpreted as an integer", span);
    }

    private static int ReplacementInt(
        object[] arguments,
        int index,
        int currentValue,
        LythonSourceSpan span,
        LythonRuntime.ExecutionContext context,
        bool assigned)
    {
        var value = ArgAt(arguments, index);
        return !assigned || value is null ? currentValue : ToInt(value, context, span);
    }

    private static PyTimezone? ReplacementTimezone(
        object[] arguments,
        int index,
        PyTimezone? currentValue,
        string owner,
        LythonSourceSpan span,
        bool assigned)
    {
        if (!assigned)
        {
            return currentValue;
        }

        return ArgAt(arguments, index) switch
        {
            null => currentValue,
            PyNone => null,
            PyTimezone timezone => timezone,
            _ => throw new LythonRuntimeException("TypeError", $"{owner}(..., tzinfo=...) expects a timezone or None.", span),
        };
    }


    private static string GetTimespec(object? value, int position, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool assigned)
    {
        // Omitted values keep the default; every explicit value (including
        // None) validates like CPython, which numbers the argument.
        if (!assigned || value is null)
        {
            return "auto";
        }

        if (!PyStringOps.TryAsString(value, out var text))
        {
            throw new LythonRuntimeException("TypeError", $"isoformat() argument {position} must be str, not {IsoformatTypeName(value, context)}", span);
        }

        var timespec = text.AsString();
        return timespec is "auto" or "hours" or "minutes" or "seconds" or "milliseconds" or "microseconds"
            ? timespec
            : throw new LythonRuntimeException("ValueError", "Unknown timespec value", span);
    }

    private static string GetSeparator(object? value, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool assigned)
    {
        if (!assigned || value is null)
        {
            return "T";
        }

        if (!PyStringOps.TryAsString(value, out var text) || text.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", $"isoformat() argument 1 must be a unicode character, not {IsoformatTypeName(value, context)}", span);
        }

        return text.AsString();
    }

    private static bool IsAssigned(BoundCallArguments bound, int index)
        => index < bound.Assigned.Length && bound.Assigned[index];

    private static string IsoformatTypeName(object? value, LythonRuntime.ExecutionContext context)
        => value is null or PyNone ? "None" : LythonRuntime.UnboundTypeMethod.PythonTypeName(value, context);

    private static object? ArgAt(object[] arguments, int index) => index < arguments.Length ? arguments[index] : null;
}
