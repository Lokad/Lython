using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDateTimeOps
{
    public static string FormatOffset(TimeSpan offset)
    {
        // Totals, not components: TimeSpan fractions split milliseconds from
        // microseconds, and hours cap at a day.
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var totalMicroseconds = offset.Duration().Ticks / 10;
        var totalSeconds = totalMicroseconds / 1_000_000;
        var microseconds = totalMicroseconds % 1_000_000;
        var text = $"{sign}{totalSeconds / 3600:00}:{totalSeconds % 3600 / 60:00}";
        if (totalSeconds % 60 != 0 || microseconds != 0)
        {
            text += $":{totalSeconds % 60:00}";
        }

        if (microseconds != 0)
        {
            text += $".{microseconds:000000}";
        }

        return text;
    }

    public static string FormatIsoTime(TimeOnly value, string timespec)
    {
        return timespec switch
        {
            "auto" => value.Millisecond == 0 && value.Microsecond == 0
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

    public static PyString FormatValue(object value, PyString format, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (format.Length == 0)
        {
            return value switch
            {
                PyDate date => date.IsoFormat(context.MemoryGovernor, span),
                PyTime time => time.IsoFormat("auto", context.MemoryGovernor, span),
                PyDateTime dateTime => dateTime.IsoFormat(" ", "auto", context.MemoryGovernor, span),
                _ => PyString.Empty
            };
        }

        return value switch
        {
            PyDate date => Strftime(date.Value, format, span, context),
            PyTime time => Strftime(time.Value, time.TzInfo, format, span, context),
            PyDateTime dateTime => Strftime(dateTime.Value, dateTime.TzInfo, format, span, context),
            _ => throw new LythonRuntimeException("TypeError", "__format__ expects a date, time, or datetime value.", span)
        };
    }

    public static PyString Strftime(DateOnly value, PyString format, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => FormatStrftime(value.ToDateTime(TimeOnly.MinValue), null, format, span, context);

    public static PyString Strftime(TimeOnly value, PyTimezone? timezone, PyString format, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var dateTime = new DateTime(1900, 1, 1, value.Hour, value.Minute, value.Second, value.Millisecond, DateTimeKind.Unspecified)
            .AddTicks(value.Ticks % TimeSpan.TicksPerMillisecond);
        return FormatStrftime(dateTime, timezone, format, span, context);
    }

    public static PyString Strftime(DateTime value, PyTimezone? timezone, PyString format, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
        => FormatStrftime(value, timezone, format, span, context);

    public static PyString FormatStrftime(DateTime value, PyTimezone? timezone, PyString format,
        LythonSourceSpan span, LythonRuntime.ExecutionContext context, int? weekdayOverride = null, int? yearDayOverride = null)
    {
        var mondayBasedWeekday = weekdayOverride ?? ((int)value.DayOfWeek + 6) % 7;
        var yearDay = yearDayOverride ?? value.DayOfYear;
        var displayWeekday = new DateTime(2024, 1, 1).AddDays(mondayBasedWeekday);
        var input = format.Utf8Bytes.Span;
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        try
        {
            for (var i = 0; i < input.Length; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                var ch = input[i];
                if (ch != (byte)'%')
                {
                    builder.Append(ch);
                    continue;
                }

                if (i + 1 >= input.Length)
                {
                    throw new LythonRuntimeException("ValueError", "Invalid format string", span);
                }

                var directive = (char)input[++i];
                switch (directive)
                {
                    case '%':
                        builder.Append((byte)'%');
                        break;
                    case 'Y':
                        builder.AppendString(value.Year.ToString("0000", CultureInfo.InvariantCulture));
                        break;
                    case 'y':
                        builder.AppendString((value.Year % 100).ToString("00", CultureInfo.InvariantCulture));
                        break;
                    case 'm':
                        builder.AppendString(value.Month.ToString("00", CultureInfo.InvariantCulture));
                        break;
                    case 'd':
                        builder.AppendString(value.Day.ToString("00", CultureInfo.InvariantCulture));
                        break;
                    case 'H':
                        builder.AppendString(value.Hour.ToString("00", CultureInfo.InvariantCulture));
                        break;
                    case 'I':
                        var hour12 = value.Hour % 12;
                        builder.AppendString((hour12 == 0 ? 12 : hour12).ToString("00", CultureInfo.InvariantCulture));
                        break;
                    case 'p':
                        builder.AppendString(value.Hour < 12 ? "AM" : "PM");
                        break;
                    case 'M':
                        builder.AppendString(value.Minute.ToString("00", CultureInfo.InvariantCulture));
                        break;
                    case 'S':
                        builder.AppendString(value.Second.ToString("00", CultureInfo.InvariantCulture));
                        break;
                    case 'f':
                        builder.AppendString(((value.Ticks % TimeSpan.TicksPerSecond) / 10).ToString("000000", CultureInfo.InvariantCulture));
                        break;
                    case 'z':
                        builder.AppendString(timezone is null ? string.Empty : FormatCompactOffset(timezone.Offset));
                        break;
                    case 'Z':
                        if (timezone is not null) builder.Append(timezone.Name);
                        break;
                    case 'a':
                        builder.AppendString(displayWeekday.ToString("ddd", CultureInfo.InvariantCulture));
                        break;
                    case 'A':
                        builder.AppendString(displayWeekday.ToString("dddd", CultureInfo.InvariantCulture));
                        break;
                    case 'b':
                    case 'h':
                        builder.AppendString(value.ToString("MMM", CultureInfo.InvariantCulture));
                        break;
                    case 'B':
                        builder.AppendString(value.ToString("MMMM", CultureInfo.InvariantCulture));
                        break;
                    case 'j':
                        builder.AppendString(yearDay.ToString("000", CultureInfo.InvariantCulture));
                        break;
                    case 'w':
                        builder.AppendString(((mondayBasedWeekday + 1) % 7).ToString(CultureInfo.InvariantCulture));
                        break;
                    case 'u':
                        builder.AppendString((mondayBasedWeekday + 1).ToString(CultureInfo.InvariantCulture));
                        break;
                    case 'U':
                        builder.AppendString(WeekNumber(yearDay, mondayBasedWeekday, sundayFirst: true).ToString("00", CultureInfo.InvariantCulture));
                        break;
                    case 'W':
                        builder.AppendString(WeekNumber(yearDay, mondayBasedWeekday, sundayFirst: false).ToString("00", CultureInfo.InvariantCulture));
                        break;
                    case 'G':
                        builder.AppendString(ISOWeek.GetYear(value).ToString("0000", CultureInfo.InvariantCulture));
                        break;
                    case 'V':
                        builder.AppendString(ISOWeek.GetWeekOfYear(value).ToString("00", CultureInfo.InvariantCulture));
                        break;
                    case 'c':
                        builder.AppendString(displayWeekday.ToString("ddd", CultureInfo.InvariantCulture));
                        builder.Append((byte)' ');
                        builder.AppendString(value.ToString("MMM", CultureInfo.InvariantCulture));
                        builder.Append((byte)' ');
                        builder.AppendString(value.Day.ToString(CultureInfo.InvariantCulture).PadLeft(2, ' '));
                        builder.AppendString(value.ToString(" HH:mm:ss yyyy", CultureInfo.InvariantCulture));
                        break;
                    case 'x':
                        builder.AppendString(value.ToString("MM/dd/yy", CultureInfo.InvariantCulture));
                        break;
                    case 'X':
                        builder.AppendString(value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
                        break;
                    default:
                        throw new LythonRuntimeException("ValueError", "Invalid format string", span);
                }
            }

            context.CheckExecution(span);
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }

    private static object SubtractDateTimes(PyDateTime left, PyDateTime right, LythonSourceSpan span)
    {
        if ((left.TzInfo is null) != (right.TzInfo is null))
        {
            throw new LythonRuntimeException("TypeError", "can't subtract offset-naive and offset-aware datetimes", span);
        }

        return left.TzInfo is null
            ? new PyTimedelta(left.Value - right.Value)
            : new PyTimedelta(new BigInteger((left.ToUtcTicks() - right.ToUtcTicks()) / 10));
    }

    private static int CompareDateTimes(PyDateTime left, PyDateTime right, LythonSourceSpan span)
    {
        if ((left.TzInfo is null) != (right.TzInfo is null))
        {
            throw new LythonRuntimeException("TypeError", "can't compare offset-naive and offset-aware datetimes", span);
        }

        return left.TzInfo is null
            ? left.Value.CompareTo(right.Value)
            : left.ToUtcTicks().CompareTo(right.ToUtcTicks());
    }

    private static int CompareTimes(PyTime left, PyTime right, LythonSourceSpan span)
    {
        if ((left.TzInfo is null) != (right.TzInfo is null))
        {
            throw new LythonRuntimeException("TypeError", "can't compare offset-naive and offset-aware times", span);
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
        => value.Ticks - offset.Ticks;

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
