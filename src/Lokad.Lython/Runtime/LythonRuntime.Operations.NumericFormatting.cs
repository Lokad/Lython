using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static void AppendFloatingFormat(GovernedByteBuilder output, double value,
        InterpolatedFormatSpecifier spec, ExecutionContext context, LythonSourceSpan span, string typeName)
    {
        if (!double.IsFinite(value) || spec.Type is not ('f' or 'F' or 'e' or 'E'))
        {
            if (!TryFormatFloatingValue(value, spec, span, context.MemoryGovernor, out var text, out var prefix, typeName))
                throw new InvalidOperationException("Unexpected floating format category.");
            AppendInterpolatedFormatPadding(output, text, spec, prefix, numeric: true, span, context);
            return;
        }
        var capacity = SimpleFloatingCapacity(spec, span);
        using var working = capacity <= 512 ? null : context.MemoryGovernor.ReserveTemporary(32L + capacity, span);
        if (working is not null) context.CheckExecution(span);
        Span<byte> digits = capacity <= 512 ? stackalloc byte[512] : new byte[capacity];
        var count = WriteSimpleFloatingDigits(value, spec, digits);
        Span<byte> sign = stackalloc byte[1];
        var signLength = 0;
        var start = 0;
        if (digits[0] == '-') { sign[signLength++] = (byte)'-'; start = 1; }
        else if (spec.Sign is '+' or ' ') sign[signLength++] = (byte)spec.Sign.Value;
        var body = digits[start..count];
        var grouped = 0;
        if (spec.Grouping is not null)
        {
            grouped = body.IndexOf((byte)'.');
            if (grouped < 0) grouped = body.IndexOfAny((byte)'e', (byte)'E');
            if (grouped < 0) grouped = body.Length;
        }
        AppendAsciiNumericParts(output, sign[..signLength], body, spec, grouped, context, span);
    }

    // Complex formatting still needs component text. Keep a representation
    // bridge, sharing the actual digit and exponent rules with direct output.
    private static string FormatSimpleFloatingText(double value, InterpolatedFormatSpecifier spec,
        MemoryGovernor? governor, LythonSourceSpan span)
    {
        var capacity = SimpleFloatingCapacity(spec, span);
        using var working = capacity <= 512 ? null : governor?.ReserveTemporary(32L + capacity, span);
        Span<byte> digits = capacity <= 512 ? stackalloc byte[512] : new byte[capacity];
        var count = WriteSimpleFloatingDigits(value, spec, digits);
        return Encoding.UTF8.GetString(digits[..count]);
    }

    private static int SimpleFloatingCapacity(InterpolatedFormatSpecifier spec, LythonSourceSpan span)
    {
        // Fixed double needs at most309 integral digits; exponential needs a
        // short exponent. Include sign/point and alternate-form insertion.
        var capacity = 352L + spec.Precision.GetValueOrDefault(6);
        if (capacity > Array.MaxLength) throw RuntimeErrors.Memory("formatted float exceeds supported capacity", span);
        return (int)capacity;
    }

    private static int WriteSimpleFloatingDigits(double value, InterpolatedFormatSpecifier spec, Span<byte> destination)
    {
        Span<char> descriptor = stackalloc char[11];
        descriptor[0] = spec.Type is 'f' or 'F' ? 'F' : 'E';
        if (!spec.Precision.GetValueOrDefault(6).TryFormat(descriptor[1..], out var length, provider: CultureInfo.InvariantCulture))
            throw new InvalidOperationException("Floating precision descriptor was insufficient.");
        if (!value.TryFormat(destination, out var count, descriptor[..(length + 1)], CultureInfo.InvariantCulture))
            throw new InvalidOperationException("Floating digit bound was insufficient.");
        var marker = destination[..count].IndexOf((byte)'E');
        if (marker >= 0)
        {
            destination[marker] = spec.Type == 'e' ? (byte)'e' : (byte)'E';
            var exponentStart = marker + 2;
            var trimmedStart = exponentStart;
            while (count - trimmedStart > 2 && destination[trimmedStart] == '0') trimmedStart++;
            var removed = trimmedStart - exponentStart;
            destination[trimmedStart..count].CopyTo(destination[exponentStart..]);
            count -= removed;
        }
        if (spec.Alternate && destination[..count].IndexOf((byte)'.') < 0)
        {
            var point = marker >= 0 ? marker : count;
            destination[point..count].CopyTo(destination[(point + 1)..]);
            destination[point] = (byte)'.';
            count++;
        }
        return count;
    }

    private static void AppendIntegerFormat(GovernedByteBuilder output, BigInteger value,
        InterpolatedFormatSpecifier spec, ExecutionContext context, LythonSourceSpan span, string typeName, int minimumDigits = 0)
    {
        if (spec.Precision is not null)
            throw new LythonRuntimeException("ValueError", "Precision not allowed in integer format specifier", span);
        var type = spec.Type ?? 'd';
        var bitsPerDigit = type switch
        {
            'd' or 'n' => 0, 'b' => 1, 'o' => 3, 'x' or 'X' => 4,
            _ => throw new LythonRuntimeException("ValueError", $"Unknown format code '{type}' for object of type '{typeName}'", span),
        };
        if (spec.Grouping is { } grouping && bitsPerDigit != 0)
            throw new LythonRuntimeException("ValueError", $"Cannot specify '{grouping}' with '{type}'.", span);

        var magnitude = BigInteger.Abs(value);
        var bits = magnitude.GetBitLength();
        // 30103/100000 conservatively exceeds log10(2). Binary bases have
        // exact lengths and use no per-digit BigInteger division or allocation.
        var digitBound = bitsPerDigit == 0 ? checked(bits * 30103 / 100000 + 1)
            : Math.Max(1, (bits + bitsPerDigit - 1) / bitsPerDigit);
        if (digitBound > Array.MaxLength) throw RuntimeErrors.Memory("formatted integer exceeds supported capacity", span);
        var capacity = (int)digitBound;
        var rawCount = magnitude.GetByteCount(isUnsigned: true);
        var rawCapacity = bitsPerDigit == 0 ? 0 : rawCount;
        var scratchBytes = (capacity > 256 ? 32L + capacity : 0)
            + (rawCapacity > 128 ? 32L + rawCapacity : 0)
            // Opaque BCL decimal conversion needs working headroom as well as
            // the destination. Its synchronous conversion is checked on return.
            + (bitsPerDigit == 0 && rawCount > 128 ? 32L + 4L * rawCount : 0);
#if !NET11_0_OR_GREATER
        if (bitsPerDigit == 0 && capacity > 256) scratchBytes += 32L + 2L * capacity;
#endif
        using var working = scratchBytes == 0 ? null : context.MemoryGovernor.ReserveTemporary(scratchBytes, span);
        if (working is not null) context.CheckExecution(span);
        Span<byte> digits = capacity <= 256 ? stackalloc byte[256] : new byte[capacity];
        int count;
        if (bitsPerDigit == 0)
        {
#if NET11_0_OR_GREATER
            if (!TryFormatUtf8Number(magnitude, digits, out count, "D"))
                throw new InvalidOperationException("Integer digit bound was insufficient.");
#else
            // .NET10's inherited UTF8 interface allocates even for small values.
            // A short stack bridge, or admitted char storage, avoids CLR strings.
            Span<char> characters = capacity <= 256 ? stackalloc char[256] : new char[capacity];
            if (!magnitude.TryFormat(characters, out count, "D", CultureInfo.InvariantCulture))
                throw new InvalidOperationException("Integer digit bound was insufficient.");
            for (var index = 0; index < count; index++)
            {
                if ((index & 1023) == 0) context.CheckExecution(span);
                digits[index] = (byte)characters[index];
            }
#endif
        }
        else
        {
            Span<byte> raw = rawCapacity <= 128 ? stackalloc byte[128] : new byte[rawCapacity];
            if (!magnitude.TryWriteBytes(raw, out var rawLength, isUnsigned: true))
                throw new InvalidOperationException("Integer byte bound was insufficient.");
            var alphabet = type == 'X' ? "0123456789ABCDEF"u8 : "0123456789abcdef"u8;
            count = capacity;
            for (var index = 0; index < count; index++)
            {
                if ((index & 1023) == 0) context.CheckExecution(span);
                var bit = (long)(count - index - 1) * bitsPerDigit;
                var source = (int)(bit >> 3);
                var part = (int)raw[source];
                if (source + 1 < rawLength) part |= raw[source + 1] << 8;
                digits[index] = alphabet[(part >> (int)(bit & 7)) & ((1 << bitsPerDigit) - 1)];
            }
        }

        Span<byte> prefix = stackalloc byte[3];
        var prefixLength = 0;
        if (value.Sign < 0) prefix[prefixLength++] = (byte)'-';
        else if (spec.Sign is '+' or ' ') prefix[prefixLength++] = (byte)spec.Sign.Value;
        if (spec.Alternate && bitsPerDigit != 0)
        {
            prefix[prefixLength++] = (byte)'0';
            prefix[prefixLength++] = (byte)type;
        }
        AppendAsciiNumericParts(output, prefix[..prefixLength], digits[..count], spec,
            spec.Grouping is null ? 0 : count, context, span, minimumDigits);
    }

