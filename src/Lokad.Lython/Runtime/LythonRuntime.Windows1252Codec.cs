using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // The only CP1252 differences from Latin-1 are this 32-byte interval.
    // Negative slots are undefined and must use the selected error handler.
    private static ReadOnlySpan<int> Windows1252SpecialScalars =>
    [
        0x20ac, -1, 0x201a, 0x0192, 0x201e, 0x2026, 0x2020, 0x2021,
        0x02c6, 0x2030, 0x0160, 0x2039, 0x0152, -1, 0x017d, -1,
        -1, 0x2018, 0x2019, 0x201c, 0x201d, 0x2022, 0x2013, 0x2014,
        0x02dc, 0x2122, 0x0161, 0x203a, 0x0153, -1, 0x017e, 0x0178,
    ];

    private static bool TryEncodeSingleByteScalar(int scalar, TextEncodingMode encoding, out byte value)
    {
        if (encoding != TextEncodingMode.Windows1252)
        {
            if (scalar <= SingleByteMaximumScalar(encoding))
            {
                value = (byte)scalar;
                return true;
            }
            value = 0;
            return false;
        }
        if (scalar < 128 || scalar is >= 160 and <= 255)
        {
            value = (byte)scalar;
            return true;
        }
        var special = Windows1252SpecialScalars;
        for (var index = 0; index < special.Length; index++)
        {
            if (special[index] != scalar) continue;
            value = (byte)(index + 128);
            return true;
        }
        value = 0;
        return false;
    }

    internal static PyString DecodeWindows1252Text(ReadOnlySpan<byte> source, ExecutionContext context,
        LythonSourceSpan? span, TextErrorMode errors, TextNewlineMode newline)
    {
        long length = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if ((index & 1023) == 0) context.CheckExecutionBudget(span);
            var value = source[index];
            var scalar = value is >= 128 and < 160 ? Windows1252SpecialScalars[value - 128] : value;
            if (scalar < 0)
            {
                length += errors switch
                {
                    TextErrorMode.Ignore => 0,
                    TextErrorMode.Replace => 3,
                    TextErrorMode.BackslashReplace => 4,
                    _ => throw new LythonRuntimeException("UnicodeDecodeError",
                        $"'charmap' codec can't decode byte 0x{value:x2} in position {index}: character maps to <undefined>", span),
                };
            }
            else
            {
                length += new Rune(scalar).Utf8SequenceLength;
                if (value == '\r' && newline == TextNewlineMode.TranslateUniversal &&
                    index + 1 < source.Length && source[index + 1] == '\n') index++;
            }
        }
        if (length == 0) return PyString.Empty;
        context.MemoryGovernor.EnsureCanReserve(PyString.EstimateApproximateBytes(0) + length, span);
        if (length > int.MaxValue) throw RuntimeErrors.Runtime("CP1252 decoded text exceeds the supported buffer size", span);
        var bytes = new byte[(int)length];
        var offset = 0;
        const string hex = "0123456789abcdef";
        for (var index = 0; index < source.Length; index++)
        {
            if ((index & 1023) == 0) context.CheckExecutionBudget(span);
            var value = source[index];
            if (value == '\r' && newline == TextNewlineMode.TranslateUniversal)
            {
                if (index + 1 < source.Length && source[index + 1] == '\n') index++;
                value = (byte)'\n';
            }
            var scalar = value is >= 128 and < 160 ? Windows1252SpecialScalars[value - 128] : value;
            if (scalar >= 0)
            {
                offset += new Rune(scalar).EncodeToUtf8(bytes.AsSpan(offset));
            }
            else if (errors == TextErrorMode.Replace)
            {
                bytes[offset++] = 0xef; bytes[offset++] = 0xbf; bytes[offset++] = 0xbd;
            }
            else if (errors == TextErrorMode.BackslashReplace)
            {
                bytes[offset++] = (byte)'\\'; bytes[offset++] = (byte)'x';
                bytes[offset++] = (byte)hex[value >> 4]; bytes[offset++] = (byte)hex[value & 15];
            }
        }
        return PyString.FromOwnedUtf8(bytes, context.MemoryGovernor, span);
    }
}
