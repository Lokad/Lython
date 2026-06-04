using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyTimedelta : IPyTruthyValue, IPyHashableValue, IPyRenderableValue
{
    public PyTimedelta(TimeSpan value)
    {
        Value = Normalize(value);
    }

    public TimeSpan Value { get; }

    public BigInteger Days => new(GetNormalizedParts().Days);

    public BigInteger Seconds => new(GetNormalizedParts().Seconds);

    public BigInteger Microseconds => new(GetNormalizedParts().Microseconds);

    public bool IsTruthy() => Value != TimeSpan.Zero;

    public int GetPyHashCode() => HashCode.Combine(Value.Ticks);

    public double TotalSeconds() => Value.TotalSeconds;

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        var parts = GetNormalizedParts();
        return PyString.FromString($"datetime.timedelta(days={parts.Days}, seconds={parts.Seconds}, microseconds={parts.Microseconds})");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public override bool Equals(object? obj) => obj is PyTimedelta other && Value.Equals(other.Value);

    public override int GetHashCode() => GetPyHashCode();

    public override string ToString() => RenderPython(default).AsString();

    private static TimeSpan Normalize(TimeSpan value)
    {
        var ticks = value.Ticks - (value.Ticks % 10);
        return new TimeSpan(ticks);
    }

    private (long Days, long Seconds, long Microseconds) GetNormalizedParts()
    {
        const long ticksPerDay = TimeSpan.TicksPerDay;
        const long ticksPerSecond = TimeSpan.TicksPerSecond;
        const long ticksPerMicrosecond = 10;

        var totalTicks = Value.Ticks;
        var days = Math.DivRem(totalTicks, ticksPerDay, out var remainderTicks);
        if (remainderTicks < 0)
        {
            remainderTicks += ticksPerDay;
            days -= 1;
        }

        var seconds = remainderTicks / ticksPerSecond;
        var microseconds = (remainderTicks % ticksPerSecond) / ticksPerMicrosecond;
        return (days, seconds, microseconds);
    }
}

internal sealed class PyTimezone : IPyTruthyValue, IPyHashableValue, IPyRenderableValue
{
    public static readonly PyTimezone Utc = new(TimeSpan.Zero, "UTC");

    public PyTimezone(TimeSpan offset, string? name = null)
    {
        Offset = offset;
        Name = name ?? BuildDefaultName(offset);
    }

    public TimeSpan Offset { get; }

    public string Name { get; }

    public bool IsTruthy() => true;

    public int GetPyHashCode() => HashCode.Combine(Offset.Ticks, Name);

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        if (Offset == TimeSpan.Zero && Name == "UTC")
        {
            return PyString.FromString("datetime.timezone.utc");
        }

        return PyString.FromString($"datetime.timezone({PyDateTimeOps.FormatOffset(Offset)})");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public override bool Equals(object? obj) => obj is PyTimezone other && Offset.Equals(other.Offset) && string.Equals(Name, other.Name, StringComparison.Ordinal);

    public override int GetHashCode() => GetPyHashCode();

    public override string ToString() => Name;

    private static string BuildDefaultName(TimeSpan offset)
        => offset == TimeSpan.Zero ? "UTC" : PyDateTimeOps.FormatOffset(offset);
}

internal sealed class PyDate : IPyTruthyValue, IPyHashableValue, IPyRenderableValue
{
    public PyDate(DateOnly value)
    {
        Value = value;
    }

    public DateOnly Value { get; }

    public BigInteger Year => new(Value.Year);

    public BigInteger Month => new(Value.Month);

    public BigInteger Day => new(Value.Day);

    public bool IsTruthy() => true;

    public int GetPyHashCode() => HashCode.Combine(Value.DayNumber);

    public int Weekday() => ((int)Value.DayOfWeek + 6) % 7;

    public int IsoWeekday() => Weekday() + 1;

