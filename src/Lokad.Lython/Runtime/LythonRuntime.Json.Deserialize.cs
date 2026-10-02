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
        private object ParseJsonText(PyString text, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            if (TryConvertJsonConstant(text, options, context, span, out var constant))
            {
                return constant;
            }

            // Bound the streaming backend like the previous document model: it
            // reads ahead without budget callbacks, so reserve a conservative
            // multiple up front and hold it until the governed values below take
            // ownership.
            using var documentCharge = context.MemoryGovernor.ReserveTemporary(checked(4L * text.Utf8Bytes.Length), span);
            // Walk string spans once: strict mode reports the first bare control
            // byte, while lenient mode collects them for escape rewriting below.
            // Either way the streaming backend never sees a bare control byte.
            var input = new JsonParseInput(text, text.Utf8Bytes, Mapper: null);
            var controls = ScanJsonStringControls(input.Source.Span);
            if (controls is { Count: > 0 })
            {
                if (options.Strict)
                {
                    throw CreateJsonControlCharacterError(input, controls[0], span, context);
                }

                var rewritten = RewriteJsonControls(input.Source, controls);
                documentCharge.Grow(rewritten.Length, span);
                input = new JsonParseInput(text, rewritten, new JsonControlMap(controls.ToArray()));
            }

            try
            {
                var decoded = DecodeJsonPrefix(input, options, context, span, startByte: 0);
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
        // happened (the common case); divergent only for lenient parses whose
        // strings carried bare control bytes.
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

        private static LythonRuntimeException CreateJsonControlCharacterError(JsonParseInput input, int bytePosition, LythonSourceSpan span, ExecutionContext context)
        {
            var location = ComputeJsonErrorLocation(input.Document, bytePosition);
            return NewJsonDecodeFailure(
                input,
                "Invalid control character at: line " + location.Line + " column " + location.Column + " (char " + input.Document.ByteIndexToRuneIndex(bytePosition) + ")",
                bytePosition,
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

            // The explicit depth cap preserves the previous document-backend limit.
            var reader = new Utf8JsonReader(bytes.Slice(position), new JsonReaderOptions { MaxDepth = 64 });
            if (!reader.Read())
            {
                throw CreateJsonExpectingError(input, bytes.Length, JsonIncompleteExpectation.Value, span, context);
            }

            var rootStart = position + (int)reader.TokenStartIndex;
            var rootIsContainer = reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray;
            var value = ConvertJsonReaderValue(ref reader, input, position, options, context, span);
            var rootEnd = rootIsContainer ? position + (int)reader.BytesConsumed : ScanJsonTokenEnd(bytes, rootStart);
            return new JsonPrefixDecode(value, rootEnd);
        }

        private static int SkipJsonWhitespace(ReadOnlySpan<byte> bytes, int position)
        {
            while (position < bytes.Length && bytes[position] is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r')
            {
                position++;
            }

            return position;
        }

        private static object ConvertJsonReaderValue(ref Utf8JsonReader reader, JsonParseInput input, int baseByte, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            context.EnterInterpreterFrame(span);
            try
            {
                // PropertyName shares the string path: the reader classifies a quoted
                // string by colon lookahead, so a key without its colon still decodes
                // as a string here and the separator checks below report it.
                return reader.TokenType switch
                {
                    JsonTokenType.StartObject => ConvertJsonReaderObject(ref reader, input, baseByte, options, context, span),
                    JsonTokenType.StartArray => ConvertJsonReaderArray(ref reader, input, baseByte, options, context, span),
                    JsonTokenType.String or JsonTokenType.PropertyName => ConvertJsonReaderString(ref reader, context, span),
                    JsonTokenType.True => true,
                    JsonTokenType.False => false,
                    JsonTokenType.Null => PyNone.Instance,
                    JsonTokenType.Number => ConvertJsonReaderNumber(ref reader, options, context, span),
                    _ => throw new InvalidOperationException($"Unsupported JSON value kind: {reader.TokenType}")
                };
            }
            finally
            {
                context.LeaveInterpreterFrame();
            }
        }

        private static object ConvertJsonReaderObject(ref Utf8JsonReader reader, JsonParseInput input, int baseByte, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
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
                var valueEnd = -1;
                while (true)
                {
                    if (!reader.Read())
                    {
                        throw CreateJsonExpectingError(input, baseByte + (int)reader.BytesConsumed, JsonIncompleteExpectation.PropertyName, span, context);
                    }

                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        if (IsJsonPreviousNonWhitespaceComma(bytes, baseByte + (int)reader.TokenStartIndex))
                        {
                            throw CreateJsonExpectingError(input, baseByte + (int)reader.TokenStartIndex, JsonIncompleteExpectation.PropertyName, span, context);
                        }

                        break;
                    }

                    if (reader.TokenType is not (JsonTokenType.PropertyName or JsonTokenType.String))
                    {
                        throw CreateJsonExpectingError(input, baseByte + (int)reader.TokenStartIndex, JsonIncompleteExpectation.PropertyName, span, context);
                    }

                    if (valueEnd >= 0)
                    {
                        EnsureJsonComma(bytes, valueEnd, input, span, context);
                    }

                    context.CheckExecutionBudget(span);
                    // Track the key before converting the value: a denied
                    // value conversion must not strand the key charge.
                    var keyStart = baseByte + (int)reader.TokenStartIndex;
                    var key = ConvertJsonReaderString(ref reader, context, span);
                    var keyEnd = ScanJsonTokenEnd(bytes, keyStart);
                    EnsureJsonColon(bytes, keyEnd, input, span, context);
                    if (!reader.Read())
                    {
                        throw CreateJsonExpectingError(input, baseByte + (int)reader.BytesConsumed, JsonIncompleteExpectation.Value, span, context);
                    }

                    var valueStart = baseByte + (int)reader.TokenStartIndex;
                    var valueIsContainer = reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray;
                    var value = ConvertJsonReaderValue(ref reader, input, baseByte, options, context, span);
                    var pair = PyTuple.FromOwnedArray([key, value], context.MemoryGovernor, span);
                    context.Services.State.CallTemporaries.TrackFreshMutable(pair, pair.CommittedStorageBytes, span);
                    pairs.Add(pair);
                    context.ObserveCollectionCount(pairs.Count, span);
                    valueEnd = valueIsContainer ? baseByte + (int)reader.BytesConsumed : ScanJsonTokenEnd(bytes, valueStart);
                }

                return InvokeJsonCallback(options.ObjectPairsHook, pairs, context, span);
            }

            var result = new PyDict(context.MemoryGovernor, span);
            context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes, span);
            var dictValueEnd = -1;
            while (true)
            {
                if (!reader.Read())
                {
                    throw CreateJsonExpectingError(input, baseByte + (int)reader.BytesConsumed, JsonIncompleteExpectation.PropertyName, span, context);
                }

                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    if (IsJsonPreviousNonWhitespaceComma(bytes, baseByte + (int)reader.TokenStartIndex))
                    {
                        throw CreateJsonExpectingError(input, baseByte + (int)reader.TokenStartIndex, JsonIncompleteExpectation.PropertyName, span, context);
                    }

                    break;
                }

                if (reader.TokenType is not (JsonTokenType.PropertyName or JsonTokenType.String))
                {
                    throw CreateJsonExpectingError(input, baseByte + (int)reader.TokenStartIndex, JsonIncompleteExpectation.PropertyName, span, context);
                }

                if (dictValueEnd >= 0)
                {
                    EnsureJsonComma(bytes, dictValueEnd, input, span, context);
                }

                context.CheckExecutionBudget(span);
                var dictKeyStart = baseByte + (int)reader.TokenStartIndex;
                var dictKey = ConvertJsonReaderString(ref reader, context, span);
                var dictKeyEnd = ScanJsonTokenEnd(bytes, dictKeyStart);
                EnsureJsonColon(bytes, dictKeyEnd, input, span, context);
                if (!reader.Read())
                {
                    throw CreateJsonExpectingError(input, baseByte + (int)reader.BytesConsumed, JsonIncompleteExpectation.Value, span, context);
                }

                var dictValueStart = baseByte + (int)reader.TokenStartIndex;
                var dictValueIsContainer = reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray;
                var dictValue = ConvertJsonReaderValue(ref reader, input, baseByte, options, context, span);
                result.SetItem(dictKey, dictValue);
                context.ObserveCollectionCount(result.Count, span);
                dictValueEnd = dictValueIsContainer ? baseByte + (int)reader.BytesConsumed : ScanJsonTokenEnd(bytes, dictValueStart);
            }

            return options.ObjectHook is null
                ? result
                : InvokeJsonCallback(options.ObjectHook, result, context, span);
        }

        private static PyList ConvertJsonReaderArray(ref Utf8JsonReader reader, JsonParseInput input, int baseByte, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
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
                if (!reader.Read())
                {
                    throw CreateJsonExpectingError(input, baseByte + (int)reader.BytesConsumed, JsonIncompleteExpectation.Value, span, context);
                }

                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    if (IsJsonPreviousNonWhitespaceComma(bytes, baseByte + (int)reader.TokenStartIndex))
                    {
                        throw CreateJsonExpectingError(input, baseByte + (int)reader.TokenStartIndex, JsonIncompleteExpectation.Value, span, context);
                    }

                    break;
                }

                if (valueEnd >= 0)
                {
                    EnsureJsonComma(bytes, valueEnd, input, span, context);
                }

                context.CheckExecutionBudget(span);
                var arrayValueStart = baseByte + (int)reader.TokenStartIndex;
                var arrayValueIsContainer = reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray;
                result.Add(ConvertJsonReaderValue(ref reader, input, baseByte, options, context, span));
                context.ObserveCollectionCount(result.Count, span);
                valueEnd = arrayValueIsContainer ? baseByte + (int)reader.BytesConsumed : ScanJsonTokenEnd(bytes, arrayValueStart);
            }

            return result;
        }

        private static object ConvertJsonReaderNumber(ref Utf8JsonReader reader, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            // Single-segment input never sequences, so the value span always holds
            // the complete raw token.
            var raw = Encoding.UTF8.GetString(reader.ValueSpan);
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
                if (reader.TryGetInt64(out var integer))
                {
                    return new BigInteger(integer);
                }

                return OwnFreshInteger(BigInteger.Parse(raw, CultureInfo.InvariantCulture), context.MemoryGovernor, context.Services.State.CallTemporaries, span);
            }

            return reader.GetDouble();
        }

        private static JsonLoadOptions ParseJsonLoadOptions(object[] arguments, LythonSourceSpan span)
        {
            EnsureUnsupportedJsonClassIsNone(GetOptional(arguments, 1), "cls", span);
            return new JsonLoadOptions(
                OptionalJsonCallable(GetOptional(arguments, 2), "object_hook", span),
                OptionalJsonCallable(GetOptional(arguments, 3), "parse_float", span),
                OptionalJsonCallable(GetOptional(arguments, 4), "parse_int", span),
                OptionalJsonCallable(GetOptional(arguments, 5), "parse_constant", span),
                OptionalJsonCallable(GetOptional(arguments, 6), "object_pairs_hook", span),
                ParseJsonBoolOption(GetOptional(arguments, 7), defaultValue: true));
        }

        private static JsonDumpOptions ParseJsonDumpOptions(
            object[] arguments,
            JsonDumpCallForm callForm,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var offset = callForm == JsonDumpCallForm.Dumps ? 0 : 1;
            EnsureUnsupportedJsonClassIsNone(GetOptional(arguments, offset + 5), "cls", span);
            var skipKeys = ParseJsonBoolOption(GetOptional(arguments, offset + 1), defaultValue: false);
            var ensureAscii = ParseJsonBoolOption(GetOptional(arguments, offset + 2), defaultValue: true);
            var checkCircular = ParseJsonBoolOption(GetOptional(arguments, offset + 3), defaultValue: true);
            var allowNan = ParseJsonBoolOption(GetOptional(arguments, offset + 4), defaultValue: true);
            var indent = ParseJsonIndent(GetOptional(arguments, offset + 6), span);
            var separators = ParseJsonSeparators(GetOptional(arguments, offset + 7), indent is not null, span, context);
            var defaultCallable = OptionalJsonCallable(GetOptional(arguments, offset + 8), "default", span);
            var sortKeys = ParseJsonBoolOption(GetOptional(arguments, offset + 9), defaultValue: false);
            return new JsonDumpOptions(skipKeys, ensureAscii, checkCircular, allowNan, indent, separators.ItemSeparator, separators.KeySeparator, defaultCallable, sortKeys);
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
        private static void EnsureJsonComma(ReadOnlySpan<byte> bytes, int valueEnd, JsonParseInput input, LythonSourceSpan span, ExecutionContext context)
        {
            var position = SkipJsonWhitespace(bytes, valueEnd);
            if (position >= bytes.Length || bytes[position] != (byte)',')
            {
                throw CreateJsonExpectingError(input, Math.Min(position, bytes.Length), JsonIncompleteExpectation.Comma, span, context);
            }
        }

        // Requires a colon as the first non-whitespace byte at or after keyEnd,
        // mirroring the comma rule above.
        private static void EnsureJsonColon(ReadOnlySpan<byte> bytes, int keyEnd, JsonParseInput input, LythonSourceSpan span, ExecutionContext context)
        {
            var position = SkipJsonWhitespace(bytes, keyEnd);
            if (position >= bytes.Length || bytes[position] != (byte)':')
            {
                throw CreateJsonExpectingError(input, Math.Min(position, bytes.Length), JsonIncompleteExpectation.Colon, span, context);
            }
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
