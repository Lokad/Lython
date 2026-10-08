using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static bool IsUtf16Encoding(TextEncodingMode encoding)
        => encoding is TextEncodingMode.Utf16 or TextEncodingMode.Utf16LittleEndian or TextEncodingMode.Utf16BigEndian;

    private static bool UsesBinaryTextTransport(TextEncodingMode encoding)
        => IsSingleByteEncoding(encoding) || IsUtf16Encoding(encoding);

    private static ushort ReadUtf16Unit(ReadOnlySpan<byte> bytes, bool bigEndian)
        => bigEndian ? (ushort)((bytes[0] << 8) | bytes[1]) : (ushort)(bytes[0] | (bytes[1] << 8));

    // Whole byte codecs allow native-order data without a BOM. Text streams
    // require a BOM once a full unit exists, including when errors are ignored.
    private static TextEncodingMode ResolveUtf16ByteOrder(ref ReadOnlySpan<byte> source,
        TextEncodingMode encoding, bool stream, LythonSourceSpan? span)
    {
        if (encoding != TextEncodingMode.Utf16) return encoding;
        if (source.Length >= 2)
        {
            if (source[0] == 0xff && source[1] == 0xfe)
            {
                source = source[2..];
                return TextEncodingMode.Utf16LittleEndian;
            }
            if (source[0] == 0xfe && source[1] == 0xff)
            {
                source = source[2..];
                return TextEncodingMode.Utf16BigEndian;
            }
            if (stream) throw Utf16MissingBom(span);
        }
        // Lython's sys.byteorder is little on every host.
        return TextEncodingMode.Utf16LittleEndian;
    }

    private static LythonRuntimeException Utf16MissingBom(LythonSourceSpan? span)
        => new("UnicodeDecodeError", "UTF-16 stream does not start with BOM", span);

    internal static PyString DecodeUtf16Text(ReadOnlySpan<byte> source, TextEncodingMode encoding,
        ExecutionContext context, LythonSourceSpan? span, TextErrorMode errors,
        TextNewlineMode newline, bool stream = false)
    {
        var originalLength = source.Length;
        encoding = ResolveUtf16ByteOrder(ref source, encoding, stream, span);
        var errorOffset = originalLength - source.Length;
        var bigEndian = encoding == TextEncodingMode.Utf16BigEndian;
        long length = 0;
        for (var index = 0; index < source.Length;)
        {
            if ((index & 1023) == 0) context.CheckExecution(span);
            if (TryDecodeUtf16Scalar(source[index..], bigEndian, out var scalar, out var consumed, out var reason))
            {
                length += new Rune(scalar).Utf8SequenceLength;
                if (scalar == '\r' && newline == TextNewlineMode.TranslateUniversal &&
                    index + consumed + 1 < source.Length && ReadUtf16Unit(source[(index + consumed)..], bigEndian) == '\n')
                    consumed += 2;
            }
            else
            {
                length += errors switch
                {
                    TextErrorMode.Ignore => 0,
                    TextErrorMode.Replace => 3,
                    TextErrorMode.BackslashReplace => 4L * consumed,
                    _ => throw Utf16DecodeError(source[index], index + errorOffset, consumed, bigEndian, reason, span),
                };
            }
            index += consumed;
        }
        if (length == 0) return PyString.Empty;
        context.MemoryGovernor.EnsureCanReserve(PyString.EstimateApproximateBytes(0) + length, span);
        if (length > int.MaxValue) throw RuntimeErrors.Runtime("UTF-16 decoded text exceeds the supported buffer size", span);
        var bytes = new byte[(int)length];
        var offset = 0;
        const string hex = "0123456789abcdef";
        for (var index = 0; index < source.Length;)
        {
            if ((index & 1023) == 0) context.CheckExecution(span);
            if (TryDecodeUtf16Scalar(source[index..], bigEndian, out var scalar, out var consumed, out _))
            {
                if (scalar == '\r' && newline == TextNewlineMode.TranslateUniversal)
                {
                    scalar = '\n';
                    if (index + consumed + 1 < source.Length && ReadUtf16Unit(source[(index + consumed)..], bigEndian) == '\n')
                        consumed += 2;
                }
                offset += new Rune(scalar).EncodeToUtf8(bytes.AsSpan(offset));
            }
            else if (errors == TextErrorMode.Replace)
            {
                bytes[offset++] = 0xef; bytes[offset++] = 0xbf; bytes[offset++] = 0xbd;
            }
            else if (errors == TextErrorMode.BackslashReplace)
            {
                foreach (var value in source.Slice(index, consumed))
                {
                    bytes[offset++] = (byte)'\\'; bytes[offset++] = (byte)'x';
                    bytes[offset++] = (byte)hex[value >> 4]; bytes[offset++] = (byte)hex[value & 15];
                }
            }
            index += consumed;
        }
        return PyString.FromOwnedUtf8(bytes, context.MemoryGovernor, span);
    }

    private static LythonRuntimeException Utf16DecodeError(byte firstByte, int position, int consumed,
        bool bigEndian, string reason, LythonSourceSpan? span)
    {
        var unit = consumed == 1 ? $"byte 0x{firstByte:x2} in position {position}"
            : $"bytes in position {position}-{position + consumed - 1}";
        return new LythonRuntimeException("UnicodeDecodeError",
            $"'{(bigEndian ? "utf-16-be" : "utf-16-le")}' codec can't decode {unit}: {reason}", span);
    }

    private static bool TryDecodeUtf16Scalar(ReadOnlySpan<byte> source, bool bigEndian,
        out int scalar, out int consumed, out string reason)
    {
        scalar = 0;
        consumed = Math.Min(2, source.Length);
        reason = "truncated data";
        if (source.Length < 2) return false;
        var first = ReadUtf16Unit(source, bigEndian);
        if (first is >= 0xdc00 and <= 0xdfff)
        {
            reason = "illegal encoding";
            return false;
        }
        if (first is < 0xd800 or > 0xdbff)
        {
            scalar = first;
            return true;
        }
        if (source.Length < 4)
        {
            consumed = source.Length;
            reason = "unexpected end of data";
            return false;
        }
        var second = ReadUtf16Unit(source[2..], bigEndian);
        if (second is < 0xdc00 or > 0xdfff)
        {
            reason = "illegal UTF-16 surrogate";
            return false;
        }
        scalar = 0x10000 + ((first - 0xd800) << 10) + second - 0xdc00;
        consumed = 4;
        return true;
    }

    private static byte[] EncodeUtf16Text(PyString text, TextEncodingMode encoding,
        TextNewlineMode newline, ExecutionContext context, LythonSourceSpan? span, bool outputAlreadyFunded)
    {
        var length = Utf16EncodedByteCount(text, encoding, newline, context, span);
        if (!outputAlreadyFunded) context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(length), span);
        if (length == 0) return [];
        var source = text.Utf8Bytes.Span;
        var bytes = new byte[length];
        var offset = 0;
        var bigEndian = encoding == TextEncodingMode.Utf16BigEndian;
        if (encoding == TextEncodingMode.Utf16) PutUnit(0xfeff);
        var position = 0;
        for (var index = 0; index < source.Length;)
        {
            if ((position++ & 1023) == 0) context.CheckExecution(span);
            _ = Rune.DecodeFromUtf8(source[index..], out var rune, out var consumed);
            index += consumed;
            if (rune.Value == '\n' && newline is TextNewlineMode.PreserveCarriageReturn or TextNewlineMode.PreserveCarriageReturnLineFeed)
            {
                PutUnit('\r');
                if (newline == TextNewlineMode.PreserveCarriageReturnLineFeed) PutUnit('\n');
            }
            else if (rune.Value <= 0xffff) PutUnit((ushort)rune.Value);
            else
            {
                var value = rune.Value - 0x10000;
                PutUnit((ushort)(0xd800 | (value >> 10)));
                PutUnit((ushort)(0xdc00 | (value & 0x3ff)));
            }
        }
        return bytes;

        void PutUnit(ushort value)
        {
            bytes[offset++] = (byte)(bigEndian ? value >> 8 : value & 255);
            bytes[offset++] = (byte)(bigEndian ? value & 255 : value >> 8);
        }
    }

    private static MemoryGovernor.TemporaryMemoryReservation? ReserveUtf16Output(PyString text,
        TextEncodingMode encoding, TextNewlineMode newline, ExecutionContext context, LythonSourceSpan? span)
        => IsUtf16Encoding(encoding)
            ? context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(Utf16EncodedByteCount(text, encoding, newline, context, span)), span)
            : null;

    private static int Utf16EncodedByteCount(PyString text, TextEncodingMode encoding,
        TextNewlineMode newline, ExecutionContext context, LythonSourceSpan? span)
    {
        var source = text.Utf8Bytes.Span;
        long length = encoding == TextEncodingMode.Utf16 ? 2 : 0;
        var position = 0;
        for (var index = 0; index < source.Length;)
        {
            if ((position++ & 1023) == 0) context.CheckExecution(span);
            _ = Rune.DecodeFromUtf8(source[index..], out var rune, out var consumed);
            index += consumed;
            length += rune.Utf16SequenceLength * 2;
            if (rune.Value == '\n' && newline == TextNewlineMode.PreserveCarriageReturnLineFeed) length += 2;
        }
        if (length > int.MaxValue) throw RuntimeErrors.Runtime("UTF-16 encoded text exceeds the supported buffer size", span);
        return (int)length;
    }
}
