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
        // N17: hot fixed signatures hoisted per family (see ListMembers).
        private static readonly LythonCallableSignature DateFormatSignature = LythonCallableSignature.Create("date.__format__", ["format_spec"]);
        private static readonly LythonCallableSignature DateStrftimeSignature = LythonCallableSignature.Create("date.strftime", ["format"]);
        private static readonly LythonCallableSignature DateReplaceSignature = LythonCallableSignature.Create("date.replace", ["year", "month", "day"], 0);
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
                "ctime" => BoundCallable.CreateNoArguments(date, "date.ctime", static (receiver, span, context) => PyDateTimeOps.CTime(receiver.Value, context.MemoryGovernor, span)),
                "isoformat" => BoundCallable.CreateNoArguments(date, "date.isoformat", static (receiver, span, context) => receiver.IsoFormat(context.MemoryGovernor, span)),
                "__format__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "date.__format__(format_spec) expects one string argument.", span);
                    }

                    if (!PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", $"__format__() argument 1 must be str, not {IsoformatTypeName(arguments[0], context)}", span);
                    }

                    return PyDateTimeOps.FormatValue(date, format, span, context);
                }, DateFormatSignature),
                "strftime" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "date.strftime(format) expects one string argument.", span);
                    }

                    if (!PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", $"strftime() argument 1 must be str, not {IsoformatTypeName(arguments[0], context)}", span);
                    }

                    return PyDateTimeOps.Strftime(date.Value, format, span, context);
                }, DateStrftimeSignature),
                "replace" => BoundCallable.CreateWithPresence(
                    (bound, span, context) => ReplaceAsync(date, bound, span, context, false).GetAwaiter().GetResult(),
                    DateReplaceSignature,
                    (bound, span, context) => ReplaceAsync(date, bound, span, context, true)),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        private static async ValueTask<object> ReplaceAsync(PyDate date, BoundCallArguments bound,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var arguments = bound.Values;
            bool IsAssigned(int index) => index < bound.Assigned.Length && bound.Assigned[index];
            int year, month, day;
            try
            {
                year = await ReplacementIntAsync(arguments, 0, (int)date.Year, span, context, IsAssigned(0), asynchronous).ConfigureAwait(false);
                month = await ReplacementIntAsync(arguments, 1, (int)date.Month, span, context, IsAssigned(1), asynchronous).ConfigureAwait(false);
                day = await ReplacementIntAsync(arguments, 2, (int)date.Day, span, context, IsAssigned(2), asynchronous).ConfigureAwait(false);
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
        }

    }

    internal static class TimeMembers
    {
        // N17: hot fixed signatures hoisted per family (see ListMembers).
        private static readonly LythonCallableSignature TimeIsoformatSignature = LythonCallableSignature.Create("time.isoformat", ["timespec"], 0);
        private static readonly LythonCallableSignature TimeFormatSignature = LythonCallableSignature.Create("time.__format__", ["format_spec"]);
        private static readonly LythonCallableSignature TimeStrftimeSignature = LythonCallableSignature.Create("time.strftime", ["format"]);
        private static readonly LythonCallableSignature TimeReplaceSignature = LythonCallableSignature.Create("time.replace", ["hour", "minute", "second", "microsecond", "tzinfo", "fold"], 0);
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
                    static (receiver, span, context) => receiver.TzInfo is null ? PyNone.Instance : receiver.TzInfo.Name),
                "dst" => BoundCallable.CreateNoArguments(time, "time.dst", static (_, _, _) => PyNone.Instance),
                "isoformat" => BoundCallable.CreateWithPresence((bound, span, context) =>
                {
                    if (bound.Values.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "time.isoformat([timespec]) expects zero or one argument.", span);
                    }

                    var timespec = GetTimespec(ArgAt(bound.Values, 0), 1, span, context, IsAssigned(bound, 0));
                    return time.IsoFormat(timespec, context.MemoryGovernor, span);
                }, TimeIsoformatSignature),
                "__format__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "time.__format__(format_spec) expects one string argument.", span);
                    }

                    if (!PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", $"__format__() argument 1 must be str, not {IsoformatTypeName(arguments[0], context)}", span);
                    }

                    return PyDateTimeOps.FormatValue(time, format, span, context);
                }, TimeFormatSignature),
                "strftime" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "time.strftime(format) expects one string argument.", span);
                    }

                    if (!PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", $"strftime() argument 1 must be str, not {IsoformatTypeName(arguments[0], context)}", span);
                    }

                    return PyDateTimeOps.Strftime(time.Value, time.TzInfo, format, span, context);
                }, TimeStrftimeSignature),
                "replace" => BoundCallable.CreateWithPresence(
                    (bound, span, context) => ReplaceAsync(time, bound, span, context, false).GetAwaiter().GetResult(),
                    TimeReplaceSignature,
                    (bound, span, context) => ReplaceAsync(time, bound, span, context, true)),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        private static async ValueTask<object> ReplaceAsync(PyTime time, BoundCallArguments bound,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var arguments = bound.Values;
            bool IsAssigned(int index) => index < bound.Assigned.Length && bound.Assigned[index];
            int hour, minute, second, microsecond, fold;
            try
            {
                hour = await ReplacementIntAsync(arguments, 0, (int)time.Hour, span, context, IsAssigned(0), asynchronous).ConfigureAwait(false);
                minute = await ReplacementIntAsync(arguments, 1, (int)time.Minute, span, context, IsAssigned(1), asynchronous).ConfigureAwait(false);
                second = await ReplacementIntAsync(arguments, 2, (int)time.Second, span, context, IsAssigned(2), asynchronous).ConfigureAwait(false);
                microsecond = await ReplacementIntAsync(arguments, 3, (int)time.Microsecond, span, context, IsAssigned(3), asynchronous).ConfigureAwait(false);
                fold = await ReplacementIntAsync(arguments, 5, time.Fold, span, context, IsAssigned(5), asynchronous).ConfigureAwait(false);
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
                ReplacementTimezone(arguments, 4, time.TzInfo, span, context, IsAssigned(4)),
                fold), context, span);
        }

    }

    internal static class DateTimeMembers
    {
        // N17: hot fixed signatures hoisted per family (see ListMembers).
        private static readonly LythonCallableSignature DateTimeAstimezoneSignature = LythonCallableSignature.Create("datetime.astimezone", ["tz"], 0);
        private static readonly LythonCallableSignature DateTimeIsoformatSignature = LythonCallableSignature.Create("datetime.isoformat", ["sep", "timespec"], 0);
        private static readonly LythonCallableSignature DateTimeFormatSignature = LythonCallableSignature.Create("datetime.__format__", ["format_spec"]);
        private static readonly LythonCallableSignature DateTimeStrftimeSignature = LythonCallableSignature.Create("datetime.strftime", ["format"]);
        private static readonly LythonCallableSignature DateTimeReplaceSignature = LythonCallableSignature.Create("datetime.replace", ["year", "month", "day", "hour", "minute", "second", "microsecond", "tzinfo", "fold"], 0);
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
                    var ticks = receiver.ToUtcTicks();
                    if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                        throw new LythonRuntimeException("OverflowError", "date value out of range", span);
                    var utcValue = new DateTime(ticks, DateTimeKind.Unspecified);
                    return PyDateTimeOps.TimeTuple(utcValue, 0, context, span);
                }),
                "ctime" => BoundCallable.CreateNoArguments(dateTime, "datetime.ctime", static (receiver, span, context) => PyDateTimeOps.CTime(receiver.Value, context.MemoryGovernor, span)),
                "timestamp" => BoundCallable.CreateNoArguments(dateTime, "datetime.timestamp", static (receiver, span, context) =>
                {
                    var localOffset = TimeSpan.Zero;
                    if (receiver.TzInfo is null)
                    {
                        context.CheckExecution(span);
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
                    static (receiver, span, context) => receiver.TzInfo is null ? PyNone.Instance : receiver.TzInfo.Name),
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
                        _ => throw PyDateTimeOps.InvalidTimezone(ArgAt(arguments, 0), context, span)
                    };

                    context.CheckExecution(span);
                    return PyDateTimeOps.OwnDateTimeValue(PyDateTimeOps.Astimezone(dateTime, targetTimezone, context.Host.LocalNow.Offset, span), context, span);
                }, DateTimeAstimezoneSignature),
                "isoformat" => BoundCallable.CreateWithPresence((bound, span, context) =>
                {
                    if (bound.Values.Length > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.isoformat([sep][, timespec]) expects zero to two arguments.", span);
                    }

                    var separator = GetSeparator(ArgAt(bound.Values, 0), span, context, IsAssigned(bound, 0));
                    var timespec = GetTimespec(ArgAt(bound.Values, 1), 2, span, context, IsAssigned(bound, 1));
                    return dateTime.IsoFormat(separator, timespec, context.MemoryGovernor, span);
                }, DateTimeIsoformatSignature),
                "__format__" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.__format__(format_spec) expects one string argument.", span);
                    }

                    if (!PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", $"__format__() argument 1 must be str, not {IsoformatTypeName(arguments[0], context)}", span);
                    }

                    return PyDateTimeOps.FormatValue(dateTime, format, span, context);
                }, DateTimeFormatSignature),
                "strftime" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "datetime.strftime(format) expects one string argument.", span);
                    }

                    if (!PyStringOps.TryAsString(arguments[0], out var format))
                    {
                        throw new LythonRuntimeException("TypeError", $"strftime() argument 1 must be str, not {IsoformatTypeName(arguments[0], context)}", span);
                    }

                    return PyDateTimeOps.Strftime(dateTime.Value, dateTime.TzInfo, format, span, context);
                }, DateTimeStrftimeSignature),
                "replace" => BoundCallable.CreateWithPresence(
                    (bound, span, context) => ReplaceAsync(dateTime, bound, span, context, false).GetAwaiter().GetResult(),
                    DateTimeReplaceSignature,
                    (bound, span, context) => ReplaceAsync(dateTime, bound, span, context, true)),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static async ValueTask<object> ReplaceAsync(PyDateTime dateTime, BoundCallArguments bound,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var arguments = bound.Values;
            bool IsAssigned(int index) => index < bound.Assigned.Length && bound.Assigned[index];
            int year, month, day, hour, minute, second, microsecond, fold;
            try
            {
                year = await ReplacementIntAsync(arguments, 0, (int)dateTime.Year, span, context, IsAssigned(0), asynchronous).ConfigureAwait(false);
                month = await ReplacementIntAsync(arguments, 1, (int)dateTime.Month, span, context, IsAssigned(1), asynchronous).ConfigureAwait(false);
                day = await ReplacementIntAsync(arguments, 2, (int)dateTime.Day, span, context, IsAssigned(2), asynchronous).ConfigureAwait(false);
                hour = await ReplacementIntAsync(arguments, 3, (int)dateTime.Hour, span, context, IsAssigned(3), asynchronous).ConfigureAwait(false);
                minute = await ReplacementIntAsync(arguments, 4, (int)dateTime.Minute, span, context, IsAssigned(4), asynchronous).ConfigureAwait(false);
                second = await ReplacementIntAsync(arguments, 5, (int)dateTime.Second, span, context, IsAssigned(5), asynchronous).ConfigureAwait(false);
                microsecond = await ReplacementIntAsync(arguments, 6, (int)dateTime.Microsecond, span, context, IsAssigned(6), asynchronous).ConfigureAwait(false);
                fold = await ReplacementIntAsync(arguments, 8, dateTime.Fold, span, context, IsAssigned(8), asynchronous).ConfigureAwait(false);
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
                ReplacementTimezone(arguments, 7, dateTime.TzInfo, span, context, IsAssigned(7)),
                fold), context, span);
        }

    }

    internal static class TimezoneMembers
    {
        // N17: hot fixed signatures hoisted per family (see ListMembers).
        private static readonly LythonCallableSignature TimezoneUtcoffsetSignature = LythonCallableSignature.Create("timezone.utcoffset", ["dt"]);
        private static readonly LythonCallableSignature TimezoneTznameSignature = LythonCallableSignature.Create("timezone.tzname", ["dt"]);
        private static readonly LythonCallableSignature TimezoneDstSignature = LythonCallableSignature.Create("timezone.dst", ["dt"]);
        public static bool TryGetMember(PyTimezone timezone, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "utcoffset" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments[0] is not (PyNone or PyDateTime))
                        throw new LythonRuntimeException("TypeError", "utcoffset() argument must be a datetime instance or None", span);

                    return PyDateTimeOps.OwnDateTimeValue(new PyTimedelta(timezone.Offset), context, span);
                }, TimezoneUtcoffsetSignature),
                "tzname" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments[0] is not (PyNone or PyDateTime))
                        throw new LythonRuntimeException("TypeError", "tzname() argument must be a datetime instance or None", span);

                    return timezone.Name;
                }, TimezoneTznameSignature),
                "dst" => BoundCallable.Create((arguments, span, _) =>
                {
                    if (arguments[0] is not (PyNone or PyDateTime))
                        throw new LythonRuntimeException("TypeError", "dst() argument must be a datetime instance or None", span);

                    return PyNone.Instance;
                }, TimezoneDstSignature),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    private static async ValueTask<int> ToIntAsync(object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        // Replacement fields coerce through __index__ like CPython; remaining
        // rejections name the original type instead of the factory.
        var coerced = await LythonRuntime.CoerceTypeIndexAsync(value, context, span, asynchronous).ConfigureAwait(false);
        if (coerced is bool flag)
        {
            return flag ? 1 : 0;
        }

        if (coerced is BigInteger integer)
        {
            return (int)integer;
        }

        throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(value, context) + "' object cannot be interpreted as an integer", span);
    }

    private static async ValueTask<int> ReplacementIntAsync(
        object[] arguments,
        int index,
        int currentValue,
        LythonSourceSpan span,
        LythonRuntime.ExecutionContext context,
        bool assigned, bool asynchronous)
    {
        var value = ArgAt(arguments, index);
        return !assigned || value is null ? currentValue : await ToIntAsync(value, context, span, asynchronous).ConfigureAwait(false);
    }

    private static PyTimezone? ReplacementTimezone(
        object[] arguments,
        int index,
        PyTimezone? currentValue,
        LythonSourceSpan span,
        LythonRuntime.ExecutionContext context,
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
            _ => throw new LythonRuntimeException("TypeError", $"tzinfo argument must be None or of a tzinfo subclass, not type '{LythonRuntime.UnboundTypeMethod.PythonTypeName(ArgAt(arguments, index), context)}'", span),
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
