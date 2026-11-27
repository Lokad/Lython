using System.Buffers.Text;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Unicode;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class JsonModule : PyModule
    {
        internal object ParseJsonText(PyString text, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
            => ParseJsonTextCoreAsync(text, options, context, span, false).GetAwaiter().GetResult();

        internal ValueTask<object> ParseJsonTextAsync(PyString text, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
            => ParseJsonTextCoreAsync(text, options, context, span, true);

        private async ValueTask<object> ParseJsonTextCoreAsync(PyString text, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            options = await options.PrepareAsync(context, span, asynchronous).ConfigureAwait(false);
            context.EnterInterpreterFrame(span);
            try
            {
                var decoded = await ReadJsonValueAsync(text, options, context, span, 0, asynchronous).ConfigureAwait(false);
                context.CheckExecution(span);
                ExecutionState.EnsureInterpreterStack(span);
                var end = SkipJsonWhitespace(text.Utf8Span, decoded.End, context, span);
                if (end < text.ByteLength)
                {
                    throw CreateJsonTrailingDataError(text, end, span, context);
                }

                return decoded.Value;
            }
            finally { context.LeaveInterpreterFrame(); }
        }

        // Reads exactly one value at the given Python string index with no
        // leading whitespace skip, returning the value and the absolute end
        // index in the original string. The suffix is never materialized.
        internal (object Value, int EndRune) DecodeJsonRawValue(PyString document, long idxRune, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
            => DecodeJsonRawValueCoreAsync(document, idxRune, options, context, span, false).GetAwaiter().GetResult();

        internal ValueTask<(object Value, int EndRune)> DecodeJsonRawValueAsync(PyString document, long idxRune, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
            => DecodeJsonRawValueCoreAsync(document, idxRune, options, context, span, true);

        private async ValueTask<(object Value, int EndRune)> DecodeJsonRawValueCoreAsync(PyString document, long idxRune, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            context.CheckExecution(span);
            if (idxRune > document.Length)
            {
                throw CreateJsonExpectingErrorAtRune(document, idxRune, span, context);
            }

            var startOriginal = document.GetByteIndexForRuneBoundary((int)idxRune);
            context.EnterInterpreterFrame(span);
            try
            {
                var (value, endByte) = await ReadJsonValueExactAsync(document, options, context, span, startOriginal, asynchronous).ConfigureAwait(false);
                context.CheckExecution(span);
                ExecutionState.EnsureInterpreterStack(span);
                return (value, document.ByteIndexToRuneIndex(endByte));
            }
            finally { context.LeaveInterpreterFrame(); }
        }

        private ValueTask<(object Value, int End)> ReadJsonValueAsync(PyString input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int position, bool asynchronous)
        {
            var bytes = input.Utf8Bytes;
            position = SkipJsonWhitespace(bytes.Span, position, context, span);
            if (position >= bytes.Length)
            {
                throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.Value, span, context);
            }

            return ReadJsonValueExactAsync(input, options, context, span, position, asynchronous);
        }

        // Reads one value at the exact byte position with no leading
        // whitespace skip, for prefix entry points whose contract starts at
        // the given index.
        private ValueTask<(object Value, int End)> ReadJsonValueExactAsync(PyString input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int position, bool asynchronous)
        {
            var bytes = input.Utf8Span;
            if (position < bytes.Length && bytes[position] is (byte)'{' or (byte)'[')
            {
                return ReadJsonContainerAsync(input, options, context, span, position, bytes[position] == (byte)'{', asynchronous);
            }

            return TryReadJsonScalar(input, options, context, span, position, asynchronous,
                out var value, out var end, out var pending)
                ? new((value, end)) : CompleteRootJsonScalarAsync(pending, end);
        }

        // Default scalar values go straight into construction. Only an actual
        // suspended callback returns a pending operation and its byte end.
        private static bool TryReadJsonScalar(PyString input, JsonLoadOptions options,
            ExecutionContext context, LythonSourceSpan span, int position, bool asynchronous,
            [NotNullWhen(true)] out object? value, out int end, out ValueTask<object> pending)
        {
            context.CheckExecution(span);
            var bytes = input.Utf8Span;
            pending = default;
            if (position >= bytes.Length)
                throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.Value, span, context);

            var first = bytes[position];
            if (first == (byte)'"')
            {
                (value, end) = ReadJsonString(input, (bool)options.Strict, position, context, span, out _);
                return true;
            }
            if (first is (byte)'t' or (byte)'f' or (byte)'n')
            {
                if (TryMatchJsonLiteral(bytes, position, "true", out end))
                    value = true;
                else if (TryMatchJsonLiteral(bytes, position, "false", out end))
                    value = false;
                else if (TryMatchJsonLiteral(bytes, position, "null", out end))
                    value = PyNone.Instance;
                else throw CreateJsonExpectingError(input, position, JsonIncompleteExpectation.Value, span, context);
                return true;
            }

            object? callback;
            PyString argument;
            if (TryMatchJsonConstant(bytes, position, out var constant, out end))
            {
                callback = options.ParseConstant;
                if (callback is null)
                {
                    value = constant switch
                    {
                        "NaN" => PythonNaN,
                        "Infinity" => double.PositiveInfinity,
                        _ => double.NegativeInfinity,
                    };
                    return true;
                }
                argument = CreateString(constant, context, span);
            }
            else if (TryMatchJsonNumber(bytes, position, context, span, out end,
                         out var significand, out var fractionDigits))
            {
                callback = fractionDigits == 0 ? options.ParseInt : options.ParseFloat;
                if (callback is null)
                {
                    var raw = bytes.Slice(position, end - position);
                    value = ConvertJsonNumber(raw, significand, fractionDigits, context, span);
                    return true;
                }
                // Callback text remains execution/result-owned even on failure:
                // user code may retain it before returning, suspending or raising.
                argument = input.CopyOrBorrowUtf8Range(position, end - position, context.MemoryGovernor, span, context);
            }
            else throw CreateJsonExpectingError(input, position, JsonIncompleteExpectation.Value, span, context);

            var converted = InvokeJsonCallbackCoreAsync(callback, argument, context, span, asynchronous);
            if (converted.IsCompleted)
            {
                value = converted.GetAwaiter().GetResult();
                return true;
            }
            value = null;
            pending = converted;
            return false;
        }

        private static async ValueTask<(object Value, int End)> CompleteRootJsonScalarAsync(
            ValueTask<object> converted, int end)
            => (await converted.ConfigureAwait(false), end);

        private static (PyString Value, int End) ReadJsonString(PyString input, bool strict, int position,
            ExecutionContext context, LythonSourceSpan span, out bool escaped,
            PyDict.RecordKeys? expectedPropertyNames = null, int expectedPosition = 0)
        {
            var original = input.Utf8Span;
            var end = position + 1;
            var reduction = 0;
            var escapedScalar = 0;
            escaped = false;
            var closed = false;
            var unpairedSurrogate = false;
            var nextCheck = position + Math.Min(1024, original.Length - position);
            while (end < original.Length)
            {
                if (end >= nextCheck)
                {
                    context.CheckExecution(span);
                    nextCheck = end + Math.Min(1024, original.Length - end);
                }
                var current = original[end++];
                if (current == (byte)'"') { closed = true; break; }
                if (current == (byte)'\\')
                {
                    escaped = true;
                    var start = end - 1;
                    escapedScalar = ReadJsonEscape(input, ref end, position, context, span);
                    unpairedSurrogate |= escapedScalar is >= 0xd800 and <= 0xdfff;
                    var width = escapedScalar < 0x80 ? 1 : escapedScalar < 0x800 ? 2 : escapedScalar < 0x10000 ? 3 : 4;
                    reduction += end - start - width;
                }
                else
                {
                    if (strict && current < 0x20)
                        throw NewJsonDecodeFailure(input, "Invalid control character at", end - 1, span, context);
                }
            }
            if (!closed)
                throw NewJsonDecodeFailure(input, "Unterminated string starting at", position, span, context);
            if (unpairedSurrogate)
                throw new LythonRuntimeException("ValueError", "Lython does not support unpaired Unicode surrogates in JSON strings.", span);

            var content = original.Slice(position + 1, end - position - 2);
            // Public string ingress is strict. Keep this defense for malformed
            // internal storage as well; never publish an invalid guest string.
            if (!Utf8.IsValid(content)) throw new DecoderFallbackException("JSON string contains invalid UTF-8.");
            context.CheckExecution(span);
            var length = content.Length - reduction;
            if (!escaped || length == 1)
            {
                // A one-byte result is ASCII. If escaped, the sizing pass
                // already read the only scalar, so there is nothing to fill.
                var character = length == 1 ? PyString.FromAsciiCharacter(escaped ? (byte)escapedScalar : content[0]) : null;
                var existing = expectedPropertyNames?.FindName(escaped ? character!.Utf8Span : content,
                    expectedPosition, context, span);
                var plain = existing ?? character ?? input.CopyOrBorrowUtf8Range(position + 1, length, context.MemoryGovernor, span);
                context.CheckExecution(span);
                return (plain, end);
            }

            if (expectedPropertyNames is not null && length <= PyString.InlineUtf8Capacity)
                return (ReadShortEscapedJsonName(input, position, end, length, expectedPropertyNames,
                    expectedPosition, context, span), end);

            // Validate/measure before admitting the exact final string. No
            // rewritten token, backend parser or temporary unescaped copy.
            var bytes = PyString.EstimateApproximateBytes(length);
            using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
            context.CheckExecution(span);
            var value = PyString.CreateReservedUtf8(length, context.MemoryGovernor);
            var written = WriteEscapedJsonString(input, position, end, value.UnpublishedUtf8, context, span);
            value.CompleteUnpublishedUtf8(written);
            context.CheckExecution(span);
            var shared = expectedPropertyNames?.FindName(value.Utf8Span, expectedPosition, context, span);
            context.CheckExecution(span);
            if (shared is not null) return (shared, end);
            storage.Commit(bytes);
            return (value, end);
        }

        private static PyString ReadShortEscapedJsonName(PyString input, int position,
            int end, int length, PyDict.RecordKeys expected, int expectedPosition, ExecutionContext context, LythonSourceSpan span)
        {
            // A layout hit needs only its already-owned canonical name. Use
            // bounded scratch for the lookup, and admit a guest string on a miss.
            Span<byte> decoded = stackalloc byte[PyString.InlineUtf8Capacity];
            var written = WriteEscapedJsonString(input, position, end, decoded[..length], context, span);
            decoded = decoded[..written];
            context.CheckExecution(span);
            var shared = expected.FindName(decoded, expectedPosition, context, span);
            context.CheckExecution(span);
            return shared ?? PyString.CopyFromUtf8(decoded, context.MemoryGovernor, span, context);
        }

        private static int WriteEscapedJsonString(PyString input, int position, int end,
            Span<byte> output, ExecutionContext context, LythonSourceSpan span)
        {
            var original = input.Utf8Span;
            var read = position + 1;
            var written = 0;
            while (read < end - 1)
            {
                context.CheckExecution(span);
                var chunk = original.Slice(read, Math.Min(4096, end - 1 - read));
                var escape = chunk.IndexOf((byte)'\\');
                var count = escape < 0 ? chunk.Length : escape;
                chunk[..count].CopyTo(output[written..]);
                written += count;
                read += count;
                if (escape >= 0)
                {
                    read++;
                    var scalar = ReadJsonEscape(input, ref read, position, context, span);
                    written += new Rune(scalar).EncodeToUtf8(output[written..]);
                }
            }
            return written;
        }

        // The slash has been consumed. A lone surrogate is reported only after
        // validating the complete token, preserving syntax-error precedence.
        private static int ReadJsonEscape(PyString input, ref int position, int openingQuote,
            ExecutionContext context, LythonSourceSpan span)
        {
            var bytes = input.Utf8Span;
            if (position == bytes.Length)
                throw NewJsonDecodeFailure(input, "Unterminated string starting at", openingQuote, span, context);
            var escapePosition = position;
            var escape = bytes[position++];
            switch (escape)
            {
                case (byte)'"': case (byte)'\\': case (byte)'/': return escape;
                case (byte)'b': return 8;
                case (byte)'f': return 12;
                case (byte)'n': return 10;
                case (byte)'r': return 13;
                case (byte)'t': return 9;
                case (byte)'u':
                    if (!TryReadJsonHex4(bytes, position, out var scalar))
                        throw NewJsonDecodeFailure(input, "Invalid \\uXXXX escape", escapePosition, span, context);
                    position += 4;
                    if (scalar is >= 0xd800 and <= 0xdbff && bytes.Length - position >= 6 &&
                        bytes[position] == (byte)'\\' && bytes[position + 1] == (byte)'u' &&
                        TryReadJsonHex4(bytes, position + 2, out var low) && low is >= 0xdc00 and <= 0xdfff)
                    {
                        position += 6;
                        return 0x10000 + ((scalar - 0xd800) << 10) + low - 0xdc00;
                    }
                    return scalar;
                default:
                    throw NewJsonDecodeFailure(input, "Invalid \\escape", escapePosition - 1, span, context);
            }
        }

        private static bool TryReadJsonHex4(ReadOnlySpan<byte> bytes, int position, out int scalar)
        {
            scalar = 0;
            // Python requires a following byte as well: four hex digits ending
            // at EOF are an invalid escape, rather than an unterminated token.
            if (bytes.Length - position <= 4) return false;
            for (var i = 0; i < 4; i++)
            {
                var digit = bytes[position + i] switch
                {
                    >= (byte)'0' and <= (byte)'9' => bytes[position + i] - '0',
                    >= (byte)'a' and <= (byte)'f' => bytes[position + i] - 'a' + 10,
                    >= (byte)'A' and <= (byte)'F' => bytes[position + i] - 'A' + 10,
                    _ => -1,
                };
                if (digit < 0) return false;
                scalar = (scalar << 4) | digit;
            }
            return true;
        }

        // Keys share the enclosing decoder admission, with no callback.
        private static (PyString Value, int End) ReadJsonObjectKey(PyString input, JsonLoadOptions options,
            int position, ExecutionContext context, LythonSourceSpan span,
            (PyDict.RecordKeys? Keys, bool PlainNames) expected, int expectedPosition, ref bool plainNames)
        {
            context.CheckExecution(span);
            // A strict decode of an unescaped name proves its UTF-8 contains
            // neither syntax bytes nor raw controls. An exact name and closing
            // quote can consume the token without lexing or validating it again.
            if (expected.PlainNames && expected.Keys?.MatchDelimitedName(input.Utf8Span[(position + 1)..],
                expectedPosition, (byte)'"', context, span) is { } key)
                return (key, position + 2 + key.ByteLength);

            var strict = (bool)options.Strict;
            var property = ReadJsonString(input, strict, position, context, span, out var escaped,
                expected.Keys, expectedPosition);
            plainNames &= strict && !escaped;
            return property;
        }

        private static int NextJsonContainerToken(PyString input, int valueEnd, bool isObject,
            ExecutionContext context, LythonSourceSpan span)
        {
            var bytes = input.Utf8Span;
            var position = SkipJsonWhitespace(bytes, valueEnd, context, span);
            if (position < bytes.Length && bytes[position] == (isObject ? (byte)'}' : (byte)']')) return position;
            if (position >= bytes.Length || bytes[position] != (byte)',')
                throw CreateJsonExpectingError(input, Math.Min(position, bytes.Length), JsonIncompleteExpectation.Comma, span, context);
            var comma = position;
            position = SkipJsonWhitespace(bytes, comma + 1, context, span);
            if (position < bytes.Length && bytes[position] == (isObject ? (byte)'}' : (byte)']'))
                throw NewJsonDecodeFailure(input, isObject
                    ? "Illegal trailing comma before end of object" : "Illegal trailing comma before end of array",
                    comma, span, context);
            return position;
        }

        private static void AppendJsonContainerValue(object result, PyString? key, object value,
            ExecutionContext context, LythonSourceSpan span)
        {
            if (result is PyDict dictionary) dictionary.SetRecordItem(key!, value, context, span);
            else ((PyList)result).Add(key is null ? value : new PyTuple([key, value], context.MemoryGovernor, span));
            context.CheckExecution(span);
        }

        // Matches a JSON constant literal (NaN/Infinity/-Infinity) like CPython:
        // case-sensitive prefix with no terminator validation.
        private static bool TryMatchJsonConstant(ReadOnlySpan<byte> bytes, int position, out string literal, out int end)
        {
            if (position < bytes.Length && bytes[position] == (byte)'-')
            {
                if (HasJsonLiteral(bytes, position, "-Infinity"))
                {
                    literal = "-Infinity";
                    end = position + 9;
                    return true;
                }
            }
            else if (position < bytes.Length && bytes[position] == (byte)'N')
            {
                if (HasJsonLiteral(bytes, position, "NaN"))
                {
                    literal = "NaN";
                    end = position + 3;
                    return true;
                }
            }
            else if (position < bytes.Length && bytes[position] == (byte)'I')
            {
                if (HasJsonLiteral(bytes, position, "Infinity"))
                {
                    literal = "Infinity";
                    end = position + 8;
                    return true;
                }
            }

            literal = string.Empty;
            end = position;
            return false;
        }

        private static bool HasJsonLiteral(ReadOnlySpan<byte> bytes, int position, string literal)
        {
            if (position + literal.Length > bytes.Length)
            {
                return false;
            }

            for (var index = 0; index < literal.Length; index++)
            {
                if (bytes[position + index] != (byte)literal[index])
                {
                    return false;
                }
            }

            return true;
        }

        // Matches a JSON literal (true/false/null) like CPython:
        // case-sensitive prefix with no terminator validation, so adjacent
        // values split at the literal end.
        private static bool TryMatchJsonLiteral(ReadOnlySpan<byte> bytes, int position, string literal, out int end)
        {
            if (HasJsonLiteral(bytes, position, literal))
            {
                end = position + literal.Length;
                return true;
            }

            end = position;
            return false;
        }

        // Matches CPython NUMBER_RE (-?(?:0|[1-9]\d*))(\.\d+)?([eE][-+]?\d+)?
        // as a prefix: a fraction or exponent without digits does not extend
        // the match, so "1e" ends after "1".
        // A negative fraction width denotes a floating token with an exponent.
        // The significand is used only when at most 19 digits were accumulated.
        private static bool TryMatchJsonNumber(ReadOnlySpan<byte> bytes, int position,
            ExecutionContext context, LythonSourceSpan span, out int end,
            out ulong significand, out int fractionDigits)
        {
            end = position;
            significand = 0;
            fractionDigits = 0;
            var cursor = position;
            if (cursor < bytes.Length && bytes[cursor] == (byte)'-')
            {
                cursor++;
            }

            if (cursor >= bytes.Length)
            {
                return false;
            }

            if (bytes[cursor] == (byte)'0')
            {
                cursor++;
            }
            else if (bytes[cursor] is >= (byte)'1' and <= (byte)'9')
            {
                cursor = ReadJsonSignificandDigits(bytes, cursor, ref significand, context, span);
            }
            else
            {
                return false;
            }

            if (cursor < bytes.Length && bytes[cursor] == (byte)'.')
            {
                var fraction = cursor + 1;
                if (fraction >= bytes.Length || bytes[fraction] is < (byte)'0' or > (byte)'9')
                {
                    end = cursor;
                    return true;
                }

                cursor = ReadJsonSignificandDigits(bytes, fraction, ref significand, context, span);
                fractionDigits = cursor - fraction;
            }

            if (cursor < bytes.Length && bytes[cursor] is (byte)'e' or (byte)'E')
            {
                var exponent = cursor + 1;
                if (exponent < bytes.Length && bytes[exponent] is (byte)'+' or (byte)'-')
                {
                    exponent++;
                }

                if (exponent >= bytes.Length || bytes[exponent] is < (byte)'0' or > (byte)'9')
                {
                    end = cursor;
                    return true;
                }

                do
                {
                    if ((exponent & 1023) == 0) context.CheckExecution(span);
                    exponent++;
                }
                while (exponent < bytes.Length && bytes[exponent] is >= (byte)'0' and <= (byte)'9');
                cursor = exponent;
                fractionDigits = -1;
            }

            end = cursor;
            return true;
        }

        private static int ReadJsonSignificandDigits(ReadOnlySpan<byte> bytes, int cursor, ref ulong significand,
            ExecutionContext context, LythonSourceSpan span)
        {
            var value = significand;
            do
            {
                if ((cursor & 1023) == 0) context.CheckExecution(span);
                value = unchecked(value * 10 + (uint)(bytes[cursor] - (byte)'0'));
                cursor++;
            }
            while (cursor < bytes.Length && bytes[cursor] is >= (byte)'0' and <= (byte)'9');
            significand = value;
            return cursor;
        }

        private static int SkipJsonWhitespace(ReadOnlySpan<byte> bytes, int position, ExecutionContext context, LythonSourceSpan span)
        {
            while (position < bytes.Length && bytes[position] is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r')
            {
                if ((position & 1023) == 0) context.CheckExecution(span);
                position++;
            }

            return position;
        }

        // Default conversion of a validated native NUMBER_RE token. Hook
        // dispatch lives at the scalar boundary; this path cannot suspend.
        private static object ConvertJsonNumber(ReadOnlySpan<byte> raw, ulong significand, int fractionDigits,
            ExecutionContext context, LythonSourceSpan span)
        {
            var negative = raw[0] == (byte)'-';
            var unsignedLength = raw.Length - (negative ? 1 : 0);
            if (fractionDigits == 0)
            {
                if (unsignedLength <= 19 && significand <= (negative ? 1UL << 63 : long.MaxValue))
                    return new BigInteger(negative ? unchecked(-(long)significand) : (long)significand);

                // Admit large-magnitude construction before any limb/text allocation.
                // Larger tokens keep exact parsing, not a numeric input ceiling.
                using var integerAdmission = GuardIntegerParseBytes(raw.Length, 4, context.MemoryGovernor, span);
                // Keep conservative working headroom for the opaque decimal
                // parser, including the UTF-16 token on .NET 10.
                using var numberScratch = context.MemoryGovernor.ReserveTemporary(checked(4L * raw.Length), span);
                context.CheckExecution(span);
                var integer = ParseWideJsonInteger(raw);
                context.CheckExecution(span);
                return OwnFreshInteger(integer, context.MemoryGovernor, span);
            }

            // Up to 19 digits including the integer part leaves at most 18
            // fractional digits, within the exact divisor table.
            if (fractionDigits > 0 && unsignedLength <= 20 && significand <= 1UL << 53)
                return ConvertExactDecimalToDouble(significand, fractionDigits, negative);

            // The prefix lexer has validated the complete native token. A
            // partial parse is an invariant defect, never a parser fallback.
            if (!Utf8Parser.TryParse(raw, out double floating, out var consumed) || consumed != raw.Length)
                throw new InvalidOperationException("A validated JSON floating-point token did not parse completely.");
            return floating;
        }

        private static BigInteger ParseWideJsonInteger(ReadOnlySpan<byte> raw)
        {
            // The UTF-8 BCL overload has more fixed allocation than the
            // character overload for these short, already validated tokens.
            const int stackCapacity = 128;
            if (raw.Length <= stackCapacity)
            {
                Span<char> characters = stackalloc char[stackCapacity];
                var count = Encoding.UTF8.GetChars(raw, characters);
                return BigInteger.Parse(characters[..count], CultureInfo.InvariantCulture);
            }

#if NET11_0_OR_GREATER
            return BigInteger.Parse(raw, CultureInfo.InvariantCulture);
#else
            return BigInteger.Parse(Encoding.UTF8.GetString(raw), CultureInfo.InvariantCulture);
#endif
        }

        private enum JsonIncompleteExpectation
        {
            Value,
            PropertyName,
            Comma,
            Colon,
        }

        // Reports a cleanly truncated prefix with CPython wording at the absolute
        // byte position: what the truncated input was still expecting there.
        private static LythonRuntimeException CreateJsonExpectingError(PyString input, int bytePosition, JsonIncompleteExpectation expectation, LythonSourceSpan span, ExecutionContext context)
        {
            var message = expectation switch
            {
                JsonIncompleteExpectation.PropertyName => "Expecting property name enclosed in double quotes",
                JsonIncompleteExpectation.Comma => "Expecting ',' delimiter",
                JsonIncompleteExpectation.Colon => "Expecting ':' delimiter",
                _ => "Expecting value",
            };

            return NewJsonDecodeFailure(input, message, bytePosition, span, context);
        }

        // Whole-document decoding rejects the suffix; raw_decode leaves it alone.
        private static LythonRuntimeException CreateJsonTrailingDataError(PyString input, int bytePosition, LythonSourceSpan span, ExecutionContext context)
            => NewJsonDecodeFailure(input, "Extra data", bytePosition, span, context);

        // Requires a colon as the first non-whitespace byte at or after keyEnd,
        // mirroring the comma rule above.
        private static int EnsureJsonColon(ReadOnlySpan<byte> bytes, int keyEnd, PyString input, LythonSourceSpan span, ExecutionContext context)
        {
            var position = SkipJsonWhitespace(bytes, keyEnd, context, span);
            if (position >= bytes.Length || bytes[position] != (byte)':')
            {
                throw CreateJsonExpectingError(input, Math.Min(position, bytes.Length), JsonIncompleteExpectation.Colon, span, context);
            }

            return position;
        }
    }
}
