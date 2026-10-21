using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDateTimeOps
{
    public static (PyDateTime Value, bool HasNumericOffset, bool HasZoneName) ParseStrptime(PyString input, PyString pattern, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        // Fund CLR input/captures and composite-format growth before conversion.
        // All are scratch, returned independently of CLR collection.
        using var textStorage = context.MemoryGovernor.ReserveTemporary(checked(2048L + 6L * input.Utf8Bytes.Length + 64L * pattern.Utf8Bytes.Length), span);
        context.CheckExecution(span);
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

        if (leapYearFix)
        {
            resolved = CreateParsedDate(1900, resolved.Value.Month, resolved.Value.Day, span);
        }

        year = resolved.Value.Year;
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
        var parsedDate = CreateParsedDate(year, month, day, span);
        var parsedTime = CreateParsedTime(hour, minute, second, microsecond, span);
        var value = parsedDate.ToDateTime(parsedTime);

        var timezone = ParseStrptimeTimezone(Capture('z'), Capture('Z'), span);
        return (new PyDateTime(value, timezone), Capture('z') is not null, Capture('Z') is not null);
    }

    private static string ExpandCompositeStrptimeDirectives(string format, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var expanded = new StringBuilder(format.Length);
        for (var index = 0; index < format.Length; index++)
        {
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

    private static PyTime ParseTime(string text, LythonSourceSpan span)
    {
        text = NormalizeIsoText(text);
        if (text.StartsWith('T'))
        {
            text = text[1..];
        }

        if (text.EndsWith("Z", StringComparison.Ordinal))
        {
            text = text[..^1] + "+00:00";
        }

        if (TryParseTrailingOffset(text, out var body, out var offset))
        {
            return new PyTime(ParseIsoTime(body, span), new PyTimezone(offset));
        }

        return new PyTime(ParseIsoTime(text, span));
    }

    private static PyDateTime ParseDateTime(string text, LythonSourceSpan span)
    {
        text = NormalizeIsoText(text);
        if (text.EndsWith("Z", StringComparison.Ordinal))
        {
            text = text[..^1] + "+00:00";
        }

        if (TryParseTrailingOffset(text, out var body, out var offset))
        {
            return ParseIsoDateTime(body, new PyTimezone(offset), span);
        }

        return ParseIsoDateTime(text, null, span);
    }

    private static DateOnly ParseIsoDate(string text, LythonSourceSpan span)
    {
        var calendarMatch = CalendarDateRegex.Match(text);
        if (calendarMatch.Success)
        {
            var basic = calendarMatch.Groups["basicYear"].Success;
            var year = ParseIsoComponent(calendarMatch, basic ? "basicYear" : "year");
            var month = ParseIsoComponent(calendarMatch, basic ? "basicMonth" : "month");
            var day = ParseIsoComponent(calendarMatch, basic ? "basicDay" : "day");
            return CreateParsedDate(year, month, day, span);
        }

        var weekMatch = WeekDateRegex.Match(text);
        if (weekMatch.Success)
        {
            var basic = weekMatch.Groups["basicYear"].Success;
            var year = ParseIsoComponent(weekMatch, basic ? "basicYear" : "year");
            var week = ParseIsoComponent(weekMatch, basic ? "basicWeek" : "week");
            var weekdayGroup = weekMatch.Groups[basic ? "basicWeekday" : "weekday"];
            var weekday = weekdayGroup.Success
                ? int.Parse(weekdayGroup.Value, CultureInfo.InvariantCulture)
                : 1;
            if (weekday is < 1 or > 7 || week is < 1 or > 53)
            {
                throw new FormatException("Invalid ISO week date.");
            }

            try
            {
                var dayOfWeek = weekday == 7 ? DayOfWeek.Sunday : (DayOfWeek)weekday;
                var value = DateOnly.FromDateTime(ISOWeek.ToDateTime(year, week, dayOfWeek));
                if (ISOWeek.GetYear(value.ToDateTime(TimeOnly.MinValue)) != year ||
                    ISOWeek.GetWeekOfYear(value.ToDateTime(TimeOnly.MinValue)) != week)
                {
                    throw new FormatException("Invalid ISO week date.");
                }

                return value;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new FormatException("Invalid ISO week date.", ex);
            }
        }

        throw new FormatException("Invalid ISO date string.");
    }

    private static PyDateTime ParseIsoDateTime(string text, PyTimezone? timezone, LythonSourceSpan span)
    {
        foreach (var dateLength in IsoDatePrefixLengths)
        {
            if (text.Length < dateLength)
            {
                continue;
            }

            DateOnly date;
            try
            {
                date = ParseIsoDate(text[..dateLength], span);
            }
            catch (FormatException)
            {
                continue;
            }

            if (text.Length == dateLength)
            {
                return new PyDateTime(date.ToDateTime(TimeOnly.MinValue), timezone);
            }

            if (text[dateLength] is not ('T' or ' '))
            {
                continue;
            }

            var time = ParseIsoTime(text[(dateLength + 1)..], span);
            return new PyDateTime(date.ToDateTime(time), timezone);
        }

        throw new FormatException("Invalid ISO datetime string.");
    }

    private static TimeOnly ParseIsoTime(string text, LythonSourceSpan span)
    {
        var match = ExtendedTimeRegex.Match(text);
        int hour;
        int minute;
        int second;
        int microsecond;
        if (match.Success)
        {
            hour = ParseIsoComponent(match, "hour");
            minute = match.Groups["minute"].Success ? ParseIsoComponent(match, "minute") : 0;
            second = match.Groups["second"].Success ? ParseIsoComponent(match, "second") : 0;
            microsecond = ParseIsoFraction(match.Groups["fraction"]);
        }
        else
        {
            var fractionSeparator = text.IndexOfAny(['.', ',']);
            var digits = fractionSeparator < 0 ? text : text[..fractionSeparator];
            var fraction = fractionSeparator < 0 ? string.Empty : text[(fractionSeparator + 1)..];
            if (digits.Length is not (2 or 4 or 6) || !digits.All(char.IsAsciiDigit) ||
                fraction.Any(ch => !char.IsAsciiDigit(ch)) ||
                fraction.Length > 0 && digits.Length != 6)
            {
                throw new FormatException("Invalid ISO time string.");
            }

            hour = int.Parse(digits[..2], CultureInfo.InvariantCulture);
            minute = digits.Length >= 4 ? int.Parse(digits.Substring(2, 2), CultureInfo.InvariantCulture) : 0;
            second = digits.Length == 6 ? int.Parse(digits.Substring(4, 2), CultureInfo.InvariantCulture) : 0;
            microsecond = ParseIsoFractionText(fraction);
        }

        return CreateParsedTime(hour, minute, second, microsecond, span);
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
    private static int ParseIsoComponent(Match match, string groupName)
        => int.Parse(match.Groups[groupName].Value, CultureInfo.InvariantCulture);

    private static int ParseIsoFraction(Group group)
        => group.Success ? ParseIsoFractionText(group.Value) : 0;

    private static int ParseIsoFractionText(string fraction)
        // Extra digits truncate like CPython instead of failing the shape.
        => fraction.Length == 0 ? 0 : int.Parse(fraction.Length > 6 ? fraction[..6] : fraction.PadRight(6, '0'), CultureInfo.InvariantCulture);

    private static bool TryParseTrailingOffset(string text, out string body, out TimeSpan offset)
    {
        for (var i = text.Length - 1; i > 0; i--)
        {
            if (text[i] is not ('+' or '-'))
            {
                continue;
            }

            if (TryParseOffsetText(text[i..], out offset))
            {
                body = text[..i];
                return true;
            }
        }

        body = text;
        offset = default;
        return false;
    }

    private static bool TryParseOffsetText(string text, out TimeSpan offset)
    {
        if (string.Equals(text, "Z", StringComparison.OrdinalIgnoreCase))
        {
            offset = TimeSpan.Zero;
            return true;
        }

        var match = OffsetTextRegex.Match(text);
        if (!match.Success)
        {
            offset = default;
            return false;
        }

        var hours = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
        var minutes = int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture);
        var seconds = match.Groups["second"].Success
            ? int.Parse(match.Groups["second"].Value, CultureInfo.InvariantCulture)
            : 0;
        var microseconds = match.Groups["fraction"].Success
            ? int.Parse(match.Groups["fraction"].Value.PadRight(6, '0'), CultureInfo.InvariantCulture)
            : 0;
        if (hours > 23 || minutes > 59 || seconds > 59)
        {
            offset = default;
            return false;
        }

        var sign = match.Groups["sign"].Value == "-" ? -1L : 1L;
        var ticks = (hours * 3600L + minutes * 60L + seconds) * TimeSpan.TicksPerSecond + microseconds * 10L;
        offset = TimeSpan.FromTicks(sign * ticks);
        return true;
    }

    private static string NormalizeIsoText(string text)
        => text.Replace(',', '.');

}
