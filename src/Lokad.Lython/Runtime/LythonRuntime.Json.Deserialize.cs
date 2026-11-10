using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using System.Text.Unicode;
using Lokad.Lython.Runtime.Numbers;
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
            catch (JsonException ex)
            {
                throw CreateJsonDecodeError(text, ex, span, context);
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
            catch (JsonException ex)
            {
                throw CreateJsonDecodeError(document, ex, span, context);
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
            context.CheckExecution(span);
            var bytes = input.Utf8Bytes;
            if (position >= bytes.Length)
            {
                throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.Value, span, context);
            }

            // Strings and true/false/null finish under decoder admission.
            // Preserve scalar storage/prefix rules without an async token helper.
            var first = bytes.Span[position];
            if (first is (byte)'"' or (byte)'t' or (byte)'f' or (byte)'n')
            {
                if (first == (byte)'"')
                {
                    var (value, end) = ReadJsonString(input, (bool)options.Strict, position, context, span);
                    return new((value, end));
                }
                if (TryMatchJsonLiteral(bytes.Span, position, "true", out var trueEnd))
                {
                    return new(((object)true, trueEnd));
                }

                if (TryMatchJsonLiteral(bytes.Span, position, "false", out var falseEnd))
                {
                    return new(((object)false, falseEnd));
                }

                if (TryMatchJsonLiteral(bytes.Span, position, "null", out var nullEnd))
                {
                    return new((PyNone.Instance, nullEnd));
                }

                throw CreateJsonExpectingError(input, position, JsonIncompleteExpectation.Value, span, context);
            }

            // The child parser owns its storage and suspended users. No generic
            // token state is needed around an unchanged container result.
            if (first is (byte)'{' or (byte)'[')
            {
                return ReadJsonContainerAsync(input, options, context, span, position, first == (byte)'{', asynchronous);
            }

            return ReadJsonNumberOrConstantAsync(input, options, context, span, position, asynchronous);
        }

        private ValueTask<(object Value, int End)> ReadJsonNumberOrConstantAsync(PyString input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int position, bool asynchronous)
        {
            var bytes = input.Utf8Bytes;
            if (TryMatchJsonConstant(bytes.Span, position, out var constant, out var constantEnd))
            {
                var converted = ConvertJsonConstantAsync(constant, options, context, span, asynchronous);
                return converted.IsCompletedSuccessfully
                    ? new((converted.Result, constantEnd))
                    : CompleteJsonScalarAsync(converted, constantEnd);
            }

            // The bounded decoder owns admission through completed/pending hooks.
            // Numbers retain Python's prefix grammar and token storage ownership.
            if (TryMatchJsonNumber(bytes.Span, position, context, span, out var numberEnd))
            {
                var converted = ConvertJsonNumberTextAsync(bytes, position, numberEnd, options, context, span, asynchronous);
                return converted.IsCompletedSuccessfully
                    ? new((converted.Result, numberEnd))
                    : CompleteJsonScalarAsync(converted, numberEnd);
            }
            throw CreateJsonExpectingError(input, position, JsonIncompleteExpectation.Value, span, context);
        }

        private static async ValueTask<(object Value, int End)> CompleteJsonScalarAsync(
            ValueTask<object> converted, int end)
            => (await converted.ConfigureAwait(false), end);

        private static (PyString Value, int End) ReadJsonString(PyString input, bool strict, int position, ExecutionContext context, LythonSourceSpan span, PyDict.RecordKeys? expectedPropertyNames = null, int expectedPosition = 0)
        {
            // Lenient rewriting affects only the token; coordinates keep the original document.
            var original = input.Utf8Span;
            var end = position + 1;
            var controls = 0;
            var escaped = false;
            var closed = false;
            var strictControl = false;
            var nextCheck = position + Math.Min(1024, original.Length - position);
            while (end < original.Length)
            {
                if (end >= nextCheck)
                {
                    context.CheckExecution(span);
                    nextCheck = end + Math.Min(1024, original.Length - end);
                }
                var current = original[end++];
                if (current == (byte)'\\')
                {
                    escaped = true;
                    if (end < original.Length) end++;
                }
                else if (current == (byte)'"')
                {
                    closed = true;
                    break;
                }
                else if (current < 0x20)
                {
                    if (strict) strictControl = true;
                    else controls++;
                }
            }

            var token = input.Utf8Bytes.Slice(position, end - position);
            if (closed && !escaped && !strictControl && Utf8.IsValid(token.Span[1..^1]))
            {
                // The scan already established a complete plain string. No
                // backend decoding scratch or second token parse is needed.
                context.CheckExecution(span);
                var existing = expectedPropertyNames?.FindName(token.Span[1..^1], expectedPosition, context, span);
                var value = existing ?? PyString.CopyFromUtf8(token.Span[1..^1], context.MemoryGovernor, span);
                context.CheckExecution(span);
                return (value, end);
            }
            // Fund backend decoding and, only for lenient bare controls, the
            // exact expanded buffer before allocation. No offset list/copy or
            // storage proportional to an unused suffix is needed.
            using var scratch = context.MemoryGovernor.ReserveTemporary(checked(4L * token.Length), span);
            if (controls > 0)
            {
                var rewrittenLength = checked(token.Length + 5L * controls);
                scratch.Grow(checked(32L + rewrittenLength), span);
                var rewritten = new byte[checked((int)rewrittenLength)];
                const string hex = "0123456789abcdef";
                var written = 0;
                nextCheck = 0;
                for (var read = 0; read < token.Length; read++)
                {
                    if (read >= nextCheck)
                    {
                        context.CheckExecution(span);
                        nextCheck = read + Math.Min(1024, token.Length - read);
                    }
                    var current = token.Span[read];
                    if (current == (byte)'\\')
                    {
                        rewritten[written++] = current;
                        if (++read < token.Length) rewritten[written++] = token.Span[read];
                    }
                    else if (current < 0x20)
                    {
                        rewritten[written++] = (byte)'\\';
                        rewritten[written++] = (byte)'u';
                        rewritten[written++] = (byte)'0';
                        rewritten[written++] = (byte)'0';
                        rewritten[written++] = (byte)hex[current >> 4];
                        rewritten[written++] = (byte)hex[current & 15];
                    }
                    else
                    {
                        rewritten[written++] = current;
                    }
                }
                token = rewritten;
            }

            try
            {
                var reader = new Utf8JsonReader(token.Span, new JsonReaderOptions());
                if (!reader.Read()) throw CreateJsonExpectingError(input, end, JsonIncompleteExpectation.Value, span, context);
                // Keep invalid UTF-8 on the backend's established error path.
                PyString value;
                if (!reader.ValueIsEscaped && Utf8.IsValid(reader.ValueSpan))
                    value = CreateUtf8String(token.Slice(1, reader.ValueSpan.Length), context, span);
                else if (reader.ValueIsEscaped && Utf8.IsValid(reader.ValueSpan))
                {
                    // Unescaping cannot grow the UTF-8 token. The existing
                    // 4x token scratch funds larger managed buffers before
                    // allocation; the exact final copy owns separate funding.
                    Span<byte> unescaped = reader.ValueSpan.Length <= 256
                        ? stackalloc byte[256] : new byte[reader.ValueSpan.Length];
                    var written = reader.CopyString(unescaped);
                    context.CheckExecution(span);
                    value = PyString.CopyFromUtf8(unescaped[..written], context.MemoryGovernor, span);
                }
                else value = CreateString(reader.GetString() ?? string.Empty, context, span);
                context.CheckExecution(span);
                var existing = expectedPropertyNames?.FindName(value.Utf8Span, expectedPosition, context, span);
                if (existing is not null)
                {
                    if (!ReferenceEquals(value, existing)) context.MemoryGovernor.RefundUnpublishedValue(value);
                    value = existing;
                }
                return (value, end);
            }
            catch (JsonException ex)
            {
                var reported = ComputeJsonErrorBytePosition(token.Span, ex.LineNumber.GetValueOrDefault(), ex.BytePositionInLine.GetValueOrDefault());
                var relative = NormalizeJsonErrorBytePosition(token.Span, reported, ex.Message);
                if (controls > 0)
                {
                    var read = position;
                    var copy = 0;
                    nextCheck = position;
                    while (read < end && copy < relative)
                    {
                        if (read >= nextCheck)
                        {
                            context.CheckExecution(span);
                            nextCheck = read + Math.Min(1024, end - read);
                        }
                        var current = original[read];
                        var width = current < 0x20 ? 6 : 1;
                        if (copy + width > relative) break;
                        read++;
                        copy += width;
                        if (current == (byte)'\\' && read < end && copy < relative)
                        {
                            read++;
                            copy++;
                        }
                    }
                    relative = read - position;
                }
                var message = ex.Message;
                var originalPosition = position + relative;
                if (strict && originalPosition < original.Length && original[originalPosition] < 0x20)
                {
                    message = "Invalid control character at";
                }
                throw NewJsonDecodeFailure(input, message, originalPosition, ex, span, context);
            }
        }

        // Keys share the enclosing decoder admission, with no callback.
        private static (PyString Value, int End) ReadJsonObjectKey(PyString input, JsonLoadOptions options, int position, ExecutionContext context, LythonSourceSpan span, PyDict.RecordKeys? expected, int expectedPosition)
        {
            context.CheckExecution(span);
            return ReadJsonString(input, (bool)options.Strict, position, context, span, expected, expectedPosition);
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
                    comma, innerException: null, span, context);
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
        private static bool TryMatchJsonNumber(ReadOnlySpan<byte> bytes, int position, ExecutionContext context, LythonSourceSpan span, out int end)
        {
            end = position;
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
                do
                {
                    if ((cursor & 1023) == 0) context.CheckExecution(span);
                    cursor++;
                }
                while (cursor < bytes.Length && bytes[cursor] is >= (byte)'0' and <= (byte)'9');
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

                do
                {
                    if ((fraction & 1023) == 0) context.CheckExecution(span);
                    fraction++;
                }
                while (fraction < bytes.Length && bytes[fraction] is >= (byte)'0' and <= (byte)'9');
                cursor = fraction;
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
            }

            end = cursor;
            return true;
        }

        // Converts an already-matched constant literal: the parse_constant hook
        // receives the literal text, otherwise the IEEE double. Shared by
        // whole-text and nested positions.
        private static ValueTask<object> ConvertJsonConstantAsync(string literal, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            if (options.ParseConstant is not null)
            {
                var constantText = CreateString(literal, context, span);
                return InvokeJsonCallbackCoreAsync(options.ParseConstant, constantText, context, span, asynchronous);
            }

            return new(literal switch
            {
                "NaN" => PythonNaN,
                "Infinity" => double.PositiveInfinity,
                _ => double.NegativeInfinity,
            });
        }

        // Reports container nesting past the previous backend limit with its
        // wording: entering a 65th nested container fails like before.
        private static LythonRuntimeException CreateJsonDepthError(PyString input, int bytePosition, bool isArray, LythonSourceSpan span, ExecutionContext context)
        {
            var line = 0;
            var lineStart = 0;
            var bytes = input.Utf8Span;
            for (var index = 0; index < bytePosition; index++)
            {
                if (bytes[index] == (byte)'\n')
                {
                    line++;
                    lineStart = index + 1;
                }
            }

            return NewJsonDecodeFailure(
                input,
                "The maximum configured depth of 64 has been exceeded. Cannot read next JSON " + (isArray ? "array." : "object.") + " LineNumber: " + line + " | BytePositionInLine: " + (bytePosition - lineStart) + ".",
                bytePosition,
                innerException: null,
                span,
                context);
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

        // Converts an already-matched NUMBER_RE token: hooks receive the raw
        // token text, otherwise defaults parse invariant UTF-8. Large integers
        // retain their existing text fallback. Shared by whole-text/nested values.
        private static ValueTask<object> ConvertJsonNumberTextAsync(ReadOnlyMemory<byte> bytes, int start, int end, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            var raw = bytes.Slice(start, end - start);
            var isFloat = raw.Span.IndexOfAny((byte)'.', (byte)'e', (byte)'E') >= 0;
            var callback = isFloat ? options.ParseFloat : options.ParseInt;
            if (callback is not null)
            {
                // Fund callback text before invocation. It stays batch-owned
                // because the callback may retain it even when invocation fails.
                var text = CreateUtf8String(raw, context, span);
                return InvokeJsonCallbackCoreAsync(callback, text, context, span, asynchronous);
            }

            if (!isFloat)
            {
                // A signed Int64 decimal token needs at most 20 ASCII bytes.
                // Its bounded span parse allocates no limbs; the resulting small
                // scalar uses the existing execution/container headroom.
                if (raw.Length <= 20 && long.TryParse(raw.Span, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    return new(new BigInteger(integer));
                }

                // Admit large-magnitude construction before any limb/text allocation.
                // Larger tokens keep exact parsing, not a numeric input ceiling.
                using var integerAdmission = GuardIntegerParseBytes(raw.Length, 4, context.MemoryGovernor, span);
                // Only the text fallback allocates token-sized temporary storage.
                // Span parsing needs no such lease; callback text already owns
                // its funding before invocation, including retained aliases.
                using var numberScratch = context.MemoryGovernor.ReserveTemporary(checked(4L * raw.Length), span);
                return new(OwnFreshInteger(BigInteger.Parse(Encoding.UTF8.GetString(raw.Span), CultureInfo.InvariantCulture), context.MemoryGovernor, span));
            }

            // The prefix lexer has validated the complete native token. A
            // partial parse is an invariant defect, never a parser fallback.
            if (!Utf8Parser.TryParse(raw.Span, out double floating, out var consumed) || consumed != raw.Length)
                throw new InvalidOperationException("A validated JSON floating-point token did not parse completely.");
            return new(floating);
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

            return NewJsonDecodeFailure(input, message, bytePosition, innerException: null, span, context);
        }

        // Whole-document decoding rejects the suffix; raw_decode leaves it alone.
        private static LythonRuntimeException CreateJsonTrailingDataError(PyString input, int bytePosition, LythonSourceSpan span, ExecutionContext context)
            => NewJsonDecodeFailure(input, "Extra data", bytePosition, innerException: null, span, context);

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
