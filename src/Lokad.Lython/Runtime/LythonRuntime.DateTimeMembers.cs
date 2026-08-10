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
                "isocalendar" => BoundCallable.CreateNoArguments(date, "date.isocalendar", static (receiver, _, _) => PyDateTimeOps.IsoCalendar(receiver.Value)),
                "toordinal" => BoundCallable.CreateNoArguments(date, "date.toordinal", static (receiver, _, _) => receiver.ToOrdinal()),
                "timetuple" => BoundCallable.CreateNoArguments(date, "date.timetuple", static (receiver, _, _) => PyDateTimeOps.TimeTuple(receiver.Value)),
                "ctime" => BoundCallable.CreateNoArguments(date, "date.ctime", static (receiver, _, _) => PyDateTimeOps.CTime(receiver.Value)),
                "isoformat" => BoundCallable.CreateNoArguments(date, "date.isoformat", static (receiver, _, _) => receiver.IsoFormat()),
                "__format__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", "date.__format__(format_spec) expects one string argument.", span);
                    }

                    return PyDateTimeOps.FormatValue(date, format, span);
                }, "date.__format__", ["format_spec"]),
                "strftime" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", "date.strftime(format) expects one string argument.", span);
                    }

                    return PyDateTimeOps.Strftime(date.Value, format, span);
                }, "date.strftime", ["format"]),
                "replace" => BoundCallable.Create((arguments, span, _) =>
                {
                    return new PyDate(new DateOnly(
                        ReplacementInt(arguments, 0, (int)date.Year, "date.replace", span),
                        ReplacementInt(arguments, 1, (int)date.Month, "date.replace", span),
                        ReplacementInt(arguments, 2, (int)date.Day, "date.replace", span)));
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
                    static (receiver, _, _) => receiver.TzInfo is null ? PyNone.Instance : new PyTimedelta(receiver.TzInfo.Offset)),
                "tzname" => BoundCallable.CreateNoArguments(
                    time,
                    "time.tzname",
                    static (receiver, _, _) => receiver.TzInfo is null ? PyNone.Instance : PyString.FromString(receiver.TzInfo.Name)),
                "dst" => BoundCallable.CreateNoArguments(time, "time.dst", static (_, _, _) => PyNone.Instance),
                "isoformat" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "time.isoformat([timespec]) expects zero or one argument.", span);
                    }

                    var timespec = GetTimespec(ArgAt(arguments, 0), "time.isoformat", span);
                    return time.IsoFormat(timespec);
                }, "time.isoformat", ["timespec"], 0),
                "__format__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", "time.__format__(format_spec) expects one string argument.", span);
                    }

                    return PyDateTimeOps.FormatValue(time, format, span);
                }, "time.__format__", ["format_spec"]),
                "strftime" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", "time.strftime(format) expects one string argument.", span);
                    }

                    return PyDateTimeOps.Strftime(time.Value, time.TzInfo, format, span);
                }, "time.strftime", ["format"]),
                "replace" => BoundCallable.Create((arguments, span, context) =>
                {
                    _ = context;
                    var microArg = ArgAt(arguments, 3);
                    var microsecond = microArg is null or PyNone ? (int)time.Microsecond : ToInt(microArg, "time.replace", span);
                    var foldArg = ArgAt(arguments, 5);
                    var fold = foldArg is null or PyNone ? time.Fold : ToFold(foldArg, "time.replace", span);
                    return new PyTime(
                        new TimeOnly(
                            ReplacementInt(arguments, 0, (int)time.Hour, "time.replace", span),
                            ReplacementInt(arguments, 1, (int)time.Minute, "time.replace", span),
                            ReplacementInt(arguments, 2, (int)time.Second, "time.replace", span),
                            microsecond / 1000,
                            microsecond % 1000),
                        ReplacementTimezone(arguments, 4, time.TzInfo, "time.replace", span),
                        fold);
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
                "date" => BoundCallable.CreateNoArguments(dateTime, "datetime.date", static (receiver, _, _) => receiver.DatePart()),
                "time" => BoundCallable.CreateNoArguments(dateTime, "datetime.time", static (receiver, _, _) => receiver.NaiveTimePart()),
                "timetz" => BoundCallable.CreateNoArguments(dateTime, "datetime.timetz", static (receiver, _, _) => receiver.TimePart()),
                "weekday" => BoundCallable.CreateNoArguments(dateTime, "datetime.weekday", static (receiver, _, _) => new BigInteger(receiver.DatePart().Weekday())),
                "isoweekday" => BoundCallable.CreateNoArguments(dateTime, "datetime.isoweekday", static (receiver, _, _) => new BigInteger(receiver.DatePart().IsoWeekday())),
                "isocalendar" => BoundCallable.CreateNoArguments(dateTime, "datetime.isocalendar", static (receiver, _, _) => PyDateTimeOps.IsoCalendar(receiver.DatePart().Value)),
                "toordinal" => BoundCallable.CreateNoArguments(dateTime, "datetime.toordinal", static (receiver, _, _) => receiver.ToOrdinal()),
                "timetuple" => BoundCallable.CreateNoArguments(
                    dateTime,
                    "datetime.timetuple",
                    static (receiver, _, _) => PyDateTimeOps.TimeTuple(receiver.Value, receiver.TzInfo is null ? -1 : 0)),
                "utctimetuple" => BoundCallable.CreateNoArguments(dateTime, "datetime.utctimetuple", static (receiver, _, _) =>
                {
                    var utcValue = receiver.TzInfo is null
                        ? receiver.Value
                        : new DateTime(receiver.ToUtcTicks(), DateTimeKind.Unspecified);
                    return PyDateTimeOps.TimeTuple(utcValue, 0);
                }),
                "ctime" => BoundCallable.CreateNoArguments(dateTime, "datetime.ctime", static (receiver, _, _) => PyDateTimeOps.CTime(receiver.Value)),
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
                    static (receiver, _, _) => receiver.TzInfo is null ? PyNone.Instance : new PyTimedelta(receiver.TzInfo.Offset)),
                "tzname" => BoundCallable.CreateNoArguments(
                    dateTime,
                    "datetime.tzname",
                    static (receiver, _, _) => receiver.TzInfo is null ? PyNone.Instance : PyString.FromString(receiver.TzInfo.Name)),
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
                    return PyDateTimeOps.Astimezone(dateTime, targetTimezone, context.Host.LocalNow.Offset, span);
                }, "datetime.astimezone", ["tz"], 0),
                "isoformat" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.isoformat([sep][, timespec]) expects zero to two arguments.", span);
                    }

                    var separator = GetSeparator(ArgAt(arguments, 0), "datetime.isoformat", span);
                    var timespec = GetTimespec(ArgAt(arguments, 1), "datetime.isoformat", span);
                    return dateTime.IsoFormat(separator, timespec);
                }, "datetime.isoformat", ["sep", "timespec"], 0),
                "__format__" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.__format__(format_spec) expects one string argument.", span);
                    }

                    return PyDateTimeOps.FormatValue(dateTime, format, span);
                }, "datetime.__format__", ["format_spec"]),
                "strftime" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.strftime(format) expects one string argument.", span);
                    }

                    return PyDateTimeOps.Strftime(dateTime.Value, dateTime.TzInfo, format, span);
                }, "datetime.strftime", ["format"]),
                "replace" => BoundCallable.Create((arguments, span, _) =>
                {
                    var microArg = ArgAt(arguments, 6);
                    var microsecond = microArg is null or PyNone ? (int)dateTime.Microsecond : ToInt(microArg, "datetime.replace", span);
                    var foldArg = ArgAt(arguments, 8);
                    var fold = foldArg is null or PyNone ? dateTime.Fold : ToFold(foldArg, "datetime.replace", span);
                    return new PyDateTime(
                        new DateTime(
                            ReplacementInt(arguments, 0, (int)dateTime.Year, "datetime.replace", span),
                            ReplacementInt(arguments, 1, (int)dateTime.Month, "datetime.replace", span),
                            ReplacementInt(arguments, 2, (int)dateTime.Day, "datetime.replace", span),
                            ReplacementInt(arguments, 3, (int)dateTime.Hour, "datetime.replace", span),
                            ReplacementInt(arguments, 4, (int)dateTime.Minute, "datetime.replace", span),
                            ReplacementInt(arguments, 5, (int)dateTime.Second, "datetime.replace", span),
                            microsecond / 1000,
                            DateTimeKind.Unspecified).AddTicks((microsecond % 1000) * 10L),
                        ReplacementTimezone(arguments, 7, dateTime.TzInfo, "datetime.replace", span),
                        fold);
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
                "utcoffset" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "timezone.utcoffset(dt) expects one argument.", span);
                    }

                    return new PyTimedelta(timezone.Offset);
                }, "timezone.utcoffset", ["dt"]),
                "tzname" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "timezone.tzname(dt) expects one argument.", span);
                    }

                    return PyString.FromString(timezone.Name);
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

    private static int ToInt(object value, string owner, LythonSourceSpan span)
    {
        if (value is BigInteger integer)
        {
            return (int)integer;
        }

        throw new LythonRuntimeException("TypeError", $"{owner} expects integer fields.", span);
    }

    private static int ReplacementInt(
        object[] arguments,
        int index,
        int currentValue,
        string owner,
        LythonSourceSpan span)
    {
        var value = ArgAt(arguments, index);
        return value is null or PyNone ? currentValue : ToInt(value, owner, span);
    }

    private static PyTimezone? ReplacementTimezone(
        object[] arguments,
        int index,
        PyTimezone? currentValue,
        string owner,
        LythonSourceSpan span)
        => ArgAt(arguments, index) switch
        {
            null => currentValue,
            PyNone => null,
            PyTimezone timezone => timezone,
            _ => throw new LythonRuntimeException("TypeError", $"{owner}(..., tzinfo=...) expects a timezone or None.", span),
        };

    private static int ToFold(object value, string owner, LythonSourceSpan span)
    {
        var fold = ToInt(value, owner, span);
        if (fold is not 0 and not 1)
        {
            throw new LythonRuntimeException("ValueError", $"{owner} fold must be either 0 or 1.", span);
        }

        return fold;
    }

    private static string GetTimespec(object? value, string owner, LythonSourceSpan span)
    {
        if (value is null or PyNone)
        {
            return "auto";
        }

        if (!PyStringOps.TryAsString(value, out var text))
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(..., timespec=...) expects a string.", span);
        }

        var timespec = text.AsString();
        return timespec is "auto" or "hours" or "minutes" or "seconds" or "milliseconds" or "microseconds"
            ? timespec
            : throw new LythonRuntimeException("ValueError", "Unknown timespec value.", span);
    }

    private static string GetSeparator(object? value, string owner, LythonSourceSpan span)
    {
        if (value is null or PyNone)
        {
            return "T";
        }

        if (!PyStringOps.TryAsString(value, out var text) || text.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(..., sep=...) expects a one-character string.", span);
        }

        return text.AsString();
    }

    private static object? ArgAt(object[] arguments, int index) => index < arguments.Length ? arguments[index] : null;
}