    public PyString IsoFormat() => PyString.FromString(Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    public BigInteger ToOrdinal() => new(Value.DayNumber + 1);

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString($"datetime.date({Value.Year}, {Value.Month}, {Value.Day})");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => IsoFormat();

    public override bool Equals(object? obj) => obj is PyDate other && Value.Equals(other.Value);

    public override int GetHashCode() => GetPyHashCode();

    public override string ToString() => IsoFormat().AsString();
}

internal sealed class PyTime : IPyTruthyValue, IPyHashableValue, IPyRenderableValue
{
    public PyTime(TimeOnly value, PyTimezone? tzinfo = null)
    {
        Value = value;
        TzInfo = tzinfo;
    }

    public TimeOnly Value { get; }

    public PyTimezone? TzInfo { get; }

    public BigInteger Hour => new(Value.Hour);

    public BigInteger Minute => new(Value.Minute);

    public BigInteger Second => new(Value.Second);

    public BigInteger Microsecond => new(Value.Microsecond);

    public bool IsTruthy() => true;

    public int GetPyHashCode() => HashCode.Combine(Value.Ticks, TzInfo?.GetPyHashCode() ?? 0);

    public PyString IsoFormat()
    {
        var text = Value.ToString(Value.Microsecond == 0 ? "HH:mm:ss" : "HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
        if (TzInfo is not null)
        {
            text += PyDateTimeOps.FormatOffset(TzInfo.Offset);
        }

        return PyString.FromString(text);
    }

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString($"datetime.time({Value.Hour}, {Value.Minute}, {Value.Second}, {Value.Microsecond})");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => IsoFormat();

    public override bool Equals(object? obj)
        => obj is PyTime other &&
           Value.Equals(other.Value) &&
           Equals(TzInfo, other.TzInfo);

    public override int GetHashCode() => GetPyHashCode();

    public override string ToString() => IsoFormat().AsString();
}

internal sealed class PyDateTime : IPyTruthyValue, IPyHashableValue, IPyRenderableValue
{
    public PyDateTime(DateTime value, PyTimezone? tzinfo = null)
    {
        Value = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        TzInfo = tzinfo;
    }

    public DateTime Value { get; }

    public PyTimezone? TzInfo { get; }

    public BigInteger Year => new(Value.Year);

    public BigInteger Month => new(Value.Month);

    public BigInteger Day => new(Value.Day);

    public BigInteger Hour => new(Value.Hour);

    public BigInteger Minute => new(Value.Minute);

    public BigInteger Second => new(Value.Second);

    public BigInteger Microsecond => new(Value.Microsecond);

    public bool IsTruthy() => true;

    public int GetPyHashCode() => HashCode.Combine(Value.Ticks, TzInfo?.GetPyHashCode() ?? 0);

    public PyDate DatePart() => new(DateOnly.FromDateTime(Value));

    public PyTime TimePart() => new(TimeOnly.FromDateTime(Value), TzInfo);

    public PyTime NaiveTimePart() => new(TimeOnly.FromDateTime(Value));

    public PyString IsoFormat()
    {
        var text = Value.ToString(Value.Microsecond == 0 ? "yyyy-MM-dd'T'HH:mm:ss" : "yyyy-MM-dd'T'HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
        if (TzInfo is not null)
        {
            text += PyDateTimeOps.FormatOffset(TzInfo.Offset);
        }

        return PyString.FromString(text);
    }

    public DateTimeOffset ToOffset()
        => new(Value, TzInfo?.Offset ?? TimeSpan.Zero);

    public BigInteger ToOrdinal() => new(DateOnly.FromDateTime(Value).DayNumber + 1);

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString($"datetime.datetime({Value.Year}, {Value.Month}, {Value.Day}, {Value.Hour}, {Value.Minute}, {Value.Second}, {Value.Microsecond})");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => IsoFormat();

    public override bool Equals(object? obj)
        => obj is PyDateTime other &&
           Value.Equals(other.Value) &&
           Equals(TzInfo, other.TzInfo);

    public override int GetHashCode() => GetPyHashCode();

    public override string ToString() => IsoFormat().AsString();
}

internal sealed class PyIsoCalendarDate : IPySequenceValue, IPyIndexableValue, IPyIterableValue, IPyTruthyValue, IPyRenderableValue, IPyDynamicAttributes
{
    private readonly PyTuple _items;

