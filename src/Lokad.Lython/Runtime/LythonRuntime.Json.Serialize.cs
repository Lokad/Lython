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
        private static PyString SerializeJsonText(object value, JsonDumpOptions options, ExecutionContext context, LythonSourceSpan span)
        {
            try
            {
                var builder = new StringBuilder();
                // Charge output growth as it happens; CreateString charges the
                // final value, at which point this temporary is released.
                using var charge = new JsonGrowthCharge(context.MemoryGovernor, span);
                var active = options.CheckCircular ? new HashSet<object>(ReferenceEqualityComparer.Instance) : null;
                AppendJsonValue(builder, value, options, context, span, depth: 0, defaultDepth: 0, active, charge);
                return CreateString(builder.ToString(), context, span);
            }
            catch (InvalidOperationException ex)
            {
                throw new LythonRuntimeException("TypeError", ex.Message, span);
            }
        }

        /// <summary>
        /// Charges JSON output growth incrementally so a huge document meets
        /// the memory budget while building instead of only at the final
        /// string. Two bytes per character approximate the UTF-16 backing
        /// store; the final string charges separately on ownership transfer.
        /// </summary>
        private sealed class JsonGrowthCharge : IDisposable
        {
            private const int ObserveQuantumChars = 4096;

            private readonly MemoryGovernor.TemporaryMemoryReservation _reservation;
            private readonly LythonSourceSpan? _span;
            private long _chargedChars;

            public JsonGrowthCharge(MemoryGovernor governor, LythonSourceSpan? span)
            {
                _reservation = governor.ReserveTemporary(0, span);
                _span = span;
            }

            public void Observe(int currentLength)
            {
                if (currentLength - _chargedChars < ObserveQuantumChars)
                {
                    return;
                }

                _reservation.Grow(checked(2L * (currentLength - _chargedChars)), _span);
                _chargedChars = currentLength;
            }

            public void Dispose() => _reservation.Dispose();
        }

        private static void AppendJsonValue(
            StringBuilder builder,
            object value,
            JsonDumpOptions options,
            ExecutionContext context,
            LythonSourceSpan span,
            int depth,
            int defaultDepth,
            HashSet<object>? active,
            JsonGrowthCharge charge)
        {
            void AppendDictionary(PyDict dict)
            {
                if (active is not null && !active.Add(dict))
                {
                    throw new LythonRuntimeException("ValueError", "Circular reference detected.", span);
                }

                try
                {
                    if (!options.SortKeys)
                    {
                        // Stream entries in dictionary order without copying them
                        // aside; only the sorted path below pays for scratch.
                        builder.Append("{");
                        var index = 0;
                        foreach (var pair in dict)
                        {
                            context.CheckExecutionBudget(span);
                            if (!IsSupportedJsonObjectKey(pair.Key))
                            {
                                if (options.SkipKeys)
                                {
                                    continue;
                                }

                                throw new InvalidOperationException("json.dumps() requires dictionary keys to be strings, numbers, booleans, or None.");
                            }

                            if (index > 0)
                            {
                                builder.Append(options.ItemSeparator);
                            }

                            AppendJsonValuePrefix(builder, options, depth + 1, index);
                            _ = TryConvertJsonObjectKey(pair.Key, skipKeys: false, out var key);
                            AppendJsonString(builder, key, options.EnsureAscii, charge, context, span);
                            builder.Append(options.KeySeparator);
                            AppendJsonValue(builder, pair.Value, options, context, span, depth + 1, defaultDepth, active, charge);
                            index++;
                        }

                        if (index > 0)
                        {
                            AppendJsonContainerSuffix(builder, options, depth);
                        }

                        builder.Append("}");
                        return;
                    }

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

                    entries.Sort((left, right) => PyComparison.Compare(left.OriginalKey, right.OriginalKey, span));
                    builder.Append("{");
                    for (var index = 0; index < entries.Count; index++)
                    {
                        if (index > 0)
                        {
                            builder.Append(options.ItemSeparator);
                        }

                        AppendJsonValuePrefix(builder, options, depth + 1, index);
                        _ = TryConvertJsonObjectKey(entries[index].OriginalKey, skipKeys: false, out var key);
                        AppendJsonString(builder, key, options.EnsureAscii, charge, context, span);
                        builder.Append(options.KeySeparator);
                        AppendJsonValue(builder, entries[index].Value, options, context, span, depth + 1, defaultDepth, active, charge);
                    }

                    if (entries.Count > 0)
                    {
                        AppendJsonContainerSuffix(builder, options, depth);
                    }

                    builder.Append("}");
                }
                finally
                {
                    _ = active?.Remove(dict);
                }
            }

            context.CheckExecutionBudget(span);
            charge.Observe(builder.Length);
            if (depth >= ExecutionLimits.MaxInterpreterDepth || defaultDepth >= ExecutionLimits.MaxInterpreterDepth)
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
                    AppendJsonString(builder, text.AsString(), options.EnsureAscii, charge, context, span);
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
                    AppendJsonSequence(builder, list, options, context, span, depth, defaultDepth, active, charge);
                    return;
                case PyTuple tuple:
                    AppendJsonSequence(builder, tuple, options, context, span, depth, defaultDepth, active, charge);
                    return;
                case PyDict dict:
                    AppendDictionary(dict);
                    return;
                default:
                    if (options.DefaultCallable is not null)
                    {
                        var replacement = InvokeJsonCallback(options.DefaultCallable, value, context, span);
                        if (ReferenceEquals(replacement, value))
                        {
                            throw new LythonRuntimeException("ValueError", "json.dumps default returned the original unsupported object.", span);
                        }

                        // Replacements that keep producing fresh unsupported objects
                        // would otherwise recurse past the container depth guard, so
                        // default applications carry their own depth budget.
                        AppendJsonValue(builder, replacement, options, context, span, depth, defaultDepth + 1, active, charge);
                        return;
                    }

                    throw new InvalidOperationException($"Unsupported json.dumps value type: {JsonValueTypeName(value)}");
            }
        }

        private static string JsonValueTypeName(object value)
            => value switch
            {
                PyString => "str",
                PyBytes => "bytes",
                PyList => "list",
                PyDict => "dict",
                PyTuple => "tuple",
                PySet => "set",
                bool => "bool",
                BigInteger or int => "int",
                double => "float",
                PyNone => "NoneType",
                null => "NoneType",
                PyInstance instance => instance.Type.Name,
                _ => value.GetType().Name,
            };

        private static void AppendJsonSequence(
            StringBuilder builder,
            IEnumerable<object> sequence,
            JsonDumpOptions options,
            ExecutionContext context,
            LythonSourceSpan span,
            int depth,
            int defaultDepth,
            HashSet<object>? active,
            JsonGrowthCharge charge)
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
                    AppendJsonValue(builder, item, options, context, span, depth + 1, defaultDepth, active, charge);
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

        private static bool TryConvertJsonObjectKey(object keyValue, bool skipKeys, out string key)
        {
            switch (keyValue)
            {
                case PyString text:
                    key = text.AsString();
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
            => key is PyString or BigInteger or int or bool or PyNone ||
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

        private static void AppendJsonString(StringBuilder builder, string text, bool ensureAscii, JsonGrowthCharge charge, ExecutionContext context, LythonSourceSpan span)
        {
            builder.Append('"');
            for (var i = 0; i < text.Length; i++)
            {
                if ((i & 4095) == 0)
                {
                    // Long scalars must stay interruptible and budget-checked.
                    context.CheckExecutionBudget(span);
                    charge.Observe(builder.Length);
                }
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
    }
}
