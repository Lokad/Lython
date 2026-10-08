using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class HtmlModule
    {
        private static async ValueTask<object> UnescapeAsync(object[] args, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            if (args[0] is PyString text) return UnescapeText(text, span, context.Services);
            var hasReference = asynchronous
                ? await ContainsAsync(args[0], Ampersand, context, span).ConfigureAwait(false)
                : Contains(args[0], Ampersand, context, span);
            context.Services.CheckExecution(span);
            if (!hasReference) return args[0];
            throw new LythonRuntimeException("TypeError", "expected string for HTML entity decoding", span);
        }

        internal static PyString UnescapeText(PyString text, LythonSourceSpan span, ExecutionServices services)
        {
            var source = text.Utf8Bytes.Span;
            var builder = new GovernedByteBuilder(services.MemoryGovernor, span);
            var changed = false;
            long scalars = 0;
            long nextCheck = 0;
            Span<byte> encoded = stackalloc byte[4];
            try
            {
                for (var cursor = 0; cursor < source.Length;)
                {
                    if (cursor >= nextCheck)
                    {
                        services.CheckExecution(span);
                        nextCheck = (long)cursor + 1024;
                    }
                    if (source[cursor] == (byte)'&' && TryReadReference(source, cursor, span, services, out var reference))
                    {
                        scalars += reference.Scalars;
                        CheckHtmlOutputLength(scalars, span, services);
                        if (!changed) { builder.Append(source[..cursor]); changed = true; }
                        if (reference.Text is not null) builder.Append(reference.Text);
                        else if (reference.Scalar >= 0)
                        {
                            var rune = new Rune(reference.Scalar);
                            var width = rune.EncodeToUtf8(encoded);
                            builder.Append(encoded[..width]);
                        }
                        cursor = reference.EndByte;
                    }
                    else
                    {
                        Rune.DecodeFromUtf8(source[cursor..], out _, out var width);
                        scalars++;
                        CheckHtmlOutputLength(scalars, span, services);
                        if (changed) builder.Append(source.Slice(cursor, width));
                        cursor += width;
                    }
                }
                return changed ? FinishHtmlText(builder, span, services) : text;
            }
            finally { builder.Release(); }
        }

        private readonly record struct DecodedReference(int EndByte, PyString? Text, int Scalar, int Scalars);

        private static bool TryReadReference(ReadOnlySpan<byte> source, int ampersand,
            LythonSourceSpan span, ExecutionServices services, out DecodedReference reference)
        {
            reference = default;
            var start = ampersand + 1;
            if (start >= source.Length) return false;
            if (source[start] == (byte)'#')
                return TryReadNumericReference(source, start + 1, span, services, out reference);

            // All HTML5 keys are ASCII letters/digits plus an optional semicolon.
            // Only the longest known prefix can replace a named reference;
            // unmatched punctuation/Unicode remains in the original input.
            Span<char> name = stackalloc char[33];
            var length = 0;
            while (length < 32 && length < source.Length - start)
            {
                var value = source[start + length];
                if (value is not (>= (byte)'A' and <= (byte)'Z')
                    and not (>= (byte)'a' and <= (byte)'z')
                    and not (>= (byte)'0' and <= (byte)'9')) break;
                name[length++] = (char)value;
            }
            if (length == 0) return false;
            if (length < source.Length - start && source[start + length] == (byte)';') name[length++] = ';';
            for (; length > 0; length--)
            {
                if (HtmlEntityData.TryGetNamed(name[..length], out var entity))
                {
                    reference = new DecodedReference(start + length, entity.Text, -1, entity.Scalars);
                    return true;
                }
            }
            return false;
        }

        private static bool TryReadNumericReference(ReadOnlySpan<byte> source, int start,
            LythonSourceSpan span, ExecutionServices services, out DecodedReference reference)
        {
            reference = default;
            var hexadecimal = start < source.Length && source[start] is (byte)'x' or (byte)'X';
            if (hexadecimal) start++;
            var cursor = start;
            var value = 0;
            while (cursor < source.Length)
            {
                var digit = HtmlDigit(source[cursor], hexadecimal);
                if (digit < 0) break;
                if (((cursor - start) & 1023) == 0) services.CheckExecution(span);
                // A capped accumulator avoids arbitrary integer allocation while
                // still consuming the complete reference and honoring its limits.
                if (value < 0x110000) value = Math.Min(0x110000, value * (hexadecimal ? 16 : 10) + digit);
                cursor++;
            }
            if (cursor == start) return false;
            if (!hexadecimal && cursor - start > 4300)
                throw new LythonRuntimeException("ValueError", "HTML numeric reference exceeds the 4300 decimal digit limit", span);
            if (cursor < source.Length && source[cursor] == (byte)';') cursor++;
            var scalar = NormalizeNumericReference(value);
            reference = new DecodedReference(cursor, null, scalar, scalar < 0 ? 0 : 1);
            return true;
        }

        private static int HtmlDigit(byte value, bool hexadecimal) => value switch
        {
            >= (byte)'0' and <= (byte)'9' => value - '0',
            >= (byte)'a' and <= (byte)'f' when hexadecimal => value - 'a' + 10,
            >= (byte)'A' and <= (byte)'F' when hexadecimal => value - 'A' + 10,
            _ => -1,
        };

        internal static int NormalizeNumericReference(int scalar)
        {
            if (HtmlEntityData.TryMapInvalidNumericReference(scalar, out var mapped)) return mapped;
            if (scalar > 0x10ffff || scalar is >= 0xd800 and <= 0xdfff) return 0xfffd;
            if (scalar is >= 1 and <= 8 or 11 or >= 14 and <= 31 or 127 or >= 0xfdd0 and <= 0xfdef
                || (scalar & 0xffff) >= 0xfffe) return -1;
            return scalar;
        }
    }
}
