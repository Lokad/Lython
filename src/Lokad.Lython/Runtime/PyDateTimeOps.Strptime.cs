using System.Globalization;

namespace Lokad.Lython.Runtime;

internal static partial class PyDateTimeOps
{
    private readonly record struct StrptimeChoice(int NextFormat, int Text, int Alternative, char Directive, int MaximumLength = 0);

    // This is the strptime directive language, not a guest regex dialect. Only
    // ambiguous fields/whitespace need choice points; literals allocate nothing.
    private static Dictionary<char, string> MatchStrptime(string text, string format, string originalFormat,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        Span<bool> seen = stackalloc bool[128];
        seen.Clear();
        var choices = 0;
        for (var i = 0; i < format.Length; i++)
        {
            if ((i & 1023) == 0) context.CheckExecution(span);
            if (char.IsWhiteSpace(format[i]))
            {
                choices++;
                while (i + 1 < format.Length && char.IsWhiteSpace(format[i + 1]))
                { if ((++i & 1023) == 0) context.CheckExecution(span); }
            }
            else if (format[i] == '%')
            {
                if (i + 1 == format.Length) continue; // A trailing % is literal.
                var directive = format[++i];
                if (directive == '%') continue;
                if ("YymdHIpMSfzZaAbBjwuUWGV".IndexOf(directive) < 0)
                    throw new LythonRuntimeException("ValueError", $"'{directive}' is a bad directive in format '{originalFormat}'", span);
                if (seen[directive])
                    throw new LythonRuntimeException(LythonRuntime.ModuleException("re", "PatternError"),
                        "redefinition of group name '" + directive + "'", span);
                seen[directive] = true;
                choices++;
            }
        }

        using var storage = context.MemoryGovernor.ReserveTemporary(checked(2048L + 24L * choices), span);
        var frames = new StrptimeChoice[choices];
        var bounds = new Dictionary<char, (int Start, int Length)>();
        var depth = 0;
        var position = 0;
        var input = 0;
        while (true)
        {
            if ((position & 1023) == 0) context.CheckExecution(span);
            if (position == format.Length)
            {
                if (input != text.Length) throw new FormatException("unconverted data remains: " + text[input..]);
                context.CheckExecution(span);
                var captures = new Dictionary<char, string>();
                foreach (var (capturedDirective, value) in bounds)
                {
                    context.CheckExecution(span);
                    var field = text.Substring(value.Start, value.Length);
                    // Python accepts Unicode decimal digits in its numeric
                    // fields; CLR integer parsing needs their ASCII values.
                    if ("YydHMSzjwuUWGV".Contains(capturedDirective))
                        field = string.Create(field.Length, field, static (target, source) =>
                        {
                            for (var i = 0; i < source.Length; i++)
                                target[i] = char.IsDigit(source[i]) ? (char)('0' + (int)char.GetNumericValue(source[i])) : source[i];
                        });
                    captures.Add(capturedDirective, field);
                }
                return captures;
            }
            var directive = '\0';
            var literal = format[position++];
            var variable = char.IsWhiteSpace(literal);
            if (variable)
                while (position < format.Length && char.IsWhiteSpace(format[position]))
                { if ((position & 1023) == 0) context.CheckExecution(span); position++; }
            else if (literal == '%' && position < format.Length)
            {
                directive = format[position++];
                variable = directive != '%';
                literal = '%';
            }
            if (variable)
            {
                var frame = new StrptimeChoice(position, input, 0, directive);
                if (TryChoice(frame, out var length))
                {
                    frames[depth++] = frame with { MaximumLength = length };
                    input += length;
                    continue;
                }
            }
            else if (input < text.Length && char.ToLowerInvariant(literal) == char.ToLowerInvariant(text[input]))
            { input++; continue; }

            var resumed = false;
            while (depth > 0)
            {
                context.CheckExecution(span);
                var frame = frames[--depth];
                if (frame.Directive != '\0') bounds.Remove(frame.Directive);
                frame = frame with { Alternative = frame.Alternative + 1 };
                if (!TryChoice(frame, out var length)) continue;
                frames[depth++] = frame;
                position = frame.NextFormat;
                input = frame.Text + length;
                resumed = true;
                break;
            }
            if (!resumed) throw new FormatException($"time data '{text}' does not match format '{originalFormat}'");
        }

        bool TryChoice(StrptimeChoice frame, out int length)
        {
            context.CheckExecution(span);
            if (frame.MaximumLength > 0 && frame.Directive is '\0' or 'Z')
            {
                length = frame.MaximumLength - frame.Alternative;
                if (length <= 0) return false;
            }
            else if (frame.Directive == '\0')
            {
                var end = frame.Text;
                while (end < text.Length && char.IsWhiteSpace(text[end]))
                { if ((end & 1023) == 0) context.CheckExecution(span); end++; }
                length = end - frame.Text;
                if (length == 0) return false;
            }
            else
            {
                length = StrptimeFieldLength(text.AsSpan(frame.Text), frame.Directive, frame.Alternative, context, span);
                if (length == 0) return false;
            }
            if (frame.Directive != '\0') bounds[frame.Directive] = (frame.Text, length);
            return true;
        }
    }

