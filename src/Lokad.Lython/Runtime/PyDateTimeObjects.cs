using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyTimedelta : IPyTruthyValue, IPyHashableValue, IPyRenderableValue
{
    private readonly record struct NormalizedTimedeltaParts(BigInteger Days, int Seconds, int Microseconds);

    public static readonly BigInteger MicrosecondsPerDay = new(86_400_000_000L);
    private static readonly BigInteger MinimumMicroseconds = -999_999_999 * MicrosecondsPerDay;
    private static readonly BigInteger MaximumMicroseconds = 999_999_999 * MicrosecondsPerDay + MicrosecondsPerDay - 1;

    public static readonly PyTimedelta Min = new(MinimumMicroseconds);
    public static readonly PyTimedelta Max = new(MaximumMicroseconds);
    public static readonly PyTimedelta Resolution = new(TimeSpan.FromTicks(10));

    public PyTimedelta(TimeSpan value)
        : this(new BigInteger(value.Ticks / 10))
    {
    }

    public PyTimedelta(BigInteger totalMicroseconds)
    {
        if (totalMicroseconds < MinimumMicroseconds || totalMicroseconds > MaximumMicroseconds)
        {
            throw new OverflowException("timedelta is outside Python's supported day range");
        }

        TotalMicroseconds = totalMicroseconds;
        var days = BigInteger.DivRem(totalMicroseconds, MicrosecondsPerDay, out var remainderMicroseconds);
        if (remainderMicroseconds.Sign < 0)
        {
            remainderMicroseconds += MicrosecondsPerDay;
            days -= 1;
        }

        _normalized = new NormalizedTimedeltaParts(
            days,
            (int)(remainderMicroseconds / 1_000_000),
            (int)(remainderMicroseconds % 1_000_000));
    }

    private readonly NormalizedTimedeltaParts _normalized;

    public BigInteger TotalMicroseconds { get; }

    public TimeSpan Value => new(checked((long)(TotalMicroseconds * 10)));

    public BigInteger Days => _normalized.Days;

    public BigInteger Seconds => new(_normalized.Seconds);

    public BigInteger Microseconds => new(_normalized.Microseconds);

    public bool IsTruthy() => !TotalMicroseconds.IsZero;

    public int GetPyHashCode() => TotalMicroseconds.GetHashCode();

    public double TotalSeconds() => (double)TotalMicroseconds / 1_000_000.0;

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        var parts = _normalized;
        return PyString.FromString($"datetime.timedelta(days={parts.Days}, seconds={parts.Seconds}, microseconds={parts.Microseconds})");
    }

    public PyString RenderInterpolated(PyRenderingContext context)
    {
        _ = context;
        var parts = _normalized;
        var builder = new StringBuilder();
        if (!parts.Days.IsZero)
        {
            builder.Append(parts.Days.ToString(CultureInfo.InvariantCulture));
            builder.Append(BigInteger.Abs(parts.Days) == BigInteger.One ? " day, " : " days, ");
        }

        var hours = parts.Seconds / 3600;
        var minutes = parts.Seconds % 3600 / 60;
        var seconds = parts.Seconds % 60;
        builder.Append(hours.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(minutes.ToString("00", CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(seconds.ToString("00", CultureInfo.InvariantCulture));
        if (parts.Microseconds != 0)
        {
            builder.Append('.');
            builder.Append(parts.Microseconds.ToString("000000", CultureInfo.InvariantCulture));
        }

        return PyString.FromString(builder.ToString());
    }

    public override bool Equals(object? obj) => obj is PyTimedelta other && TotalMicroseconds.Equals(other.TotalMicroseconds);

    public override int GetHashCode() => GetPyHashCode();

    public override string ToString() => RenderInterpolated(default).AsString();

}

internal sealed class PyTimezone : IPyTruthyValue, IPyHashableValue, IPyRenderableValue
{
    public static readonly PyTimezone Utc = new(TimeSpan.Zero, "UTC");

    public PyTimezone(TimeSpan offset) : this(offset, null) { }

    public PyTimezone(TimeSpan offset, string? name)
    {
        Offset = offset;
        Name = name ?? BuildDefaultName(offset);
    }

    public TimeSpan Offset { get; }

    public string Name { get; }

    public bool IsTruthy() => true;

    public int GetPyHashCode() => HashCode.Combine(Offset.Ticks);

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

    public override bool Equals(object? obj) => obj is PyTimezone other && Offset.Equals(other.Offset);

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
    public PyTime(TimeOnly value) : this(value, null, 0) { }

    public PyTime(TimeOnly value, PyTimezone? tzinfo) : this(value, tzinfo, 0) { }

    public PyTime(TimeOnly value, PyTimezone? tzinfo, int fold)
    {
        Value = value;
        TzInfo = tzinfo;
        Fold = fold;
    }

    public TimeOnly Value { get; }

    public PyTimezone? TzInfo { get; }

    public int Fold { get; }

    public BigInteger Hour => new(Value.Hour);

    public BigInteger Minute => new(Value.Minute);

    public BigInteger Second => new(Value.Second);

    public BigInteger Microsecond => new(Value.Microsecond);

    public bool IsTruthy() => true;

    public int GetPyHashCode() => HashCode.Combine(TzInfo is null ? Value.Ticks : PyDateTimeOps.AdjustTimeTicks(Value, TzInfo.Offset));

    public PyString IsoFormat()
        => IsoFormat("auto");

    public PyString IsoFormat(string timespec)
    {
        var text = PyDateTimeOps.FormatIsoTime(Value, timespec);
        if (TzInfo is not null)
        {
            text += PyDateTimeOps.FormatOffset(TzInfo.Offset);
        }

        return PyString.FromString(text);
    }

    public PyString RenderPython(PyRenderingContext context)
    {
        var builder = new StringBuilder($"datetime.time({Value.Hour}, {Value.Minute}");
        if (Value.Second != 0 || Value.Microsecond != 0)
        {
            builder.Append($", {Value.Second}");
        }

        if (Value.Microsecond != 0)
        {
            builder.Append($", {Value.Microsecond}");
        }

        if (TzInfo is not null)
        {
            builder.Append(", tzinfo=");
            builder.Append(TzInfo.RenderPython(context).AsString());
        }

        if (Fold != 0)
        {
            builder.Append(", fold=1");
        }

        builder.Append(')');
        return PyString.FromString(builder.ToString());
    }

    public PyString RenderInterpolated(PyRenderingContext context) => IsoFormat();

    public override bool Equals(object? obj)
        => obj is PyTime other &&
           PyDateTimeOps.TimeEquals(this, other);

    public override int GetHashCode() => GetPyHashCode();

    public override string ToString() => IsoFormat().AsString();
}

internal sealed class PyDateTime : IPyTruthyValue, IPyHashableValue, IPyRenderableValue
{
    public PyDateTime(DateTime value) : this(value, null, 0) { }

    public PyDateTime(DateTime value, PyTimezone? tzinfo) : this(value, tzinfo, 0) { }

    public PyDateTime(DateTime value, PyTimezone? tzinfo, int fold)
    {
        Value = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        TzInfo = tzinfo;
        Fold = fold;
    }

    public DateTime Value { get; }

    public PyTimezone? TzInfo { get; }

    public int Fold { get; }

    public BigInteger Year => new(Value.Year);

    public BigInteger Month => new(Value.Month);

    public BigInteger Day => new(Value.Day);

    public BigInteger Hour => new(Value.Hour);

    public BigInteger Minute => new(Value.Minute);

    public BigInteger Second => new(Value.Second);

    public BigInteger Microsecond => new(Value.Microsecond);

    public bool IsTruthy() => true;

    public int GetPyHashCode() => HashCode.Combine(TzInfo is null ? Value.Ticks : ToUtcTicks());

    public PyDate DatePart() => new(DateOnly.FromDateTime(Value));

    public PyTime TimePart() => new(TimeOnly.FromDateTime(Value), TzInfo, Fold);

    public PyTime NaiveTimePart() => new(TimeOnly.FromDateTime(Value), tzinfo: null, fold: Fold);

    public PyString IsoFormat()
        => IsoFormat("T", "auto");

    public PyString IsoFormat(string separator)
        => IsoFormat(separator, "auto");

    public PyString IsoFormat(string separator, string timespec)
    {
        var text = Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + separator + PyDateTimeOps.FormatIsoTime(TimeOnly.FromDateTime(Value), timespec);
        if (TzInfo is not null)
        {
            text += PyDateTimeOps.FormatOffset(TzInfo.Offset);
        }

        return PyString.FromString(text);
    }

    public long ToUtcTicks()
        => Value.Ticks - (TzInfo?.Offset.Ticks ?? 0);

    public BigInteger ToOrdinal() => new(DateOnly.FromDateTime(Value).DayNumber + 1);

    public PyString RenderPython(PyRenderingContext context)
    {
        var builder = new StringBuilder($"datetime.datetime({Value.Year}, {Value.Month}, {Value.Day}, {Value.Hour}, {Value.Minute}");
        if (Value.Second != 0 || Value.Microsecond != 0)
        {
            builder.Append($", {Value.Second}");
        }

        if (Value.Microsecond != 0)
        {
            builder.Append($", {Value.Microsecond}");
        }

        if (Fold != 0)
        {
            builder.Append(", fold=1");
        }

        if (TzInfo is not null)
        {
            builder.Append(", tzinfo=");
            builder.Append(TzInfo.RenderPython(context).AsString());
        }

        builder.Append(')');
        return PyString.FromString(builder.ToString());
    }

    public PyString RenderInterpolated(PyRenderingContext context)
    {
        _ = context;
        return IsoFormat(" ");
    }

    public override bool Equals(object? obj)
        => obj is PyDateTime other &&
           PyDateTimeOps.DateTimeEquals(this, other);

    public override int GetHashCode() => GetPyHashCode();

    public override string ToString() => IsoFormat(" ").AsString();
}

internal sealed class PyIsoCalendarDate : IPySequenceValue, IPyIndexableValue, IPyIterableValue, IPyTruthyValue, IPyRenderableValue, IPyDynamicAttributes
{
    private readonly PyTuple _items;

    public PyIsoCalendarDate(int year, int week, int weekday)
    {
        Year = new BigInteger(year);
        Week = new BigInteger(week);
        Weekday = new BigInteger(weekday);
        _items = PyTuple.FromOwnedArray([Year, Week, Weekday]);
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

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        value = name switch
        {
            "year" => Year,
            "week" => Week,
            "weekday" => Weekday,
            _ => MissingMemberValue.Instance,
        };

        return !ReferenceEquals(value, MissingMemberValue.Instance);
    }
    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString($"datetime.IsoCalendarDate(year={Year}, week={Week}, weekday={Weekday})");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}
