using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDateTimeOps
{
    public static string FormatOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        offset = offset.Duration();
        var text = $"{sign}{offset.Hours:00}:{offset.Minutes:00}";
        if (offset.Seconds != 0 || offset.Microseconds != 0)
        {
            text += $":{offset.Seconds:00}";
        }

        if (offset.Microseconds != 0)
        {
            text += $".{offset.Microseconds:000000}";
        }

        return text;
    }

    public static string FormatIsoTime(TimeOnly value, string timespec)
    {
        return timespec switch
        {
            "auto" => value.Microsecond == 0
                ? value.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                : value.ToString("HH:mm:ss.ffffff", CultureInfo.InvariantCulture),
            "hours" => value.ToString("HH", CultureInfo.InvariantCulture),
            "minutes" => value.ToString("HH:mm", CultureInfo.InvariantCulture),
            "seconds" => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            "milliseconds" => value.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
            "microseconds" => value.ToString("HH:mm:ss.ffffff", CultureInfo.InvariantCulture),
            _ => throw new LythonRuntimeException("ValueError", "Unknown timespec value.", default)
        };
    }

    public static PyString FormatValue(object value, PyString format, LythonSourceSpan span)
    {
        if (format.Length == 0)
        {
            return value switch
            {
                PyDate date => date.IsoFormat(),
                PyTime time => time.IsoFormat(),
                PyDateTime dateTime => dateTime.IsoFormat(" "),
                _ => PyString.FromString(string.Empty)
            };
        }

        return value switch
        {
            PyDate date => Strftime(date.Value, format, span),
            PyTime time => Strftime(time.Value, time.TzInfo, format, span),
            PyDateTime dateTime => Strftime(dateTime.Value, dateTime.TzInfo, format, span),
            _ => throw new LythonRuntimeException("TypeError", "__format__ expects a date, time, or datetime value.", span)
        };
    }

    public static PyString Strftime(DateOnly value, PyString format, LythonSourceSpan span)
        => PyString.FromString(FormatStrftime(value.ToDateTime(TimeOnly.MinValue), null, format.AsString(), span));

    public static PyString Strftime(TimeOnly value, PyTimezone? timezone, PyString format, LythonSourceSpan span)
    {
        var dateTime = new DateTime(1900, 1, 1, value.Hour, value.Minute, value.Second, value.Millisecond, DateTimeKind.Unspecified)
            .AddTicks(value.Ticks % TimeSpan.TicksPerMillisecond);
        return PyString.FromString(FormatStrftime(dateTime, timezone, format.AsString(), span));
    }

    public static PyString Strftime(DateTime value, PyTimezone? timezone, PyString format, LythonSourceSpan span)
        => PyString.FromString(FormatStrftime(value, timezone, format.AsString(), span));

    public static string FormatStrftime(DateTime value, PyTimezone? timezone, string format, LythonSourceSpan span)
        => FormatStrftime(value, timezone, format, span, null, null);

    public static string FormatStrftime(DateTime value, PyTimezone? timezone, string format, LythonSourceSpan span, int? weekdayOverride)
        => FormatStrftime(value, timezone, format, span, weekdayOverride, null);

    public static string FormatStrftime(
        DateTime value,
        PyTimezone? timezone,
        string format,
        LythonSourceSpan span,
        int? weekdayOverride,
        int? yearDayOverride)
    {
        var mondayBasedWeekday = weekdayOverride ?? ((int)value.DayOfWeek + 6) % 7;
        var yearDay = yearDayOverride ?? value.DayOfYear;
        var displayWeekday = new DateTime(2024, 1, 1).AddDays(mondayBasedWeekday);
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
            switch (directive)
            {
                case '%':
                    builder.Append('%');
                    break;
                case 'Y':
                    builder.Append(value.Year.ToString("0000", CultureInfo.InvariantCulture));
                    break;
                case 'y':
                    builder.Append((value.Year % 100).ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'm':
                    builder.Append(value.Month.ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'd':
                    builder.Append(value.Day.ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'H':
                    builder.Append(value.Hour.ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'I':
                    var hour12 = value.Hour % 12;
                    builder.Append((hour12 == 0 ? 12 : hour12).ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'p':
                    builder.Append(value.Hour < 12 ? "AM" : "PM");
                    break;
                case 'M':
                    builder.Append(value.Minute.ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'S':
                    builder.Append(value.Second.ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'f':
                    builder.Append(value.Microsecond.ToString("000000", CultureInfo.InvariantCulture));
                    break;
                case 'z':
                    builder.Append(timezone is null ? string.Empty : FormatCompactOffset(timezone.Offset));
                    break;
                case 'Z':
                    builder.Append(timezone?.Name ?? string.Empty);
                    break;
                case 'a':
                    builder.Append(displayWeekday.ToString("ddd", CultureInfo.InvariantCulture));
                    break;
                case 'A':
                    builder.Append(displayWeekday.ToString("dddd", CultureInfo.InvariantCulture));
                    break;
                case 'b':
                case 'h':
                    builder.Append(value.ToString("MMM", CultureInfo.InvariantCulture));
                    break;
                case 'B':
                    builder.Append(value.ToString("MMMM", CultureInfo.InvariantCulture));
                    break;
                case 'j':
                    builder.Append(yearDay.ToString("000", CultureInfo.InvariantCulture));
                    break;
                case 'w':
                    builder.Append(((mondayBasedWeekday + 1) % 7).ToString(CultureInfo.InvariantCulture));
                    break;
                case 'u':
                    builder.Append((mondayBasedWeekday + 1).ToString(CultureInfo.InvariantCulture));
                    break;
                case 'U':
                    builder.Append(WeekNumber(yearDay, mondayBasedWeekday, sundayFirst: true).ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'W':
                    builder.Append(WeekNumber(yearDay, mondayBasedWeekday, sundayFirst: false).ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'G':
                    builder.Append(ISOWeek.GetYear(value).ToString("0000", CultureInfo.InvariantCulture));
                    break;
                case 'V':
                    builder.Append(ISOWeek.GetWeekOfYear(value).ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'c':
                    builder.Append(displayWeekday.ToString("ddd", CultureInfo.InvariantCulture));
                    builder.Append(' ');
                    builder.Append(value.ToString("MMM", CultureInfo.InvariantCulture));
                    builder.Append(' ');
                    builder.Append(value.Day.ToString(CultureInfo.InvariantCulture).PadLeft(2, ' '));
                    builder.Append(value.ToString(" HH:mm:ss yyyy", CultureInfo.InvariantCulture));
                    break;
                case 'x':
                    builder.Append(value.ToString("MM/dd/yy", CultureInfo.InvariantCulture));
                    break;
                case 'X':
                    builder.Append(value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
                    break;
                default:
                    throw new LythonRuntimeException("ValueError", $"strftime directive '%{directive}' is not supported in Lython yet.", span);
            }
        }

        return builder.ToString();
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
            "datetime.tzinfo" => value is PyTimezone,
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
            : new PyTimedelta(new BigInteger((left.ToUtcTicks() - right.ToUtcTicks()) / 10));
    }

    private static int CompareDateTimes(PyDateTime left, PyDateTime right, LythonSourceSpan span)
    {
        if ((left.TzInfo is null) != (right.TzInfo is null))
        {
            throw new LythonRuntimeException("TypeError", "Cannot compare naive and timezone-aware datetimes.", span);
        }

        return left.TzInfo is null
            ? left.Value.CompareTo(right.Value)
            : left.ToUtcTicks().CompareTo(right.ToUtcTicks());
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

        var leftAdjusted = AdjustTimeTicks(left.Value, left.TzInfo.RequireNotNull().Offset);
        var rightAdjusted = AdjustTimeTicks(right.Value, right.TzInfo.RequireNotNull().Offset);
        return leftAdjusted.CompareTo(rightAdjusted);
    }

    public static bool DateTimeEquals(PyDateTime left, PyDateTime right)
    {
        if ((left.TzInfo is null) != (right.TzInfo is null))
        {
            return false;
        }

        return left.TzInfo is null
            ? left.Value == right.Value
            : left.ToUtcTicks() == right.ToUtcTicks();
    }

    public static bool TimeEquals(PyTime left, PyTime right)
    {
        if ((left.TzInfo is null) != (right.TzInfo is null))
        {
            return false;
        }

        if (left.TzInfo is null)
        {
            return left.Value == right.Value;
        }

        return AdjustTimeTicks(left.Value, left.TzInfo.RequireNotNull().Offset) == AdjustTimeTicks(right.Value, right.TzInfo.RequireNotNull().Offset);
    }

    public static long AdjustTimeTicks(TimeOnly value, TimeSpan offset)
    {
        var ticks = (value.Ticks - offset.Ticks) % TimeSpan.TicksPerDay;
        return ticks < 0 ? ticks + TimeSpan.TicksPerDay : ticks;
    }

    private static int WeekNumber(DateTime value, DayOfWeek firstDay)
    {
        var first = new DateTime(value.Year, 1, 1);
        var offset = ((int)firstDay - (int)first.DayOfWeek + 7) % 7;
        var firstWeekStart = first.AddDays(offset);
        if (value.Date < firstWeekStart)
        {
            return 0;
        }

        return ((value.Date - firstWeekStart).Days / 7) + 1;
    }

    private static int WeekNumber(int yearDay, int mondayBasedWeekday, bool sundayFirst)
    {
        var zeroBasedDay = yearDay - 1;
        var currentWeekday = sundayFirst ? (mondayBasedWeekday + 1) % 7 : mondayBasedWeekday;
        var firstDayWeekday = (currentWeekday - zeroBasedDay % 7 + 7) % 7;
        var firstWeekStart = (7 - firstDayWeekday) % 7;
        return zeroBasedDay < firstWeekStart ? 0 : 1 + (zeroBasedDay - firstWeekStart) / 7;
    }

    private static string FormatCompactOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        offset = offset.Duration();
        var text = $"{sign}{(int)offset.TotalHours:00}{offset.Minutes:00}";
        if (offset.Seconds != 0 || offset.Microseconds != 0)
        {
            text += $"{offset.Seconds:00}";
        }

        if (offset.Microseconds != 0)
        {
            text += $".{offset.Microseconds:000000}";
        }

        return text;
    }

}
