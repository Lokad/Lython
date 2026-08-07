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
    private sealed class JsonModule : PyModule
    {
        public static readonly JsonModule Instance = new();

        private JsonModule() : base("json")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "load" => new BuiltinCallable(LythonKnownCallableSignatures.JsonLoad, Load),
                "loads" => new BuiltinCallable(LythonKnownCallableSignatures.JsonLoads, Loads),
                "dump" => new BuiltinCallable(LythonKnownCallableSignatures.JsonDump, Dump),
                "dumps" => new BuiltinCallable(LythonKnownCallableSignatures.JsonDumps, Dumps),
                "JSONDecodeError" => new ExceptionTypeValue("JSONDecodeError"),
                "JSONEncoder" => new UnsupportedJsonClassFactory("json.JSONEncoder"),
                "JSONDecoder" => new UnsupportedJsonClassFactory("json.JSONDecoder"),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private object Load(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length < 1 || arguments[0] is not ExecutionContext.TextFileHandle file)
            {
                throw new LythonRuntimeException("TypeError", "json.load(fp, *, ...) expects a readable text file handle.", span);
            }

            var loadOptions = ParseJsonLoadOptions(arguments, span);
            return ParseJsonText(file.Read(), loadOptions, context, span);
        }

        private object Loads(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length < 1 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "json.loads(s, *, ...) expects a string argument.", span);
            }

            var loadOptions = ParseJsonLoadOptions(arguments, span);
            return ParseJsonText(text, loadOptions, context, span);
        }

        private object Dump(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length < 2 || arguments[1] is not ExecutionContext.TextFileHandle file)
            {
                throw new LythonRuntimeException("TypeError", "json.dump(obj, fp, *, ...) expects an object and writable text file handle.", span);
            }

            var text = SerializeJsonText(arguments[0], ParseJsonDumpOptions(arguments, JsonDumpCallForm.Dump, span), context, span);
            _ = file.Write(text);
            return PyNone.Instance;
        }

        private object Dumps(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length < 1)
            {
                throw new LythonRuntimeException("TypeError", "json.dumps(obj, *, ...) expects one object argument.", span);
            }

            return SerializeJsonText(arguments[0], ParseJsonDumpOptions(arguments, JsonDumpCallForm.Dumps, span), context, span);
        }

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
                    pairs.Add(new PyTuple([
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
            LythonSourceSpan span)
        {
            var offset = callForm == JsonDumpCallForm.Dumps ? 0 : 1;
            EnsureUnsupportedJsonClassIsNone(GetOptional(arguments, offset + 5), "cls", span);
            var skipKeys = ParseJsonBoolOption(GetOptional(arguments, offset + 1), defaultValue: false);
            var ensureAscii = ParseJsonBoolOption(GetOptional(arguments, offset + 2), defaultValue: true);
            var checkCircular = ParseJsonBoolOption(GetOptional(arguments, offset + 3), defaultValue: true);
            var allowNan = ParseJsonBoolOption(GetOptional(arguments, offset + 4), defaultValue: true);
            var indent = ParseJsonIndent(GetOptional(arguments, offset + 6), span);
            var separators = ParseJsonSeparators(GetOptional(arguments, offset + 7), indent is not null, span);
            var defaultCallable = OptionalJsonCallable(GetOptional(arguments, offset + 8), "default", span);
            var sortKeys = ParseJsonBoolOption(GetOptional(arguments, offset + 9), defaultValue: false);
            return new JsonDumpOptions(skipKeys, ensureAscii, checkCircular, allowNan, indent, separators.ItemSeparator, separators.KeySeparator, defaultCallable, sortKeys);
        }

        private static PyString SerializeJsonText(object value, JsonDumpOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            try
            {
                var builder = new StringBuilder();
                var active = options.CheckCircular ? new HashSet<object>(ReferenceEqualityComparer.Instance) : null;
                AppendJsonValue(builder, value, options, context, span, depth: 0, active);
                return CreateString(builder.ToString(), context, span);
            }
            catch (InvalidOperationException ex)
            {
                throw new LythonRuntimeException("TypeError", ex.Message, span);
            }
        }

        private static void AppendJsonValue(
            StringBuilder builder,
            object value,
            JsonDumpOptions options,
            ExecutionContext context,
            LythonSourceSpan span,
            int depth,
            HashSet<object>? active)
        {
            context.CheckExecutionBudget(span);
            if (depth >= ExecutionLimits.MaxInterpreterDepth)
            {
                throw new LythonRuntimeException(
                    "RecursionError",
                    "maximum recursion depth exceeded while encoding a JSON object",
                    span);
            }

            switch (value)
            {
                case PyNone:
                    builder.Append("null");
                    return;
                case PyString text:
                    AppendJsonString(builder, text.AsString(), options.EnsureAscii);
                    return;
                case string text:
                    AppendJsonString(builder, text, options.EnsureAscii);
                    return;
                case bool boolean:
                    builder.Append(boolean ? "true" : "false");
                    return;
                case BigInteger integer:
                    builder.Append(integer.ToString(CultureInfo.InvariantCulture));
                    return;
                case int integer:
                    builder.Append(integer.ToString(CultureInfo.InvariantCulture));
                    return;
                case double floating:
                    AppendJsonDouble(builder, floating, options, span);
                    return;
                case PyDecimal decimalValue:
                    builder.Append(PyDecimalOps.Format(decimalValue));
                    return;
                case PyList list:
                    AppendJsonSequence(builder, list, options, context, span, depth, active);
                    return;
                case PyTuple tuple:
                    AppendJsonSequence(builder, tuple, options, context, span, depth, active);
                    return;
                case PyDict dict:
                    AppendJsonDict(builder, dict, options, context, span, depth, active);
                    return;
                default:
                    if (options.DefaultCallable is not null)
                    {
                        var replacement = InvokeJsonCallback(options.DefaultCallable, value, context, span);
                        if (ReferenceEquals(replacement, value))
                        {
                            throw new LythonRuntimeException("ValueError", "json.dumps default returned the original unsupported object.", span);
                        }

                        AppendJsonValue(builder, replacement, options, context, span, depth, active);
                        return;
                    }

                    throw new InvalidOperationException($"Unsupported json.dumps value type: {value.GetType().Name}");
            }
        }

        private static void AppendJsonSequence(
            StringBuilder builder,
            IEnumerable<object> sequence,
            JsonDumpOptions options,
            ExecutionContext context,
            LythonSourceSpan span,
            int depth,
            HashSet<object>? active)
        {
            if (active is not null && !active.Add(sequence))
            {
                throw new LythonRuntimeException("ValueError", "Circular reference detected.", span);
            }

            try
            {
                var items = sequence as IReadOnlyCollection<object> ?? sequence.ToArray();
                builder.Append('[');
                var index = 0;
                foreach (var item in items)
                {
                    if (index > 0)
                    {
                        builder.Append(options.ItemSeparator);
                    }

                    AppendJsonValuePrefix(builder, options, depth + 1, index);
                    AppendJsonValue(builder, item, options, context, span, depth + 1, active);
                    index++;
                }

                if (index > 0)
                {
                    AppendJsonContainerSuffix(builder, options, depth);
                }

                builder.Append(']');
            }
            finally
            {
                _ = active?.Remove(sequence);
            }
        }

        private static void AppendJsonDict(
            StringBuilder builder,
            PyDict dict,
            JsonDumpOptions options,
            ExecutionContext context,
            LythonSourceSpan span,
            int depth,
            HashSet<object>? active)
        {
            if (active is not null && !active.Add(dict))
            {
                throw new LythonRuntimeException("ValueError", "Circular reference detected.", span);
            }

            try
            {
                var entries = new List<(object OriginalKey, object Value)>();
                foreach (var pair in dict)
                {
                    context.CheckExecutionBudget(span);
                    if (IsSupportedJsonObjectKey(pair.Key))
                    {
                        entries.Add((pair.Key, pair.Value));
                    }
                    else if (!options.SkipKeys)
                    {
                        throw new InvalidOperationException("json.dumps() requires dictionary keys to be strings, numbers, booleans, or None.");
                    }
                }

                if (options.SortKeys)
                {
                    entries.Sort((left, right) => PyComparison.Compare(left.OriginalKey, right.OriginalKey, span));
                }

                builder.Append('{');
                for (var index = 0; index < entries.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(options.ItemSeparator);
                    }

                    AppendJsonValuePrefix(builder, options, depth + 1, index);
                    _ = TryConvertJsonObjectKey(entries[index].OriginalKey, skipKeys: false, out var key);
                    AppendJsonString(builder, key, options.EnsureAscii);
                    builder.Append(options.KeySeparator);
                    AppendJsonValue(builder, entries[index].Value, options, context, span, depth + 1, active);
                }

                if (entries.Count > 0)
                {
                    AppendJsonContainerSuffix(builder, options, depth);
                }

                builder.Append('}');
            }
            finally
            {
                _ = active?.Remove(dict);
            }
        }

        private static bool TryConvertJsonObjectKey(object keyValue, bool skipKeys, out string key)
        {
            switch (keyValue)
            {
                case PyString text:
                    key = text.AsString();
                    return true;
                case string text:
                    key = text;
                    return true;
                case BigInteger integer:
                    key = integer.ToString(CultureInfo.InvariantCulture);
                    return true;
                case int integer:
                    key = integer.ToString(CultureInfo.InvariantCulture);
                    return true;
                case double floating when double.IsFinite(floating):
                    key = Numbers.PyNumberOps.RenderFloat(floating);
                    return true;
                case bool boolean:
                    key = boolean ? "true" : "false";
                    return true;
                case PyNone:
                    key = "null";
                    return true;
                default:
                    if (skipKeys)
                    {
                        key = string.Empty;
                        return false;
                    }

                    throw new InvalidOperationException("json.dumps() requires dictionary keys to be strings, numbers, booleans, or None.");
            }
        }

        private static bool IsSupportedJsonObjectKey(object key)
            => key is PyString or string or BigInteger or int or bool or PyNone ||
               key is double floating && double.IsFinite(floating);

        private static void AppendJsonValuePrefix(StringBuilder builder, JsonDumpOptions options, int depth, int index)
        {
            _ = index;
            if (options.IndentUnit is null)
            {
                return;
            }

            builder.Append('\n');
            AppendJsonIndent(builder, options, depth);
        }

        private static void AppendJsonContainerSuffix(StringBuilder builder, JsonDumpOptions options, int depth)
        {
            if (options.IndentUnit is null)
            {
                return;
            }

            builder.Append('\n');
            AppendJsonIndent(builder, options, depth);
        }

        private static void AppendJsonIndent(StringBuilder builder, JsonDumpOptions options, int depth)
        {
            for (var i = 0; i < depth; i++)
            {
                builder.Append(options.IndentUnit);
            }
        }

        private static void AppendJsonString(StringBuilder builder, string text, bool ensureAscii)
        {
            builder.Append('"');
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                switch (ch)
                {
                    case '"':
                        builder.Append("\\\"");
                        continue;
                    case '\\':
                        builder.Append("\\\\");
                        continue;
                    case '\b':
                        builder.Append("\\b");
                        continue;
                    case '\f':
                        builder.Append("\\f");
                        continue;
                    case '\n':
                        builder.Append("\\n");
                        continue;
                    case '\r':
                        builder.Append("\\r");
                        continue;
                    case '\t':
                        builder.Append("\\t");
                        continue;
                }

                if (ch < 0x20 || ensureAscii && ch > 0x7f)
                {
                    builder.Append("\\u");
                    builder.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    continue;
                }

                builder.Append(ch);
            }

            builder.Append('"');
        }

        private static void AppendJsonDouble(StringBuilder builder, double value, JsonDumpOptions options, LythonSourceSpan span)
        {
            if (double.IsNaN(value))
            {
                if (!options.AllowNan)
                {
                    throw new LythonRuntimeException("ValueError", "Out of range float values are not JSON compliant.", span);
                }

                builder.Append("NaN");
                return;
            }

            if (double.IsPositiveInfinity(value))
            {
                if (!options.AllowNan)
                {
                    throw new LythonRuntimeException("ValueError", "Out of range float values are not JSON compliant.", span);
                }

                builder.Append("Infinity");
                return;
            }

            if (double.IsNegativeInfinity(value))
            {
                if (!options.AllowNan)
                {
                    throw new LythonRuntimeException("ValueError", "Out of range float values are not JSON compliant.", span);
                }

                builder.Append("-Infinity");
                return;
            }

            builder.Append(Numbers.PyNumberOps.RenderFloat(value));
        }

        private static bool TryConvertJsonConstant(PyString text, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            var trimmed = text.AsString().Trim();
            if (trimmed is not ("NaN" or "Infinity" or "-Infinity"))
            {
                value = PyNone.Instance;
                return false;
            }

            var constantText = CreateString(trimmed, context, span);
            value = options.ParseConstant is not null
                ? InvokeJsonCallback(options.ParseConstant, constantText, context, span)
                : trimmed switch
                {
                    "NaN" => double.NaN,
                    "Infinity" => double.PositiveInfinity,
                    _ => double.NegativeInfinity
                };
            return true;
        }

        private static LythonRuntimeException CreateJsonDecodeError(PyString document, JsonException exception, LythonSourceSpan span, ExecutionContext context)
        {
            var reportedBytePosition = ComputeJsonErrorBytePosition(
                document.Utf8Bytes.Span,
                exception.LineNumber.GetValueOrDefault(),
                exception.BytePositionInLine.GetValueOrDefault());
            var bytePosition = NormalizeJsonErrorBytePosition(document.Utf8Bytes.Span, reportedBytePosition, exception.Message);
            var position = document.ByteIndexToRuneIndex(bytePosition);
            var location = ComputeJsonErrorLocation(document, bytePosition);
            var payload = new PyDict(context.MemoryGovernor, span);
            payload.SetItem(PyString.FromString("msg"), CreateString(exception.Message, context, span));
            payload.SetItem(PyString.FromString("doc"), document);
            payload.SetItem(PyString.FromString("pos"), new BigInteger(position));
            payload.SetItem(PyString.FromString("lineno"), new BigInteger(location.Line));
            payload.SetItem(PyString.FromString("colno"), new BigInteger(location.Column));
            return new LythonRuntimeException("JSONDecodeError", exception.Message, span, exception, payload);
        }

        private static int ComputeJsonErrorBytePosition(ReadOnlySpan<byte> text, long lineNumber, long bytePositionInLine)
        {
            var line = 0L;
            var position = 0;
            while (line < lineNumber && position < text.Length)
            {
                if (text[position++] == '\n')
                {
                    line++;
                }
            }

            return (int)Math.Min(text.Length, position + bytePositionInLine);
        }

        private static int NormalizeJsonErrorBytePosition(ReadOnlySpan<byte> text, int reportedPosition, string message)
        {
            if (message.Contains("invalid escapable character", StringComparison.Ordinal) &&
                reportedPosition > 0 && text[reportedPosition - 1] == (byte)'\\')
            {
                return reportedPosition - 1;
            }

            if (message.Contains("not a hex digit following '\\u'", StringComparison.Ordinal))
            {
                for (var position = Math.Min(reportedPosition - 1, text.Length - 1);
                     position > 0 && position >= reportedPosition - 6;
                     position--)
                {
                    if (text[position] == (byte)'u' && text[position - 1] == (byte)'\\')
                    {
                        return position;
                    }
                }
            }

            if (message.Contains("Expected end of string", StringComparison.Ordinal))
            {
                var openingQuote = FindUnterminatedJsonStringStart(text, reportedPosition);
                if (openingQuote >= 0)
                {
                    return openingQuote;
                }
            }

            if (message.Contains("invalid JSON literal", StringComparison.Ordinal))
            {
                var position = Math.Min(reportedPosition, text.Length);
                while (position > 0 && IsAsciiLetter(text[position - 1]))
                {
                    position--;
                }

                return position;
            }

            if (message.Contains("invalid within a number", StringComparison.Ordinal) ||
                message.Contains("Expected a digit ('0'-'9'), but instead reached end of data", StringComparison.Ordinal))
            {
                var tokenStart = reportedPosition;
                while (tokenStart > 0 && IsJsonNumberTokenByte(text[tokenStart - 1]))
                {
                    tokenStart--;
                }

                for (var position = tokenStart; position < reportedPosition; position++)
                {
                    if (text[position] is (byte)'.' or (byte)'e' or (byte)'E')
                    {
                        return position;
                    }
                }

                return tokenStart;
            }

            return reportedPosition;
        }

        private static int FindUnterminatedJsonStringStart(ReadOnlySpan<byte> text, int end)
        {
            var openingQuote = -1;
            var escaped = false;
            for (var position = 0; position < Math.Min(end, text.Length); position++)
            {
                if (openingQuote < 0)
                {
                    if (text[position] == (byte)'"')
                    {
                        openingQuote = position;
                    }
                }
                else if (escaped)
                {
                    escaped = false;
                }
                else if (text[position] == (byte)'\\')
                {
                    escaped = true;
                }
                else if (text[position] == (byte)'"')
                {
                    openingQuote = -1;
                }
            }

            return openingQuote;
        }

        private static JsonSourceLocation ComputeJsonErrorLocation(PyString document, int bytePosition)
        {
            var line = 1;
            var lineStart = 0;
            var bytes = document.Utf8Bytes.Span;
            for (var position = 0; position < Math.Min(bytePosition, bytes.Length); position++)
            {
                if (bytes[position] == (byte)'\n')
                {
                    line++;
                    lineStart = position + 1;
                }
            }

            var column = document.ByteIndexToRuneIndex(bytePosition) - document.ByteIndexToRuneIndex(lineStart) + 1;
            return new JsonSourceLocation(line, column);
        }

        private static bool IsAsciiLetter(byte value)
            => value is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z';

        private static bool IsJsonNumberTokenByte(byte value)
            => value is >= (byte)'0' and <= (byte)'9' or (byte)'-' or (byte)'+' or (byte)'.' or (byte)'e' or (byte)'E';

        private static object InvokeJsonCallback(object callable, object argument, ExecutionContext context, LythonSourceSpan span)
            => InvokeCallableTarget(callable, span, span, context, [CallArgumentValue.Positional(argument)]);

        private static object? OptionalJsonCallable(object value, string parameterName, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return null;
            }

            if (value is ICallable)
            {
                return value;
            }

            throw new LythonRuntimeException("TypeError", $"json option {parameterName}=... expects a callable or None.", span);
        }

        private static void EnsureUnsupportedJsonClassIsNone(object value, string parameterName, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return;
            }

            throw new LythonRuntimeException("NotImplementedError", $"json {parameterName}=... custom encoder/decoder classes are not supported by Lython.", span);
        }

        private static bool ParseJsonBoolOption(object value, bool defaultValue)
            => ReferenceEquals(value, PyNone.Instance) ? defaultValue : IsTruthy(value);

        private static string? ParseJsonIndent(object value, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return null;
            }

            if (PyStringOps.TryAsString(value, out var text))
            {
                return text.AsString();
            }

            if (value is BigInteger integer)
            {
                if (integer <= BigInteger.Zero)
                {
                    return string.Empty;
                }

                if (integer > 32)
                {
                    throw new LythonRuntimeException("OverflowError", "json.dumps(indent=...) is too large for Lython.", span);
                }

                return new string(' ', (int)integer);
            }

            throw new LythonRuntimeException("TypeError", "json.dumps(indent=...) expects an integer, string, or None.", span);
        }

        private static JsonSeparators ParseJsonSeparators(object value, bool pretty, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return pretty ? new JsonSeparators(",", ": ") : new JsonSeparators(", ", ": ");
            }

            if (value is not PyTuple and not PyList)
            {
                throw new LythonRuntimeException("TypeError", "json.dumps(separators=...) expects a two-item tuple/list of strings or None.", span);
            }

            var items = ToSequence(value, span).ToArray();
            if (items.Length != 2 ||
                !PyStringOps.TryAsString(items[0], out var itemSeparator) ||
                !PyStringOps.TryAsString(items[1], out var keySeparator))
            {
                throw new LythonRuntimeException("TypeError", "json.dumps(separators=...) expects a two-item tuple/list of strings.", span);
            }

            return new JsonSeparators(itemSeparator.AsString(), keySeparator.AsString());
        }

        private static object GetOptional(object[] arguments, int index)
            => index < arguments.Length ? arguments[index] : PyNone.Instance;

        private sealed record JsonLoadOptions(
            object? ObjectHook,
            object? ParseFloat,
            object? ParseInt,
            object? ParseConstant,
            object? ObjectPairsHook);

        private enum JsonDumpCallForm
        {
            Dump,
            Dumps
        }

        private readonly record struct JsonSeparators(string ItemSeparator, string KeySeparator);

        private readonly record struct JsonSourceLocation(int Line, int Column);

        private sealed record JsonDumpOptions(
            bool SkipKeys,
            bool EnsureAscii,
            bool CheckCircular,
            bool AllowNan,
            string? IndentUnit,
            string ItemSeparator,
            string KeySeparator,
            object? DefaultCallable,
            bool SortKeys);

        private sealed class UnsupportedJsonClassFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
        {
            public UnsupportedJsonClassFactory(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                _ = arguments;
                context.CheckExecutionBudget(span);
                throw new LythonRuntimeException("NotImplementedError", $"{Name} custom classes are not supported by Lython's JSON subset.", span);
            }

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString("<class '" + Name + "'>");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }

        private static PyString JsonStringToPyString(JsonElement element, ExecutionContext context, LythonSourceSpan span)
        {
            return CreateString(element.GetString() ?? string.Empty, context, span);
        }

    }

}
