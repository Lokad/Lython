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
                "total_seconds" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "timedelta.total_seconds() expects no arguments.", span);
                    }

                    return delta.TotalSeconds();
                }),
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
                "weekday" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "date.weekday() expects no arguments.", span);
                    }

                    return new BigInteger(date.Weekday());
                }),
                "isoweekday" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "date.isoweekday() expects no arguments.", span);
                    }

                    return new BigInteger(date.IsoWeekday());
                }),
                "isocalendar" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "date.isocalendar() expects no arguments.", span);
                    }

                    return PyDateTimeOps.IsoCalendar(date.Value);
                }),
                "toordinal" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "date.toordinal() expects no arguments.", span);
                    }

                    return date.ToOrdinal();
                }),
                "timetuple" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "date.timetuple() expects no arguments.", span);
                    }

                    return PyDateTimeOps.TimeTuple(date.Value);
                }),
                "ctime" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "date.ctime() expects no arguments.", span);
                    }

                    return PyDateTimeOps.CTime(date.Value);
                }),
                "isoformat" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "date.isoformat() expects no arguments.", span);
                    }

                    return date.IsoFormat();
                }),
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
                "utcoffset" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "time.utcoffset() expects no arguments.", span);
                    }

                    return time.TzInfo is null ? PyNone.Instance : new PyTimedelta(time.TzInfo.Offset);
                }),
                "tzname" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "time.tzname() expects no arguments.", span);
                    }

                    return time.TzInfo is null ? PyNone.Instance : PyString.FromString(time.TzInfo.Name);
                }),
                "dst" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "time.dst() expects no arguments.", span);
                    }

                    return PyNone.Instance;
                }),
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
                "date" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.date() expects no arguments.", span);
                    }

                    return dateTime.DatePart();
                }),
                "time" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.time() expects no arguments.", span);
                    }

                    return dateTime.NaiveTimePart();
                }),
                "timetz" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.timetz() expects no arguments.", span);
                    }

                    return dateTime.TimePart();
                }),
                "weekday" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.weekday() expects no arguments.", span);
                    }

                    return new BigInteger(dateTime.DatePart().Weekday());
                }),
                "isoweekday" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.isoweekday() expects no arguments.", span);
                    }

                    return new BigInteger(dateTime.DatePart().IsoWeekday());
                }),
                "isocalendar" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.isocalendar() expects no arguments.", span);
                    }

                    return PyDateTimeOps.IsoCalendar(dateTime.DatePart().Value);
                }),
                "toordinal" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.toordinal() expects no arguments.", span);
                    }

                    return dateTime.ToOrdinal();
                }),
                "timetuple" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.timetuple() expects no arguments.", span);
                    }

                    return PyDateTimeOps.TimeTuple(dateTime.Value, dateTime.TzInfo is null ? -1 : 0);
                }),
                "utctimetuple" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.utctimetuple() expects no arguments.", span);
                    }

                    var utcValue = dateTime.TzInfo is null
                        ? dateTime.Value
                        : new DateTime(dateTime.ToUtcTicks(), DateTimeKind.Unspecified);
                    return PyDateTimeOps.TimeTuple(utcValue, 0);
                }),
                "ctime" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.ctime() expects no arguments.", span);
                    }

                    return PyDateTimeOps.CTime(dateTime.Value);
                }),
                "timestamp" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.timestamp() expects no arguments.", span);
                    }

                    var localOffset = TimeSpan.Zero;
                    if (dateTime.TzInfo is null)
                    {
                        context.RegisterHostCall(span);
                        localOffset = context.Host.LocalNow.Offset;
                    }

                    return PyDateTimeOps.Timestamp(dateTime, localOffset, span);
                }),
                "utcoffset" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.utcoffset() expects no arguments.", span);
                    }

                    return dateTime.TzInfo is null ? PyNone.Instance : new PyTimedelta(dateTime.TzInfo.Offset);
                }),
                "tzname" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.tzname() expects no arguments.", span);
                    }

                    return dateTime.TzInfo is null ? PyNone.Instance : PyString.FromString(dateTime.TzInfo.Name);
                }),
                "dst" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.dst() expects no arguments.", span);
                    }

                    return PyNone.Instance;
                }),
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
