using System.Buffers.Binary;
using System.Text;
using System.Text.Unicode;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class JsonModule
    {
        private static async ValueTask<PyString> ReadDocumentAsync(object source, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            var read = asynchronous
                ? await PyTextStream.ResolveMemberAsync(source, "read", span, context).ConfigureAwait(false)
                : PyTextStream.ResolveMember(source, "read", span, context);
            var value = asynchronous
                ? await InvokeCallableTargetAsync(read, span, span, context,
                    () => ValueTask.FromResult<CallArgumentValue[]>([])).ConfigureAwait(false)
                : InvokeCallableTarget(read, span, span, context, () => []);
            return RequireLoadsText(value, context, span);
        }

        // Module loads/load decode before constructing a custom decoder. Its
        // decode/raw_decode methods still accept only str, as in Python.
        private static PyString RequireLoadsText(object value, ExecutionContext context, LythonSourceSpan span)
        {
            if (PyStringOps.TryAsString(value, out var text))
            {
                if (text.Utf8Span.StartsWith("\uFEFF"u8))
                    throw NewJsonDecodeFailure(text, "Unexpected UTF-8 BOM (decode using utf-8-sig)", 0, span, context);
                return text;
            }
            return DecodeDocumentBytes(value, context, span);
        }

        private static PyString DecodeDocumentBytes(object value, ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            if (value is not PyBytes bytes)
                throw new LythonRuntimeException("TypeError", "json.loads(s, *, ...) expects str or bytes.", span);

            var encoding = DetectJsonEncoding(bytes.Bytes, out var offset);
            var content = bytes.Memory[offset..];
            if (encoding is JsonEncoding.Utf16Little or JsonEncoding.Utf16Big)
                return DecodeUtf16Text(offset == 2 ? bytes.Bytes : content.Span, offset == 2 ? TextEncodingMode.Utf16 : encoding == JsonEncoding.Utf16Big
                    ? TextEncodingMode.Utf16BigEndian : TextEncodingMode.Utf16LittleEndian,
                    context, span, TextErrorMode.Strict, TextNewlineMode.PreserveLineFeed);
            if (encoding is JsonEncoding.Utf32Little or JsonEncoding.Utf32Big)
                return DecodeJsonUtf32(bytes.Bytes, offset, encoding == JsonEncoding.Utf32Big, context, span);

            // Validate the whole decoded document before any hook/constructor,
            // with bounded chunks that never split a UTF-8 scalar. Malformed
            // input uses the existing codec's Python error object and positions.
            for (var start = 0; start < content.Length;)
            {
                context.CheckExecution(span);
                var end = start + Math.Min(4096, content.Length - start);
                if (end < content.Length)
                    while ((content.Span[end] & 0xC0) == 0x80 && end > start) end--;
                if (end == start || !Utf8.IsValid(content.Span[start..end]))
                    return DecodeUtf8WithErrors(content.Span, context.MemoryGovernor, span,
                        TextErrorMode.Strict, TextNewlineMode.PreserveLineFeed, context.State);
                start = end;
            }
            context.CheckExecution(span);
            return bytes.CopyOrBorrowUtf8Text(offset, context, span);
        }

        private enum JsonEncoding { Utf8, Utf16Little, Utf16Big, Utf32Little, Utf32Big }

        // Python's BOM/leading-zero rules, including two-byte scalar documents.
        // This is input decoding for the existing parser, not a second parser.
        private static JsonEncoding DetectJsonEncoding(ReadOnlySpan<byte> bytes, out int offset)
        {
            offset = 0;
            if (bytes.StartsWith<byte>([0, 0, 0xFE, 0xFF])) { offset = 4; return JsonEncoding.Utf32Big; }
            if (bytes.StartsWith<byte>([0xFF, 0xFE, 0, 0])) { offset = 4; return JsonEncoding.Utf32Little; }
            if (bytes.StartsWith<byte>([0xFE, 0xFF])) { offset = 2; return JsonEncoding.Utf16Big; }
            if (bytes.StartsWith<byte>([0xFF, 0xFE])) { offset = 2; return JsonEncoding.Utf16Little; }
            if (bytes.StartsWith("\uFEFF"u8)) { offset = 3; return JsonEncoding.Utf8; }
            if (bytes.Length >= 4)
            {
                if (bytes[0] == 0) return bytes[1] == 0 ? JsonEncoding.Utf32Big : JsonEncoding.Utf16Big;
                if (bytes[1] == 0) return bytes[2] == 0 && bytes[3] == 0 ? JsonEncoding.Utf32Little : JsonEncoding.Utf16Little;
            }
            else if (bytes.Length == 2)
            {
                if (bytes[0] == 0) return JsonEncoding.Utf16Big;
                if (bytes[1] == 0) return JsonEncoding.Utf16Little;
            }
            return JsonEncoding.Utf8;
        }

        private static PyString DecodeJsonUtf32(ReadOnlySpan<byte> bytes, int offset, bool bigEndian,
            ExecutionContext context, LythonSourceSpan span)
        {
            long length = 0;
            for (var position = offset; position < bytes.Length; position += 4)
            {
                if (((position - offset) & 4095) == 0) context.CheckExecution(span);
                var remaining = bytes[position..];
                var scalar = remaining.Length < 4 ? uint.MaxValue : bigEndian
                    ? BinaryPrimitives.ReadUInt32BigEndian(remaining) : BinaryPrimitives.ReadUInt32LittleEndian(remaining);
                if (scalar > 0x10FFFF || scalar is >= 0xD800 and <= 0xDFFF)
                    throw DecodeError(bytes, bigEndian ? "utf-32-be" : "utf-32-le", position,
                        position + Math.Min(4, remaining.Length), remaining.Length < 4 ? "truncated data"
                        : scalar > 0x10FFFF ? "code point not in range(0x110000)" : "code point in surrogate code point range(0xd800, 0xe000)",
                        context.MemoryGovernor, span, context.State);
                length += new Rune((int)scalar).Utf8SequenceLength;
            }
            if (length == 0) return PyString.Empty;
            using var storage = context.MemoryGovernor.ReserveTemporary(PyString.EstimateApproximateBytes(0) + length, span);
            if (length > int.MaxValue) throw RuntimeErrors.Runtime("UTF-32 decoded text exceeds the supported buffer size", span);
            var output = new byte[(int)length];
            var written = 0;
            for (var position = offset; position < bytes.Length; position += 4)
            {
                if (((position - offset) & 4095) == 0) context.CheckExecution(span);
                var scalar = bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(bytes[position..])
                    : BinaryPrimitives.ReadUInt32LittleEndian(bytes[position..]);
                written += new Rune((int)scalar).EncodeToUtf8(output.AsSpan(written));
            }
            context.CheckExecution(span);
            return PyString.FromOwnedUtf8(output, context.MemoryGovernor, span, storage);
        }
    }
}