    public PyIsoCalendarDate(int year, int week, int weekday)
    {
        Year = new BigInteger(year);
        Week = new BigInteger(week);
        Weekday = new BigInteger(weekday);
        _items = new PyTuple([Year, Week, Weekday]);
    }

    public BigInteger Year { get; }

    public BigInteger Week { get; }

    public BigInteger Weekday { get; }

    public int Count => _items.Count;

    public int Length => _items.Length;

    public object this[int index] => _items[index];

    public object GetItem(int index) => _items.GetItem(index);

    public object CreateSlice(IEnumerable<object> items) => new PyTuple(items);

    public object GetIndex(int index) => _items.GetIndex(index);

    public object GetSlice(IEnumerable<int> indices) => _items.GetSlice(indices);

    public bool IsTruthy() => true;

    public IEnumerable<object> Iterate() => _items;

    public IEnumerator<object> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public bool TryGetMember(string name, out object value)
    {
        value = name switch
        {
            "year" => Year,
            "week" => Week,
            "weekday" => Weekday,
            _ => null!,
        };

        return value is not null;
    }

    public bool TrySetMember(string name, object value)
    {
        _ = name;
        _ = value;
        return false;
    }

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString($"datetime.IsoCalendarDate(year={Year}, week={Week}, weekday={Weekday})");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}

internal static class PyDateTimeOps
{
    private static readonly DateTimeOffset UnixEpoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class TypeMemberCallable : LythonRuntime.ICallable
    {
        private readonly Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> _implementation;
        private readonly string _name;
        private readonly string[]? _parameterNames;
        private readonly int _requiredCount;

        public TypeMemberCallable(string name, Func<object[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> implementation, string[]? parameterNames = null, int? requiredCount = null)
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
        CreateTimedelta);

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
            "fromtimestamp" => new TypeMemberCallable("datetime.datetime.fromtimestamp", DateTimeFromTimestamp, ["timestamp", "tz"], 1),
            "strptime" => new TypeMemberCallable("datetime.datetime.strptime", DateTimeStrptime, ["date_string", "format"]),
            "now" => new TypeMemberCallable("datetime.datetime.now", DateTimeNow, ["tz"], 0),
            "utcfromtimestamp" => new TypeMemberCallable("datetime.datetime.utcfromtimestamp", DateTimeUtcFromTimestamp, ["timestamp"]),
            "utcnow" => new TypeMemberCallable("datetime.datetime.utcnow", DateTimeUtcNow),
            _ => null
        });

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

        var totalTicks =
            (decimal)weeks * 7m * TimeSpan.TicksPerDay +
            (decimal)days * TimeSpan.TicksPerDay +
            (decimal)hours * TimeSpan.TicksPerHour +
            (decimal)minutes * TimeSpan.TicksPerMinute +
            (decimal)seconds * TimeSpan.TicksPerSecond +
            (decimal)milliseconds * TimeSpan.TicksPerMillisecond +
            (decimal)microseconds * 10m;

        return new PyTimedelta(new TimeSpan((long)Math.Round(totalTicks, MidpointRounding.ToEven)));
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
            ["hour", "minute", "second", "microsecond", "tzinfo"],
            0);

        return new PyTime(
            new TimeOnly(
                GetInteger(ArgAt(bound, 0), "datetime.time", span),
                GetInteger(ArgAt(bound, 1), "datetime.time", span),
                GetInteger(ArgAt(bound, 2), "datetime.time", span),
                GetInteger(ArgAt(bound, 3), "datetime.time", span) / 1000,
                GetInteger(ArgAt(bound, 3), "datetime.time", span) % 1000),
            GetTimezone(ArgAt(bound, 4), "datetime.time", span));
    }

