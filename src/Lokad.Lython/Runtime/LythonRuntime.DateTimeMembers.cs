using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class TimedeltaMembers
    {
        public static bool TryGetMember(PyTimedelta delta, string name, out object value)
        {
            value = name switch
            {
                "days" => delta.Days,
                "seconds" => delta.Seconds,
                "microseconds" => delta.Microseconds,
                "total_seconds" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "timedelta.total_seconds() expects no arguments.", span);
                    }

                    return delta.TotalSeconds();
                }),
                _ => null!
            };

            return value is not null;
        }
    }

    internal static class DateMembers
    {
        public static bool TryGetMember(PyDate date, string name, out object value)
        {
            value = name switch
            {
                "year" => date.Year,
                "month" => date.Month,
                "day" => date.Day,
                "weekday" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "date.weekday() expects no arguments.", span);
                    }

                    return new BigInteger(date.Weekday());
                }),
                "isoweekday" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "date.isoweekday() expects no arguments.", span);
                    }

                    return new BigInteger(date.IsoWeekday());
                }),
                "isocalendar" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "date.isocalendar() expects no arguments.", span);
                    }

                    return PyDateTimeOps.IsoCalendar(date.Value);
                }),
                "toordinal" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "date.toordinal() expects no arguments.", span);
                    }

                    return date.ToOrdinal();
                }),
                "timetuple" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "date.timetuple() expects no arguments.", span);
                    }

                    return PyDateTimeOps.TimeTuple(date.Value);
                }),
                "ctime" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "date.ctime() expects no arguments.", span);
                    }

                    return PyDateTimeOps.CTime(date.Value);
                }),
                "isoformat" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "date.isoformat() expects no arguments.", span);
                    }

                    return date.IsoFormat();
                }),
                "strftime" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", "date.strftime(format) expects one string argument.", span);
                    }

                    return PyDateTimeOps.Strftime(date.Value, format, span);
                }, "date.strftime", ["format"]),
                "replace" => new BoundCallable((arguments, span, _) =>
                {
                    return new PyDate(new DateOnly(
                        ArgAt(arguments, 0) is null or PyNone ? (int)date.Year : ToInt(ArgAt(arguments, 0)!, "date.replace", span),
                        ArgAt(arguments, 1) is null or PyNone ? (int)date.Month : ToInt(ArgAt(arguments, 1)!, "date.replace", span),
                        ArgAt(arguments, 2) is null or PyNone ? (int)date.Day : ToInt(ArgAt(arguments, 2)!, "date.replace", span)));
                }, "date.replace", ["year", "month", "day"], 0),
                _ => null!
            };

            return value is not null;
        }
    }

    internal static class TimeMembers
    {
        public static bool TryGetMember(PyTime time, string name, out object value)
        {
            value = name switch
            {
                "hour" => time.Hour,
                "minute" => time.Minute,
                "second" => time.Second,
                "microsecond" => time.Microsecond,
                "tzinfo" => time.TzInfo is null ? PyNone.Instance : time.TzInfo,
                "utcoffset" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "time.utcoffset() expects no arguments.", span);
                    }

                    return time.TzInfo is null ? PyNone.Instance : new PyTimedelta(time.TzInfo.Offset);
                }),
                "tzname" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "time.tzname() expects no arguments.", span);
                    }

                    return time.TzInfo is null ? PyNone.Instance : PyString.FromString(time.TzInfo.Name);
                }),
                "dst" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "time.dst() expects no arguments.", span);
                    }

                    return PyNone.Instance;
                }),
                "isoformat" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "time.isoformat() expects no arguments.", span);
                    }

                    return time.IsoFormat();
                }),
                "strftime" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", "time.strftime(format) expects one string argument.", span);
                    }

                    return PyDateTimeOps.Strftime(time.Value, time.TzInfo, format, span);
                }, "time.strftime", ["format"]),
                "replace" => new BoundCallable((arguments, span, context) =>
                {
                    _ = context;
                    var microArg = ArgAt(arguments, 3);
                    var microsecond = microArg is null or PyNone ? (int)time.Microsecond : ToInt(microArg, "time.replace", span);
                    return new PyTime(
                        new TimeOnly(
                            ArgAt(arguments, 0) is null or PyNone ? (int)time.Hour : ToInt(ArgAt(arguments, 0)!, "time.replace", span),
                            ArgAt(arguments, 1) is null or PyNone ? (int)time.Minute : ToInt(ArgAt(arguments, 1)!, "time.replace", span),
                            ArgAt(arguments, 2) is null or PyNone ? (int)time.Second : ToInt(ArgAt(arguments, 2)!, "time.replace", span),
                            microsecond / 1000,
                            microsecond % 1000),
                        ArgAt(arguments, 4) switch
                        {
                            null => time.TzInfo,
                            PyNone => null,
                            PyTimezone tz => tz,
                            _ => throw new LythonRuntimeException("TypeError", "time.replace(..., tzinfo=...) expects a timezone or None.", span)
                        });
                }, "time.replace", ["hour", "minute", "second", "microsecond", "tzinfo"], 0),
                _ => null!
            };

            return value is not null;
        }
    }

    internal static class DateTimeMembers
    {
        public static bool TryGetMember(PyDateTime dateTime, string name, out object value)
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
                "date" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.date() expects no arguments.", span);
                    }

                    return dateTime.DatePart();
                }),
                "time" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.time() expects no arguments.", span);
                    }

                    return dateTime.NaiveTimePart();
                }),
                "timetz" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.timetz() expects no arguments.", span);
                    }

                    return dateTime.TimePart();
                }),
                "weekday" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.weekday() expects no arguments.", span);
                    }

                    return new BigInteger(dateTime.DatePart().Weekday());
                }),
                "isoweekday" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.isoweekday() expects no arguments.", span);
                    }

                    return new BigInteger(dateTime.DatePart().IsoWeekday());
                }),
                "isocalendar" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.isocalendar() expects no arguments.", span);
                    }

                    return PyDateTimeOps.IsoCalendar(dateTime.DatePart().Value);
                }),
                "toordinal" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.toordinal() expects no arguments.", span);
                    }

                    return dateTime.ToOrdinal();
                }),
                "timetuple" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.timetuple() expects no arguments.", span);
                    }

                    return PyDateTimeOps.TimeTuple(dateTime.Value, dateTime.TzInfo is null ? -1 : 0);
                }),
                "utctimetuple" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.utctimetuple() expects no arguments.", span);
                    }

                    var utcValue = dateTime.TzInfo is null ? dateTime.Value : dateTime.ToOffset().UtcDateTime;
                    return PyDateTimeOps.TimeTuple(utcValue, 0);
                }),
                "ctime" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.ctime() expects no arguments.", span);
                    }

                    return PyDateTimeOps.CTime(dateTime.Value);
                }),
                "timestamp" => new BoundCallable((arguments, span, context) =>
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
                "utcoffset" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.utcoffset() expects no arguments.", span);
                    }

                    return dateTime.TzInfo is null ? PyNone.Instance : new PyTimedelta(dateTime.TzInfo.Offset);
                }),
                "tzname" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.tzname() expects no arguments.", span);
                    }

                    return dateTime.TzInfo is null ? PyNone.Instance : PyString.FromString(dateTime.TzInfo.Name);
                }),
                "dst" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.dst() expects no arguments.", span);
                    }

                    return PyNone.Instance;
                }),
                "isoformat" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.isoformat() expects no arguments.", span);
                    }

                    return dateTime.IsoFormat();
                }),
                "strftime" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.strftime(format) expects one string argument.", span);
                    }

                    return PyDateTimeOps.Strftime(dateTime.Value, dateTime.TzInfo, format, span);
                }, "datetime.strftime", ["format"]),
                "replace" => new BoundCallable((arguments, span, _) =>
                {
                    var microArg = ArgAt(arguments, 6);
                    var microsecond = microArg is null or PyNone ? (int)dateTime.Microsecond : ToInt(microArg, "datetime.replace", span);
                    return new PyDateTime(
                        new DateTime(
                            ArgAt(arguments, 0) is null or PyNone ? (int)dateTime.Year : ToInt(ArgAt(arguments, 0)!, "datetime.replace", span),
                            ArgAt(arguments, 1) is null or PyNone ? (int)dateTime.Month : ToInt(ArgAt(arguments, 1)!, "datetime.replace", span),
                            ArgAt(arguments, 2) is null or PyNone ? (int)dateTime.Day : ToInt(ArgAt(arguments, 2)!, "datetime.replace", span),
                            ArgAt(arguments, 3) is null or PyNone ? (int)dateTime.Hour : ToInt(ArgAt(arguments, 3)!, "datetime.replace", span),
                            ArgAt(arguments, 4) is null or PyNone ? (int)dateTime.Minute : ToInt(ArgAt(arguments, 4)!, "datetime.replace", span),
                            ArgAt(arguments, 5) is null or PyNone ? (int)dateTime.Second : ToInt(ArgAt(arguments, 5)!, "datetime.replace", span),
                            microsecond / 1000,
                            DateTimeKind.Unspecified).AddTicks((microsecond % 1000) * 10L),
                        ArgAt(arguments, 7) switch
                        {
                            null => dateTime.TzInfo,
                            PyNone => null,
                            PyTimezone tz => tz,
                            _ => throw new LythonRuntimeException("TypeError", "datetime.replace(..., tzinfo=...) expects a timezone or None.", span)
                        });
                }, "datetime.replace", ["year", "month", "day", "hour", "minute", "second", "microsecond", "tzinfo"], 0),
                _ => null!
            };

            return value is not null;
        }

        private static int ToInt(object value, string owner, LythonSourceSpan span)
        {
            if (value is BigInteger integer)
            {
                return (int)integer;
            }

            throw new LythonRuntimeException("TypeError", $"{owner} expects integer fields.", span);
        }
    }

    internal static class TimezoneMembers
    {
        public static bool TryGetMember(PyTimezone timezone, string name, out object value)
        {
            value = name switch
            {
                "utcoffset" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "timezone.utcoffset(dt) expects one argument.", span);
                    }

                    return new PyTimedelta(timezone.Offset);
                }, "timezone.utcoffset", ["dt"]),
                "tzname" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "timezone.tzname(dt) expects one argument.", span);
                    }

                    return PyString.FromString(timezone.Name);
                }, "timezone.tzname", ["dt"]),
                "dst" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "timezone.dst(dt) expects one argument.", span);
                    }

                    return PyNone.Instance;
                }, "timezone.dst", ["dt"]),
                _ => null!
            };

            return value is not null;
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

    private static object? ArgAt(object[] arguments, int index) => index < arguments.Length ? arguments[index] : null;
}
