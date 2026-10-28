using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDateTimeOps
{
    public static (PyDateTime Value, bool HasNumericOffset, bool HasZoneName) ParseStrptime(PyString input, PyString pattern, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var fields = ParseStrptimeFields(input, pattern, span, context);
        var date = CreateParsedDate(fields.Year, fields.Month, fields.Day, span);
        var time = CreateParsedTime(fields.Hour, fields.Minute, fields.Second, fields.Microsecond, span);
        return (new PyDateTime(date.ToDateTime(time), fields.Timezone), fields.HasNumericOffset, fields.HasZoneName);
    }

    internal readonly record struct ParsedTimeFields(int Year, int Month, int Day, int Hour, int Minute,
        int Second, int Microsecond, int Weekday, int YearDay, PyTimezone? Timezone, bool HasNumericOffset, bool HasZoneName);

    internal static ParsedTimeFields ParseStrptimeFields(PyString input, PyString pattern, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        // Fund CLR input/captures and composite-format growth before conversion.
        // All are scratch, returned independently of CLR collection.
        using var textStorage = context.MemoryGovernor.ReserveTemporary(checked(2048L + 6L * input.Utf8Bytes.Length + 64L * pattern.Utf8Bytes.Length), span);
        context.CheckExecution(span);
        try
        {
            var text = input.AsString();
            var originalFormat = pattern.AsString();
            var format = ExpandCompositeStrptimeDirectives(originalFormat, context, span);
            var captures = MatchStrptime(text, format, originalFormat, context, span);
            string? Capture(char directive)
                => captures.TryGetValue(directive, out var value) ? value : null;

            static int ParseInt(string? value, int fallback)
                => string.IsNullOrEmpty(value) ? fallback : int.Parse(value, CultureInfo.InvariantCulture);

            var year = ParseYear(Capture('Y'), Capture('y'));
            var month = ParseMonth(Capture('m'), Capture('b'), Capture('B'));
            var day = ParseInt(Capture('d'), 1);

            var hasYear = Capture('Y') is not null || Capture('y') is not null;
            var hasIsoYear = Capture('G') is not null;
            var hasIsoWeek = Capture('V') is not null;
            var hasJulian = Capture('j') is not null;

            // Week dates resolve through a Monday-based weekday and an optional
            // Sunday- or Monday-start week number; later directives win like the
            // CPython group walk. Weekday names stay unresolved (own slice).
            int? weekday = null;
            int? weekOfYear = null;
            var weekStartsMonday = false;
            foreach (var directive in captures.Keys)
            {
                if (directive == 'w' && Capture('w') is { } sundayBased)
                {
                    var sundayZero = int.Parse(sundayBased, CultureInfo.InvariantCulture);
                    weekday = sundayZero == 0 ? 6 : sundayZero - 1;
                }
                else if (directive == 'u' && Capture('u') is { } mondayBased)
                {
                    weekday = int.Parse(mondayBased, CultureInfo.InvariantCulture) - 1;
                }
                else if (directive == 'a' && Capture('a') is { } abbreviatedDay)
                {
                    weekday = WeekdayFromName(abbreviatedDay, abbreviated: true);
                }
                else if (directive == 'A' && Capture('A') is { } fullDay)
                {
                    weekday = WeekdayFromName(fullDay, abbreviated: false);
                }
                else if (directive == 'U' && Capture('U') is { } sundayWeek)
                {
                    weekOfYear = int.Parse(sundayWeek, CultureInfo.InvariantCulture);
                    weekStartsMonday = false;
                }
                else if (directive == 'W' && Capture('W') is { } mondayWeek)
                {
                    weekOfYear = int.Parse(mondayWeek, CultureInfo.InvariantCulture);
                    weekStartsMonday = true;
                }
            }

            if (hasIsoYear)
            {
                if (hasJulian)
                {
                    throw new FormatException("Day of the year directive '%j' is not compatible with ISO year directive '%G'. Use '%Y' instead.");
                }

                if (!hasIsoWeek || weekday is null)
                {
                    throw new FormatException("ISO year directive '%G' must be used with the ISO week directive '%V' and a weekday directive ('%A', '%a', '%w', or '%u').");
                }
            }
            else if (hasIsoWeek)
            {
                if (!hasYear || weekday is null)
                {
                    throw new FormatException("ISO week directive '%V' must be used with the ISO year directive '%G' and a weekday directive ('%A', '%a', '%w', or '%u').");
                }

                throw new FormatException("ISO week directive '%V' is incompatible with the year directive '%Y'. Use the ISO year '%G' instead.");
            }

            var computationYear = year;
            var leapYearFix = !hasYear && month == 2 && day == 29;
            if (leapYearFix)
            {
                computationYear = 1904;
            }

            int? julian = hasJulian ? int.Parse(Capture('j').RequireNotNull(), CultureInfo.InvariantCulture) : null;
            DateOnly? resolved = null;
            if (julian is null && weekday is not null)
            {
                if (weekOfYear is not null)
                {
                    julian = CalcJulianFromWeek(computationYear, weekOfYear.Value, weekday.Value, weekStartsMonday, span);
                }
                else if (hasIsoYear && hasIsoWeek)
                {
                    resolved = DateFromIsoCalendarValue(
                        new BigInteger(int.Parse(Capture('G').RequireNotNull(), CultureInfo.InvariantCulture)),
                        new BigInteger(int.Parse(Capture('V').RequireNotNull(), CultureInfo.InvariantCulture)),
                        new BigInteger(weekday.Value + 1),
                        span,
                        context);
                }
            }

            if (resolved is null)
            {
                if (julian is null)
                {
                    resolved = CreateParsedDate(computationYear, month, day, span);
                }
                else
                {
                    var start = CreateParsedDate(computationYear, 1, 1, span);
                    resolved = DateFromOrdinalValue(new BigInteger(start.DayNumber + julian.Value), span, context);
                }
            }

            year = leapYearFix ? 1900 : resolved.Value.Year;
            month = resolved.Value.Month;
            day = resolved.Value.Day;

            var hour = ParseInt(Capture('H'), 0);
            if (Capture('I') is { } hour12Text)
            {
                hour = int.Parse(hour12Text, CultureInfo.InvariantCulture) % 12;
                if (string.Equals(Capture('p'), "PM", StringComparison.OrdinalIgnoreCase))
                {
                    hour += 12;
                }
            }

            var minute = ParseInt(Capture('M'), 0);
            var second = ParseInt(Capture('S'), 0);
            var microsecond = ParseMicrosecond(Capture('f'));
            var timezone = ParseStrptimeTimezone(Capture('z'), Capture('Z'), span);
            return new ParsedTimeFields(year, month, day, hour, minute, second, microsecond,
                weekday ?? ((int)resolved.Value.DayOfWeek + 6) % 7,
                hasJulian ? julian!.Value : resolved.Value.DayOfYear,
                timezone, Capture('z') is not null, Capture('Z') is not null);
        }
        catch (Exception error) when (error is FormatException || error is LythonRuntimeException { Attribution: null })
        {
            // The admitted estimate also covers formatted input/format errors.
            // A guest can retain the error after parsing scratch ends.
            textStorage.Commit(checked(32L + 2L * error.Message.Length));
            throw;
        }
    }

    private static string ExpandCompositeStrptimeDirectives(string format, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var expanded = new StringBuilder(format.Length);
        for (var index = 0; index < format.Length; index++)
        {
            if ((index & 1023) == 0) context.CheckExecution(span);
            if (format[index] != '%' || index + 1 >= format.Length)
            {
                expanded.Append(format[index]);
                continue;
            }

            var directive = format[index + 1];
            switch (directive)
            {
                case '%':
                    expanded.Append("%%");
                    index++;
                    break;
                case 'c':
                    expanded.Append("%a %b %d %H:%M:%S %Y");
                    index++;
                    break;
                case 'x':
                    expanded.Append("%m/%d/%y");
                    index++;
                    break;
                case 'X':
                    expanded.Append("%H:%M:%S");
                    index++;
                    break;
                default:
                    expanded.Append('%').Append(directive);
                    index++;
                    break;
            }
        }

        return expanded.ToString();
    }

    private static int ParseYear(string? yearText, string? shortYearText)
    {
        if (!string.IsNullOrEmpty(yearText))
        {
            return int.Parse(yearText, CultureInfo.InvariantCulture);
        }

        if (string.IsNullOrEmpty(shortYearText))
        {
            return 1900;
        }

        var shortYear = int.Parse(shortYearText, CultureInfo.InvariantCulture);
        return shortYear <= 68 ? 2000 + shortYear : 1900 + shortYear;
    }

    private static int ParseMonth(string? monthText, string? abbreviatedName, string? fullName)
    {
        if (!string.IsNullOrEmpty(monthText))
        {
            return int.Parse(monthText, CultureInfo.InvariantCulture);
        }

        var name = abbreviatedName ?? fullName;
        if (string.IsNullOrEmpty(name))
        {
            return 1;
        }

        for (var i = 1; i <= 12; i++)
        {
            var date = new DateTime(2000, i, 1);
            if (string.Equals(name, date.ToString("MMM", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, date.ToString("MMMM", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        throw new FormatException("month name is not recognized.");
    }

    private static int ParseMicrosecond(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return int.Parse(text.PadRight(6, '0'), CultureInfo.InvariantCulture);
    }

    private static PyTimezone? ParseStrptimeTimezone(string? offsetText, string? nameText, LythonSourceSpan span)
    {
        if (!string.IsNullOrEmpty(offsetText))
        {
            var offset = ParseStrptimeOffset(offsetText, span);
            ValidateTimezoneOffset(new BigInteger(offset.Ticks / 10), span);
            return new PyTimezone(offset);
        }

        return nameText is not null &&
            (string.Equals(nameText, "UTC", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(nameText, "GMT", StringComparison.OrdinalIgnoreCase))
            ? PyTimezone.Utc
            : null;
    }

    private static TimeSpan ParseStrptimeOffset(string text, LythonSourceSpan span)
    {
        // The directive shape already constrains minutes, seconds and the
        // fraction; inconsistent colons and the non-numeric seconds slice
        // report exactly like CPython.
        if (text == "Z")
        {
            return TimeSpan.Zero;
        }

        var negative = text[0] == '-';
        var hours = int.Parse(text.Substring(1, 2), CultureInfo.InvariantCulture);
        int minutes;
        string rest;
        if (text[3] == ':')
        {
            minutes = int.Parse(text.Substring(4, 2), CultureInfo.InvariantCulture);
            rest = text.Length > 6 ? text[6..] : string.Empty;
            if (rest.Length != 0 && !rest.StartsWith(':'))
            {
                throw new FormatException($"Inconsistent use of : in {text}");
            }

            rest = rest.Length == 0 ? string.Empty : rest[1..];
        }
        else
        {
            minutes = int.Parse(text.Substring(3, 2), CultureInfo.InvariantCulture);
            rest = text.Length > 5 ? text[5..] : string.Empty;
            if (rest.Length != 0 && rest[0] == ':')
            {
                throw new LythonRuntimeException("ValueError", $"invalid literal for int() with base 10: '{text.Substring(5, Math.Min(2, text.Length - 5))}'", span);
            }
        }

        var seconds = 0;
        var microseconds = 0;
        if (rest.Length != 0)
        {
            seconds = int.Parse(rest.Substring(0, 2), CultureInfo.InvariantCulture);
            if (rest.Length > 2)
            {
                microseconds = int.Parse(rest.Substring(3).PadRight(6, '0'), CultureInfo.InvariantCulture);
            }
        }

        var ticks = ((hours * 3600L + minutes * 60L + seconds) * TimeSpan.TicksPerSecond) + microseconds * 10L;
        return TimeSpan.FromTicks(negative ? -ticks : ticks);
    }

    private static void ValidateTimezoneOffset(BigInteger totalMicroseconds, LythonSourceSpan span)
    {
        if (totalMicroseconds <= -86_400_000_000 || totalMicroseconds >= 86_400_000_000)
        {
            throw new LythonRuntimeException("ValueError", $"offset must be a timedelta strictly between -timedelta(hours=24) and timedelta(hours=24), not {RenderOffsetDelta(totalMicroseconds)}.", span);
        }
    }

    internal static string RenderOffsetDelta(BigInteger totalMicroseconds)
    {
        // Floor-based normalization mirrors timedelta repr for the range text.
        var days = totalMicroseconds / 86_400_000_000;
        var remainder = totalMicroseconds % 86_400_000_000;
        if (remainder.Sign < 0)
        {
            days -= BigInteger.One;
            remainder += 86_400_000_000;
        }

        var seconds = remainder / 1_000_000;
        var microseconds = remainder % 1_000_000;
        var builder = new StringBuilder("datetime.timedelta(");
        var separator = string.Empty;
        if (!days.IsZero)
        {
            builder.Append("days=").Append(days.ToString(CultureInfo.InvariantCulture));
            separator = ", ";
        }

        if (!seconds.IsZero)
        {
            builder.Append(separator).Append("seconds=").Append(seconds.ToString(CultureInfo.InvariantCulture));
            separator = ", ";
        }

        if (!microseconds.IsZero)
        {
            builder.Append(separator).Append("microseconds=").Append(microseconds.ToString(CultureInfo.InvariantCulture));
        }

        if (separator.Length == 0)
        {
            builder.Append('0');
        }

        return builder.Append(')').ToString();
    }

    private static DateOnly CreateParsedDate(int year, int month, int day, LythonSourceSpan span)
    {
        // Well-shaped components fail with the construction range texts like
        // CPython instead of the generic malformed-string text.
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

        return new DateOnly(year, month, day);
    }

    private static TimeOnly CreateParsedTime(int hour, int minute, int second, int microsecond, LythonSourceSpan span)
    {
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

        return new TimeOnly(hour, minute, second, microsecond / 1000, microsecond % 1000);
    }

    private static readonly string[] AbbreviatedDayNames = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
    private static readonly string[] FullDayNames = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    private static int WeekdayFromName(string name, bool abbreviated)
    {
        // Tight directive shapes guarantee a hit; the scan stays defensive.
        var table = abbreviated ? AbbreviatedDayNames : FullDayNames;
        for (var index = 0; index < table.Length; index++)
        {
            if (string.Equals(name, table[index], StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return 0;
    }

    private static int CalcJulianFromWeek(int year, int weekOfYear, int dayOfWeek, bool weekStartsMonday, LythonSourceSpan span)
    {
        // Mirrors CPython _calc_julian_from_U_or_W with Monday-based weekdays.
        var start = CreateParsedDate(year, 1, 1, span);
        var firstWeekday = ((int)start.DayOfWeek + 6) % 7;
        if (!weekStartsMonday)
        {
            firstWeekday = (firstWeekday + 1) % 7;
            dayOfWeek = (dayOfWeek + 1) % 7;
        }

        var weekZeroLength = (7 - firstWeekday) % 7;
        return weekOfYear == 0
            ? 1 + dayOfWeek - firstWeekday
            : 1 + weekZeroLength + 7 * (weekOfYear - 1) + dayOfWeek;

    }
}