    public static object CreateDateTime(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _ = context;
        var bound = CallBinder.BindNamedArguments(
            arguments,
            span,
            "datetime.datetime",
            "Builtin",
            ["year", "month", "day", "hour", "minute", "second", "microsecond", "tzinfo"],
            3);

        var microsecond = GetInteger(ArgAt(bound, 6), "datetime.datetime", span);
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
            GetTimezone(ArgAt(bound, 7), "datetime.datetime", span));
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

        if (delta.Value.Ticks % TimeSpan.TicksPerMinute != 0)
        {
            throw new LythonRuntimeException("ValueError", "datetime.timezone(...) only supports whole-minute offsets.", span);
        }

        if (delta.Value <= TimeSpan.FromHours(-24) || delta.Value >= TimeSpan.FromHours(24))
        {
            throw new LythonRuntimeException("ValueError", "datetime.timezone(...) offset must be strictly between -24h and +24h.", span);
        }

        var name = ArgAt(bound, 1) switch
        {
            null or PyNone => null,
            _ when PyStringOps.TryAsString(ArgAt(bound, 1)!, out var text) => text.AsString(),
            _ => throw new LythonRuntimeException("TypeError", "datetime.timezone(offset[, name]) expects name to be a string or None.", span)
        };

        return delta.Value == TimeSpan.Zero && name is null
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
            return new PyDate(DateOnly.Parse(text.AsString(), CultureInfo.InvariantCulture));
        }
        catch (FormatException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
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
        catch (FormatException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
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
        catch (FormatException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
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

        return new PyDateTime(DateTime.SpecifyKind(instant.ToOffset(tz.Offset).DateTime, DateTimeKind.Unspecified), tz);
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

        return new PyDateTime(date.ToDateTime(time.Value), timezone);
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
            var translated = TranslateStrftimeFormat(format.AsString(), span);
            if (translated.Contains("zzz", StringComparison.Ordinal))
            {
                var parsedOffset = DateTimeOffset.ParseExact(text.AsString(), translated, CultureInfo.InvariantCulture);
                return new PyDateTime(parsedOffset.DateTime, new PyTimezone(parsedOffset.Offset));
            }

            return new PyDateTime(DateTime.ParseExact(text.AsString(), translated, CultureInfo.InvariantCulture));
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

        var instant = context.Host.UtcNow.ToOffset(tz.Offset);
        return new PyDateTime(DateTime.SpecifyKind(instant.DateTime, DateTimeKind.Unspecified), tz);
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

    public static PyTuple TimeTuple(DateTime dateTime, int isDst = -1)
        => CreateTimeTuple(DateOnly.FromDateTime(dateTime), TimeOnly.FromDateTime(dateTime), isDst);

    public static double Timestamp(PyDateTime dateTime, TimeSpan localOffset, LythonSourceSpan span)
    {
        try
        {
            var offset = dateTime.TzInfo?.Offset ?? localOffset;
            return (new DateTimeOffset(dateTime.Value, offset) - UnixEpoch).TotalSeconds;
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
            (PyTimedelta lhs, PyTimedelta rhs) => new PyTimedelta(lhs.Value + rhs.Value),
            (PyDate date, PyTimedelta delta) => new PyDate(date.Value.AddDays(delta.Value.Days)),
            (PyTimedelta delta, PyDate date) => new PyDate(date.Value.AddDays(delta.Value.Days)),
            (PyDateTime dateTime, PyTimedelta delta) => new PyDateTime(dateTime.Value + delta.Value, dateTime.TzInfo),
            (PyTimedelta delta, PyDateTime dateTime) => new PyDateTime(dateTime.Value + delta.Value, dateTime.TzInfo),
            _ => throw new LythonRuntimeException("TypeError", "Operands are not compatible with '+'.", span)
        };
    }

    public static object Subtract(object left, object right, LythonSourceSpan span)
    {
        return (left, right) switch
        {
            (PyTimedelta lhs, PyTimedelta rhs) => new PyTimedelta(lhs.Value - rhs.Value),
            (PyDate lhs, PyTimedelta rhs) => new PyDate(lhs.Value.AddDays(-rhs.Value.Days)),
            (PyDate lhs, PyDate rhs) => new PyTimedelta(TimeSpan.FromDays(lhs.Value.DayNumber - rhs.Value.DayNumber)),
            (PyDateTime lhs, PyTimedelta rhs) => new PyDateTime(lhs.Value - rhs.Value, lhs.TzInfo),
            (PyDateTime lhs, PyDateTime rhs) => SubtractDateTimes(lhs, rhs, span),
            _ => throw new LythonRuntimeException("TypeError", "Operands are not compatible with '-'.", span)
        };
    }

    public static object Negate(object operand, LythonSourceSpan span)
    {
        return operand switch
        {
            PyTimedelta delta => new PyTimedelta(-delta.Value),
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
            (PyTimedelta delta, _) when TryGetScale(right, out var scale) => ScaleTimedelta(delta, 1.0 / scale, span, checkZero: true),
            _ => throw new LythonRuntimeException("TypeError", "Operands are not compatible with '/'.", span)
        };
    }

    public static object FloorDivide(object left, object right, LythonSourceSpan span)
    {
        return (left, right) switch
        {
            (PyTimedelta delta, PyTimedelta other) => new BigInteger(Math.Floor(DivideTimedeltas(delta, other, span))),
            (PyTimedelta delta, _) when TryGetScale(right, out var scale) => ScaleTimedelta(delta, 1.0 / scale, span, floor: true, checkZero: true),
            _ => throw new LythonRuntimeException("TypeError", "Operands are not compatible with '//'.", span)
        };
    }

    public static int Compare(object left, object right, LythonSourceSpan span)
    {
        return (left, right) switch
        {
            (PyTimedelta lhs, PyTimedelta rhs) => lhs.Value.CompareTo(rhs.Value),
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
        return $"{sign}{offset.Hours:00}:{offset.Minutes:00}";
    }

    public static PyString Strftime(DateOnly value, PyString format, LythonSourceSpan span)
        => PyString.FromString(value.ToString(TranslateStrftimeFormat(format.AsString(), span), CultureInfo.InvariantCulture));

    public static PyString Strftime(TimeOnly value, PyTimezone? timezone, PyString format, LythonSourceSpan span)
    {
        var translated = TranslateStrftimeFormat(format.AsString(), span);
        if (timezone is null)
        {
            return PyString.FromString(value.ToString(translated, CultureInfo.InvariantCulture));
        }

        var dateTimeOffset = new DateTimeOffset(
            1,
            1,
            1,
            value.Hour,
            value.Minute,
            value.Second,
            value.Millisecond,
            timezone.Offset)
            .AddTicks(value.Ticks % TimeSpan.TicksPerMillisecond);
        return PyString.FromString(dateTimeOffset.ToString(translated, CultureInfo.InvariantCulture));
    }

    public static PyString Strftime(DateTime value, PyTimezone? timezone, PyString format, LythonSourceSpan span)
    {
        var translated = TranslateStrftimeFormat(format.AsString(), span);
        if (timezone is null)
        {
            return PyString.FromString(value.ToString(translated, CultureInfo.InvariantCulture));
        }

        return PyString.FromString(new DateTimeOffset(value, timezone.Offset).ToString(translated, CultureInfo.InvariantCulture));
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
            : new PyTimedelta(left.ToOffset() - right.ToOffset());
    }

    private static int CompareDateTimes(PyDateTime left, PyDateTime right, LythonSourceSpan span)
    {
        if ((left.TzInfo is null) != (right.TzInfo is null))
        {
            throw new LythonRuntimeException("TypeError", "Cannot compare naive and timezone-aware datetimes.", span);
        }

        return left.TzInfo is null
            ? left.Value.CompareTo(right.Value)
            : left.ToOffset().CompareTo(right.ToOffset());
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

        var leftAdjusted = DateTime.Today.Add(left.Value.ToTimeSpan()) - left.TzInfo!.Offset;
        var rightAdjusted = DateTime.Today.Add(right.Value.ToTimeSpan()) - right.TzInfo!.Offset;
        return leftAdjusted.CompareTo(rightAdjusted);
    }

    private static PyTime ParseTime(string text)
    {
        if (text.EndsWith("Z", StringComparison.Ordinal))
        {
            text = text[..^1] + "+00:00";
        }

        if (TryParseTrailingOffset(text, out var body, out var offset))
        {
            return new PyTime(TimeOnly.Parse(body, CultureInfo.InvariantCulture), new PyTimezone(offset));
        }

        return new PyTime(TimeOnly.Parse(text, CultureInfo.InvariantCulture));
    }

    private static PyDateTime ParseDateTime(string text)
    {
        if (text.EndsWith("Z", StringComparison.Ordinal))
        {
            text = text[..^1] + "+00:00";
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var offsetValue) && TryParseTrailingOffset(text, out _, out var offset))
        {
            return new PyDateTime(offsetValue.DateTime, new PyTimezone(offset));
        }

        return new PyDateTime(DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.None));
    }

    private static bool TryParseTrailingOffset(string text, out string body, out TimeSpan offset)
    {
        if (text.Length >= 6 && (text[^6] == '+' || text[^6] == '-') && text[^3] == ':')
        {
            body = text[..^6];
            var sign = text[^6] == '-' ? -1 : 1;
            if (int.TryParse(text.Substring(text.Length - 5, 2), CultureInfo.InvariantCulture, out var hours) &&
                int.TryParse(text.Substring(text.Length - 2, 2), CultureInfo.InvariantCulture, out var minutes))
            {
                offset = new TimeSpan(sign * hours, sign * minutes, 0);
                return true;
            }
        }

        body = text;
        offset = default;
        return false;
    }

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

    private static object? ArgAt(object[] arguments, int index) => index < arguments.Length ? arguments[index] : null;

    private static string TranslateStrftimeFormat(string format, LythonSourceSpan span)
    {
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
            builder.Append(directive switch
            {
                '%' => "%",
                'Y' => "yyyy",
                'm' => "MM",
                'd' => "dd",
                'H' => "HH",
                'M' => "mm",
                'S' => "ss",
                'f' => "ffffff",
                'z' => "zzz",
                'y' => "yy",
                _ => throw new LythonRuntimeException("ValueError", $"strftime directive '%{directive}' is not supported in Lython yet.", span)
            });
        }

        return builder.ToString();
    }

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

    private static PyTimedelta ScaleTimedelta(PyTimedelta delta, double scale, LythonSourceSpan span, bool floor = false, bool checkZero = false)
    {
        if (checkZero && double.IsInfinity(scale))
        {
            throw new LythonRuntimeException("ValueError", "division by zero", span);
        }

        var scaledTicks = delta.Value.Ticks * scale;
        var roundedTicks = floor
            ? Math.Floor(scaledTicks)
            : Math.Round(scaledTicks, MidpointRounding.ToEven);
        return new PyTimedelta(new TimeSpan((long)roundedTicks));
    }

    private static double DivideTimedeltas(PyTimedelta left, PyTimedelta right, LythonSourceSpan span)
    {
        if (right.Value == TimeSpan.Zero)
        {
            throw new LythonRuntimeException("ValueError", "division by zero", span);
        }

        return (double)left.Value.Ticks / right.Value.Ticks;
    }
}
