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

            try
            {
                // Bound the BCL document model before parsing: it materializes
                // the whole input without budget callbacks, so reserve a
                // conservative multiple up front and hold it until the governed
                // values below take ownership.
                using var documentCharge = context.MemoryGovernor.ReserveTemporary(checked(4L * text.Utf8Bytes.Length), span);
                using var document = JsonDocument.Parse(text.Utf8Bytes);
                return ConvertJson(document.RootElement, options, context, span);
            }
            catch (JsonException ex)
            {
                throw CreateJsonDecodeError(text, ex, span, context);
            }
        }

        private static object ConvertJson(JsonElement element, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            context.EnterInterpreterFrame(span);
            try
            {
                return element.ValueKind switch
                {
                    JsonValueKind.Object => ConvertJsonObject(element, options, context, span),
                    JsonValueKind.Array => ConvertJsonArray(element, options, context, span),
                    JsonValueKind.String => JsonStringToPyString(element, context, span),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => PyNone.Instance,
                    JsonValueKind.Number => ConvertJsonNumber(element, options, context, span),
                    _ => throw new InvalidOperationException($"Unsupported JSON value kind: {element.ValueKind}")
                };
            }
            finally
            {
                context.LeaveInterpreterFrame();
            }
        }

        private static object ConvertJsonObject(JsonElement element, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            if (options.ObjectPairsHook is not null)
            {
                // R10: every fresh graph node owns a refundable pool snapshot
                // so dropped parses reclaim on sweep. Hook results adopt via
                // plain TrackCallResult inside InvokeJsonCallback, never
                // refunding an arbitrary callback value that may alias live
                // state.
                var pairs = new PyList([], context.MemoryGovernor, span);
                context.Services.State.CallTemporaries.TrackFreshMutable(pairs, pairs.CommittedStorageBytes, span);
                foreach (var property in element.EnumerateObject())
                {
                    context.CheckExecutionBudget(span);
                    // Track the key before converting the value: a denied
                    // value conversion must not strand the key charge.
                    var key = CreateString(property.Name, context, span);
                    context.Services.State.CallTemporaries.TrackFreshString(key, span);
                    var value = ConvertJson(property.Value, options, context, span);
                    var pair = PyTuple.FromOwnedArray([key, value], context.MemoryGovernor, span);
                    context.Services.State.CallTemporaries.TrackFreshMutable(pair, pair.CommittedStorageBytes, span);
                    pairs.Add(pair);
                    context.ObserveCollectionCount(pairs.Count, span);
                }

                return InvokeJsonCallback(options.ObjectPairsHook, pairs, context, span);
            }

            var result = new PyDict(context.MemoryGovernor, span);
            context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes, span);
            foreach (var property in element.EnumerateObject())
            {
                context.CheckExecutionBudget(span);
                var key = CreateString(property.Name, context, span);
                context.Services.State.CallTemporaries.TrackFreshString(key, span);
                var value = ConvertJson(property.Value, options, context, span);
                result.SetItem(key, value);
                context.ObserveCollectionCount(result.Count, span);
            }

            return options.ObjectHook is null
                ? result
                : InvokeJsonCallback(options.ObjectHook, result, context, span);
        }

        private static PyList ConvertJsonArray(JsonElement element, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            // R10: the fresh list owns a refundable snapshot; each element
            // adopts ownership at its own construction boundary below, so a
            // dropped array reclaims both the backing and every element.
            var result = new PyList([], context.MemoryGovernor, span);
            context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes, span);
            foreach (var item in element.EnumerateArray())
            {
                context.CheckExecutionBudget(span);
                result.Add(ConvertJson(item, options, context, span));
                context.ObserveCollectionCount(result.Count, span);
            }

            return result;
        }

        private static object ConvertJsonNumber(JsonElement element, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            var raw = element.GetRawText();
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
                if (element.TryGetInt64(out var integer))
                {
                    return new BigInteger(integer);
                }

                return OwnFreshInteger(BigInteger.Parse(raw, CultureInfo.InvariantCulture), context.MemoryGovernor, context.Services.State.CallTemporaries, span);
            }

            return element.GetDouble();
        }

        private static JsonLoadOptions ParseJsonLoadOptions(object[] arguments, LythonSourceSpan span)
        {
            EnsureUnsupportedJsonClassIsNone(GetOptional(arguments, 1), "cls", span);
            return new JsonLoadOptions(
                OptionalJsonCallable(GetOptional(arguments, 2), "object_hook", span),
                OptionalJsonCallable(GetOptional(arguments, 3), "parse_float", span),
                OptionalJsonCallable(GetOptional(arguments, 4), "parse_int", span),
                OptionalJsonCallable(GetOptional(arguments, 5), "parse_constant", span),
                OptionalJsonCallable(GetOptional(arguments, 6), "object_pairs_hook", span));
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
    }
}