#if NET11_0_OR_GREATER
    private static bool TryFormatUtf8Number<T>(T value, Span<byte> destination, out int written,
        ReadOnlySpan<char> format) where T : IUtf8SpanFormattable
        => value.TryFormat(destination, out written, format, CultureInfo.InvariantCulture);
#endif

    // Prefix and digits are ASCII. Grouping concerns only the initial integral
    // digits; float fractions/exponents therefore share this same output path.
    private static void AppendAsciiNumericParts(GovernedByteBuilder output, ReadOnlySpan<byte> prefix,
        ReadOnlySpan<byte> digits, InterpolatedFormatSpecifier spec, int groupedDigits,
        ExecutionContext context, LythonSourceSpan span, int minimumDigits = 0)
    {
        var separators = groupedDigits == 0 ? 0 : (groupedDigits - 1) / 3;
        var zeroes = Math.Max(0, minimumDigits - digits.Length);
        var textLength = (long)prefix.Length + digits.Length + zeroes + separators;
        if (textLength > Array.MaxLength) throw RuntimeErrors.Memory("formatted number exceeds supported capacity", span);
        var length = (int)textLength;
        var padding = Math.Max(0, spec.Width.GetValueOrDefault() - length);
        var (align, fill, left) = padding == 0 ? ('<', new Rune(' '), 0)
            : GetInterpolatedPadding(spec, padding, numeric: true, span);
        var groupedZeroes = 0;
        if (groupedDigits != 0 && padding != 0 && align == '=' && fill.Value == '0')
        {
            // Sign-aware zero fill belongs to the integral group. Invert
            // n + (n-1)/3 to find the minimum digit count that meets width;
            // a separator can make the result one scalar wider than requested.
            var integralWidth = (long)spec.Width!.Value - prefix.Length - (digits.Length - groupedDigits);
            var paddedDigits = integralWidth - (integralWidth - 1) / 4;
            groupedZeroes = (int)(paddedDigits - groupedDigits);
            groupedDigits += groupedZeroes;
            separators = (groupedDigits - 1) / 3;
            textLength = (long)prefix.Length + digits.Length + zeroes + groupedZeroes + separators;
            padding = left = 0;
        }
        Span<byte> fillBytes = stackalloc byte[4];
        var fillLength = fill.EncodeToUtf8(fillBytes);
        var size = checked(textLength + (long)padding * fillLength);
        if (size > Array.MaxLength) throw RuntimeErrors.Memory("padded string exceeds supported capacity", span);
        context.CheckExecution(span);
        var destination = output.GetWriteWindow((int)size).Span;
        var token = context.State.CancellationToken;
        var offset = 0;
        if (align != '=')
        {
            var bytes = left * fillLength;
            PyStringOps.FillPadding(destination[..bytes], fillBytes[..fillLength], token);
            offset += bytes;
        }
        prefix.CopyTo(destination[offset..]);
        offset += prefix.Length;
        if (align == '=')
        {
            var bytes = padding * fillLength;
            PyStringOps.FillPadding(destination.Slice(offset, bytes), fillBytes[..fillLength], token);
            offset += bytes;
        }
        PyStringOps.FillPadding(destination.Slice(offset, zeroes), "0"u8, token);
        offset += zeroes;
        if (separators == 0)
        {
            digits.CopyTo(destination[offset..]);
            offset += digits.Length;
        }
        else for (var index = 0; index < digits.Length + groupedZeroes; index++)
        {
            if ((index & 1023) == 0) context.CheckExecution(span);
            if (index != 0 && index < groupedDigits && (groupedDigits - index) % 3 == 0)
                destination[offset++] = (byte)spec.Grouping!.Value;
            destination[offset++] = index < groupedZeroes ? (byte)'0' : digits[index - groupedZeroes];
        }
        PyStringOps.FillPadding(destination[offset..], fillBytes[..fillLength], token);
        context.CheckExecution(span);
        output.Advance((int)size);
    }

    private static (char Align, Rune Fill, int Left) GetInterpolatedPadding(
        InterpolatedFormatSpecifier spec, int padding, bool numeric, LythonSourceSpan span)
    {
        var align = spec.Align ?? (numeric ? '>' : '<');
        var fill = new Rune(spec.Fill >= 0 ? spec.Fill : spec.ZeroPad ? '0' : ' ');
        if (numeric && spec.ZeroPad && spec.Align is null) { align = '='; fill = new Rune('0'); }
        if (align == '=' && !numeric)
            throw new LythonRuntimeException("ValueError", "'=' alignment requires a numeric value.", span);
        var left = align switch
        {
            '<' or '=' => 0, '>' => padding, '^' => padding / 2,
            _ => throw new LythonRuntimeException("ValueError", $"Unknown alignment option '{align}'.", span),
        };
        return (align, fill, left);
    }
}
