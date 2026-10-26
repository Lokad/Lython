using System.Globalization;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDateTimeOps
{
    // ISO inputs are already UTF-8. Only the six significant fractional digits
    // become values; arbitrarily long tails are validated without copying them.
    private static DateOnly ParseIsoDate(ReadOnlySpan<byte> text, ref int cursor, LythonSourceSpan span)
    {
        var year = IsoDigits(text, ref cursor, 4);
        var extended = cursor < text.Length && text[cursor] == '-';
        if (extended) cursor++;
        if (cursor < text.Length && text[cursor] == 'W')
        {
            cursor++;
            var week = IsoDigits(text, ref cursor, 2);
            var day = 1;
            if (extended && cursor + 1 < text.Length && text[cursor] == '-' && IsIsoDigit(text[cursor + 1]))
            {
                cursor++;
                day = IsoDigits(text, ref cursor, 1);
            }
            else if (!extended && cursor < text.Length && IsIsoDigit(text[cursor]))
                day = IsoDigits(text, ref cursor, 1);
            if (year is < 1 or > 9999 || week < 1 || week > ISOWeek.GetWeeksInYear(year) || day is < 1 or > 7)
                throw new FormatException("Invalid ISO week date.");
            try { return DateFromIsoCalendarParts(year, week, day); }
            catch (ArgumentOutOfRangeException ex) { throw new FormatException("Invalid ISO week date.", ex); }
        }
        var month = IsoDigits(text, ref cursor, 2);
        if (extended) IsoExpect(text, ref cursor, (byte)'-');
        return CreateParsedDate(year, month, IsoDigits(text, ref cursor, 2), span);
    }

    private static PyTime ParseTime(PyString input, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        var text = input.Utf8Bytes.Span;
        var cursor = text.Length > 0 && text[0] == 'T' ? 1 : 0;
        var time = ParseIsoClock(text, ref cursor, span, context, out var zone);
        if (cursor != text.Length) throw new FormatException("Invalid ISO time string.");
        return new PyTime(time, zone);
    }

    private static PyDateTime ParseDateTime(PyString input, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        var text = input.Utf8Bytes.Span;
        var cursor = 0;
        var date = ParseIsoDate(text, ref cursor, span);
        if (cursor == text.Length) return new PyDateTime(date.ToDateTime(TimeOnly.MinValue));
        // Python allows any single Unicode scalar as the separator.
        if (Rune.DecodeFromUtf8(text[cursor..], out _, out var width) != System.Buffers.OperationStatus.Done)
            throw new FormatException("Invalid ISO datetime separator.");
        cursor += width;
        var time = ParseIsoClock(text, ref cursor, span, context, out var zone);
        if (cursor != text.Length) throw new FormatException("Invalid ISO datetime string.");
        return new PyDateTime(date.ToDateTime(time), zone);
    }

    private static TimeOnly ParseIsoClock(ReadOnlySpan<byte> text, ref int cursor, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, out PyTimezone? zone)
    {
        ReadIsoClock(text, ref cursor, span, context, out var hour, out var minute, out var second, out var micros);
        var time = CreateParsedTime(hour, minute, second, micros, span);
        zone = null;
        if (cursor < text.Length && text[cursor] == 'Z')
        {
            cursor++;
            zone = PyTimezone.Utc;
        }
        else if (cursor < text.Length && text[cursor] is (byte)'+' or (byte)'-')
        {
            var sign = text[cursor++] == '-' ? -1L : 1L;
            ReadIsoClock(text, ref cursor, span, context, out hour, out minute, out second, out micros);
            // CPython canonicalizes a zero H:M:S offset even with a fraction.
            if (hour == 0 && minute == 0 && second == 0) zone = PyTimezone.Utc;
            else
            {
                var ticks = sign * ((hour * 3600L + minute * 60L + second) * TimeSpan.TicksPerSecond + micros * 10L);
                if (Math.Abs(ticks) >= TimeSpan.TicksPerDay)
                    throw new LythonRuntimeException("ValueError", "offset must be strictly between -24h and +24h", span);
                zone = OwnDateTimeValue(new PyTimezone(TimeSpan.FromTicks(ticks)), context, span);
            }
        }
        return time;
    }

    private static void ReadIsoClock(ReadOnlySpan<byte> text, ref int cursor, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, out int hour, out int minute, out int second, out int micros)
    {
        hour = IsoDigits(text, ref cursor, 2);
        minute = second = micros = 0;
        var extended = cursor < text.Length && text[cursor] == ':';
        if (extended || cursor < text.Length && IsIsoDigit(text[cursor]))
        {
            if (extended) cursor++;
            minute = IsoDigits(text, ref cursor, 2);
            if (extended && cursor < text.Length && text[cursor] == ':' ||
                !extended && cursor < text.Length && IsIsoDigit(text[cursor]))
            {
                if (extended) cursor++;
                second = IsoDigits(text, ref cursor, 2);
            }
        }
        if (cursor < text.Length && text[cursor] is (byte)'.' or (byte)',')
        {
            cursor++;
            var start = cursor;
            while (cursor < text.Length && IsIsoDigit(text[cursor]))
            {
                if ((cursor & 1023) == 0) context.CheckExecution(span);
                if (cursor - start < 6) micros = micros * 10 + text[cursor] - '0';
                cursor++;
            }
            if (cursor == start) throw new FormatException("Invalid ISO fraction.");
            for (var i = cursor - start; i < 6; i++) micros *= 10;
        }
    }

    private static bool IsIsoDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';

    private static int IsoDigits(ReadOnlySpan<byte> text, ref int cursor, int count)
    {
        if (text.Length - cursor < count) throw new FormatException("Incomplete ISO component.");
        var value = 0;
        for (var i = 0; i < count; i++)
        {
            if (!IsIsoDigit(text[cursor])) throw new FormatException("Invalid ISO digit.");
            value = value * 10 + text[cursor++] - '0';
        }
        return value;
    }

    private static void IsoExpect(ReadOnlySpan<byte> text, ref int cursor, byte value)
    {
        if (cursor >= text.Length || text[cursor++] != value) throw new FormatException("Invalid ISO separator.");
    }

    private static LythonRuntimeException InvalidIsoFormat(PyString text, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        // The diagnostic retains the original text; admit its CLR construction
        // and retained message before decoding, including replacement overlap.
        var bytes = checked(512L + 6L * text.Utf8Bytes.Length);
        context.MemoryGovernor.Reserve(bytes, span);
        context.MemoryGovernor.Commit(bytes);
        return new LythonRuntimeException("ValueError", $"Invalid isoformat string: '{text.AsString()}'", span);
    }
}
