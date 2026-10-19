using System.Buffers;
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
            try
            {
                var decoded = await ReadJsonValueAsync(text, options, context, span, 0, 0, asynchronous).ConfigureAwait(false);
                var end = SkipJsonWhitespace(text.Utf8Bytes.Span, decoded.End, context, span);
                if (end < text.Utf8Bytes.Length)
                {
                    throw CreateJsonTrailingDataError(text, end, span, context);
                }

                return decoded.Value;
            }
            catch (JsonException ex)
            {
                throw CreateJsonDecodeError(text, ex, span, context);
            }
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
            try
            {
                var (value, endByte) = await ReadJsonValueExactAsync(document, options, context, span, 0, startOriginal, asynchronous).ConfigureAwait(false);
                return (value, document.ByteIndexToRuneIndex(endByte));
            }
            catch (JsonException ex)
            {
                throw CreateJsonDecodeError(document, ex, span, context);
            }
        }

        private ValueTask<(object Value, int End)> ReadJsonValueAsync(PyString input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int depth, int position, bool asynchronous)
        {
            var bytes = input.Utf8Bytes;
            position = SkipJsonWhitespace(bytes.Span, position, context, span);
            if (position >= bytes.Length)
            {
                throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.Value, span, context);
            }

            return ReadJsonValueExactAsync(input, options, context, span, depth, position, asynchronous);
        }

        // Reads one value at the exact byte position with no leading
        // whitespace skip, for prefix entry points whose contract starts at
        // the given index.
        private ValueTask<(object Value, int End)> ReadJsonValueExactAsync(PyString input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int depth, int position, bool asynchronous)
        {
            context.CheckExecution(span);
            var bytes = input.Utf8Bytes;
            if (position >= bytes.Length)
            {
                throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.Value, span, context);
            }

            // Strings and true/false/null cannot suspend. Keep the same scalar
            // admission and prefix rules without entering the async token helper.
            var first = bytes.Span[position];
            if (first is (byte)'"' or (byte)'t' or (byte)'f' or (byte)'n')
            {
                context.EnterInterpreterFrame(span);
                try
                {
                    if (first == (byte)'"') return new(ReadJsonString(input, IsTruthy(options.Strict), position, context, span));
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
                finally { context.LeaveInterpreterFrame(); }
            }

            // The child parser owns its storage and suspended users. No generic
            // token state is needed around an unchanged container result.
            if (first is (byte)'{' or (byte)'[')
            {
                if (depth >= 64)
                {
                    throw CreateJsonDepthError(input, position, first == (byte)'[', span, context);
                }

                return first == (byte)'{'
                    ? ReadJsonObjectAsync(input, options, context, span, depth + 1, position, asynchronous)
                    : ReadJsonArrayAsync(input, options, context, span, depth + 1, position, asynchronous);
            }

            return ReadJsonNumberOrConstantAsync(input, options, context, span, position, asynchronous);
        }

        private async ValueTask<(object Value, int End)> ReadJsonNumberOrConstantAsync(PyString input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int position, bool asynchronous)
        {
            var bytes = input.Utf8Bytes;
            if (TryMatchJsonConstant(bytes.Span, position, out var constant, out var constantEnd))
            {
                return (await ConvertJsonConstantAsync(constant, options, context, span, asynchronous).ConfigureAwait(false), constantEnd);
            }

            context.EnterInterpreterFrame(span);
            try
            {
                // Numbers match Python's prefix grammar; a trailing suffix
                // belongs to whole-document validation, not this token.
                if (TryMatchJsonNumber(bytes.Span, position, context, span, out var numberEnd))
                {
                    return (await ConvertJsonNumberTextAsync(bytes, position, numberEnd, options, context, span, asynchronous).ConfigureAwait(false), numberEnd);
                }

                throw CreateJsonExpectingError(input, position, JsonIncompleteExpectation.Value, span, context);
            }
            finally
            {
                context.LeaveInterpreterFrame();
            }
        }

        private static (PyString Value, int End) ReadJsonString(PyString input, bool strict, int position, ExecutionContext context, LythonSourceSpan span)
        {
            // Lenient rewriting affects only the token; coordinates keep the original document.
            var original = input.Utf8Bytes.Span;
            var end = position + 1;
            var controls = 0;
            while (end < original.Length)
            {
                if ((end & 1023) == 0) context.CheckExecution(span);
                var current = original[end++];
                if (current == (byte)'\\')
                {
                    if (end < original.Length) end++;
                }
                else if (current == (byte)'"')
                {
                    break;
                }
                else if (!strict && current < 0x20)
                {
                    controls++;
                }
            }

            var token = input.Utf8Bytes.Slice(position, end - position);
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
                for (var read = 0; read < token.Length; read++)
                {
                    if ((read & 1023) == 0) context.CheckExecution(span);
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
                else if (reader.ValueIsEscaped && reader.ValueSpan.Length <= 256 && Utf8.IsValid(reader.ValueSpan))
                {
                    // A bounded managed buffer avoids a temporary CLR string
                    // and re-encoding. The existing token reservation remains;
                    // the final copy admits its own surviving UTF-8 storage.
                    Span<byte> unescaped = stackalloc byte[256];
                    var written = reader.CopyString(unescaped);
                    value = PyString.CopyFromUtf8(unescaped[..written], context.MemoryGovernor, span);
                }
                else value = CreateString(reader.GetString() ?? string.Empty, context, span);
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
                    while (read < end && copy < relative)
                    {
                        if ((read & 1023) == 0) context.CheckExecution(span);
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

        // Both object readers validate the quote first. Keys use the same
        // decoder and frame policy as string values, without generic dispatch
        // or a suspension point: JSON string keys never invoke a callback.
        private static (PyString Value, int End) ReadJsonObjectKey(PyString input, JsonLoadOptions options, int position, ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            context.EnterInterpreterFrame(span);
            try { return ReadJsonString(input, IsTruthy(options.Strict), position, context, span); }
            finally { context.LeaveInterpreterFrame(); }
        }

        private async ValueTask<(object Value, int End)> ReadJsonObjectAsync(PyString input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int depth, int openBrace, bool asynchronous)
        {
            var bytes = input.Utf8Bytes;
            if (options.ObjectPairsHook is not null)
            {
                var pairs = new PyList([], context.MemoryGovernor, span);
                var pairValueEnd = -1;
                while (true)
                {
                    // A closer here follows an empty container or a completed value.
                    var pairPeek = pairValueEnd < 0
                        ? SkipJsonWhitespace(bytes.Span, openBrace + 1, context, span)
                        : SkipJsonWhitespace(bytes.Span, pairValueEnd, context, span);
                    if (pairPeek < bytes.Length && bytes.Span[pairPeek] == (byte)'}')
                    {
                        return (await InvokeJsonCallbackCoreAsync(options.ObjectPairsHook, pairs, context, span, asynchronous).ConfigureAwait(false), pairPeek + 1);
                    }

                    int pairKeyStart;
                    if (pairValueEnd < 0)
                    {
                        pairKeyStart = pairPeek;
                    }
                    else
                    {
                        var pairComma = EnsureJsonComma(bytes.Span, pairValueEnd, input, span, context);
                        pairKeyStart = SkipJsonWhitespace(bytes.Span, pairComma + 1, context, span);
                        if (pairKeyStart < bytes.Length && bytes.Span[pairKeyStart] == (byte)'}')
                        {
                            throw NewJsonDecodeFailure(input, "Illegal trailing comma before end of object", pairComma, innerException: null, span, context);
                        }
                    }

                    if (pairKeyStart >= bytes.Length)
                    {
                        throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.PropertyName, span, context);
                    }

                    if (bytes.Span[pairKeyStart] != (byte)'"')
                    {
                        throw CreateJsonExpectingError(input, pairKeyStart, JsonIncompleteExpectation.PropertyName, span, context);
                    }

                    context.CheckExecution(span);
                    var (pairKey, pairKeyEnd) = ReadJsonObjectKey(input, options, pairKeyStart, context, span);

                    var pairColon = EnsureJsonColon(bytes.Span, pairKeyEnd, input, span, context);
                    var (pairValue, pairEnd) = await ReadJsonValueAsync(input, options, context, span, depth, pairColon + 1, asynchronous).ConfigureAwait(false);
                    var pair = PyTuple.FromOwnedArray([pairKey, pairValue], context.MemoryGovernor, span);
                    pairs.Add(pair);
                    context.CheckExecution(span);
                    pairValueEnd = pairEnd;
                }
            }

            var result = new PyDict(context.MemoryGovernor, span);
            var valueEnd = -1;
            while (true)
            {
                // A closer here follows an empty container or a completed value.
                var peek = valueEnd < 0
                    ? SkipJsonWhitespace(bytes.Span, openBrace + 1, context, span)
                    : SkipJsonWhitespace(bytes.Span, valueEnd, context, span);
                if (peek < bytes.Length && bytes.Span[peek] == (byte)'}')
                {
                    var end = peek + 1;
                    return options.ObjectHook is null
                        ? ((object)result, end)
                        : (await InvokeJsonCallbackCoreAsync(options.ObjectHook, result, context, span, asynchronous).ConfigureAwait(false), end);
                }

                int keyStart;
                if (valueEnd < 0)
                {
                    keyStart = peek;
                }
                else
                {
                    var memberComma = EnsureJsonComma(bytes.Span, valueEnd, input, span, context);
                    keyStart = SkipJsonWhitespace(bytes.Span, memberComma + 1, context, span);
                    if (keyStart < bytes.Length && bytes.Span[keyStart] == (byte)'}')
                    {
                        throw NewJsonDecodeFailure(input, "Illegal trailing comma before end of object", memberComma, innerException: null, span, context);
                    }
                }

                if (keyStart >= bytes.Length)
                {
                    throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.PropertyName, span, context);
                }

                if (bytes.Span[keyStart] != (byte)'"')
                {
                    throw CreateJsonExpectingError(input, keyStart, JsonIncompleteExpectation.PropertyName, span, context);
                }

                context.CheckExecution(span);
                var (memberKey, memberKeyEnd) = ReadJsonObjectKey(input, options, keyStart, context, span);

                var memberColon = EnsureJsonColon(bytes.Span, memberKeyEnd, input, span, context);
                var (memberValue, memberEnd) = await ReadJsonValueAsync(input, options, context, span, depth, memberColon + 1, asynchronous).ConfigureAwait(false);
                result.SetItem(memberKey, memberValue);
                context.CheckExecution(span);
                valueEnd = memberEnd;
            }
        }

        private async ValueTask<(object Value, int End)> ReadJsonArrayAsync(PyString input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int depth, int openBracket, bool asynchronous)
        {
            var bytes = input.Utf8Bytes;
            var result = new PyList([], context.MemoryGovernor, span);
            var valueEnd = -1;
            while (true)
            {
                // A closer here follows an empty container or a completed value.
                var peek = valueEnd < 0
                    ? SkipJsonWhitespace(bytes.Span, openBracket + 1, context, span)
                    : SkipJsonWhitespace(bytes.Span, valueEnd, context, span);
                if (peek < bytes.Length && bytes.Span[peek] == (byte)']')
                {
                    return ((object)result, peek + 1);
                }

                int elementStart;
                if (valueEnd < 0)
                {
                    elementStart = peek;
                }
                else
                {
                    var elementComma = EnsureJsonComma(bytes.Span, valueEnd, input, span, context);
                    elementStart = SkipJsonWhitespace(bytes.Span, elementComma + 1, context, span);
                    if (elementStart < bytes.Length && bytes.Span[elementStart] == (byte)']')
                    {
                        throw NewJsonDecodeFailure(input, "Illegal trailing comma before end of array", elementComma, innerException: null, span, context);
                    }
                }

                if (elementStart >= bytes.Length)
                {
                    throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.Value, span, context);
                }

                context.CheckExecution(span);
                var (element, elementEnd) = await ReadJsonValueAsync(input, options, context, span, depth, elementStart, asynchronous).ConfigureAwait(false);
                result.Add(element);
                context.CheckExecution(span);
                valueEnd = elementEnd;
            }
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
        private static async ValueTask<object> ConvertJsonConstantAsync(string literal, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            if (options.ParseConstant is not null)
            {
                var constantText = CreateString(literal, context, span);
                return await InvokeJsonCallbackCoreAsync(options.ParseConstant, constantText, context, span, asynchronous).ConfigureAwait(false);
            }

            return literal switch
            {
                "NaN" => PythonNaN,
                "Infinity" => double.PositiveInfinity,
                _ => double.NegativeInfinity,
            };
        }

        // Reports container nesting past the previous backend limit with its
        // wording: entering a 65th nested container fails like before.
        private static LythonRuntimeException CreateJsonDepthError(PyString input, int bytePosition, bool isArray, LythonSourceSpan span, ExecutionContext context)
        {
            var line = 0;
            var lineStart = 0;
            var bytes = input.Utf8Bytes.Span;
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
                // Deny on digit scale before parsing allocates the limbs; each
                // parse yields a fresh magnitude that owns its payload below.
                using var integerAdmission = GuardIntegerParseBytes(raw.Length, 4, context.MemoryGovernor, span);
                if (long.TryParse(raw.Span, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    return new(new BigInteger(integer));
                }

                // Only the text fallback allocates token-sized temporary storage.
                // Span parsing needs no such lease; callback text already owns
                // its funding before invocation, including retained aliases.
                using var numberScratch = context.MemoryGovernor.ReserveTemporary(checked(4L * raw.Length), span);
                return new(OwnFreshInteger(BigInteger.Parse(Encoding.UTF8.GetString(raw.Span), CultureInfo.InvariantCulture), context.MemoryGovernor, span));
            }

            return new(double.Parse(raw.Span, NumberStyles.Float, CultureInfo.InvariantCulture));
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

        // Requires a comma as the first non-whitespace byte at or after valueEnd:
        // missing separators surface exactly where CPython reports them.
        private static int EnsureJsonComma(ReadOnlySpan<byte> bytes, int valueEnd, PyString input, LythonSourceSpan span, ExecutionContext context)
        {
            var position = SkipJsonWhitespace(bytes, valueEnd, context, span);
            if (position >= bytes.Length || bytes[position] != (byte)',')
            {
                throw CreateJsonExpectingError(input, Math.Min(position, bytes.Length), JsonIncompleteExpectation.Comma, span, context);
            }

            return position;
        }

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
