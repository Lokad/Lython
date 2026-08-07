using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDateTimeOps
{
    public static PyDateTime ParseStrptime(string text, string format, LythonSourceSpan span)
    {
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

            if (ch != '%')
            {
                pattern.Append(Regex.Escape(ch.ToString()));
                continue;
            }

            if (i + 1 >= format.Length)
            {
                throw new LythonRuntimeException("ValueError", "strptime format string cannot end with '%'.", span);
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
            pattern.Append("(?<").Append(groupName).Append('>').Append(StrptimeDirectivePattern(directive, span)).Append(')');
        }

        pattern.Append('$');
        var match = Regex.Match(text, pattern.ToString(), RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            throw new FormatException("time data does not match format.");
        }

        string? Capture(char directive)
            => groups.TryGetValue(directive, out var group) ? match.Groups[group].Value : null;

        static int ParseInt(string? value, int fallback)
            => string.IsNullOrEmpty(value) ? fallback : int.Parse(value, CultureInfo.InvariantCulture);

        var year = ParseYear(Capture('Y'), Capture('y'));
        var month = ParseMonth(Capture('m'), Capture('b'), Capture('B'));
        var day = ParseInt(Capture('d'), 1);

        if (Capture('G') is not null || Capture('V') is not null || Capture('u') is not null)
        {
            if (Capture('G') is null || Capture('V') is null || Capture('u') is null)
            {
                throw new FormatException("ISO year, week, and weekday directives must be used together.");
            }

            var isoDate = DateFromIsoCalendarParts(
                int.Parse(Capture('G').RequireNotNull(), CultureInfo.InvariantCulture),
                int.Parse(Capture('V').RequireNotNull(), CultureInfo.InvariantCulture),
                int.Parse(Capture('u').RequireNotNull(), CultureInfo.InvariantCulture));
            year = isoDate.Year;
            month = isoDate.Month;
            day = isoDate.Day;
        }
        else if (Capture('j') is { } dayOfYearText)
        {
            var dayOfYear = int.Parse(dayOfYearText, CultureInfo.InvariantCulture);
            var date = new DateTime(year, 1, 1).AddDays(dayOfYear - 1);
            if (date.Year != year)
            {
                throw new FormatException("day of year out of range.");
            }

            month = date.Month;
            day = date.Day;
        }

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
        var value = new DateTime(year, month, day, hour, minute, second, microsecond / 1000, DateTimeKind.Unspecified)
            .AddTicks((microsecond % 1000) * 10L);

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

    private static string StrptimeDirectivePattern(char directive, LythonSourceSpan span)
    {
        return directive switch
        {
            'Y' => @"\d{1,4}",
            'y' => @"\d{2}",
            'm' => @"\d{1,2}",
            'd' => @"\d{1,2}",
            'H' => @"\d{1,2}",
            'I' => @"\d{1,2}",
            'p' => @"AM|PM|am|pm",
            'M' => @"\d{1,2}",
            'S' => @"\d{1,2}",
            'f' => @"\d{1,6}",
            'z' => @"Z|[+-]\d{2}:?\d{2}",
            'Z' => @"[A-Za-z_][A-Za-z0-9_+-]*",
            'a' or 'A' => @"[A-Za-z]+",
            'b' or 'h' or 'B' => @"[A-Za-z]+",
            'j' => @"\d{1,3}",
            'w' => @"\d",
            'u' => @"\d",
            'U' or 'W' => @"\d{1,2}",
            'G' => @"\d{1,4}",
            'V' => @"\d{1,2}",
            _ => throw new LythonRuntimeException("ValueError", $"strptime directive '%{directive}' is not supported in Lython yet.", span)
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

    private static PyTime ParseTime(string text)
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
            return new PyTime(ParseIsoTime(body), new PyTimezone(offset));
        }

        return new PyTime(ParseIsoTime(text));
    }

    private static PyDateTime ParseDateTime(string text)
    {
        text = NormalizeIsoText(text);
        if (text.EndsWith("Z", StringComparison.Ordinal))
        {
            text = text[..^1] + "+00:00";
        }

        if (TryParseTrailingOffset(text, out var body, out var offset))
        {
            return ParseIsoDateTime(body, new PyTimezone(offset));
        }

        return ParseIsoDateTime(text, null);
    }

    private static DateOnly ParseIsoDate(string text)
    {
        var calendarMatch = CalendarDateRegex.Match(text);
        if (calendarMatch.Success)
        {
            var basic = calendarMatch.Groups["basicYear"].Success;
            var year = ParseIsoComponent(calendarMatch, basic ? "basicYear" : "year");
            var month = ParseIsoComponent(calendarMatch, basic ? "basicMonth" : "month");
            var day = ParseIsoComponent(calendarMatch, basic ? "basicDay" : "day");
            try
            {
                return new DateOnly(year, month, day);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new FormatException("Invalid ISO calendar date.", ex);
            }
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

    private static PyDateTime ParseIsoDateTime(string text, PyTimezone? timezone)
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
                date = ParseIsoDate(text[..dateLength]);
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

            var time = ParseIsoTime(text[(dateLength + 1)..]);
            return new PyDateTime(date.ToDateTime(time), timezone);
        }

        throw new FormatException("Invalid ISO datetime string.");
    }

    private static TimeOnly ParseIsoTime(string text)
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
                fraction.Length > 6 || fraction.Any(ch => !char.IsAsciiDigit(ch)) ||
                fraction.Length > 0 && digits.Length != 6)
            {
                throw new FormatException("Invalid ISO time string.");
            }

            hour = int.Parse(digits[..2], CultureInfo.InvariantCulture);
            minute = digits.Length >= 4 ? int.Parse(digits.Substring(2, 2), CultureInfo.InvariantCulture) : 0;
            second = digits.Length == 6 ? int.Parse(digits.Substring(4, 2), CultureInfo.InvariantCulture) : 0;
            microsecond = fraction.Length == 0 ? 0 : int.Parse(fraction.PadRight(6, '0'), CultureInfo.InvariantCulture);
        }

        try
        {
            return new TimeOnly(hour, minute, second, microsecond / 1000, microsecond % 1000);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new FormatException("Invalid ISO time string.", ex);
        }
    }

    private static int ParseIsoComponent(Match match, string groupName)
        => int.Parse(match.Groups[groupName].Value, CultureInfo.InvariantCulture);

    private static int ParseIsoFraction(Group group)
        => group.Success ? int.Parse(group.Value.PadRight(6, '0'), CultureInfo.InvariantCulture) : 0;

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
