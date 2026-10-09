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
        // Callers fund the retained reference pairs before entering. CPython
        // sorts the original keys before applying skipkeys or key validation.
        internal static List<(object OriginalKey, object Value)> CollectSortedJsonEntries(
            PyDict dict, ExecutionContext context, LythonSourceSpan span)
        {
            var entries = new List<(object OriginalKey, object Value)>(dict.Count);
            foreach (var pair in dict)
            {
                context.CheckExecution(span);
                entries.Add((pair.Key, pair.Value));
            }
            ExecutionSort.Sort(entries, context, span, (left, right) => PyComparison.Compare(left.OriginalKey, right.OriginalKey, span));
            return entries;
        }

        internal static PyString SerializeJsonText(object value, JsonDumpOptions options, ExecutionContext context, LythonSourceSpan span)
            => SerializeJsonTextCoreAsync(value, options, context, span, false).GetAwaiter().GetResult();

        internal static ValueTask<PyString> SerializeJsonTextAsync(object value, JsonDumpOptions options, ExecutionContext context, LythonSourceSpan span)
            => SerializeJsonTextCoreAsync(value, options, context, span, true);

        private static async ValueTask<PyString> SerializeJsonTextCoreAsync(object value, JsonDumpOptions options, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            try
            {
                var builder = new StringBuilder();
                // Charge output growth as it happens; CreateString charges the
                // final value, at which point this temporary is released.
                using var charge = new JsonGrowthCharge(context.MemoryGovernor, span);
                var active = options.CheckCircular ? new HashSet<object>(ReferenceEqualityComparer.Instance) : null;
                await AppendJsonValueAsync(builder, value, options, context, span, depth: 0, defaultDepth: 0, active, charge, asynchronous).ConfigureAwait(false);
                // The UTF-16 copy below coexists briefly with the builder backing
                // and the final UTF-8 value: fund it before duplicating.
                charge.Grow(builder.Length);
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
        internal sealed class JsonGrowthCharge : IDisposable
        {
            private const int ObserveQuantumChars = 1024;

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

            // Funds a variable-sized text append before it lands, so arbitrary
            // strings (indent units, separators, large numbers) deny before
            // allocating instead of up to a quantum afterwards.
            public void Grow(long chars)
            {
                if (chars > 0)
                {
                    _reservation.Grow(checked(2L * chars), _span);
                    _chargedChars += chars;
                }
            }

            // Funds non-text scratch (such as the sort entry list) that lives
            // beside the builder for the remainder of the dump.
            public void GrowBytes(long bytes)
            {
                if (bytes > 0)
                {
                    _reservation.Grow(bytes, _span);
                }
            }

            public void Dispose() => _reservation.Dispose();
        }

        private static async ValueTask AppendJsonValueAsync(
            StringBuilder builder,
            object value,
            JsonDumpOptions options,
            ExecutionContext context,
            LythonSourceSpan span,
            int depth,
            int defaultDepth,
            HashSet<object>? active,
            JsonGrowthCharge charge,
            bool asynchronous)
        {
            async ValueTask AppendDictionaryAsync(PyDict dict)
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
                            context.CheckExecution(span);
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
                                AppendSeparator(builder, options.ItemSeparator, charge);
                            }

                            AppendJsonValuePrefix(builder, options, depth + 1, index, charge, context, span);
                            _ = TryConvertJsonObjectKey(pair.Key, skipKeys: false, out var key);
                            AppendJsonString(builder, key, options.EnsureAscii, charge, context, span);
                            AppendSeparator(builder, options.KeySeparator, charge);
                            await AppendJsonValueAsync(builder, pair.Value, options, context, span, depth + 1, defaultDepth, active, charge, asynchronous).ConfigureAwait(false);
                            index++;
                        }

                        if (index > 0)
                        {
                            AppendJsonContainerSuffix(builder, options, depth, charge, context, span);
                        }

                        builder.Append("}");
                        return;
                    }

                    // Sorting retains one reference pair per entry beside the live
                    // dict: fund that scratch (pairs plus backing) before building
                    // it instead of growing uncharged.
                    charge.GrowBytes(checked(32L * dict.Count));
                    var entries = CollectSortedJsonEntries(dict, context, span);
                    builder.Append("{");
                    var emitted = 0;
                    foreach (var entry in entries)
                    {
                        if (!IsSupportedJsonObjectKey(entry.OriginalKey))
                        {
                            if (options.SkipKeys) continue;
                            throw new InvalidOperationException("json.dumps() requires dictionary keys to be strings, numbers, booleans, or None.");
                        }
                        if (emitted > 0)
                        {
                            AppendSeparator(builder, options.ItemSeparator, charge);
                        }

                        AppendJsonValuePrefix(builder, options, depth + 1, emitted, charge, context, span);
                        _ = TryConvertJsonObjectKey(entry.OriginalKey, skipKeys: false, out var key);
                        AppendJsonString(builder, key, options.EnsureAscii, charge, context, span);
                        AppendSeparator(builder, options.KeySeparator, charge);
                        await AppendJsonValueAsync(builder, entry.Value, options, context, span, depth + 1, defaultDepth, active, charge, asynchronous).ConfigureAwait(false);
                        emitted++;
                    }

                    if (emitted > 0)
                    {
                        AppendJsonContainerSuffix(builder, options, depth, charge, context, span);
                    }

                    builder.Append("}");
                }
                finally
                {
                    _ = active?.Remove(dict);
                }
            }

            context.CheckExecution(span);
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
                case object numericValue when PyNumberOps.TryAsInteger(numericValue, out var integer):
                    // Giant magnitudes render slowly: bracket the conversion itself.
                    context.CheckExecution(span);
                    AppendJsonNumber(builder, integer.ToString(CultureInfo.InvariantCulture), charge, context, span);
                    return;
                case double floating:
                    AppendJsonDouble(builder, floating, options, charge, context, span);
                    return;
                case PyDecimal decimalValue:
                    AppendJsonNumber(builder, PyDecimalOps.Format(decimalValue), charge, context, span);
                    return;
                case PyList list:
                    await AppendJsonSequenceAsync(builder, list, options, context, span, depth, defaultDepth, active, charge, asynchronous).ConfigureAwait(false);
                    return;
                case PyTuple tuple:
                    await AppendJsonSequenceAsync(builder, tuple, options, context, span, depth, defaultDepth, active, charge, asynchronous).ConfigureAwait(false);
                    return;
                case PyDict dict:
                    await AppendDictionaryAsync(dict).ConfigureAwait(false);
                    return;
                case PyCounter counter:
                    await AppendDictionaryAsync(counter.InnerDict).ConfigureAwait(false);
                    return;
                case PyDefaultDict defaultDict:
                    await AppendDictionaryAsync(defaultDict.InnerDict).ConfigureAwait(false);
                    return;
                default:
                    if (options.DefaultCallable is not null)
                    {
                        var replacement = await InvokeJsonCallbackCoreAsync(options.DefaultCallable, value, context, span, asynchronous).ConfigureAwait(false);
                        if (ReferenceEquals(replacement, value))
                        {
                            throw new LythonRuntimeException("ValueError", "json.dumps default returned the original unsupported object.", span);
                        }

                        // Replacements that keep producing fresh unsupported objects
                        // would otherwise recurse past the container depth guard, so
                        // default applications carry their own depth budget.
                        await AppendJsonValueAsync(builder, replacement, options, context, span, depth, defaultDepth + 1, active, charge, asynchronous).ConfigureAwait(false);
                        return;
                    }

                    throw new InvalidOperationException($"Unsupported json.dumps value type: {JsonValueTypeName(value)}");
            }
        }

        internal static string JsonValueTypeName(object value)
            => value switch
            {
                PyString => "str",
                PyBytes => "bytes",
                PyList => "list",
                PyDict => "dict",
                PyTuple => "tuple",
                PySet => "set",
                bool => "bool",
                _ when PyNumberOps.IsInteger(value) => "int",
                double => "float",
                PyNone => "NoneType",
                null => "NoneType",
                PyInstance instance => instance.Type.Name,
                _ => value.GetType().Name,
            };

        private static async ValueTask AppendJsonSequenceAsync(
            StringBuilder builder,
            IEnumerable<object> sequence,
            JsonDumpOptions options,
            ExecutionContext context,
            LythonSourceSpan span,
            int depth,
            int defaultDepth,
            HashSet<object>? active,
            JsonGrowthCharge charge, bool asynchronous)
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
                        AppendSeparator(builder, options.ItemSeparator, charge);
                    }

                    AppendJsonValuePrefix(builder, options, depth + 1, index, charge, context, span);
                    await AppendJsonValueAsync(builder, item, options, context, span, depth + 1, defaultDepth, active, charge, asynchronous).ConfigureAwait(false);
                    index++;
                }

                if (index > 0)
                {
                    AppendJsonContainerSuffix(builder, options, depth, charge, context, span);
                }

                builder.Append(']');
            }
            finally
            {
                _ = active?.Remove(sequence);
            }
        }

        internal static bool TryConvertJsonObjectKey(object keyValue, bool skipKeys, out string key)
        {
            switch (keyValue)
            {
                case PyString text:
                    key = text.AsString();
                    return true;
                case bool boolean:
                    key = boolean ? "true" : "false";
                    return true;
                case object numericValue when PyNumberOps.TryAsInteger(numericValue, out var integer):
                    key = integer.ToString(CultureInfo.InvariantCulture);
                    return true;
                case double floating when double.IsFinite(floating):
                    key = Numbers.PyNumberOps.RenderFloat(floating);
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

        internal static bool IsSupportedJsonObjectKey(object key)
            => key is PyString or PyNone || PyNumberOps.IsInteger(key) ||
               key is double floating && double.IsFinite(floating);

        internal static void AppendJsonValuePrefix(StringBuilder builder, JsonDumpOptions options, int depth, int index, JsonGrowthCharge charge, ExecutionContext context, LythonSourceSpan span)
        {
            _ = index;
            if (options.IndentUnit is null)
            {
                return;
            }

            builder.Append('\n');
            AppendJsonIndent(builder, options, depth, charge, context, span);
        }

        internal static void AppendJsonContainerSuffix(StringBuilder builder, JsonDumpOptions options, int depth, JsonGrowthCharge charge, ExecutionContext context, LythonSourceSpan span)
        {
            if (options.IndentUnit is null)
            {
                return;
            }

            builder.Append('\n');
            AppendJsonIndent(builder, options, depth, charge, context, span);
        }

        private static void AppendJsonIndent(StringBuilder builder, JsonDumpOptions options, int depth, JsonGrowthCharge charge, ExecutionContext context, LythonSourceSpan span)
        {
            // An arbitrary indent unit repeats once per nesting level: fund the
            // whole run before writing any of it, staying interruptible.
            charge.Grow(checked((long)options.IndentUnit!.Length * depth));
            for (var i = 0; i < depth; i++)
            {
                context.CheckExecution(span);
                builder.Append(options.IndentUnit);
            }
        }

        internal static void AppendSeparator(StringBuilder builder, string separator, JsonGrowthCharge charge)
        {
            charge.Grow(separator.Length);
            builder.Append(separator);
        }

        internal static void AppendJsonString(StringBuilder builder, string text, bool ensureAscii, JsonGrowthCharge charge, ExecutionContext context, LythonSourceSpan span)
        {
            builder.Append('"');
            for (var i = 0; i < text.Length; i++)
            {
                if ((i & 4095) == 0)
                {
                    // Long scalars must stay interruptible and budget-checked.
                    context.CheckExecution(span);
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

        // Large-number writes bypass the scalar loop below, so fund the
        // rendered text before it lands and stay interruptible across the
        // (potentially slow) conversion itself.
        internal static void AppendJsonNumber(StringBuilder builder, string rendered, JsonGrowthCharge charge, ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            charge.Grow(rendered.Length);
            builder.Append(rendered);
        }

        internal static void AppendJsonDouble(StringBuilder builder, double value, JsonDumpOptions options, JsonGrowthCharge charge, ExecutionContext context, LythonSourceSpan span)
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

            AppendJsonNumber(builder, Numbers.PyNumberOps.RenderFloat(value), charge, context, span);
        }
    }
}
