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
    // constructions leak nothing). Member and operator results use the same
    // wrapper; transient scalar projections (ordinals, timestamps, counts)
    // stay free.
    private const long DateTimeValueBytes = 64;

    internal static T OwnDateTimeValue<T>(T value, LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
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
        @"^(?<hour>\d{2})(?::(?<minute>\d{2})(?::(?<second>\d{2})(?:[.,](?<fraction>\d+))?)?)?$",
        RegexOptions.CultureInvariant);

    internal sealed class TypeMemberCallable : LythonRuntime.ICallable, IPyDynamicAttributes, IPyContextualDynamicAttributes
    {
        private readonly Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> _implementation;
        private readonly LythonCallableSignature _signature;

        // Type members expose CPython-style identity like C-implemented
        // methods: the short __name__, the module-less __qualname__, a None
        // __module__, and the defining type as __self__ resolved through
        // the run import registry.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString(ShortMemberName(_signature.Name));
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString(QualMemberName(_signature.Name));
                return true;
            }

            if (name is "__module__")
            {
                value = PyNone.Instance;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public bool TryGetMember(string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__self__" &&
                TryResolveOwnerType(context, out var owner) &&
                owner is not null)
            {
                value = owner;
                return true;
            }

            return TryGetMember(name, out value);
        }

        private static string ShortMemberName(string name)
        {
            var dot = name.LastIndexOf((char)46);
            return dot < 0 ? name : name.Substring(dot + 1);
        }

        private static string QualMemberName(string name)
        {
            var dot = name.IndexOf((char)46);
            return dot < 0 ? name : name.Substring(dot + 1);
        }

        private bool TryResolveOwnerType(LythonRuntime.ExecutionContext context, [MaybeNullWhen(false)] out object value)
        {
            value = PyNone.Instance;
            var dot = _signature.Name.IndexOf((char)46);
            if (dot < 0)
            {
                return false;
            }

            var moduleName = _signature.Name.Substring(0, dot);
            var rest = _signature.Name.Substring(dot + 1);
            var typeDot = rest.IndexOf((char)46);
            var typeName = typeDot < 0 ? rest : rest.Substring(0, typeDot);
            if (!context.State.ImportedModules.TryGetValue(moduleName, out var module) ||
                !module.TryGetCachedMember(typeName, out value) ||
                value is null)
            {
                value = PyNone.Instance;
                return false;
            }

            return true;
        }

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
            "min" => PyTimezone.Min,
            "max" => PyTimezone.Max,
            _ => null
        });

    // isocalendar results share one opaque type object like the other
    // runtime types; construction stays unsupported, matching the opaque
    // family, since guests only ever receive these values.
    public static readonly PyBuiltinRuntimeType IsoCalendarDateType = new(
        "datetime.IsoCalendarDate",
        static (arguments, span, context) => throw new LythonRuntimeException("TypeError", "Runtime type objects cannot be constructed directly in Lython.", span));

    public static object CreateTimedelta(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var boundArguments = CallBinder.BindNamedArgumentsWithPresence(arguments, span, TimedeltaCallSignature, PythonCallableKind.Builtin);
        var bound = boundArguments.Values;
        bool IsAssigned(int index) => index < boundArguments.Assigned.Length && boundArguments.Assigned[index];

        // CPython accumulates every component exactly in microseconds, splits
        // whole units only at the end, and rounds a leftover fraction half to
        // even; the day count passes through a C-int conversion before the
        // magnitude check, so huge totals fail as OverflowError instead of
        // leaking host arithmetic failures.
        var totalMicroseconds = BigInteger.Zero;
        var leftover = 0.0;
        totalMicroseconds = AccumulateTimedeltaComponent("microseconds", ArgAt(bound, 2), IsAssigned(2), BigInteger.One, totalMicroseconds, ref leftover, span, context);
        totalMicroseconds = AccumulateTimedeltaComponent("milliseconds", ArgAt(bound, 3), IsAssigned(3), new BigInteger(1_000), totalMicroseconds, ref leftover, span, context);
        totalMicroseconds = AccumulateTimedeltaComponent("seconds", ArgAt(bound, 1), IsAssigned(1), new BigInteger(1_000_000), totalMicroseconds, ref leftover, span, context);
        totalMicroseconds = AccumulateTimedeltaComponent("minutes", ArgAt(bound, 4), IsAssigned(4), new BigInteger(60_000_000), totalMicroseconds, ref leftover, span, context);
        totalMicroseconds = AccumulateTimedeltaComponent("hours", ArgAt(bound, 5), IsAssigned(5), new BigInteger(3_600_000_000L), totalMicroseconds, ref leftover, span, context);
        totalMicroseconds = AccumulateTimedeltaComponent("days", ArgAt(bound, 0), IsAssigned(0), new BigInteger(86_400_000_000L), totalMicroseconds, ref leftover, span, context);
        totalMicroseconds = AccumulateTimedeltaComponent("weeks", ArgAt(bound, 6), IsAssigned(6), new BigInteger(604_800_000_000L), totalMicroseconds, ref leftover, span, context);

        if (leftover != 0.0)
        {
            var whole = Math.Round(leftover, MidpointRounding.ToEven);
            if (Math.Abs(whole - leftover) == 0.5)
            {
                var odd = !totalMicroseconds.IsEven;
                whole = 2.0 * Math.Round((leftover + (odd ? 1.0 : 0.0)) * 0.5, MidpointRounding.ToEven) - (odd ? 1.0 : 0.0);
            }

            totalMicroseconds += (BigInteger)whole;
        }

        return OwnDateTimeValue(CreateTimedelta(totalMicroseconds, span), context, span);
    }

    private static BigInteger FloorDivRem(BigInteger value, BigInteger divisor, out BigInteger remainder)
    {
        var quotient = BigInteger.DivRem(value, divisor, out remainder);
        if (remainder.Sign < 0)
        {
            quotient -= BigInteger.One;
            remainder += divisor;
        }

        return quotient;
    }

    public static object CreateDate(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var boundArguments = CallBinder.BindNamedArgumentsWithPresence(arguments, span, DateCallSignature, PythonCallableKind.Builtin);
        var bound = boundArguments.Values;
        bool IsAssigned(int index) => index < boundArguments.Assigned.Length && boundArguments.Assigned[index];

        // CPython converts every component before validating ranges, so an
        // oversized integer fails as OverflowError even beside bad ranges.
        int year, month, day;
        try
        {
            year = GetInteger(ArgAt(bound, 0), IsAssigned(0), span, context);
            month = GetInteger(ArgAt(bound, 1), IsAssigned(1), span, context);
            day = GetInteger(ArgAt(bound, 2), IsAssigned(2), span, context);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C long", span, ex);
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

        return OwnDateTimeValue(new PyDate(new DateOnly(year, month, day)), context, span);
    }

    public static object CreateTime(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var boundArguments = CallBinder.BindNamedArgumentsWithPresence(arguments, span, TimeCallSignature, PythonCallableKind.Builtin);
        var bound = boundArguments.Values;
        bool IsAssigned(int index) => index < boundArguments.Assigned.Length && boundArguments.Assigned[index];

        int hour, minute, second, microsecond, fold;
        try
        {
            hour = GetInteger(ArgAt(bound, 0), IsAssigned(0), span, context);
            minute = GetInteger(ArgAt(bound, 1), IsAssigned(1), span, context);
            second = GetInteger(ArgAt(bound, 2), IsAssigned(2), span, context);
            microsecond = GetInteger(ArgAt(bound, 3), IsAssigned(3), span, context);
            fold = GetInteger(ArgAt(bound, 5), IsAssigned(5), span, context);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C long", span, ex);
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

        return OwnDateTimeValue(new PyTime(
            new TimeOnly(hour, minute, second, microsecond / 1000, microsecond % 1000),
            GetTimezone(ArgAt(bound, 4), span, context),
            fold), context, span);
    }

    public static object CreateDateTime(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var boundArguments = CallBinder.BindNamedArgumentsWithPresence(arguments, span, DateTimeCallSignature, PythonCallableKind.Builtin);
        var bound = boundArguments.Values;
        bool IsAssigned(int index) => index < boundArguments.Assigned.Length && boundArguments.Assigned[index];

        int year, month, day, hour, minute, second, microsecond, fold;
        try
        {
            year = GetInteger(ArgAt(bound, 0), IsAssigned(0), span, context);
            month = GetInteger(ArgAt(bound, 1), IsAssigned(1), span, context);
            day = GetInteger(ArgAt(bound, 2), IsAssigned(2), span, context);
            hour = GetInteger(ArgAt(bound, 3), IsAssigned(3), span, context);
            minute = GetInteger(ArgAt(bound, 4), IsAssigned(4), span, context);
            second = GetInteger(ArgAt(bound, 5), IsAssigned(5), span, context);
            microsecond = GetInteger(ArgAt(bound, 6), IsAssigned(6), span, context);
            fold = GetInteger(ArgAt(bound, 8), IsAssigned(8), span, context);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C long", span, ex);
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

        return OwnDateTimeValue(new PyDateTime(
            new DateTime(year, month, day, hour, minute, second, microsecond / 1000, DateTimeKind.Unspecified).AddTicks((microsecond % 1000) * 10L),
            GetTimezone(ArgAt(bound, 7), span, context),
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
            throw new LythonRuntimeException("TypeError", $"timezone() argument 1 must be datetime.timedelta, not {NoneOrTypeName(ArgAt(bound, 0), context)}", span);
        }

        ValidateTimezoneOffset(delta.TotalMicroseconds, span);

        var name = ArgAt(bound, 1) switch
        {
            null or PyNone => null,
            _ when PyStringOps.TryAsString(ArgAt(bound, 1).RequireNotNull(), out var text) => text.AsString(),
            _ => throw new LythonRuntimeException("TypeError", $"timezone() argument 2 must be str, not {NoneOrTypeName(ArgAt(bound, 1), context)}", span)
        };

        return delta.TotalMicroseconds.IsZero && name is null
            ? PyTimezone.Utc
            : OwnDateTimeValue(new PyTimezone(delta.Value, name), context, span);
    }

    internal static LythonRuntimeException InvalidTimezone(object? value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => new("TypeError", $"tzinfo argument must be None or of a tzinfo subclass, not type '{LythonRuntime.UnboundTypeMethod.PythonTypeName(value, context)}'", span);
    private static string NoneOrTypeName(object? value, LythonRuntime.ExecutionContext context)
        => value is null or PyNone ? "None" : LythonRuntime.UnboundTypeMethod.PythonTypeName(value, context);

    public static object DateFromIsoFormat(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
        {
            throw new LythonRuntimeException("TypeError", "fromisoformat: argument must be str", span);
        }

        try
        {
            return OwnDateTimeValue(new PyDate(ParseIsoDate(text.AsString(), span)), context, span);
        }
        catch (FormatException)
        {
            throw new LythonRuntimeException("ValueError", $"Invalid isoformat string: '{text.AsString()}'", span);
        }
    }

    public static object DateFromOrdinal(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "datetime.date.fromordinal(ordinal) expects one integer argument.", span);
        }

        return OwnDateTimeValue(new PyDate(DateFromOrdinalValue(arguments[0], span, context)), context, span);
    }

    public static object DateFromIsoCalendar(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length != 3)
        {
            throw new LythonRuntimeException("TypeError", "datetime.date.fromisocalendar(year, week, day) expects three integer arguments.", span);
        }

        return new PyDate(DateFromIsoCalendarValue(arguments[0], arguments[1], arguments[2], span, context));
    }

    public static object DateFromTimestamp(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "datetime.date.fromtimestamp(timestamp) expects one argument.", span);
        }

        context.RegisterHostCall(span);
        var instant = DateTimeOffsetFromTimestamp(TimestampToFlooredSeconds(CoerceTimestampNumber(arguments[0], span, context)) * 1_000_000, span);
        return OwnDateTimeValue(new PyDate(DateOnly.FromDateTime(instant.ToOffset(context.Host.LocalNow.Offset).DateTime)), context, span);
    }

    public static object TimeFromIsoFormat(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
        {
            throw new LythonRuntimeException("TypeError", "fromisoformat: argument must be str", span);
        }

        try
        {
            return ParseTime(text.AsString(), span);
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
            throw new LythonRuntimeException("TypeError", "fromisoformat: argument must be str", span);
        }

        try
        {
            return ParseDateTime(text.AsString(), span);
        }
        catch (FormatException)
        {
            throw new LythonRuntimeException("ValueError", $"Invalid isoformat string: '{text.AsString()}'", span);
        }
    }

    public static object DateTimeFromOrdinal(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.fromordinal(ordinal) expects one integer argument.", span);
        }

        return OwnDateTimeValue(new PyDateTime(DateFromOrdinalValue(arguments[0], span, context).ToDateTime(TimeOnly.MinValue)), context, span);
    }

    public static object DateTimeFromIsoCalendar(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length != 3)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.fromisocalendar(year, week, day) expects three integer arguments.", span);
        }

        return OwnDateTimeValue(new PyDateTime(DateFromIsoCalendarValue(arguments[0], arguments[1], arguments[2], span, context).ToDateTime(TimeOnly.MinValue)), context, span);
    }

    public static object DateTimeFromTimestamp(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.fromtimestamp(timestamp[, tz]) expects one or two arguments.", span);
        }

        var instant = DateTimeOffsetFromTimestamp(TimestampToMicroseconds(CoerceTimestampNumber(arguments[0], span, context)), span);
        if (arguments.Length == 1 || arguments[1] is null or PyNone)
        {
            context.RegisterHostCall(span);
            return OwnDateTimeValue(new PyDateTime(DateTime.SpecifyKind(instant.ToOffset(context.Host.LocalNow.Offset).DateTime, DateTimeKind.Unspecified)), context, span);
        }

        if (arguments[1] is not PyTimezone tz)
        {
            throw InvalidTimezone(arguments[1], context, span);
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

        return OwnDateTimeValue(new PyDateTime(DateTime.SpecifyKind(DateTimeOffsetFromTimestamp(TimestampToMicroseconds(CoerceTimestampNumber(arguments[0], span, context)), span).UtcDateTime, DateTimeKind.Unspecified)), context, span);
    }

    public static object DateTimeCombine(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length is < 2 or > 3)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.combine(date, time[, tzinfo]) expects two or three arguments.", span);
        }

        var date = arguments[0] switch
        {
            PyDate value => value.Value,
            PyDateTime value => DateOnly.FromDateTime(value.Value),
            _ => throw new LythonRuntimeException("TypeError", "combine() argument 1 must be datetime.date, not " + NoneOrTypeName(arguments[0], context), span)
        };

        if (arguments[1] is not PyTime time)
        {
            throw new LythonRuntimeException("TypeError", "combine() argument 2 must be datetime.time, not " + NoneOrTypeName(arguments[1], context), span);
        }

        var timezone = arguments.Length == 2
            ? time.TzInfo
            : arguments[2] switch
            {
                null or PyNone => null,
                PyTimezone tz => tz,
                _ => throw InvalidTimezone(arguments[2], context, span)
            };

        return OwnDateTimeValue(new PyDateTime(date.ToDateTime(time.Value), timezone, time.Fold), context, span);
    }

    public static object DateTimeStrptime(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.strptime(date_string, format) expects two string arguments.", span);
        }

        if (!PyStringOps.TryAsString(arguments[0], out var text))
        {
            throw new LythonRuntimeException("TypeError", $"strptime() argument 1 must be str, not {NoneOrTypeName(arguments[0], context)}", span);
        }

        if (!PyStringOps.TryAsString(arguments[1], out var format))
        {
            throw new LythonRuntimeException("TypeError", $"strptime() argument 2 must be str, not {NoneOrTypeName(arguments[1], context)}", span);
        }

        try
        {
            return ParseStrptime(text.AsString(), format.AsString(), span, context);
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

        if (arguments.Length == 0 || arguments[0] is null or PyNone)
        {
            context.RegisterHostCall(span);
            var localNow = context.Host.LocalNow;
            return OwnDateTimeValue(new PyDateTime(DateTime.SpecifyKind(localNow.DateTime, DateTimeKind.Unspecified)), context, span);
        }

        if (arguments[0] is not PyTimezone tz)
        {
            throw InvalidTimezone(arguments[0], context, span);
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

    public static PyIsoCalendarDate IsoCalendar(DateOnly date, LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
    {
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        return OwnDateTimeValue(
            new PyIsoCalendarDate(
                ISOWeek.GetYear(dateTime),
                ISOWeek.GetWeekOfYear(dateTime),
                ((int)date.DayOfWeek + 6) % 7 + 1,
                context.MemoryGovernor,
                span),
            context,
            span);
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

    public static LythonRuntime.TimeStructTimeValue TimeTuple(DateOnly date, LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
        => LythonRuntime.TimeStructTimeValue.FromDateTime(date.ToDateTime(TimeOnly.MinValue), -1, PyNone.Instance, PyNone.Instance, context, span);

    public static LythonRuntime.TimeStructTimeValue TimeTuple(DateTime dateTime, int isDst, LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
        => LythonRuntime.TimeStructTimeValue.FromDateTime(dateTime, isDst, PyNone.Instance, PyNone.Instance, context, span);

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

    public static object Add(object left, object right, LythonRuntime.ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        return (left, right) switch
        {
            (PyTimedelta lhs, PyTimedelta rhs) => OwnDateTimeValue(CreateTimedelta(lhs.TotalMicroseconds + rhs.TotalMicroseconds, span), context, span),
            (PyDate date, PyTimedelta delta) => OwnDateTimeValue(AddDaysToDate(date, delta.Days, span), context, span),
            (PyTimedelta delta, PyDate date) => OwnDateTimeValue(AddDaysToDate(date, delta.Days, span), context, span),
            (PyDateTime dateTime, PyTimedelta delta) => OwnDateTimeValue(AddDeltaToDateTime(dateTime, delta.TotalMicroseconds, span), context, span),
            (PyTimedelta delta, PyDateTime dateTime) => OwnDateTimeValue(AddDeltaToDateTime(dateTime, delta.TotalMicroseconds, span), context, span),
            _ => throw RuntimeErrors.UnsupportedOperands(operation ?? "+", left, right, span)
        };
    }

    public static object Subtract(object left, object right, LythonRuntime.ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        return (left, right) switch
        {
            (PyTimedelta lhs, PyTimedelta rhs) => OwnDateTimeValue(CreateTimedelta(lhs.TotalMicroseconds - rhs.TotalMicroseconds, span), context, span),
            (PyDate lhs, PyTimedelta rhs) => OwnDateTimeValue(AddDaysToDate(lhs, -rhs.Days, span), context, span),
            (PyDate lhs, PyDate rhs) => OwnDateTimeValue(new PyTimedelta(TimeSpan.FromDays(lhs.Value.DayNumber - rhs.Value.DayNumber)), context, span),
            (PyDateTime lhs, PyTimedelta rhs) => OwnDateTimeValue(AddDeltaToDateTime(lhs, -rhs.TotalMicroseconds, span), context, span),
            (PyDateTime lhs, PyDateTime rhs) => OwnDateTimeValue(SubtractDateTimes(lhs, rhs, span), context, span),
            _ => throw RuntimeErrors.UnsupportedOperands(operation ?? "-", left, right, span)
        };
    }

    public static object Negate(object operand, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        return operand switch
        {
            PyTimedelta delta => OwnDateTimeValue(CreateTimedelta(-delta.TotalMicroseconds, span), context, span),
            _ => throw RuntimeErrors.BadUnaryOperand("-", operand, span)
        };
    }

    public static object Abs(PyTimedelta delta, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        return delta.TotalMicroseconds.Sign < 0
            ? OwnDateTimeValue(CreateTimedelta(-delta.TotalMicroseconds, span), context, span)
            : delta;
    }

    public static object Multiply(object left, object right, LythonRuntime.ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        return (left, right) switch
        {
            (PyTimedelta delta, _) when Numbers.PyNumberOps.TryAsInteger(right, out var factor) => OwnDateTimeValue(CreateTimedelta(delta.TotalMicroseconds * factor, span), context, span),
            (PyTimedelta delta, _) when Numbers.PyNumberOps.TryAsNumber(right, out var number) && number.IsFloat => OwnDateTimeValue(ScaleFloat(delta, number.Floating, divide: false, span), context, span),
            (_, PyTimedelta delta) when Numbers.PyNumberOps.TryAsInteger(left, out var leftFactor) => OwnDateTimeValue(CreateTimedelta(delta.TotalMicroseconds * leftFactor, span), context, span),
            (_, PyTimedelta delta) when Numbers.PyNumberOps.TryAsNumber(left, out var leftNumber) && leftNumber.IsFloat => OwnDateTimeValue(ScaleFloat(delta, leftNumber.Floating, divide: false, span), context, span),
            _ => throw RuntimeErrors.UnsupportedOperands(operation ?? "*", left, right, span)
        };
    }

    public static object Divide(object left, object right, LythonRuntime.ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        return (left, right) switch
        {
            (PyTimedelta delta, PyTimedelta other) => DivideTimedeltas(delta, other, span),
            (PyTimedelta delta, _) when Numbers.PyNumberOps.TryAsInteger(right, out var divisor) => OwnDateTimeValue(CreateTimedelta(DivideNearest(delta.TotalMicroseconds, divisor, span), span), context, span),
            (PyTimedelta delta, _) when Numbers.PyNumberOps.TryAsNumber(right, out var number) && number.IsFloat => OwnDateTimeValue(ScaleFloat(delta, number.Floating, divide: true, span), context, span),
            _ => throw RuntimeErrors.UnsupportedOperands(operation ?? "/", left, right, span)
        };
    }

    public static object FloorDivide(object left, object right, LythonRuntime.ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        return (left, right) switch
        {
            (PyTimedelta delta, PyTimedelta other) => FloorDivideMicroseconds(delta.TotalMicroseconds, other.TotalMicroseconds, span),
            (PyTimedelta delta, _) when Numbers.PyNumberOps.TryAsInteger(right, out var divisor) => OwnDateTimeValue(CreateTimedelta(FloorDivideMicroseconds(delta.TotalMicroseconds, divisor, span), span), context, span),
            _ => throw RuntimeErrors.UnsupportedOperands(operation ?? "//", left, right, span)
        };
    }

    public static object Modulo(object left, object right, LythonRuntime.ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        if (left is PyTimedelta delta && right is PyTimedelta other)
        {
            return OwnDateTimeValue(TimedeltaModulo(delta, other, span), context, span);
        }

        throw RuntimeErrors.UnsupportedOperands(operation ?? "%", left, right, span);
    }

    public static PyTuple DivMod(PyTimedelta left, PyTimedelta right, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var quotient = FloorDivide(left, right, context, span);
        var remainder = TimedeltaModulo(left, right, span);
        return PyTuple.FromOwnedArray([quotient, remainder], context.MemoryGovernor, span);
    }

    private static LythonRuntimeException CompareFailed(string? operation, object left, object right, LythonSourceSpan span)
        => operation is null
            ? new LythonRuntimeException("TypeError", "Values are not comparable.", span)
            : RuntimeErrors.UnsupportedComparison(operation, left, right, span);

    public static int Compare(object left, object right, LythonSourceSpan span, string? operation = null)
    {
        return (left, right) switch
        {
            (PyTimedelta lhs, PyTimedelta rhs) => lhs.TotalMicroseconds.CompareTo(rhs.TotalMicroseconds),
            (PyDate lhs, PyDate rhs) => lhs.Value.CompareTo(rhs.Value),
            (PyTime lhs, PyTime rhs) => CompareTimes(lhs, rhs, span),
            (PyDateTime lhs, PyDateTime rhs) => CompareDateTimes(lhs, rhs, span),
            _ => throw CompareFailed(operation, left, right, span)
        };
    }

}
