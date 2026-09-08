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
                var pairs = new PyList([], context.MemoryGovernor, span);
                foreach (var property in element.EnumerateObject())
                {
                    context.CheckExecutionBudget(span);
                    pairs.Add(PyTuple.FromOwnedArray([
                        CreateString(property.Name, context, span),
                        ConvertJson(property.Value, options, context, span)
                    ], context.MemoryGovernor, span));
                    context.ObserveCollectionCount(pairs.Count, span);
                }

                return InvokeJsonCallback(options.ObjectPairsHook, pairs, context, span);
            }

            var result = new PyDict(context.MemoryGovernor, span);
            foreach (var property in element.EnumerateObject())
            {
                context.CheckExecutionBudget(span);
                result.SetItem(CreateString(property.Name, context, span), ConvertJson(property.Value, options, context, span));
                context.ObserveCollectionCount(result.Count, span);
            }

            return options.ObjectHook is null
                ? result
                : InvokeJsonCallback(options.ObjectHook, result, context, span);
        }

        private static PyList ConvertJsonArray(JsonElement element, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            var result = new PyList([], context.MemoryGovernor, span);
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
                return InvokeJsonCallback(options.ParseFloat, CreateString(raw, context, span), context, span);
            }

            if (!isFloat && options.ParseInt is not null)
            {
                return InvokeJsonCallback(options.ParseInt, CreateString(raw, context, span), context, span);
            }

            if (element.TryGetInt64(out var integer))
            {
                return new BigInteger(integer);
            }

            if (!isFloat)
            {
                return BigInteger.Parse(raw, CultureInfo.InvariantCulture);
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
