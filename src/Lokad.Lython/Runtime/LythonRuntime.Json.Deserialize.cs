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
        {
            // Bound the streaming backend like the previous document model: it
            // reads ahead without budget callbacks, so reserve a conservative
            // multiple up front and hold it until the governed values below take
            // ownership.
            using var documentCharge = context.MemoryGovernor.ReserveTemporary(checked(4L * text.Utf8Bytes.Length), span);
            // Walk string spans once and escape bare controls so the streaming
            // backend (which rejects them like CPython strict mode) never sees
            // one; strict violations inside the parsed prefix are reported
            // below, so suffix controls cannot shadow the prefix result.
            var (input, controls) = CreateJsonParseInput(text, documentCharge, span);

            try
            {
                var decoded = DecodeJsonPrefix(input, options, context, span, startByte: 0);
                ThrowIfStrictControlInPrefix(input, controls, options.Strict, decoded.EndByte, span, context);
                var end = SkipJsonWhitespace(input.Source.Span, decoded.EndByte);
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

        // The bytes actually parsed plus the original document for diagnostics and
        // the map between their coordinates. Identical when no control rewriting
        // happened (the common case); divergent only when strings carried
        // bare control bytes (rewritten in either strictness mode, with strict
        // violations reported against the parsed prefix).
        private readonly record struct JsonParseInput(PyString Document, ReadOnlyMemory<byte> Source, JsonControlMap? Mapper);

        // Translates rewritten-parse coordinates back to original-document
        // coordinates after lenient control escaping (each rewritten control
        // adds five bytes). Null (the common case) means coordinates already
        // match and no translation occurs.
        private sealed class JsonControlMap
        {
            private readonly int[] _originals;

            public JsonControlMap(int[] originals)
            {
                _originals = originals;
            }

            public int ToOriginal(int copyPosition)
            {
                var low = 0;
                var high = _originals.Length;
                while (low < high)
                {
                    var middle = (low + high) / 2;
                    if (_originals[middle] + 5 * middle + 6 <= copyPosition)
                    {
                        low = middle + 1;
                    }
                    else
                    {
                        high = middle;
                    }
                }

                if (low < _originals.Length && _originals[low] + 5 * low <= copyPosition)
                {
                    return _originals[low];
                }

                return copyPosition - 5 * low;
            }

            // Maps an original-document byte position to rewritten-parse
            // coordinates (each preceding control adds five bytes), so
            // original string indices convert to parse positions. An original
            // control maps to the start of its six-byte escape.
            public int ToCopy(int originalPosition)
            {
                var low = 0;
                var high = _originals.Length;
                while (low < high)
                {
                    var middle = (low + high) / 2;
                    if (_originals[middle] < originalPosition)
                    {
                        low = middle + 1;
                    }
                    else
                    {
                        high = middle;
                    }
                }

                return originalPosition + 5 * low;
            }
        }

        // Walks string spans once, collecting bare control bytes (bytes below
        // 0x20 inside quoted strings). Backslashes quote the next byte, so
        // invalid escapes stay for the backend error; bytes outside strings
        // belong to structure and are validated during parsing instead. Returns
        // null when clean so the common path allocates nothing.
        private static List<int>? ScanJsonStringControls(ReadOnlySpan<byte> bytes)
        {
            List<int>? controls = null;
            var position = 0;
            while (position < bytes.Length)
            {
                if (bytes[position] != (byte)'"')
                {
                    position++;
                    continue;
                }

                position++;
                while (position < bytes.Length)
                {
                    var current = bytes[position];
                    if (current == (byte)'\\')
                    {
                        position += 2;
                        continue;
                    }

                    if (current == (byte)'"')
                    {
                        position++;
                        break;
                    }

                    if (current < (byte)' ')
                    {
                        controls ??= new List<int>();
                        controls.Add(position);
                    }

                    position++;
                }
            }

            return controls;
        }

        // Escapes collected bare controls as u00XX sequences so the streaming
        // backend (which rejects them like CPython strict mode) can parse
        // lenient input; coordinates map back through JsonControlMap. Values
        // decode identically since the escapes denote the same characters.
        private static byte[] RewriteJsonControls(ReadOnlyMemory<byte> source, List<int> controls)
        {
            var input = source.Span;
            var rewritten = new byte[source.Length + 5 * controls.Count];
            const string hex = "0123456789ABCDEF";
            var read = 0;
            var written = 0;
            foreach (var control in controls)
            {
                var chunk = control - read;
                input.Slice(read, chunk).CopyTo(rewritten.AsSpan(written));
                read += chunk;
                written += chunk;
                rewritten[written++] = (byte)'\\';
                rewritten[written++] = (byte)'u';
                rewritten[written++] = (byte)'0';
                rewritten[written++] = (byte)'0';
                rewritten[written++] = (byte)hex[(input[control] >> 4) & 0xF];
                rewritten[written++] = (byte)hex[input[control] & 0xF];
                read++;
            }

            input.Slice(read).CopyTo(rewritten.AsSpan(written));
            return rewritten;
        }

        // Builds the parse input once for whole-document and prefix entry
        // points: bare control bytes are escape-rewritten so the streaming
        // backend never sees one, in either strictness mode. Strict violations
        // inside the parsed prefix are reported by the caller, which knows the
        // prefix end; coordinates map back through the control map.
        private static (JsonParseInput Input, List<int>? Controls) CreateJsonParseInput(PyString text, MemoryGovernor.TemporaryMemoryReservation charge, LythonSourceSpan span)
        {
            var input = new JsonParseInput(text, text.Utf8Bytes, Mapper: null);
            var controls = ScanJsonStringControls(input.Source.Span);
            if (controls is { Count: > 0 })
            {
                var rewritten = RewriteJsonControls(input.Source, controls);
                charge.Grow(rewritten.Length, span);
                input = new JsonParseInput(text, rewritten, new JsonControlMap(controls.ToArray()));
            }

            return (input, controls);
        }

        // Reports the first bare control byte when strict mode parsed a prefix
        // covering it. Controls at or past the prefix end belong to the
        // untouched suffix and never shadow the prefix result.
        private static void ThrowIfStrictControlInPrefix(JsonParseInput input, List<int>? controls, bool strict, int endCopyByte, LythonSourceSpan span, ExecutionContext context)
        {
            if (!strict || controls is not { Count: > 0 })
            {
                return;
            }

            var endOriginal = input.Mapper is null ? endCopyByte : input.Mapper.ToOriginal(endCopyByte);
            if (controls[0] < endOriginal)
            {
                throw CreateJsonControlCharacterError(input, controls[0], span, context);
            }
        }

        // Reads exactly one value at the given Python string index with no
        // leading whitespace skip, returning the value and the absolute end
        // index in the original string. The suffix is never materialized.
        internal (object Value, int EndRune) DecodeJsonRawValue(PyString document, long idxRune, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            using var documentCharge = context.MemoryGovernor.ReserveTemporary(checked(4L * document.Utf8Bytes.Length), span);
            var (input, controls) = CreateJsonParseInput(document, documentCharge, span);
            if (idxRune > document.Length)
            {
                throw CreateJsonExpectingErrorAtRune(input, idxRune, span, context);
            }

            var startOriginal = document.GetByteIndexForRuneBoundary((int)idxRune);
            var startCopy = input.Mapper is null ? startOriginal : input.Mapper.ToCopy(startOriginal);
            try
            {
                var (value, endCopy) = ReadJsonValueExact(input, options, context, span, 0, startCopy);
                ThrowIfStrictControlInPrefix(input, controls, options.Strict, endCopy, span, context);
                var endOriginal = input.Mapper is null ? endCopy : input.Mapper.ToOriginal(endCopy);
                return (value, document.ByteIndexToRuneIndex(endOriginal));
            }
            catch (JsonException ex)
            {
                throw CreateJsonDecodeError(input, ex, span, context);
            }
        }

        private static LythonRuntimeException CreateJsonControlCharacterError(JsonParseInput input, int bytePosition, LythonSourceSpan span, ExecutionContext context)
        {
            var location = ComputeJsonErrorLocation(input.Document, bytePosition);
            return NewJsonDecodeFailure(
                input,
                "Invalid control character at: line " + location.Line + " column " + location.Column + " (char " + input.Document.ByteIndexToRuneIndex(bytePosition) + ")",
                // bytePosition is original-document: round-trip through the
                // rewritten coordinates so the shared failure builder (which
                // maps parse positions back) reports the same position.
                input.Mapper is null ? bytePosition : input.Mapper.ToCopy(bytePosition),
                innerException: null,
                span,
                context);
        }

        // Reads exactly one JSON value starting at startByte (after insignificant
        // whitespace) without parsing or copying the rest of the input, so prefixes
        // never materialize the suffix. Whole-document callers then require
        // end-of-input; prefix callers take the value end offset. All offsets are
        // absolute byte positions in the original text.
        private JsonPrefixDecode DecodeJsonPrefix(JsonParseInput input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int startByte)
        {
            var bytes = input.Source.Span;
            var position = SkipJsonWhitespace(bytes, startByte);
            if (position >= bytes.Length)
            {
                throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.Value, span, context);
            }

            var (value, end) = ReadJsonValue(input, options, context, span, 0, position);
            return new JsonPrefixDecode(value, end);
        }

        private (object Value, int End) ReadJsonValue(JsonParseInput input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int depth, int position)
        {
            var bytes = input.Source.Span;
            position = SkipJsonWhitespace(bytes, position);
            if (position >= bytes.Length)
            {
                throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.Value, span, context);
            }

            return ReadJsonValueExact(input, options, context, span, depth, position);
        }

        // Reads one value at the exact byte position with no leading
        // whitespace skip, for prefix entry points whose contract starts at
        // the given index.
        private (object Value, int End) ReadJsonValueExact(JsonParseInput input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int depth, int position)
        {
            var bytes = input.Source.Span;
            if (position >= bytes.Length)
            {
                throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.Value, span, context);
            }

            if (TryMatchJsonConstant(bytes, position, out var constant, out var constantEnd))
            {
                return (ConvertJsonConstant(constant, options, context, span), constantEnd);
            }

            var first = bytes[position];
            if (first is (byte)'{' or (byte)'[')
            {
                if (depth >= 64)
                {
                    throw CreateJsonDepthError(input, position, first == (byte)'[', span, context);
                }

                return first == (byte)'{'
                    ? ReadJsonObject(input, options, context, span, depth + 1, position)
                    : ReadJsonArray(input, options, context, span, depth + 1, position);
            }

            context.EnterInterpreterFrame(span);
            try
            {
                // CPython prefix rules: literals match case-sensitive prefixes
                // with no terminator check, and numbers match NUMBER_RE, so
                // adjacent values end exactly where CPython ends them (whole
                // document callers then report the remainder as trailing data).
                if (TryMatchJsonLiteral(bytes, position, "true", out var trueEnd))
                {
                    return ((object)true, trueEnd);
                }

                if (TryMatchJsonLiteral(bytes, position, "false", out var falseEnd))
                {
                    return ((object)false, falseEnd);
                }

                if (TryMatchJsonLiteral(bytes, position, "null", out var nullEnd))
                {
                    return (PyNone.Instance, nullEnd);
                }

                if (TryMatchJsonNumber(bytes, position, out var numberEnd))
                {
                    return (ConvertJsonNumberText(bytes, position, numberEnd, options, context, span), numberEnd);
                }

                if (first == (byte)'"')
                {
                    try
                    {
                        var reader = new Utf8JsonReader(bytes.Slice(position), new JsonReaderOptions());
                        if (!reader.Read())
                        {
                            throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.Value, span, context);
                        }

                        if (reader.TokenType is not JsonTokenType.String and not JsonTokenType.PropertyName)
                        {
                            throw CreateJsonExpectingError(input, position, JsonIncompleteExpectation.Value, span, context);
                        }

                        return (ConvertJsonReaderString(ref reader, context, span), ScanJsonTokenEnd(bytes, position));
                    }
                    catch (JsonException ex)
                    {
                        throw CreateJsonReaderError(input, position, ex, span, context);
                    }
                }

                throw CreateJsonExpectingError(input, position, JsonIncompleteExpectation.Value, span, context);
            }
            finally
            {
                context.LeaveInterpreterFrame();
            }
        }

        // Translates a backend error from a scalar reader over a slice starting
        // at sliceStart into absolute coordinates: same-line positions shift by
        // the slice offset, later lines are already absolute.
        private static LythonRuntimeException CreateJsonReaderError(JsonParseInput input, int sliceStart, JsonException exception, LythonSourceSpan span, ExecutionContext context)
        {
            var source = input.Source.Span;
            var baseLine = 0;
            var baseLineStart = 0;
            for (var index = 0; index < sliceStart; index++)
            {
                if (source[index] == (byte)'\n')
                {
                    baseLine++;
                    baseLineStart = index + 1;
                }
            }

            var relativeLine = exception.LineNumber.GetValueOrDefault();
            var absoluteLine = baseLine + relativeLine;
            var absoluteByteInLine = relativeLine == 0
                ? (sliceStart - baseLineStart) + exception.BytePositionInLine.GetValueOrDefault()
                : exception.BytePositionInLine.GetValueOrDefault();
            var reportedBytePosition = ComputeJsonErrorBytePosition(source, absoluteLine, absoluteByteInLine);
            var bytePosition = NormalizeJsonErrorBytePosition(source, reportedBytePosition, exception.Message);
            return NewJsonDecodeFailure(input, exception.Message, bytePosition, exception, span, context);
        }

        private (object Value, int End) ReadJsonObject(JsonParseInput input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int depth, int openBrace)
        {
            var bytes = input.Source.Span;
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
                        ? SkipJsonWhitespace(bytes, openBrace + 1)
                        : SkipJsonWhitespace(bytes, pairValueEnd);
                    if (pairPeek < bytes.Length && bytes[pairPeek] == (byte)'}')
                    {
                        if (IsJsonPreviousNonWhitespaceComma(bytes, pairPeek))
                        {
                            throw CreateJsonExpectingError(input, pairPeek, JsonIncompleteExpectation.PropertyName, span, context);
                        }

                        return (InvokeJsonCallback(options.ObjectPairsHook, pairs, context, span), pairPeek + 1);
                    }

                    int pairKeyStart;
                    if (pairValueEnd < 0)
                    {
                        pairKeyStart = pairPeek;
                    }
                    else
                    {
                        pairKeyStart = SkipJsonWhitespace(bytes, EnsureJsonComma(bytes, pairValueEnd, input, span, context) + 1);
                    }

                    if (pairKeyStart >= bytes.Length)
                    {
                        throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.PropertyName, span, context);
                    }

                    if (bytes[pairKeyStart] == (byte)'}')
                    {
                        throw CreateJsonExpectingError(input, pairKeyStart, JsonIncompleteExpectation.PropertyName, span, context);
                    }

                    if (bytes[pairKeyStart] != (byte)'"')
                    {
                        throw CreateJsonExpectingError(input, pairKeyStart, JsonIncompleteExpectation.PropertyName, span, context);
                    }

                    context.CheckExecutionBudget(span);
                    var (pairKeyObject, pairKeyEnd) = ReadJsonValue(input, options, context, span, depth, pairKeyStart);
                    if (pairKeyObject is not PyString pairKey)
                    {
                        throw new InvalidOperationException("JSON object key decoded to a non-string.");
                    }

                    var pairColon = EnsureJsonColon(bytes, pairKeyEnd, input, span, context);
                    var (pairValue, pairEnd) = ReadJsonValue(input, options, context, span, depth, pairColon + 1);
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
                    ? SkipJsonWhitespace(bytes, openBrace + 1)
                    : SkipJsonWhitespace(bytes, valueEnd);
                if (peek < bytes.Length && bytes[peek] == (byte)'}')
                {
                    if (IsJsonPreviousNonWhitespaceComma(bytes, peek))
                    {
                        throw CreateJsonExpectingError(input, peek, JsonIncompleteExpectation.PropertyName, span, context);
                    }

                    var end = peek + 1;
                    return options.ObjectHook is null
                        ? ((object)result, end)
                        : (InvokeJsonCallback(options.ObjectHook, result, context, span), end);
                }

                int keyStart;
                if (valueEnd < 0)
                {
                    keyStart = peek;
                }
                else
                {
                    keyStart = SkipJsonWhitespace(bytes, EnsureJsonComma(bytes, valueEnd, input, span, context) + 1);
                }

                if (keyStart >= bytes.Length)
                {
                    throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.PropertyName, span, context);
                }

                if (bytes[keyStart] == (byte)'}')
                {
                    throw CreateJsonExpectingError(input, keyStart, JsonIncompleteExpectation.PropertyName, span, context);
                }

                if (bytes[keyStart] != (byte)'"')
                {
                    throw CreateJsonExpectingError(input, keyStart, JsonIncompleteExpectation.PropertyName, span, context);
                }

                context.CheckExecutionBudget(span);
                var (memberKeyObject, memberKeyEnd) = ReadJsonValue(input, options, context, span, depth, keyStart);
                if (memberKeyObject is not PyString memberKey)
                {
                    throw new InvalidOperationException("JSON object key decoded to a non-string.");
                }

                var memberColon = EnsureJsonColon(bytes, memberKeyEnd, input, span, context);
                var (memberValue, memberEnd) = ReadJsonValue(input, options, context, span, depth, memberColon + 1);
                result.SetItem(memberKey, memberValue);
                context.ObserveCollectionCount(result.Count, span);
                valueEnd = memberEnd;
            }
        }        private (object Value, int End) ReadJsonArray(JsonParseInput input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, int depth, int openBracket)
        {
            var bytes = input.Source.Span;
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
                    ? SkipJsonWhitespace(bytes, openBracket + 1)
                    : SkipJsonWhitespace(bytes, valueEnd);
                if (peek < bytes.Length && bytes[peek] == (byte)']')
                {
                    if (IsJsonPreviousNonWhitespaceComma(bytes, peek))
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
                    elementStart = SkipJsonWhitespace(bytes, EnsureJsonComma(bytes, valueEnd, input, span, context) + 1);
                }

                if (elementStart >= bytes.Length)
                {
                    throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.Value, span, context);
                }

                if (bytes[elementStart] == (byte)']')
                {
                    throw CreateJsonExpectingError(input, elementStart, JsonIncompleteExpectation.Value, span, context);
                }

                context.CheckExecutionBudget(span);
                var (element, elementEnd) = ReadJsonValue(input, options, context, span, depth, elementStart);
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
        private static bool TryMatchJsonNumber(ReadOnlySpan<byte> bytes, int position, out int end)
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
        private static object ConvertJsonConstant(string literal, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            if (options.ParseConstant is not null)
            {
                var constantText = CreateString(literal, context, span);
                context.Services.State.CallTemporaries.TrackFreshString(constantText, span);
                return InvokeJsonCallback(options.ParseConstant, constantText, context, span);
            }

            return literal switch
            {
                "NaN" => double.NaN,
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
        }        private static int SkipJsonWhitespace(ReadOnlySpan<byte> bytes, int position)
        {
            while (position < bytes.Length && bytes[position] is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r')
            {
                position++;
            }

            return position;
        }

        // Converts an already-matched NUMBER_RE token: hooks receive the raw
        // token text, otherwise integers narrow to long where possible and
        // floats parse invariantly. Shared by whole-text and nested positions.
        private static object ConvertJsonNumberText(ReadOnlySpan<byte> bytes, int start, int end, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            var raw = Encoding.UTF8.GetString(bytes.Slice(start, end - start));
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
                return InvokeJsonCallback(options.ParseFloat, floatText, context, span);
            }

            if (!isFloat && options.ParseInt is not null)
            {
                var intText = CreateString(raw, context, span);
                context.Services.State.CallTemporaries.TrackFreshString(intText, span);
                return InvokeJsonCallback(options.ParseInt, intText, context, span);
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
            var position = SkipJsonWhitespace(bytes, valueEnd);
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
            var position = SkipJsonWhitespace(bytes, keyEnd);
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
        private static int ScanJsonTokenEnd(ReadOnlySpan<byte> bytes, int tokenStart)
        {
            if (tokenStart < bytes.Length && bytes[tokenStart] == (byte)'"')
            {
                var position = tokenStart + 1;
                while (position < bytes.Length)
                {
                    if (bytes[position] == (byte)'\\')
                    {
                        position += 2;
                        continue;
                    }

                    if (bytes[position] == (byte)'"')
                    {
                        return position + 1;
                    }

                    position++;
                }

                return bytes.Length;
            }

            var end = tokenStart;
            while (end < bytes.Length && IsJsonValueByte(bytes[end]))
            {
                end++;
            }

            return end;
        }

        private static bool IsJsonValueByte(byte value)
            => value is (>= (byte)'0' and <= (byte)'9') or (>= (byte)'a' and <= (byte)'z') or (>= (byte)'A' and <= (byte)'Z') or (byte)'.' or (byte)'+' or (byte)'-';

        // Detects a trailing comma before a closing bracket: only a comma can
        // directly precede a closer with no intervening value.
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
