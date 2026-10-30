using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
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

        internal static async ValueTask<PyString> SerializeJsonTextCoreAsync(object value, JsonDumpOptions options, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            try
            {
                var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
                try
                {
                    var active = options.CheckCircular ? new HashSet<object>(ReferenceEqualityComparer.Instance) : null;
                    await AppendJsonValueAsync(builder, value, options, context, span, depth: 0, defaultDepth: 0, active, asynchronous).ConfigureAwait(false);
                    return builder.ToPyString();
                }
                finally { builder.Release(); }
            }
            catch (InvalidOperationException ex)
            {
                throw new LythonRuntimeException("TypeError", ex.Message, span);
            }
        }

        private static async ValueTask AppendJsonDictionaryAsync(
            GovernedByteBuilder builder,
            PyDict dict,
            JsonDumpOptions options,
            ExecutionContext context,
            LythonSourceSpan span,
            int depth,
            int defaultDepth,
            HashSet<object>? active,
            bool asynchronous)
        {
            if (active is not null && !active.Add(dict))
            {
                throw new LythonRuntimeException("ValueError", "Circular reference detected.", span);
            }

            try
            {
                if (!await JsonOptionTruthAsync(options.SortKeys, context, span, asynchronous).ConfigureAwait(false))
                {
                    // Stream entries in dictionary order without copying them
                    // aside; only the sorted path below pays for scratch.
                    builder.Append((byte)'{');
                    var index = 0;
                    foreach (var pair in dict)
                    {
                        context.CheckExecution(span);
                        if (!IsSupportedJsonObjectKey(pair.Key))
                        {
                            if (await JsonOptionTruthAsync(options.SkipKeys, context, span, asynchronous).ConfigureAwait(false))
                            {
                                continue;
                            }

                            throw new InvalidOperationException("json.dumps() requires dictionary keys to be strings, numbers, booleans, or None.");
                        }

                        if (index > 0)
                        {
                            AppendSeparator(builder, options.ItemSeparator, context, span);
                        }

                        AppendJsonValuePrefix(builder, options, depth + 1, index, context, span);
                        await AppendJsonObjectKeyAsync(builder, pair.Key, options, context, span, asynchronous).ConfigureAwait(false);
                        AppendSeparator(builder, options.KeySeparator, context, span);
                        await AppendJsonValueAsync(builder, pair.Value, options, context, span, depth + 1, defaultDepth, active, asynchronous).ConfigureAwait(false);
                        index++;
                    }

                    if (index > 0)
                    {
                        AppendJsonContainerSuffix(builder, options, depth, context, span);
                    }

                    builder.Append((byte)'}');
                    return;
                }

                // Sorting retains one reference pair per entry beside the live
                // dict: fund that scratch (pairs plus backing) before building
                // it instead of growing uncharged.
                using var sorting = context.MemoryGovernor.ReserveTemporary(checked(64L + 32L * dict.Count), span);
                var entries = CollectSortedJsonEntries(dict, context, span);
                builder.Append((byte)'{');
                var emitted = 0;
                foreach (var entry in entries)
                {
                    context.CheckExecution(span);
                    if (!IsSupportedJsonObjectKey(entry.OriginalKey))
                    {
                        if (await JsonOptionTruthAsync(options.SkipKeys, context, span, asynchronous).ConfigureAwait(false)) continue;
                        throw new InvalidOperationException("json.dumps() requires dictionary keys to be strings, numbers, booleans, or None.");
                    }
                    if (emitted > 0)
                    {
                        AppendSeparator(builder, options.ItemSeparator, context, span);
                    }

                    AppendJsonValuePrefix(builder, options, depth + 1, emitted, context, span);
                    await AppendJsonObjectKeyAsync(builder, entry.OriginalKey, options, context, span, asynchronous).ConfigureAwait(false);
                    AppendSeparator(builder, options.KeySeparator, context, span);
                    await AppendJsonValueAsync(builder, entry.Value, options, context, span, depth + 1, defaultDepth, active, asynchronous).ConfigureAwait(false);
                    emitted++;
                }

                if (emitted > 0)
                {
                    AppendJsonContainerSuffix(builder, options, depth, context, span);
                }

                builder.Append((byte)'}');
            }
            finally
            {
                _ = active?.Remove(dict);
            }
        }

        private static ValueTask AppendJsonValueAsync(
            GovernedByteBuilder builder,
            object value,
            JsonDumpOptions options,
            ExecutionContext context,
            LythonSourceSpan span,
            int depth,
            int defaultDepth,
            HashSet<object>? active,
            bool asynchronous)
        {
            context.CheckExecution(span);
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
                    builder.Append("null"u8);
                    return ValueTask.CompletedTask;
                case PyString text:
                    AppendJsonString(builder, text, options.EnsureAscii, context, span);
                    return ValueTask.CompletedTask;
                case bool boolean:
                    builder.Append(boolean ? "true"u8 : "false"u8);
                    return ValueTask.CompletedTask;
                case object numericValue when PyNumberOps.TryAsInteger(numericValue, out var integer):
                    // Giant magnitudes render slowly: bracket the conversion itself.
                    context.CheckExecution(span);
                    AppendJsonInteger(builder, integer, context, span);
                    return ValueTask.CompletedTask;
                case double floating:
                    return AppendJsonDoubleAsync(builder, floating, options, context, span, asynchronous);
                case PyList list:
                    return AppendJsonSequenceAsync(builder, list, options, context, span, depth, defaultDepth, active, asynchronous);
                case object tupleLike when PyTupleLike.TryGetItems(tupleLike, out var tuple):
                    return AppendJsonSequenceAsync(builder, tuple, options, context, span, depth, defaultDepth, active, asynchronous);
                case PyDict dict:
                    return AppendJsonDictionaryAsync(builder, dict, options, context, span, depth, defaultDepth, active, asynchronous);
                case PyCounter counter:
                    return AppendJsonDictionaryAsync(builder, counter.InnerDict, options, context, span, depth, defaultDepth, active, asynchronous);
                case PyDefaultDict defaultDict:
                    return AppendJsonDictionaryAsync(builder, defaultDict.InnerDict, options, context, span, depth, defaultDepth, active, asynchronous);
                default:
                    if (options.DefaultCallable is not null)
                    {
                        return AppendJsonReplacementAsync(builder, value, options.DefaultCallable, options, context, span,
                            depth, defaultDepth, active, asynchronous);
                    }

                    throw new InvalidOperationException($"Unsupported json.dumps value type: {JsonValueTypeName(value)}");
            }
        }

        private static async ValueTask AppendJsonReplacementAsync(
            GovernedByteBuilder builder, object value, object callback, JsonDumpOptions options,
            ExecutionContext context, LythonSourceSpan span, int depth, int defaultDepth,
            HashSet<object>? active, bool asynchronous)
        {
            if (active is not null && !active.Add(value))
                throw new LythonRuntimeException("ValueError", "Circular reference detected.", span);
            try
            {
                var replacement = await InvokeJsonCallbackCoreAsync(callback, value, context, span, asynchronous).ConfigureAwait(false);
                await AppendJsonValueAsync(builder, replacement, options, context, span, depth, defaultDepth + 1, active, asynchronous).ConfigureAwait(false);
            }
            finally { active?.Remove(value); }
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
            GovernedByteBuilder builder,
            IEnumerable<object> sequence,
            JsonDumpOptions options,
            ExecutionContext context,
            LythonSourceSpan span,
            int depth,
            int defaultDepth,
            HashSet<object>? active,
            bool asynchronous)
        {
            if (active is not null && !active.Add(sequence))
            {
                throw new LythonRuntimeException("ValueError", "Circular reference detected.", span);
            }

            try
            {
                builder.Append((byte)'[');
                var index = 0;
                foreach (var item in sequence)
                {
                    if (index > 0)
                    {
                        AppendSeparator(builder, options.ItemSeparator, context, span);
                    }

                    AppendJsonValuePrefix(builder, options, depth + 1, index, context, span);
                    await AppendJsonValueAsync(builder, item, options, context, span, depth + 1, defaultDepth, active, asynchronous).ConfigureAwait(false);
                    index++;
                }

                if (index > 0)
                {
                    AppendJsonContainerSuffix(builder, options, depth, context, span);
                }

                builder.Append((byte)']');
            }
            finally
            {
                _ = active?.Remove(sequence);
            }
        }

        internal static ValueTask AppendJsonObjectKeyAsync(GovernedByteBuilder builder, object keyValue, JsonDumpOptions options,
            ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            switch (keyValue)
            {
                case PyString text:
                    AppendJsonString(builder, text, options.EnsureAscii, context, span);
                    return ValueTask.CompletedTask;
                case bool boolean:
                    builder.Append(boolean ? "\"true\""u8 : "\"false\""u8);
                    return ValueTask.CompletedTask;
                case object numericValue when PyNumberOps.TryAsInteger(numericValue, out var integer):
                    AppendJsonInteger(builder, integer, context, span, quoted: true);
                    return ValueTask.CompletedTask;
                case double floating:
                    return AppendJsonDoubleAsync(builder, floating, options, context, span, asynchronous, quoted: true);
                case PyNone:
                    builder.Append("\"null\""u8);
                    return ValueTask.CompletedTask;
                default:
                    throw new InvalidOperationException("json.dumps() requires dictionary keys to be strings, numbers, booleans, or None.");
            }
        }

        internal static bool IsSupportedJsonObjectKey(object key)
            => key is PyString or PyNone or double || PyNumberOps.IsInteger(key);

        internal static void AppendJsonValuePrefix(GovernedByteBuilder builder, JsonDumpOptions options, int depth, int index, ExecutionContext context, LythonSourceSpan span)
        {
            _ = index;
            AppendJsonContainerSuffix(builder, options, depth, context, span);
        }

        internal static void AppendJsonContainerSuffix(GovernedByteBuilder builder, JsonDumpOptions options, int depth, ExecutionContext context, LythonSourceSpan span)
        {
            if (options.IndentUnit is null) return;
            builder.Append((byte)'\n');
            for (var i = 0; i < depth; i++)
                AppendSeparator(builder, options.IndentUnit, context, span);
        }

        // Copy existing native text in bounded windows; no UTF-16 staging or
        // rune-by-rune transcoding for separators and indent units.
        internal static void AppendSeparator(GovernedByteBuilder builder, PyString separator,
            ExecutionContext context, LythonSourceSpan span)
        {
            var bytes = separator.Utf8Bytes.Span;
            while (!bytes.IsEmpty)
            {
                context.CheckExecution(span);
                var count = Math.Min(bytes.Length, 4096);
                builder.Append(bytes[..count]);
                bytes = bytes[count..];
            }
        }

        internal static void AppendJsonString(GovernedByteBuilder builder, PyString text, bool ensureAscii,
            ExecutionContext context, LythonSourceSpan span)
        {
            var bytes = text.Utf8Bytes.Span;
            builder.Append((byte)'"');
            var start = 0;
            var position = 0;
            var checkpoint = 0;
            while (position < bytes.Length)
            {
                if (position >= checkpoint)
                {
                    context.CheckExecution(span);
                    builder.Append(bytes[start..position]);
                    start = position;
                    checkpoint = position > int.MaxValue - 4096 ? int.MaxValue : position + 4096;
                }

                var scalar = (int)bytes[position];
                var consumed = 1;
                if (scalar >= 0x80)
                {
                    if (Rune.DecodeFromUtf8(bytes[position..], out var rune, out consumed) != OperationStatus.Done)
                        throw new DecoderFallbackException("Invalid UTF-8 in a JSON string.");
                    scalar = rune.Value;
                }
                if (scalar is >= 0x20 and not '"' and not '\\' && (!ensureAscii || scalar < 0x7f))
                {
                    position += consumed;
                    continue;
                }

                builder.Append(bytes[start..position]);
                ReadOnlySpan<byte> escape = scalar switch
                {
                    '"' => "\\\""u8,
                    '\\' => "\\\\"u8,
                    '\b' => "\\b"u8,
                    '\f' => "\\f"u8,
                    '\n' => "\\n"u8,
                    '\r' => "\\r"u8,
                    '\t' => "\\t"u8,
                    _ => [],
                };
                if (!escape.IsEmpty) builder.Append(escape);
                else if (scalar <= 0xffff) AppendJsonCodeUnit(builder, scalar);
                else
                {
                    scalar -= 0x10000;
                    AppendJsonCodeUnit(builder, 0xd800 + (scalar >> 10));
                    AppendJsonCodeUnit(builder, 0xdc00 + (scalar & 0x3ff));
                }
                position += consumed;
                start = position;
            }
            builder.Append(bytes[start..]);
            builder.Append((byte)'"');
        }

        private static void AppendJsonCodeUnit(GovernedByteBuilder builder, int value)
        {
            ReadOnlySpan<byte> digits = "0123456789abcdef"u8;
            Span<byte> escape = stackalloc byte[6];
            escape[0] = (byte)'\\'; escape[1] = (byte)'u';
            escape[2] = digits[value >> 12]; escape[3] = digits[(value >> 8) & 15];
            escape[4] = digits[(value >> 4) & 15]; escape[5] = digits[value & 15];
            builder.Append(escape);
        }

        internal static void AppendJsonInteger(GovernedByteBuilder builder, BigInteger value,
            ExecutionContext context, LythonSourceSpan span, bool quoted = false)
        {
            context.CheckExecution(span);
            if (value >= long.MinValue && value <= long.MaxValue)
            {
                Span<byte> digits = stackalloc byte[20];
                if (!((long)value).TryFormat(digits, out var written, provider: CultureInfo.InvariantCulture))
                    throw new InvalidOperationException("Integer formatting exceeded its fixed buffer.");
                if (quoted) builder.Append((byte)'"');
                builder.Append(digits[..written]);
                if (quoted) builder.Append((byte)'"');
                return;
            }

            // Large integer formatting still uses the BCL's invariant renderer;
            // its scratch and text coexist with admitted native output capacity.
            using var scratch = context.MemoryGovernor.ReserveTemporary(
                RuntimeMemoryEstimates.EstimateIntegerRenderingWorkingBytes(value), span);
            var rendered = value.ToString(CultureInfo.InvariantCulture);
            context.CheckExecution(span);
            if (quoted) builder.Append((byte)'"');
            AppendJsonNumber(builder, rendered, context, span);
            if (quoted) builder.Append((byte)'"');
        }

        private static void AppendJsonNumber(GovernedByteBuilder builder, string rendered, ExecutionContext context, LythonSourceSpan span)
        {
            for (var offset = 0; offset < rendered.Length;)
            {
                context.CheckExecution(span);
                var count = Math.Min(rendered.Length - offset, 4096);
                var output = builder.GetWriteWindow(count).Span;
                for (var i = 0; i < count; i++) output[i] = (byte)rendered[offset + i];
                builder.Advance(count);
                offset += count;
            }
        }

        internal static async ValueTask AppendJsonDoubleAsync(GovernedByteBuilder builder, double value, JsonDumpOptions options,
            ExecutionContext context, LythonSourceSpan span, bool asynchronous, bool quoted = false)
        {
            string rendered;
            if (double.IsFinite(value)) rendered = PyNumberOps.RenderFloat(value);
            else
            {
                if (!await JsonOptionTruthAsync(options.AllowNan, context, span, asynchronous).ConfigureAwait(false))
                    throw new LythonRuntimeException("ValueError", "Out of range float values are not JSON compliant.", span);
                rendered = double.IsNaN(value) ? "NaN" : value > 0 ? "Infinity" : "-Infinity";
            }
            context.CheckExecution(span);
            if (quoted) builder.Append((byte)'"');
            AppendJsonNumber(builder, rendered, context, span);
            if (quoted) builder.Append((byte)'"');
        }
    }
}
