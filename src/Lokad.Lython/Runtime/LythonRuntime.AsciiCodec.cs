using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static bool IsSingleByteEncoding(TextEncodingMode encoding)
        => encoding is TextEncodingMode.Latin1 or TextEncodingMode.Ascii or TextEncodingMode.Windows1252;

    private static int SingleByteMaximumScalar(TextEncodingMode encoding)
        => encoding == TextEncodingMode.Ascii ? 127 : 255;

    private static int BackslashEscapedRuneLength(Rune rune)
        => rune.Value <= 0xff ? 4 : rune.Value <= 0xffff ? 6 : 10;

    internal static PyString DecodeAsciiText(ReadOnlySpan<byte> source, ExecutionContext context,
        LythonSourceSpan? span, TextErrorMode errors, TextNewlineMode newline)
    {
        long length = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if ((index & 1023) == 0) context.CheckExecutionBudget(span);
            var value = source[index];
            if (value >= 128)
            {
                length += errors switch
                {
                    TextErrorMode.Ignore => 0,
                    TextErrorMode.Replace => 3,
                    TextErrorMode.BackslashReplace => 4,
                    _ => throw new LythonRuntimeException("UnicodeDecodeError",
                        $"'ascii' codec can't decode byte 0x{value:x2} in position {index}: ordinal not in range(128)", span),
                };
            }
            else
            {
                length++;
                if (value == '\r' && newline == TextNewlineMode.TranslateUniversal &&
                    index + 1 < source.Length && source[index + 1] == '\n') index++;
            }
        }
        if (length == 0) return PyString.Empty;
        context.MemoryGovernor.EnsureCanReserve(PyString.EstimateApproximateBytes(0) + length, span);
        if (length > int.MaxValue) throw RuntimeErrors.Runtime("ASCII decoded text exceeds the supported buffer size", span);
        var bytes = new byte[(int)length];
        var offset = 0;
        const string hex = "0123456789abcdef";
        for (var index = 0; index < source.Length; index++)
        {
            if ((index & 1023) == 0) context.CheckExecutionBudget(span);
            var value = source[index];
            if (value < 128)
            {
                if (value == '\r' && newline == TextNewlineMode.TranslateUniversal)
                {
                    if (index + 1 < source.Length && source[index + 1] == '\n') index++;
                    value = (byte)'\n';
                }
                bytes[offset++] = value;
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
