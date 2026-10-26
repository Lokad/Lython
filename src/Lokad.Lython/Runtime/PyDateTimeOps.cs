using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDateTimeOps
{
    private static readonly LythonCallableSignature TimedeltaCallSignature = LythonCallableSignature.Create(
        "datetime.timedelta",
        ["days", "seconds", "microseconds", "milliseconds", "minutes", "hours", "weeks"],
        requiredCount: 0);
    private static readonly LythonCallableSignature DateCallSignature = LythonCallableSignature.Create("datetime.date", ["year", "month", "day"]);
    private static readonly LythonCallableSignature TimeCallSignature = LythonCallableSignature.Create(
        "datetime.time",
        ["hour", "minute", "second", "microsecond", "tzinfo", "fold"],
        requiredCount: 0, maximumPositionalArgumentCount: 5);
    private static readonly LythonCallableSignature DateTimeCallSignature = LythonCallableSignature.Create(
        "datetime.datetime",
        ["year", "month", "day", "hour", "minute", "second", "microsecond", "tzinfo", "fold"],
        requiredCount: 3, maximumPositionalArgumentCount: 8);
    private static readonly LythonCallableSignature TimezoneCallSignature = LythonCallableSignature.Create(
        "datetime.timezone",
        ["offset", "name"],
        requiredCount: 1);

    private static readonly DateTimeOffset UnixEpoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Native wrappers have fixed-size payloads; variable names alias governed
    // strings and variable text uses governed builders. Charge one
    // table slot per value once built (the expression evaluates first, so failed
    // constructions leak nothing). Member and operator results use the same
    // wrapper; transient scalar projections (ordinals, timestamps, counts)
    // stay free.
    private const long DateTimeValueBytes = 64;

    internal static T OwnDateTimeValue<T>(T value, LythonRuntime.ExecutionContext context, LythonSourceSpan? span) where T : class
    {
        context.MemoryGovernor.Reserve(DateTimeValueBytes, span);
        context.MemoryGovernor.Commit(DateTimeValueBytes);
        return value;
    }

    internal sealed class TypeMemberCallable : LythonRuntime.ICallable, IPyDynamicAttributes
    {
        private readonly Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> _implementation;
        private readonly LythonCallableSignature _signature;
        private readonly Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, ValueTask<object>>? _asyncImplementation;
        private readonly PyBuiltinRuntimeType _owner;

        // Type members expose CPython-style identity like C-implemented
        // methods: the short __name__, the module-less __qualname__, a None
        // __module__, and the actual defining type as __self__.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__self__")
            {
                value = _owner;
                return true;
            }
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

        public TypeMemberCallable(PyBuiltinRuntimeType owner, string name, Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> implementation)
            : this(owner, implementation, LythonCallableSignature.Create(name))
        {
        }

        public TypeMemberCallable(PyBuiltinRuntimeType owner, string name, Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> implementation, string[] parameterNames, Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, ValueTask<object>>? asyncImplementation = null)
            : this(owner, implementation, LythonCallableSignature.Create(name, parameterNames), asyncImplementation)
        {
        }

        public TypeMemberCallable(PyBuiltinRuntimeType owner, string name, Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> implementation, string[] parameterNames, int requiredCount, Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, ValueTask<object>>? asyncImplementation = null)
            : this(owner, implementation, LythonCallableSignature.Create(name, parameterNames, requiredCount), asyncImplementation)
        {
        }

        private TypeMemberCallable(
            PyBuiltinRuntimeType owner,
            Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> implementation,
            LythonCallableSignature signature,
            Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, ValueTask<object>>? asyncImplementation = null)
        {
            _owner = owner;
            _implementation = implementation;
            _signature = signature;
            _asyncImplementation = asyncImplementation;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var positional = CallBinder.BindNamedArguments(arguments, span, _signature, PythonCallableKind.Builtin);
            return _implementation(positional, span, context);
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            if (_asyncImplementation is null) return new(Invoke(arguments, span, context));
            var positional = CallBinder.BindNamedArguments(arguments, span, _signature, PythonCallableKind.Builtin);
            return _asyncImplementation(positional, span, context);
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
            "min" => PyDate.Min,
            "max" => PyDate.Max,
            "resolution" => PyDate.Resolution,
            "fromordinal" => new TypeMemberCallable(DateType!, "datetime.date.fromordinal", DateFromOrdinal, ["ordinal"], asyncImplementation: DateFromOrdinalAsync),
            "fromisoformat" => new TypeMemberCallable(DateType!, "datetime.date.fromisoformat", DateFromIsoFormat, ["date_string"]),
            "fromisocalendar" => new TypeMemberCallable(DateType!, "datetime.date.fromisocalendar", DateFromIsoCalendar, ["year", "week", "day"], asyncImplementation: DateFromIsoCalendarAsync),
            "fromtimestamp" => new TypeMemberCallable(DateType!, "datetime.date.fromtimestamp", DateFromTimestamp, ["timestamp"], asyncImplementation: DateFromTimestampAsync),
            "today" => new TypeMemberCallable(DateType!, "datetime.date.today", DateToday),
            _ => null
        }, CreateDateAsync);

    public static readonly PyBuiltinRuntimeType TimeType = new(
        "datetime.time",
        CreateTime,
        memberName => memberName switch
        {
            "min" => PyTime.Min,
            "max" => PyTime.Max,
            "resolution" => PyTime.Resolution,
            "fromisoformat" => new TypeMemberCallable(TimeType!, "datetime.time.fromisoformat", TimeFromIsoFormat, ["time_string"]),
            _ => null
        }, CreateTimeAsync);

    public static readonly PyBuiltinRuntimeType DateTimeType = new(
        "datetime.datetime",
        CreateDateTime,
        memberName => memberName switch
        {
            "min" => PyDateTime.Min,
            "max" => PyDateTime.Max,
            "resolution" => PyDateTime.Resolution,
            "combine" => new TypeMemberCallable(DateTimeType!, "datetime.datetime.combine", DateTimeCombine, ["date", "time", "tzinfo"], 2),
            "fromordinal" => new TypeMemberCallable(DateTimeType!, "datetime.datetime.fromordinal", DateTimeFromOrdinal, ["ordinal"], asyncImplementation: DateTimeFromOrdinalAsync),
            "fromisoformat" => new TypeMemberCallable(DateTimeType!, "datetime.datetime.fromisoformat", DateTimeFromIsoFormat, ["date_string"]),
            "fromisocalendar" => new TypeMemberCallable(DateTimeType!, "datetime.datetime.fromisocalendar", DateTimeFromIsoCalendar, ["year", "week", "day"], asyncImplementation: DateTimeFromIsoCalendarAsync),
            "fromtimestamp" => new TypeMemberCallable(DateTimeType!, "datetime.datetime.fromtimestamp", DateTimeFromTimestamp, ["timestamp", "tz"], 1, asyncImplementation: DateTimeFromTimestampAsync),
            "strptime" => new TypeMemberCallable(DateTimeType!, "datetime.datetime.strptime", DateTimeStrptime, ["date_string", "format"]),
            "now" => new TypeMemberCallable(DateTimeType!, "datetime.datetime.now", DateTimeNow, ["tz"], 0),
            "utcfromtimestamp" => new TypeMemberCallable(DateTimeType!, "datetime.datetime.utcfromtimestamp", DateTimeUtcFromTimestamp, ["timestamp"], asyncImplementation: DateTimeUtcFromTimestampAsync),
            "utcnow" => new TypeMemberCallable(DateTimeType!, "datetime.datetime.utcnow", DateTimeUtcNow),
            _ => null
        }, CreateDateTimeAsync);

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
        => CreateDateCore(arguments, span, context, false).GetAwaiter().GetResult();

    public static ValueTask<object> CreateDateAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => CreateDateCore(arguments, span, context, true);

    private static async ValueTask<object> CreateDateCore(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        var boundArguments = CallBinder.BindNamedArgumentsWithPresence(arguments, span, DateCallSignature, PythonCallableKind.Builtin);
        var bound = boundArguments.Values;
        bool IsAssigned(int index) => index < boundArguments.Assigned.Length && boundArguments.Assigned[index];

        // CPython converts every component before validating ranges, so an
        // oversized integer fails as OverflowError even beside bad ranges.
        int year, month, day;
        try
        {
            year = await GetIntegerAsync(ArgAt(bound, 0), IsAssigned(0), span, context, asynchronous).ConfigureAwait(false);
            month = await GetIntegerAsync(ArgAt(bound, 1), IsAssigned(1), span, context, asynchronous).ConfigureAwait(false);
            day = await GetIntegerAsync(ArgAt(bound, 2), IsAssigned(2), span, context, asynchronous).ConfigureAwait(false);
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
        => CreateTimeCore(arguments, span, context, false).GetAwaiter().GetResult();

    public static ValueTask<object> CreateTimeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => CreateTimeCore(arguments, span, context, true);

    private static async ValueTask<object> CreateTimeCore(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        var boundArguments = CallBinder.BindNamedArgumentsWithPresence(arguments, span, TimeCallSignature, PythonCallableKind.Builtin);
        var bound = boundArguments.Values;
        bool IsAssigned(int index) => index < boundArguments.Assigned.Length && boundArguments.Assigned[index];

        int hour, minute, second, microsecond, fold;
        try
        {
            hour = await GetIntegerAsync(ArgAt(bound, 0), IsAssigned(0), span, context, asynchronous).ConfigureAwait(false);
            minute = await GetIntegerAsync(ArgAt(bound, 1), IsAssigned(1), span, context, asynchronous).ConfigureAwait(false);
            second = await GetIntegerAsync(ArgAt(bound, 2), IsAssigned(2), span, context, asynchronous).ConfigureAwait(false);
            microsecond = await GetIntegerAsync(ArgAt(bound, 3), IsAssigned(3), span, context, asynchronous).ConfigureAwait(false);
            fold = await GetIntegerAsync(ArgAt(bound, 5), IsAssigned(5), span, context, asynchronous).ConfigureAwait(false);
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
        => CreateDateTimeCore(arguments, span, context, false).GetAwaiter().GetResult();

    public static ValueTask<object> CreateDateTimeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => CreateDateTimeCore(arguments, span, context, true);

    private static async ValueTask<object> CreateDateTimeCore(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        var boundArguments = CallBinder.BindNamedArgumentsWithPresence(arguments, span, DateTimeCallSignature, PythonCallableKind.Builtin);
        var bound = boundArguments.Values;
        bool IsAssigned(int index) => index < boundArguments.Assigned.Length && boundArguments.Assigned[index];

        int year, month, day, hour, minute, second, microsecond, fold;
        try
        {
            year = await GetIntegerAsync(ArgAt(bound, 0), IsAssigned(0), span, context, asynchronous).ConfigureAwait(false);
            month = await GetIntegerAsync(ArgAt(bound, 1), IsAssigned(1), span, context, asynchronous).ConfigureAwait(false);
            day = await GetIntegerAsync(ArgAt(bound, 2), IsAssigned(2), span, context, asynchronous).ConfigureAwait(false);
            hour = await GetIntegerAsync(ArgAt(bound, 3), IsAssigned(3), span, context, asynchronous).ConfigureAwait(false);
            minute = await GetIntegerAsync(ArgAt(bound, 4), IsAssigned(4), span, context, asynchronous).ConfigureAwait(false);
            second = await GetIntegerAsync(ArgAt(bound, 5), IsAssigned(5), span, context, asynchronous).ConfigureAwait(false);
            microsecond = await GetIntegerAsync(ArgAt(bound, 6), IsAssigned(6), span, context, asynchronous).ConfigureAwait(false);
            fold = await GetIntegerAsync(ArgAt(bound, 8), IsAssigned(8), span, context, asynchronous).ConfigureAwait(false);
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
        var boundArguments = CallBinder.BindNamedArgumentsWithPresence(arguments, span, TimezoneCallSignature, PythonCallableKind.Builtin);
        var bound = boundArguments.Values;

        if (ArgAt(bound, 0) is not PyTimedelta delta)
        {
            throw new LythonRuntimeException("TypeError", $"timezone() argument 1 must be datetime.timedelta, not {NoneOrTypeName(ArgAt(bound, 0), context)}", span);
        }

        ValidateTimezoneOffset(delta.TotalMicroseconds, span);

        var name = ArgAt(bound, 1) switch
        {
            _ when !boundArguments.Assigned[1] => null,
            _ when PyStringOps.TryAsString(ArgAt(bound, 1).RequireNotNull(), out var text) => text,
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
            context.CheckExecution(span);
            var cursor = 0;
            var value = ParseIsoDate(text.Utf8Bytes.Span, ref cursor, span);
            if (cursor != text.Utf8Bytes.Length) throw new FormatException("Invalid ISO date string.");
            return OwnDateTimeValue(new PyDate(value), context, span);
        }
        catch (FormatException)
        {
            throw InvalidIsoFormat(text, span, context);
        }
    }

    public static object DateFromOrdinal(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => DateFromOrdinalCore(arguments, span, context, false).GetAwaiter().GetResult();

    public static ValueTask<object> DateFromOrdinalAsync(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => DateFromOrdinalCore(arguments, span, context, true);

    private static async ValueTask<object> DateFromOrdinalCore(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "datetime.date.fromordinal(ordinal) expects one integer argument.", span);
        }

        var indexed = (await LythonRuntime.CoerceTypeIndexAsync(arguments[0], context, span, asynchronous).ConfigureAwait(false))!;
        return OwnDateTimeValue(new PyDate(DateFromOrdinalValue(indexed, span, context)), context, span);
    }

    public static object DateFromIsoCalendar(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => DateFromIsoCalendarCore(arguments, span, context, false).GetAwaiter().GetResult();

    public static ValueTask<object> DateFromIsoCalendarAsync(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => DateFromIsoCalendarCore(arguments, span, context, true);

    private static async ValueTask<object> DateFromIsoCalendarCore(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        if (arguments.Length != 3)
        {
            throw new LythonRuntimeException("TypeError", "datetime.date.fromisocalendar(year, week, day) expects three integer arguments.", span);
        }

        return OwnDateTimeValue(new PyDate((await DateFromIsoCalendarValueAsync(arguments, span, context, asynchronous).ConfigureAwait(false))), context, span);
    }

    public static object DateFromTimestamp(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => DateFromTimestampCore(arguments, span, context, false).GetAwaiter().GetResult();

    public static ValueTask<object> DateFromTimestampAsync(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => DateFromTimestampCore(arguments, span, context, true);

    private static async ValueTask<object> DateFromTimestampCore(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "datetime.date.fromtimestamp(timestamp) expects one argument.", span);
        }

        context.CheckExecution(span);
        var indexed = (await LythonRuntime.CoerceTypeIndexAsync(arguments[0], context, span, asynchronous).ConfigureAwait(false))!;
        var instant = DateTimeOffsetFromTimestamp(TimestampToFlooredSeconds(CoerceTimestampNumber(indexed, span, context)) * 1_000_000, span);
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
            return OwnDateTimeValue(ParseTime(text, span, context), context, span);
        }
        catch (FormatException)
        {
            throw InvalidIsoFormat(text, span, context);
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
            return OwnDateTimeValue(ParseDateTime(text, span, context), context, span);
        }
        catch (FormatException)
        {
            throw InvalidIsoFormat(text, span, context);
        }
    }

    public static object DateTimeFromOrdinal(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => DateTimeFromOrdinalCore(arguments, span, context, false).GetAwaiter().GetResult();

    public static ValueTask<object> DateTimeFromOrdinalAsync(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => DateTimeFromOrdinalCore(arguments, span, context, true);

    private static async ValueTask<object> DateTimeFromOrdinalCore(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.fromordinal(ordinal) expects one integer argument.", span);
        }

        var indexed = (await LythonRuntime.CoerceTypeIndexAsync(arguments[0], context, span, asynchronous).ConfigureAwait(false))!;
        return OwnDateTimeValue(new PyDateTime(DateFromOrdinalValue(indexed, span, context).ToDateTime(TimeOnly.MinValue)), context, span);
    }

    public static object DateTimeFromIsoCalendar(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => DateTimeFromIsoCalendarCore(arguments, span, context, false).GetAwaiter().GetResult();

    public static ValueTask<object> DateTimeFromIsoCalendarAsync(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => DateTimeFromIsoCalendarCore(arguments, span, context, true);

    private static async ValueTask<object> DateTimeFromIsoCalendarCore(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        if (arguments.Length != 3)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.fromisocalendar(year, week, day) expects three integer arguments.", span);
        }

        return OwnDateTimeValue(new PyDateTime((await DateFromIsoCalendarValueAsync(arguments, span, context, asynchronous).ConfigureAwait(false)).ToDateTime(TimeOnly.MinValue)), context, span);
    }

    public static object DateTimeFromTimestamp(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => DateTimeFromTimestampCore(arguments, span, context, false).GetAwaiter().GetResult();

    public static ValueTask<object> DateTimeFromTimestampAsync(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => DateTimeFromTimestampCore(arguments, span, context, true);

    private static async ValueTask<object> DateTimeFromTimestampCore(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.fromtimestamp(timestamp[, tz]) expects one or two arguments.", span);
        }

        var indexed = (await LythonRuntime.CoerceTypeIndexAsync(arguments[0], context, span, asynchronous).ConfigureAwait(false))!;
        var instant = DateTimeOffsetFromTimestamp(TimestampToMicroseconds(CoerceTimestampNumber(indexed, span, context)), span);
        if (arguments.Length == 1 || arguments[1] is null or PyNone)
        {
            context.CheckExecution(span);
            return OwnDateTimeValue(new PyDateTime(DateTime.SpecifyKind(instant.ToOffset(context.Host.LocalNow.Offset).DateTime, DateTimeKind.Unspecified)), context, span);
        }

        if (arguments[1] is not PyTimezone tz)
        {
            throw InvalidTimezone(arguments[1], context, span);
        }

        return OwnDateTimeValue(new PyDateTime(DateTime.SpecifyKind(instant.UtcDateTime + tz.Offset, DateTimeKind.Unspecified), tz), context, span);
    }

    public static object DateTimeUtcFromTimestamp(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => DateTimeUtcFromTimestampCore(arguments, span, context, false).GetAwaiter().GetResult();

    public static ValueTask<object> DateTimeUtcFromTimestampAsync(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => DateTimeUtcFromTimestampCore(arguments, span, context, true);

    private static async ValueTask<object> DateTimeUtcFromTimestampCore(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        _ = context;
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "datetime.datetime.utcfromtimestamp(timestamp) expects one argument.", span);
        }

        var indexed = (await LythonRuntime.CoerceTypeIndexAsync(arguments[0], context, span, asynchronous).ConfigureAwait(false))!;
        return OwnDateTimeValue(new PyDateTime(DateTime.SpecifyKind(DateTimeOffsetFromTimestamp(TimestampToMicroseconds(CoerceTimestampNumber(indexed, span, context)), span).UtcDateTime, DateTimeKind.Unspecified)), context, span);
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
            var parsed = ParseStrptime(text, format, span, context);
            // %Z provides a label to time.strptime, but datetime is aware only
            // when a numeric %z offset was actually captured.
            return parsed.HasNumericOffset || parsed.Value.TzInfo is null ? parsed.Value : new PyDateTime(parsed.Value.Value);
        }
        catch (FormatException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
    }

    public static object DateToday(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = arguments;
        context.CheckExecution(span);
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
            context.CheckExecution(span);
            var localNow = context.Host.LocalNow;
            return OwnDateTimeValue(new PyDateTime(DateTime.SpecifyKind(localNow.DateTime, DateTimeKind.Unspecified)), context, span);
        }

        if (arguments[0] is not PyTimezone tz)
        {
            throw InvalidTimezone(arguments[0], context, span);
        }

        context.CheckExecution(span);
        var instant = context.Host.UtcNow.UtcDateTime + tz.Offset;
        return OwnDateTimeValue(new PyDateTime(DateTime.SpecifyKind(instant, DateTimeKind.Unspecified), tz), context, span);
    }

    public static object DateTimeUtcNow(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = arguments;
        context.CheckExecution(span);
        var utcNow = context.Host.UtcNow;
        return OwnDateTimeValue(new PyDateTime(DateTime.SpecifyKind(utcNow.UtcDateTime, DateTimeKind.Unspecified)), context, span);
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

    public static PyString CTime(DateOnly date, MemoryGovernor governor, LythonSourceSpan? span)
        => CTime(date.ToDateTime(TimeOnly.MinValue), governor, span);

    public static PyString CTime(DateTime dateTime, MemoryGovernor governor, LythonSourceSpan? span)
    {
        var prefix = dateTime.ToString("ddd MMM", CultureInfo.InvariantCulture);
        var time = dateTime.ToString("HH:mm:ss yyyy", CultureInfo.InvariantCulture);
        return PyString.FromString($"{prefix} {dateTime.Day,2} {time}", governor, span);
    }

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
        var target = targetTimezone ?? new PyTimezone(localOffset);
        if (ReferenceEquals(dateTime.TzInfo, target)) return dateTime;
        var ticks = dateTime.Value.Ticks - (dateTime.TzInfo?.Offset.Ticks ?? localOffset.Ticks) + target.Offset.Ticks;
        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            throw new LythonRuntimeException("OverflowError", "date value out of range", span);
        return new PyDateTime(new DateTime(ticks, DateTimeKind.Unspecified), target);
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