    private static int StrptimeFieldLength(ReadOnlySpan<char> input, char directive, int alternative,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (directive == 'Z')
        {
            var count = 0;
            while (count < input.Length && (char.IsAsciiLetter(input[count]) || input[count] == '_' ||
                count > 0 && (char.IsAsciiDigit(input[count]) || input[count] is '+' or '-')))
            { if ((count & 1023) == 0) context.CheckExecution(span); count++; }
            return Math.Max(0, count - alternative);
        }
        if (directive is 'a' or 'A' or 'b' or 'B' or 'p')
        {
            var names = directive switch
            {
                'a' => "Mon|Tue|Wed|Thu|Fri|Sat|Sun",
                'A' => "Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday",
                'b' => "Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec",
                'B' => "January|February|March|April|May|June|July|August|September|October|November|December",
                _ => "AM|PM"
            };
            foreach (var range in names.AsSpan().Split('|'))
            {
                var name = names.AsSpan(range);
                if (input.StartsWith(name, StringComparison.OrdinalIgnoreCase) && alternative-- == 0) return name.Length;
            }
            return 0;
        }
        if (directive == 'z')
        {
            if (input.StartsWith("Z")) return alternative == 0 ? 1 : 0;
            if (input.Length < 5 || input[0] is not ('+' or '-') || !Digits(input.Slice(1, 2))) return 0;
            var minute = input[3] == ':' ? 4 : 3;
            if (input.Length < minute + 2 || input[minute] is < '0' or > '5' || !char.IsDigit(input[minute + 1])) return 0;
            var end = minute + 2;
            var second = end < input.Length && input[end] == ':' ? end + 1 : end;
            if (input.Length >= second + 2 && input[second] is >= '0' and <= '5' && char.IsDigit(input[second + 1]))
            {
                var secondsEnd = second + 2;
                if (secondsEnd < input.Length && input[secondsEnd] == '.')
                {
                    var fractionEnd = secondsEnd + 1;
                    while (fractionEnd < input.Length && fractionEnd <= secondsEnd + 6 && char.IsDigit(input[fractionEnd])) fractionEnd++;
                    for (var length = fractionEnd; length > secondsEnd + 1; length--)
                        if (alternative-- == 0) return length;
                }
                if (alternative-- == 0) return secondsEnd;
            }
            return alternative == 0 ? end : 0;
        }

        var maximum = directive switch { 'Y' or 'G' => 4, 'y' => 2, 'f' => 6, 'j' => 3, 'w' or 'u' => 1, _ => 2 };
        var minimum = directive is 'Y' or 'G' ? 4 : directive == 'y' ? 2 : 1;
        for (var length = maximum; length >= minimum; length--)
        {
            if (input.Length < length) continue;
            var value = input[..length];
            var valid = directive switch
            {
                'Y' or 'G' or 'y' => Digits(value),
                'f' => AsciiDigits(value),
                'm' or 'I' => length == 1 ? value[0] is >= '1' and <= '9'
                    : value[0] == '1' && value[1] is >= '0' and <= '2' || value[0] == '0' && value[1] is >= '1' and <= '9',
                'd' => length == 1 ? value[0] is >= '1' and <= '9'
                    : value[0] is '1' or '2' && char.IsDigit(value[1]) || value[0] == '3' && value[1] is '0' or '1'
                        || value[0] is '0' or ' ' && value[1] is >= '1' and <= '9',
                'H' => length == 1 ? char.IsDigit(value[0])
                    : value[0] is '0' or '1' && char.IsDigit(value[1]) || value[0] == '2' && value[1] is >= '0' and <= '3',
                'M' or 'S' => length == 1 ? char.IsDigit(value[0])
                    : value[0] is >= '0' and <= '5' && char.IsDigit(value[1]) || directive == 'S' && value[0] == '6' && value[1] is '0' or '1',
                'j' => length == 1 ? value[0] is >= '1' and <= '9' : length == 2
                    ? value[0] is >= '1' and <= '9' && char.IsDigit(value[1]) || value[0] == '0' && value[1] is >= '1' and <= '9'
                    : value[0] is '1' or '2' && Digits(value[1..]) || value[0] == '3' && value[1] is >= '0' and <= '5' && char.IsDigit(value[2])
                        || value.StartsWith("36") && value[2] is >= '0' and <= '6'
                        || value[0] == '0' && value[1] is >= '1' and <= '9' && char.IsDigit(value[2])
                        || value.StartsWith("00") && value[2] is >= '1' and <= '9',
                'w' => value[0] is >= '0' and <= '6',
                'u' => value[0] is >= '1' and <= '7',
                'U' or 'W' or 'V' => length == 1 ? char.IsDigit(value[0])
                    : value[0] is >= '0' and <= '4' && char.IsDigit(value[1]) || value[0] == '5' && value[1] is >= '0' and <= '3',
                _ => false
            };
            if (valid && alternative-- == 0) return length;
        }
        return 0;

        static bool Digits(ReadOnlySpan<char> value) { foreach (var c in value) if (!char.IsDigit(c)) return false; return true; }
        static bool AsciiDigits(ReadOnlySpan<char> value) { foreach (var c in value) if (!char.IsAsciiDigit(c)) return false; return true; }
    }
}
