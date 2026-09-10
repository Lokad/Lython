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

    private static DateOnly DateFromIsoCalendarValue(object yearValue, object weekValue, object dayValue, string owner, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(yearValue, out var year) ||
            !Numbers.PyNumberOps.TryAsInteger(weekValue, out var week) ||
            !Numbers.PyNumberOps.TryAsInteger(dayValue, out var day))
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(year, week, day) expects integer fields.", span);
        }

        try
        {
            return DateFromIsoCalendarParts((int)year, (int)week, (int)day);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
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

    private static int GetFold(object? value, string owner, LythonSourceSpan span)
    {
        var fold = GetInteger(value, owner, span);
        if (fold is not 0 and not 1)
        {
            throw new LythonRuntimeException("ValueError", $"{owner} fold must be either 0 or 1.", span);
        }

        return fold;
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
