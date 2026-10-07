using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

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
            var input = new JsonParseInput(text, text.Utf8Bytes);

            try
            {
                var decoded = await DecodeJsonPrefixAsync(input, options, context, span, startByte: 0, asynchronous).ConfigureAwait(false);
                var end = SkipJsonWhitespace(input.Source.Span, decoded.EndByte, context, span);
                if (end < input.Source.Length)
                {
                    throw CreateJsonTrailingDataError(input, end, span, context);
                }

                return decoded.Value;
            }
            catch (JsonException ex)
            {
                throw CreateJsonDecodeError(input, ex, span, context);
            }
        }

        private readonly record struct JsonPrefixDecode(object Value, int EndByte);

        // Parsing coordinates always refer to the original document. Lenient
        // control escaping is confined to the current string token.
        private readonly record struct JsonParseInput(PyString Document, ReadOnlyMemory<byte> Source);

        // Reads exactly one value at the given Python string index with no
        // leading whitespace skip, returning the value and the absolute end
        // index in the original string. The suffix is never materialized.
        internal (object Value, int EndRune) DecodeJsonRawValue(PyString document, long idxRune, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
            => DecodeJsonRawValueCoreAsync(document, idxRune, options, context, span, false).GetAwaiter().GetResult();

        internal ValueTask<(object Value, int EndRune)> DecodeJsonRawValueAsync(PyString document, long idxRune, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
            => DecodeJsonRawValueCoreAsync(document, idxRune, options, context, span, true);

        private async ValueTask<(object Value, int EndRune)> DecodeJsonRawValueCoreAsync(PyString document, long idxRune, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            context.CheckExecutionBudget(span);
            var input = new JsonParseInput(document, document.Utf8Bytes);
            if (idxRune > document.Length)
            {
                throw CreateJsonExpectingErrorAtRune(input, idxRune, span, context);
            }

            var startOriginal = document.GetByteIndexForRuneBoundary((int)idxRune);
            try
            {
                var (value, endCopy) = await ReadJsonValueExactAsync(input, options, context, span, 0, startOriginal, asynchronous).ConfigureAwait(false);
                var endOriginal = endCopy;
                return (value, document.ByteIndexToRuneIndex(endOriginal));
            }
            catch (JsonException ex)
            {
                throw CreateJsonDecodeError(input, ex, span, context);
            }
        }

        // Reads exactly one JSON value starting at startByte (after insignificant
        // whitespace) without parsing or copying the rest of the input, so prefixes
        // never materialize the suffix. Whole-document callers then require
        // end-of-input; prefix callers take the value end offset. All offsets are
        // absolute byte positions in the original text.
        private async ValueTask<JsonPrefixDecode> DecodeJsonPrefixAsync(JsonParseInput input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int startByte, bool asynchronous)
        {
            var bytes = input.Source;
            var position = SkipJsonWhitespace(bytes.Span, startByte, context, span);
            if (position >= bytes.Length)
            {
                throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.Value, span, context);
            }

            var (value, end) = await ReadJsonValueAsync(input, options, context, span, 0, position, asynchronous).ConfigureAwait(false);
            return new JsonPrefixDecode(value, end);
        }

        private async ValueTask<(object Value, int End)> ReadJsonValueAsync(JsonParseInput input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int depth, int position, bool asynchronous)
        {
            var bytes = input.Source;
            position = SkipJsonWhitespace(bytes.Span, position, context, span);
            if (position >= bytes.Length)
            {
                throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.Value, span, context);
            }

            return await ReadJsonValueExactAsync(input, options, context, span, depth, position, asynchronous).ConfigureAwait(false);
        }

        // Reads one value at the exact byte position with no leading
        // whitespace skip, for prefix entry points whose contract starts at
        // the given index.
        private async ValueTask<(object Value, int End)> ReadJsonValueExactAsync(JsonParseInput input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int depth, int position, bool asynchronous)
        {
            context.CheckExecutionBudget(span);
            var bytes = input.Source;
            if (position >= bytes.Length)
            {
                throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.Value, span, context);
            }

            if (TryMatchJsonConstant(bytes.Span, position, out var constant, out var constantEnd))
            {
                return (await ConvertJsonConstantAsync(constant, options, context, span, asynchronous).ConfigureAwait(false), constantEnd);
            }

            var first = bytes.Span[position];
            if (first is (byte)'{' or (byte)'[')
            {
                if (depth >= 64)
                {
                    throw CreateJsonDepthError(input, position, first == (byte)'[', span, context);
                }

                return first == (byte)'{'
                    ? await ReadJsonObjectAsync(input, options, context, span, depth + 1, position, asynchronous).ConfigureAwait(false)
                    : await ReadJsonArrayAsync(input, options, context, span, depth + 1, position, asynchronous).ConfigureAwait(false);
            }

            context.EnterInterpreterFrame(span);
            try
            {
                // CPython prefix rules: literals match case-sensitive prefixes
                // with no terminator check, and numbers match NUMBER_RE, so
                // adjacent values end exactly where CPython ends them (whole
                // document callers then report the remainder as trailing data).
                if (TryMatchJsonLiteral(bytes.Span, position, "true", out var trueEnd))
                {
                    return ((object)true, trueEnd);
                }

                if (TryMatchJsonLiteral(bytes.Span, position, "false", out var falseEnd))
                {
                    return ((object)false, falseEnd);
                }

                if (TryMatchJsonLiteral(bytes.Span, position, "null", out var nullEnd))
                {
                    return (PyNone.Instance, nullEnd);
                }

                if (TryMatchJsonNumber(bytes.Span, position, context, span, out var numberEnd))
                {
                    using var numberScratch = context.MemoryGovernor.ReserveTemporary(checked(4L * (numberEnd - position)), span);
                    return (await ConvertJsonNumberTextAsync(bytes, position, numberEnd, options, context, span, asynchronous).ConfigureAwait(false), numberEnd);
                }

                if (first == (byte)'"')
                {
                    return ReadJsonString(input, options.Strict, position, context, span);
                }

                throw CreateJsonExpectingError(input, position, JsonIncompleteExpectation.Value, span, context);
            }
            finally
            {
                context.LeaveInterpreterFrame();
            }
        }

        private static (object Value, int End) ReadJsonString(JsonParseInput input, bool strict, int position, ExecutionContext context, LythonSourceSpan span)
        {
            var original = input.Source.Span;
            var end = position + 1;
            var controls = 0;
            while (end < original.Length)
            {
                if ((end & 1023) == 0) context.CheckExecutionBudget(span);
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

            var token = input.Source.Slice(position, end - position);
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
                    if ((read & 1023) == 0) context.CheckExecutionBudget(span);
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
                return (ConvertJsonReaderString(ref reader, context, span), end);
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
                        if ((read & 1023) == 0) context.CheckExecutionBudget(span);
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
                    var location = ComputeJsonErrorLocation(input.Document, originalPosition);
                    message = "Invalid control character at: line " + location.Line + " column " + location.Column + " (char " + input.Document.ByteIndexToRuneIndex(originalPosition) + ")";
                }
                throw NewJsonDecodeFailure(input, message, originalPosition, ex, span, context);
            }
        }

        private async ValueTask<(object Value, int End)> ReadJsonObjectAsync(JsonParseInput input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int depth, int openBrace, bool asynchronous)
        {
            var bytes = input.Source;
            if (options.ObjectPairsHook is not null)
            {
                // R10: every fresh graph node owns a refundable pool snapshot
                // so dropped parses reclaim on sweep. Hook results adopt via
                // plain TrackCallResult inside InvokeJsonCallback, never
                // refunding an arbitrary callback value that may alias live
                // state.
                var pairs = new PyList([], context.MemoryGovernor, span);
                context.Services.State.CallTemporaries.TrackFreshMutable(pairs, pairs.CommittedStorageBytes, span);
                var pairValueEnd = -1;
                while (true)
                {
                    // A closer never needs comma verification: the previous
                    // character decides between valid end and trailing comma.
                    var pairPeek = pairValueEnd < 0
                        ? SkipJsonWhitespace(bytes.Span, openBrace + 1, context, span)
                        : SkipJsonWhitespace(bytes.Span, pairValueEnd, context, span);
                    if (pairPeek < bytes.Length && bytes.Span[pairPeek] == (byte)'}')
                    {
                        if (IsJsonPreviousNonWhitespaceComma(bytes.Span, pairPeek))
                        {
                            throw CreateJsonExpectingError(input, pairPeek, JsonIncompleteExpectation.PropertyName, span, context);
                        }

                        return (await InvokeJsonCallbackCoreAsync(options.ObjectPairsHook, pairs, context, span, asynchronous).ConfigureAwait(false), pairPeek + 1);
                    }

                    int pairKeyStart;
                    if (pairValueEnd < 0)
                    {
                        pairKeyStart = pairPeek;
                    }
                    else
                    {
                        pairKeyStart = SkipJsonWhitespace(bytes.Span, EnsureJsonComma(bytes.Span, pairValueEnd, input, span, context) + 1, context, span);
                    }

                    if (pairKeyStart >= bytes.Length)
                    {
                        throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.PropertyName, span, context);
                    }

                    if (bytes.Span[pairKeyStart] == (byte)'}')
                    {
                        throw CreateJsonExpectingError(input, pairKeyStart, JsonIncompleteExpectation.PropertyName, span, context);
                    }

                    if (bytes.Span[pairKeyStart] != (byte)'"')
                    {
                        throw CreateJsonExpectingError(input, pairKeyStart, JsonIncompleteExpectation.PropertyName, span, context);
                    }

                    context.CheckExecutionBudget(span);
                    var (pairKeyObject, pairKeyEnd) = await ReadJsonValueAsync(input, options, context, span, depth, pairKeyStart, asynchronous).ConfigureAwait(false);
                    if (pairKeyObject is not PyString pairKey)
                    {
                        throw new InvalidOperationException("JSON object key decoded to a non-string.");
                    }

                    var pairColon = EnsureJsonColon(bytes.Span, pairKeyEnd, input, span, context);
                    var (pairValue, pairEnd) = await ReadJsonValueAsync(input, options, context, span, depth, pairColon + 1, asynchronous).ConfigureAwait(false);
                    var pair = PyTuple.FromOwnedArray([pairKey, pairValue], context.MemoryGovernor, span);
                    context.Services.State.CallTemporaries.TrackFreshMutable(pair, pair.CommittedStorageBytes, span);
                    pairs.Add(pair);
                    context.ObserveCollectionCount(pairs.Count, span);
                    pairValueEnd = pairEnd;
                }
            }

            var result = new PyDict(context.MemoryGovernor, span);
            context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes, span);
            var valueEnd = -1;
            while (true)
            {
                // A closer never needs comma verification: the previous
                // character decides between valid end and trailing comma.
                var peek = valueEnd < 0
                    ? SkipJsonWhitespace(bytes.Span, openBrace + 1, context, span)
                    : SkipJsonWhitespace(bytes.Span, valueEnd, context, span);
                if (peek < bytes.Length && bytes.Span[peek] == (byte)'}')
                {
                    if (IsJsonPreviousNonWhitespaceComma(bytes.Span, peek))
                    {
                        throw CreateJsonExpectingError(input, peek, JsonIncompleteExpectation.PropertyName, span, context);
                    }

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
                    keyStart = SkipJsonWhitespace(bytes.Span, EnsureJsonComma(bytes.Span, valueEnd, input, span, context) + 1, context, span);
                }

                if (keyStart >= bytes.Length)
                {
                    throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.PropertyName, span, context);
                }

                if (bytes.Span[keyStart] == (byte)'}')
                {
                    throw CreateJsonExpectingError(input, keyStart, JsonIncompleteExpectation.PropertyName, span, context);
                }

                if (bytes.Span[keyStart] != (byte)'"')
                {
                    throw CreateJsonExpectingError(input, keyStart, JsonIncompleteExpectation.PropertyName, span, context);
                }

                context.CheckExecutionBudget(span);
                var (memberKeyObject, memberKeyEnd) = await ReadJsonValueAsync(input, options, context, span, depth, keyStart, asynchronous).ConfigureAwait(false);
                if (memberKeyObject is not PyString memberKey)
                {
                    throw new InvalidOperationException("JSON object key decoded to a non-string.");
                }

                var memberColon = EnsureJsonColon(bytes.Span, memberKeyEnd, input, span, context);
                var (memberValue, memberEnd) = await ReadJsonValueAsync(input, options, context, span, depth, memberColon + 1, asynchronous).ConfigureAwait(false);
                result.SetItem(memberKey, memberValue);
                context.ObserveCollectionCount(result.Count, span);
                valueEnd = memberEnd;
            }
        }

        private async ValueTask<(object Value, int End)> ReadJsonArrayAsync(JsonParseInput input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int depth, int openBracket, bool asynchronous)
        {
            var bytes = input.Source;
            // R10: the fresh list owns a refundable snapshot; each element
            // adopts ownership at its own construction boundary below, so a
            // dropped array reclaims both the backing and every element.
            var result = new PyList([], context.MemoryGovernor, span);
            context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes, span);
            var valueEnd = -1;
            while (true)
            {
                // A closer never needs comma verification: the previous
                // character decides between valid end and trailing comma.
                var peek = valueEnd < 0
                    ? SkipJsonWhitespace(bytes.Span, openBracket + 1, context, span)
                    : SkipJsonWhitespace(bytes.Span, valueEnd, context, span);
                if (peek < bytes.Length && bytes.Span[peek] == (byte)']')
                {
                    if (IsJsonPreviousNonWhitespaceComma(bytes.Span, peek))
                    {
                        throw CreateJsonExpectingError(input, peek, JsonIncompleteExpectation.Value, span, context);
                    }

                    return ((object)result, peek + 1);
                }

                int elementStart;
                if (valueEnd < 0)
                {
                    elementStart = peek;
                }
                else
                {
                    elementStart = SkipJsonWhitespace(bytes.Span, EnsureJsonComma(bytes.Span, valueEnd, input, span, context) + 1, context, span);
                }

                if (elementStart >= bytes.Length)
                {
                    throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.Value, span, context);
                }

                if (bytes.Span[elementStart] == (byte)']')
                {
                    throw CreateJsonExpectingError(input, elementStart, JsonIncompleteExpectation.Value, span, context);
                }

                context.CheckExecutionBudget(span);
                var (element, elementEnd) = await ReadJsonValueAsync(input, options, context, span, depth, elementStart, asynchronous).ConfigureAwait(false);
                result.Add(element);
                context.ObserveCollectionCount(result.Count, span);
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
                    if ((cursor & 1023) == 0) context.CheckExecutionBudget(span);
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
                    if ((fraction & 1023) == 0) context.CheckExecutionBudget(span);
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
                    if ((exponent & 1023) == 0) context.CheckExecutionBudget(span);
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
                context.Services.State.CallTemporaries.TrackFreshString(constantText, span);
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
        private static LythonRuntimeException CreateJsonDepthError(JsonParseInput input, int bytePosition, bool isArray, LythonSourceSpan span, ExecutionContext context)
        {
            var line = 0;
            var lineStart = 0;
            var bytes = input.Source.Span;
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
                if ((position & 1023) == 0) context.CheckExecutionBudget(span);
                position++;
            }

            return position;
        }

        // Converts an already-matched NUMBER_RE token: hooks receive the raw
        // token text, otherwise integers narrow to long where possible and
        // floats parse invariantly. Shared by whole-text and nested positions.
        private static async ValueTask<object> ConvertJsonNumberTextAsync(ReadOnlyMemory<byte> bytes, int start, int end, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            var raw = Encoding.UTF8.GetString(bytes.Span.Slice(start, end - start));
            var isFloat = raw.Contains('.', StringComparison.Ordinal) ||
                raw.Contains('e', StringComparison.OrdinalIgnoreCase);
            if (isFloat && options.ParseFloat is not null)
            {
                // R10: the raw text is a fresh string the callback may drop;
                // track it before invoking so a denied callback strands
                // nothing. The callback result adopts via plain
                // TrackCallResult inside InvokeJsonCallback.
                var floatText = CreateString(raw, context, span);
                context.Services.State.CallTemporaries.TrackFreshString(floatText, span);
                return await InvokeJsonCallbackCoreAsync(options.ParseFloat, floatText, context, span, asynchronous).ConfigureAwait(false);
            }

            if (!isFloat && options.ParseInt is not null)
            {
                var intText = CreateString(raw, context, span);
                context.Services.State.CallTemporaries.TrackFreshString(intText, span);
                return await InvokeJsonCallbackCoreAsync(options.ParseInt, intText, context, span, asynchronous).ConfigureAwait(false);
            }

            if (!isFloat)
            {
                // Deny on digit scale before parsing allocates the limbs; each
                // parse yields a fresh magnitude that owns its payload below.
                GuardIntegerParseBytes(raw, 4, context.MemoryGovernor, span);
                if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    return new BigInteger(integer);
                }

                return OwnFreshInteger(BigInteger.Parse(raw, CultureInfo.InvariantCulture), context.MemoryGovernor, context.Services.State.CallTemporaries, span);
            }

            return double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static PyString ConvertJsonReaderString(ref Utf8JsonReader reader, ExecutionContext context, LythonSourceSpan span)
        {
            // R10: each decoded string owns a refundable snapshot so dropped
            // scalar parses reclaim instead of stranding. Control characters
            // are validated by the input pre-scan, so the backend never sees
            // a bare control byte here in either strictness mode.
            var value = CreateString(reader.GetString() ?? string.Empty, context, span);
            context.Services.State.CallTemporaries.TrackFreshString(value, span);
            return value;
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
        private static LythonRuntimeException CreateJsonExpectingError(JsonParseInput input, int bytePosition, JsonIncompleteExpectation expectation, LythonSourceSpan span, ExecutionContext context)
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

        // Reports trailing data with the previous whole-document backend wording:
        // the first trailing character quoted, with absolute coordinates.
        private static LythonRuntimeException CreateJsonTrailingDataError(JsonParseInput input, int bytePosition, LythonSourceSpan span, ExecutionContext context)
        {
            var trailing = input.Source.Span[bytePosition..];
            var display = Rune.DecodeFromUtf8(trailing, out var rune, out _) == OperationStatus.Done
                ? rune.ToString()
                : "?";

            return NewJsonDecodeFailure(input, "'" + display + "' is invalid after a single JSON value. Expected end of data.", bytePosition, innerException: null, span, context);
        }

        // Requires a comma as the first non-whitespace byte at or after valueEnd:
        // missing separators surface exactly where CPython reports them.
        private static int EnsureJsonComma(ReadOnlySpan<byte> bytes, int valueEnd, JsonParseInput input, LythonSourceSpan span, ExecutionContext context)
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
        private static int EnsureJsonColon(ReadOnlySpan<byte> bytes, int keyEnd, JsonParseInput input, LythonSourceSpan span, ExecutionContext context)
        {
            var position = SkipJsonWhitespace(bytes, keyEnd, context, span);
            if (position >= bytes.Length || bytes[position] != (byte)':')
            {
                throw CreateJsonExpectingError(input, Math.Min(position, bytes.Length), JsonIncompleteExpectation.Colon, span, context);
            }

            return position;
        }

        // Computes a scalar or string token end by raw scanning: the reader may
        // have consumed past the token while classifying property names, so its
        // consumed count is only trustworthy after container brackets (which need
        // no lookahead). Strings walk with escape handling; numbers, literals and
        // constants consume value characters. Never runs past unterminated input
        // in practice (the reader rejects it first); the bounds guard is defensive.
        private static bool IsJsonPreviousNonWhitespaceComma(ReadOnlySpan<byte> bytes, int tokenStart)
        {
            var position = tokenStart - 1;
            while (position >= 0 && bytes[position] is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r')
            {
                position--;
            }

            return position >= 0 && bytes[position] == (byte)',';
        }
    }
}
