using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDateTimeOps
{
    public static PyDateTime ParseStrptime(string text, string format, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var originalFormat = format;
        format = ExpandCompositeStrptimeDirectives(format);
        var pattern = new StringBuilder(format.Length * 3);
        var groups = new Dictionary<char, string>();
        var groupIndex = 0;
        pattern.Append('^');

        for (var i = 0; i < format.Length; i++)
        {
            var ch = format[i];
            if (char.IsWhiteSpace(ch))
            {
                while (i + 1 < format.Length && char.IsWhiteSpace(format[i + 1]))
                {
                    i++;
                }

                pattern.Append(@"\s+");
                continue;
            }

            if (ch != '%' || i + 1 >= format.Length)
            {
                pattern.Append(Regex.Escape(ch.ToString()));
                continue;
            }

            var directive = format[++i];
            if (directive == '%')
            {
                pattern.Append('%');
                continue;
            }

            var groupName = "g" + groupIndex.ToString(CultureInfo.InvariantCulture);
            groupIndex++;
            groups[directive] = groupName;
            pattern.Append("(?<").Append(groupName).Append('>').Append(StrptimeDirectivePattern(directive, originalFormat, span)).Append(')');
        }

        var match = Regex.Match(text, pattern.ToString(), RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            throw new FormatException($"time data '{text}' does not match format '{format}'");
        }

        if (match.Length != text.Length)
        {
            throw new FormatException($"unconverted data remains: {text[match.Length..]}");
        }

        string? Capture(char directive)
            => groups.TryGetValue(directive, out var group) ? match.Groups[group].Value : null;

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
        foreach (var directive in groups.Keys)
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

        var timezone = ParseStrptimeTimezone(Capture('z'), Capture('Z'));
        return new PyDateTime(value, timezone);
    }

    private static string ExpandCompositeStrptimeDirectives(string format)
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

    private static string StrptimeDirectivePattern(char directive, string format, LythonSourceSpan span)
    {
        // Numeric shapes mirror CPython TimeRE so out-of-shape values fail
        // matching (rather than construction) exactly like CPython.
        return directive switch
        {
            'Y' => @"\d\d\d\d",
            'y' => @"\d\d",
            'm' => @"1[0-2]|0[1-9]|[1-9]",
            'd' => @"3[0-1]|[1-2]\d|0[1-9]|[1-9]| [1-9]",
            'H' => @"2[0-3]|[0-1]\d|\d",
            'I' => @"1[0-2]|0[1-9]|[1-9]",
            'p' => @"AM|PM|am|pm",
            'M' => @"[0-5]\d|\d",
            'S' => @"6[0-1]|[0-5]\d|\d",
            'f' => @"[0-9]{1,6}",
            'z' => @"Z|[+-]\d{2}:?\d{2}",
            'Z' => @"[A-Za-z_][A-Za-z0-9_+-]*",
            'a' => @"Mon|Tue|Wed|Thu|Fri|Sat|Sun",
            'A' => @"Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday",
            'b' => @"Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec",
            'B' => @"January|February|March|April|May|June|July|August|September|October|November|December",
            'j' => @"36[0-6]|3[0-5]\d|[1-2]\d\d|0[1-9]\d|00[1-9]|[1-9]\d|0[1-9]|[1-9]",
            'w' => @"[0-6]",
            'u' => @"[1-7]",
            'U' or 'W' => @"5[0-3]|[0-4]\d|\d",
            'G' => @"\d\d\d\d",
            'V' => @"5[0-3]|[0-4]\d|\d",
            _ => throw new LythonRuntimeException("ValueError", $"'{directive}' is a bad directive in format '{format}'", span)
        };
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

    private static PyTimezone? ParseStrptimeTimezone(string? offsetText, string? nameText)
    {
        if (!string.IsNullOrEmpty(offsetText))
        {
            if (!TryParseOffsetText(offsetText, out var offset))
            {
                throw new FormatException("timezone offset is not recognized.");
            }

            return new PyTimezone(offset);
        }

        return nameText is not null &&
            (string.Equals(nameText, "UTC", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(nameText, "GMT", StringComparison.OrdinalIgnoreCase))
            ? PyTimezone.Utc
            : null;
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
