using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

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
                // Charge output growth as it happens; the final string factory
                // charges the result before this temporary is released.
                using var charge = new JsonGrowthCharge(context.MemoryGovernor, span);
                var active = options.CheckCircular ? new HashSet<object>(ReferenceEqualityComparer.Instance) : null;
                await AppendJsonValueAsync(builder, value, options, context, span, depth: 0, defaultDepth: 0, active, charge, asynchronous).ConfigureAwait(false);
                return FinishJsonText(builder, charge, context, span);
            }
            catch (InvalidOperationException ex)
            {
                throw new LythonRuntimeException("TypeError", ex.Message, span);
            }
        }

        internal static PyString FinishJsonText(StringBuilder builder, JsonGrowthCharge charge, ExecutionContext context, LythonSourceSpan span)
        {
            // Small results use bounded managed control storage. The existing
            // span factory admits retained UTF-8 storage before allocation and
            // copies the bytes; no view of these stack buffers can escape.
            if (builder.Length <= 128)
            {
                Span<char> characters = stackalloc char[128];
                builder.CopyTo(0, characters, builder.Length);
                Span<byte> utf8 = stackalloc byte[384];
                var written = StrictUtf8.GetBytes(characters[..builder.Length], utf8);
                return PyString.CopyFromUtf8(utf8[..written], context.MemoryGovernor, span);
            }

            // The large UTF-16 copy coexists with the builder and final UTF-8.
            charge.Grow(builder.Length);
            return CreateString(builder.ToString(), context, span);
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

        private static async ValueTask AppendJsonDictionaryAsync(
            StringBuilder builder,
            PyDict dict,
            JsonDumpOptions options,
            ExecutionContext context,
            LythonSourceSpan span,
            int depth,
            int defaultDepth,
            HashSet<object>? active,
            JsonGrowthCharge charge,
            bool asynchronous)
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
                        AppendJsonObjectKey(builder, pair.Key, options.EnsureAscii, charge, context, span);
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
                    AppendJsonObjectKey(builder, entry.OriginalKey, options.EnsureAscii, charge, context, span);
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

        private static ValueTask AppendJsonValueAsync(
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
            context.CheckExecution(span);
            charge.Observe(builder.Length);
            if (depth >= ExecutionLimits.MaxInterpreterDepth || defaultDepth >= ExecutionLimits.MaxInterpreterDepth)
            {
                throw RuntimeErrors.Attributed(
                    "RecursionError",
                    "maximum recursion depth exceeded while encoding a JSON object",
                    span, LythonFailureReason.InterpreterStackLimit, ExecutionLimits.MaxInterpreterDepth,
                    Math.Max(depth, defaultDepth));
            }

            switch (value)
            {
                case PyNone:
                    builder.Append("null");
                    return ValueTask.CompletedTask;
                case PyString text:
                    AppendJsonString(builder, text, options.EnsureAscii, charge, context, span);
                    return ValueTask.CompletedTask;
                case bool boolean:
                    builder.Append(boolean ? "true" : "false");
                    return ValueTask.CompletedTask;
                case object numericValue when PyNumberOps.TryAsInteger(numericValue, out var integer):
                    // Giant magnitudes render slowly: bracket the conversion itself.
                    context.CheckExecution(span);
                    AppendJsonInteger(builder, integer, charge, context, span);
                    return ValueTask.CompletedTask;
                case double floating:
                    AppendJsonDouble(builder, floating, options, charge, context, span);
                    return ValueTask.CompletedTask;
                case PyDecimal decimalValue:
                    AppendJsonNumber(builder, PyDecimalOps.Format(decimalValue), charge, context, span);
                    return ValueTask.CompletedTask;
                case PyList list:
                    return AppendJsonSequenceAsync(builder, list, options, context, span, depth, defaultDepth, active, charge, asynchronous);
                case PyTuple tuple:
                    return AppendJsonSequenceAsync(builder, tuple, options, context, span, depth, defaultDepth, active, charge, asynchronous);
                case PyDict dict:
                    return AppendJsonDictionaryAsync(builder, dict, options, context, span, depth, defaultDepth, active, charge, asynchronous);
                case PyCounter counter:
                    return AppendJsonDictionaryAsync(builder, counter.InnerDict, options, context, span, depth, defaultDepth, active, charge, asynchronous);
                case PyDefaultDict defaultDict:
                    return AppendJsonDictionaryAsync(builder, defaultDict.InnerDict, options, context, span, depth, defaultDepth, active, charge, asynchronous);
                default:
                    if (options.DefaultCallable is not null)
                    {
                        return AppendJsonReplacementAsync(builder, value, options.DefaultCallable, options, context, span,
                            depth, defaultDepth, active, charge, asynchronous);
                    }

                    throw new InvalidOperationException($"Unsupported json.dumps value type: {JsonValueTypeName(value)}");
            }
        }

        private static async ValueTask AppendJsonReplacementAsync(
            StringBuilder builder, object value, ICallable callback, JsonDumpOptions options,
            ExecutionContext context, LythonSourceSpan span, int depth, int defaultDepth,
            HashSet<object>? active, JsonGrowthCharge charge, bool asynchronous)
        {
            var replacement = await InvokeJsonCallbackCoreAsync(callback, value, context, span, asynchronous).ConfigureAwait(false);
            if (ReferenceEquals(replacement, value))
                throw new LythonRuntimeException("ValueError", "json.dumps default returned the original unsupported object.", span);

            // Replacements that keep producing fresh unsupported objects retain
            // the existing recursion guard independently of container nesting.
            await AppendJsonValueAsync(builder, replacement, options, context, span, depth, defaultDepth + 1, active, charge, asynchronous).ConfigureAwait(false);
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

        internal static void AppendJsonObjectKey(StringBuilder builder, object keyValue, bool ensureAscii,
            JsonGrowthCharge charge, ExecutionContext context, LythonSourceSpan span)
        {
            switch (keyValue)
            {
                case PyString text:
                    AppendJsonString(builder, text, ensureAscii, charge, context, span);
                    return;
                case bool boolean:
                    AppendJsonString(builder, boolean ? "true" : "false", ensureAscii, charge, context, span);
                    return;
                case object numericValue when PyNumberOps.TryAsInteger(numericValue, out var integer):
                    AppendJsonInteger(builder, integer, charge, context, span, quoted: true);
                    return;
                case double floating when double.IsFinite(floating):
                    AppendJsonString(builder, Numbers.PyNumberOps.RenderFloat(floating), ensureAscii, charge, context, span);
                    return;
                case PyNone:
                    AppendJsonString(builder, "null", ensureAscii, charge, context, span);
                    return;
                default:
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

        internal static void AppendJsonString(StringBuilder builder, PyString text, bool ensureAscii,
            JsonGrowthCharge charge, ExecutionContext context, LythonSourceSpan span)
        {
            // UTF-8 byte capacity bounds decoded UTF-16 units, including astral
            // pairs. Small inputs use bounded control storage, with no CLR copy.
            if (text.Utf8Bytes.Length <= 128)
            {
                Span<char> characters = stackalloc char[128];
                var written = text.CopyUtf16To(characters);
                AppendJsonString(builder, characters[..written], ensureAscii, charge, context, span);
                return;
            }

            // The private decode lives through escaping, alongside builder
            // growth. It never escapes or becomes a retained guest string.
            using var decode = context.MemoryGovernor.ReserveTemporary(64 + 2L * text.Utf8Bytes.Length, span);
            AppendJsonString(builder, text.AsString(), ensureAscii, charge, context, span);
        }

        internal static void AppendJsonString(StringBuilder builder, ReadOnlySpan<char> text, bool ensureAscii, JsonGrowthCharge charge, ExecutionContext context, LythonSourceSpan span)
        {
            Span<char> escapedCodeUnit = stackalloc char[4];
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

                if (ch < 0x20 || ensureAscii && ch >= 0x7f)
                {
                    builder.Append("\\u");
                    // A UTF-16 code unit always fits exactly four hex digits.
                    _ = ((int)ch).TryFormat(escapedCodeUnit, out _, "x4", CultureInfo.InvariantCulture);
                    builder.Append(escapedCodeUnit);
                    continue;
                }

                builder.Append(ch);
            }

            builder.Append('"');
        }

        internal static void AppendJsonInteger(StringBuilder builder, BigInteger value, JsonGrowthCharge charge,
            ExecutionContext context, LythonSourceSpan span, bool quoted = false)
        {
            if (value >= long.MinValue && value <= long.MaxValue)
            {
                Span<char> characters = stackalloc char[20];
                if (!((long)value).TryFormat(characters, out var written, provider: CultureInfo.InvariantCulture))
                    throw new InvalidOperationException("Integer formatting exceeded its fixed buffer.");
                AppendJsonIntegerText(builder, characters[..written], quoted, charge, context, span);
                return;
            }

            using var scratch = context.MemoryGovernor.ReserveTemporary(
                RuntimeMemoryEstimates.EstimateIntegerRenderingWorkingBytes(value), span);
            AppendJsonIntegerText(builder, value.ToString(CultureInfo.InvariantCulture), quoted, charge, context, span);
        }

        private static void AppendJsonIntegerText(StringBuilder builder, ReadOnlySpan<char> text, bool quoted,
            JsonGrowthCharge charge, ExecutionContext context, LythonSourceSpan span)
        {
            if (!quoted)
            {
                AppendJsonNumber(builder, text, charge, context, span);
                return;
            }

            // Numeric keys contain only invariant ASCII digits/sign. Fund the
            // builder before appending; retain the existing escaper checkpoints.
            charge.Grow(text.Length + 2L);
            AppendJsonString(builder, text, ensureAscii: false, charge, context, span);
        }

        // Integer conversion work is funded separately before formatting;
        // this shared append retains the number checkpoint and output charge.
        internal static void AppendJsonNumber(StringBuilder builder, ReadOnlySpan<char> rendered, JsonGrowthCharge charge, ExecutionContext context, LythonSourceSpan span)
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
