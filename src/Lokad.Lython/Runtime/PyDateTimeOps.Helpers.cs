using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDateTimeOps
{
    private static PyTuple CreateTimeTuple(DateOnly date, TimeOnly time, int isDst)
    {
        return PyTuple.FromOwnedArray([
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

    private static DateOnly DateFromOrdinalValue(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        // Ordinals convert through __index__ to a C long like CPython;
        // in-range values then resolve through the civil calendar with
        // construction-shaped range texts.
        var coerced = LythonRuntime.CoerceIndexProtocol(value, context, span);
        if (!Numbers.PyNumberOps.TryAsInteger(coerced, out var ordinal))
        {
            throw new LythonRuntimeException("TypeError", "'" + LythonRuntime.UnboundTypeMethod.PythonTypeName(value, context) + "' object cannot be interpreted as an integer", span);
        }

        if (ordinal > long.MaxValue || ordinal < long.MinValue)
        {
            throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C long", span);
        }

        if (ordinal < BigInteger.One)
        {
            throw new LythonRuntimeException("ValueError", "ordinal must be >= 1", span);
        }

        var year = CivilYearFromOrdinal((long)ordinal);
        if (year < 1 || year > 9999)
        {
            throw new LythonRuntimeException("ValueError", $"year {year} is out of range", span);
        }

        return DateOnly.FromDayNumber((int)ordinal - 1);
    }

    private static long CivilYearFromOrdinal(long ordinal)
    {
        // Ordinal day numbers are 1-based and the caller rejects values below
        // 1, so every intermediate stays non-negative and truncating division
        // matches the floor division in CPython ord_to_ymd. The bias converts
        // days since 0001-01-01 to days since the civil 0000-03-01 epoch.
        ulong z = (ulong)(ordinal - 1) + 306UL;
        ulong era = z / 146097UL;
        ulong doe = z - era * 146097UL;
        ulong yoe = (doe - doe / 1460UL + doe / 36524UL - doe / 146096UL) / 365UL;
        ulong y = yoe + era * 400UL;
        ulong doy = doe - (365UL * yoe + yoe / 4UL - yoe / 100UL);
        ulong mp = (5UL * doy + 2UL) / 153UL;
        ulong m = mp < 10UL ? mp + 3UL : mp - 9UL;
        return (long)(m <= 2UL ? y + 1UL : y);
    }

    private static DateOnly DateFromIsoCalendarValue(object yearValue, object weekValue, object dayValue, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var year = CoerceIsoCalendarComponent(yearValue, span, context);
        var week = CoerceIsoCalendarComponent(weekValue, span, context);
        var day = CoerceIsoCalendarComponent(dayValue, span, context);

        if (year < 1 || year > 9999)
        {
            throw new LythonRuntimeException("ValueError", $"Year is out of range: {year}", span);
        }

        if (week < 1 || week > ISOWeek.GetWeeksInYear(year))
        {
            throw new LythonRuntimeException("ValueError", $"Invalid week: {week}", span);
        }

        if (day < 1 || day > 7)
        {
            throw new LythonRuntimeException("ValueError", $"Invalid day: {day} (range is [1, 7])", span);
        }

        return DateFromIsoCalendarParts(year, week, day);
    }

    private static int CoerceIsoCalendarComponent(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        // Components convert through __index__ to a C int like CPython;
        // wider magnitudes fail with the ISO-specific range text.
        var coerced = LythonRuntime.CoerceIndexProtocol(value, context, span);
        if (!Numbers.PyNumberOps.TryAsInteger(coerced, out var integer))
        {
            throw new LythonRuntimeException("TypeError", "'" + LythonRuntime.UnboundTypeMethod.PythonTypeName(value, context) + "' object cannot be interpreted as an integer", span);
        }

        if (integer < int.MinValue || integer > int.MaxValue)
        {
            throw new LythonRuntimeException("ValueError", "ISO calendar component out of range", span);
        }

        return (int)integer;
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

    private static double GetTimestamp(object value, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        // Timestamps convert through __index__ like CPython; remaining
        // rejections name the original type instead of the factory.
        var coerced = LythonRuntime.CoerceIndexProtocol(value, context, span);
        if (!Numbers.PyNumberOps.TryAsNumber(coerced, out var number))
        {
            throw new LythonRuntimeException("TypeError", "'" + LythonRuntime.UnboundTypeMethod.PythonTypeName(value, context) + "' object cannot be interpreted as an integer", span);
        }

        var timestamp = number.ToDouble();
        if (double.IsInfinity(timestamp))
        {
            throw new LythonRuntimeException("OverflowError", "timestamp out of range for platform time_t", span);
        }

        if (double.IsNaN(timestamp))
        {
            throw new LythonRuntimeException("ValueError", "Invalid value NaN (not a number)", span);
        }

        // Whole seconds beyond the time_t range fail before scaling like
        // CPython; narrower overflows stay host-shaped (libc-dependent).
        var wholeSeconds = number.IsFloat ? (BigInteger)Math.Truncate(number.Floating) : number.Integer;
        if (wholeSeconds > long.MaxValue || wholeSeconds < long.MinValue)
        {
            throw new LythonRuntimeException("OverflowError", "timestamp out of range for platform time_t", span);
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

    private static BigInteger AccumulateTimedeltaComponent(
        string name,
        object? value,
        bool assigned,
        BigInteger factor,
        BigInteger sofar,
        ref double leftover,
        LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    {
        // Omitted components contribute nothing; only exact integers and
        // floats convert (no __index__), everything else names its type.
        if (!assigned || value is null)
        {
            return sofar;
        }

        switch (value)
        {
            case bool flag:
                return flag ? sofar + factor : sofar;
            case BigInteger integer:
                return sofar + integer * factor;
            case double floating:
                if (double.IsInfinity(floating))
                {
                    throw new LythonRuntimeException("OverflowError", "cannot convert float infinity to integer", span);
                }

                if (double.IsNaN(floating))
                {
                    throw new LythonRuntimeException("ValueError", "cannot convert float NaN to integer", span);
                }

                var intPart = Math.Truncate(floating);
                var total = sofar + (BigInteger)intPart * factor;
                var fracPart = floating - intPart;
                if (fracPart == 0.0)
                {
                    return total;
                }

                var scaled = (double)factor * fracPart;
                var fracInt = Math.Truncate(scaled);
                leftover += scaled - fracInt;
                return total + (BigInteger)fracInt;
            default:
                throw new LythonRuntimeException("TypeError", $"unsupported type for timedelta {name} component: {LythonRuntime.UnboundTypeMethod.PythonTypeName(value, context)}", span);
        }
    }

    private static int GetInteger(object? value, bool assigned, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        // Omitted optionals keep their zero default; every explicitly supplied
        // value (including None) coerces through __index__ like CPython, and
        // remaining rejections name the original type instead of the factory.
        if (!assigned || value is null)
        {
            return 0;
        }

        var coerced = LythonRuntime.CoerceIndexProtocol(value, context, span);
        if (!Numbers.PyNumberOps.TryAsInteger(coerced, out var integer))
        {
            throw new LythonRuntimeException("TypeError", "'" + LythonRuntime.UnboundTypeMethod.PythonTypeName(value, context) + "' object cannot be interpreted as an integer", span);
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
