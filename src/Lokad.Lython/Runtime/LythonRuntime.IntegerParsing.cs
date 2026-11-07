using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static BigInteger ParseIntegerUtf8(ReadOnlySpan<byte> input, bool unicode, int numberBase,
        LythonSourceSpan span, object original, ExecutionContext context,
        ref MemoryGovernor.TemporaryMemoryReservation? admission)
    {
        context.CheckExecution(span);
        var inputLength = input.Length;
        input = TrimIntegerWhitespace(input, unicode, context, span);
        var position = 0;
        var negative = !input.IsEmpty && input[0] == (byte)'-';
        if (!input.IsEmpty && input[0] is (byte)'+' or (byte)'-') position++;
        var firstDigit = ReadIntegerDigit(input[position..], unicode, out var firstWidth);
        var radix = numberBase;
        var prefixed = false;
        if (firstDigit == 0 && position + firstWidth < input.Length)
        {
            var prefix = input[position + firstWidth] switch
            {
                (byte)'b' or (byte)'B' => 2,
                (byte)'o' or (byte)'O' => 8,
                (byte)'x' or (byte)'X' => 16,
                _ => 0,
            };
            if (prefix != 0 && (radix == 0 || radix == prefix))
            {
                radix = prefix;
                prefixed = true;
                position += firstWidth + 1;
                if (position < input.Length && input[position] == (byte)'_') position++;
            }
        }
        if (radix == 0) radix = 10;
        var unsignedRadix = (uint)radix;
        var leadingZero = numberBase == 0 && !prefixed && firstDigit == 0;
        var previousDigit = false;
        var sawNonZero = false;
        ulong small = 0;
        var large = BigInteger.Zero;
        uint chunk = 0, power = 1;
        var nextCheck = position;
        while (position < input.Length)
        {
            if (position >= nextCheck)
            {
                context.CheckExecution(span);
                nextCheck = position + Math.Min(4096, input.Length - position);
            }
            if (input[position] == (byte)'_')
            {
                if (!previousDigit) throw InvalidIntegerText(original, numberBase, context, span);
                previousDigit = false;
                position++;
                continue;
            }
            var digit = ReadIntegerDigit(input[position..], unicode, out var width);
            if (digit < 0 || digit >= radix) throw InvalidIntegerText(original, numberBase, context, span);
            previousDigit = true;
            sawNonZero |= digit != 0;
            if (admission is null)
            {
                if (small <= (ulong.MaxValue - (uint)digit) / unsignedRadix)
                    small = checked(small * unsignedRadix + (uint)digit);
                else
                {
                    // No major magnitude exists yet. Admit its full upper bound
                    // and replacement overlap before constructing even the seed.
                    var bits = RuntimeMemoryEstimates.SaturatingMultiply(inputLength, IntegerParseBitsPerDigit(radix));
                    var bytes = RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(
                        RuntimeMemoryEstimates.SaturatingAdd(bits, 1));
                    admission = context.MemoryGovernor.ReserveTemporary(
                        RuntimeMemoryEstimates.SaturatingMultiply(3, bytes), span);
                    context.CheckExecution(span);
                    large = new BigInteger(small);
                    chunk = (uint)digit;
                    power = unsignedRadix;
                }
            }
            else
            {
                // The native radix power bounds a chunk by representation, not
                // execution fuel. One multiply/add replaces per-digit magnitudes.
                if (power > uint.MaxValue / unsignedRadix)
                {
                    context.CheckExecution(span);
                    large = large * power + chunk;
                    chunk = 0;
                    power = 1;
                }
                chunk = checked(chunk * unsignedRadix + (uint)digit);
                power = checked(power * unsignedRadix);
            }
            position += width;
        }
        if (!previousDigit || leadingZero && sawNonZero)
            throw InvalidIntegerText(original, numberBase, context, span);
        context.CheckExecution(span);
        var result = admission is null ? new BigInteger(small) : large * power + chunk;
        return negative ? -result : result;
    }

    private static int ReadIntegerDigit(ReadOnlySpan<byte> input, bool unicode, out int width)
    {
        width = 1;
        if (input.IsEmpty) return -1;
        var value = input[0];
        if (value is >= (byte)'0' and <= (byte)'9') return value - (byte)'0';
        if (value is >= (byte)'a' and <= (byte)'z') return value - (byte)'a' + 10;
        if (value is >= (byte)'A' and <= (byte)'Z') return value - (byte)'A' + 10;
        if (!unicode || value < 128) return -1;
        return Rune.DecodeFromUtf8(input, out var rune, out width) == OperationStatus.Done &&
            Rune.GetUnicodeCategory(rune) == UnicodeCategory.DecimalDigitNumber
            ? (int)Rune.GetNumericValue(rune) : -1;
    }

    private static ReadOnlySpan<byte> TrimIntegerWhitespace(ReadOnlySpan<byte> input, bool unicode,
        ExecutionContext context, LythonSourceSpan span)
    {
        var start = 0;
        var end = input.Length;
        // The caller just checked. Resume checking at the next scan chunk.
        var nextCheck = Math.Min(4096, end);
        while (start < end)
        {
            if (start >= nextCheck)
            {
                context.CheckExecution(span);
                nextCheck = start + Math.Min(4096, end - start);
            }
            var width = IntegerWhitespaceWidth(input[start..end], unicode, false);
            if (width == 0) break;
            start += width;
        }
        nextCheck = end;
        while (start < end)
        {
            if (end <= nextCheck)
            {
                context.CheckExecution(span);
                nextCheck = end - Math.Min(4096, end - start);
            }
            var width = IntegerWhitespaceWidth(input[start..end], unicode, true);
            if (width == 0) break;
            end -= width;
        }
        return input[start..end];
    }

    private static int IntegerWhitespaceWidth(ReadOnlySpan<byte> input, bool unicode, bool reverse)
    {
        var value = reverse ? input[^1] : input[0];
        if (value < 128) return value is (byte)' ' or >= 9 and <= 13 ? 1 : 0;
        if (!unicode) return 0;
        var status = reverse ? Rune.DecodeLastFromUtf8(input, out var rune, out var width)
            : Rune.DecodeFromUtf8(input, out rune, out width);
        return status == OperationStatus.Done && PyStringOps.IsPythonWhitespace(rune) ? width : 0;
    }

    private static LythonRuntimeException InvalidIntegerText(object source, int numberBase,
        ExecutionContext context, LythonSourceSpan span)
        => new("ValueError", $"invalid literal for int() with base {numberBase}: {IntFailureDisplay(source, context)}", span);
}
