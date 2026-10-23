using System.Numerics;
using System.Text;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class CsvWriterMembers
    {
        private static readonly LythonCallableSignature RowSignature = LythonCallableSignature.Create("csv.writerow", ["row"]);
        private static readonly LythonCallableSignature RowsSignature = LythonCallableSignature.Create("csv.writerows", ["rows"]);

        public static bool TryGetMember(CsvWriterObject writer, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "writerow" => BoundCallable.Create((args, span, context) => WriteRowFromValue(writer, args[0], span, context),
                    RowSignature, (args, span, context) => WriteRowFromValueAsync(writer, args[0], span, context)),
                "writerows" => BoundCallable.Create((args, span, context) => WriteRowsAsync(writer, args[0], span, context, false).GetAwaiter().GetResult(),
                    RowsSignature, (args, span, context) => WriteRowsAsync(writer, args[0], span, context, true)),
                "getvalue" => BoundCallable.Create((args, span, context) => GetValue(writer, span, context), "csv.getvalue", []),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        // Shared with the executable engine's ordinary-call path.
        public static object WriteRowFromValue(CsvWriterObject writer, object row, LythonSourceSpan span, ExecutionContext context)
            => WriteRowCoreAsync(writer, row, span, context, false).GetAwaiter().GetResult();

        public static ValueTask<object> WriteRowFromValueAsync(CsvWriterObject writer, object row, LythonSourceSpan span, ExecutionContext context)
            => WriteRowCoreAsync(writer, row, span, context, true);

        private static async ValueTask<object> WriteRowsAsync(CsvWriterObject writer, object rows, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (asynchronous)
                await foreach (var row in ToSequenceAsync(rows, span, context).ConfigureAwait(false))
                    await WriteRowCoreAsync(writer, row, span, context, true).ConfigureAwait(false);
            else foreach (var row in ToSequence(rows, span, context))
                _ = await WriteRowCoreAsync(writer, row, span, context, false).ConfigureAwait(false);
            return PyNone.Instance;
        }

        internal static async ValueTask<object> WriteRowCoreAsync(CsvWriterObject writer, object row, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous, IAsyncEnumerable<object>? values = null)
        {
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            var count = 0;
            try
            {
                if (values is not null)
                    await foreach (var item in values.ConfigureAwait(false)) await AppendAsync(item).ConfigureAwait(false);
                else if (asynchronous)
                    await foreach (var item in ToSequenceAsync(row, span, context).ConfigureAwait(false)) await AppendAsync(item).ConfigureAwait(false);
                else foreach (var item in ToSequence(row, span, context)) await AppendAsync(item).ConfigureAwait(false);
                if (count == 1 && builder.Length == 0)
                {
                    if (writer.Options.Quoting == CsvQuotingMode.None) throw CsvError("single empty field record must be quoted", span);
                    if (writer.Options.QuoteChar is null) throw CsvError("quotechar must be set when quoting is enabled", span);
                    builder.Append(writer.Options.QuoteChar); builder.Append(writer.Options.QuoteChar);
                }
                AppendBytes(builder, writer.Options.LineTerminator.Utf8Bytes.Span, context, span);
                context.CheckExecution(span);
                var rendered = builder.ToPyStringAndRelease();
                if (writer.WriteMethod is null)
                {
                    try { writer.AppendHistory(rendered, span); }
                    catch { context.MemoryGovernor.RefundUnpublishedValue(rendered); throw; }
                    return new BigInteger(rendered.Length);
                }
                context.CheckExecution(span);
                return asynchronous
                    ? await InvokeCallableTargetAsync(writer.WriteMethod, span, span, context,
                        () => ValueTask.FromResult<CallArgumentValue[]>([CallArgumentValue.Positional(rendered)])).ConfigureAwait(false)
                    : InvokeCallableTarget(writer.WriteMethod, span, span, context, () => [CallArgumentValue.Positional(rendered)]);
            }
            finally { builder.Release(); }

            async ValueTask AppendAsync(object item)
            {
                context.CheckExecution(span);
                var numeric = IsNumeric(item);
                var text = item is PyNone ? PyString.Empty
                    : await FormatSuspendedFieldAsync(item, 's', null, context, span, asynchronous).ConfigureAwait(false);
                if (count++ != 0) builder.Append(writer.Options.Delimiter);
                AppendField(builder, text, numeric, writer.Options, context, span);
            }
        }

        private static bool IsNumeric(object value)
            => PyNumberOps.TryAsNumber(value, out _) || value is PyComplex or PyDecimal || value is PyInstance instance &&
                (instance.Type.TryLookupInMro("__int__", 0, out _, out _) || instance.Type.TryLookupInMro("__float__", 0, out _, out _) ||
                 instance.Type.TryLookupInMro("__index__", 0, out _, out _));

        private static object GetValue(CsvWriterObject writer, LythonSourceSpan span, ExecutionContext context)
        {
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                for (var i = 0; i < writer.Rows.Count; i++)
                {
                    var bytes = writer.Rows[i].Utf8Bytes;
                    // The documented buffered extension omits the last terminator.
                    if (i == writer.Rows.Count - 1) bytes = bytes[..(bytes.Length - writer.Options.LineTerminator.Utf8Bytes.Length)];
                    AppendBytes(builder, bytes.Span, context, span);
                }
                context.CheckExecution(span);
                return builder.ToPyStringAndRelease();
            }
            finally { builder.Release(); }
        }

        private static void AppendBytes(GovernedByteBuilder builder, ReadOnlySpan<byte> bytes, ExecutionContext context, LythonSourceSpan span)
        {
            for (var offset = 0; offset < bytes.Length;)
            {
                context.CheckExecution(span);
                var count = Math.Min(4096, bytes.Length - offset);
                builder.Append(bytes.Slice(offset, count)); offset += count;
            }
        }

        private static int Character(PyString? text)
        {
            if (text is null) return -1;
            Rune.DecodeFromUtf8(text.Utf8Bytes.Span, out var rune, out _);
            return rune.Value;
        }

        private static bool TerminatorContains(ReadOnlySpan<byte> terminator, ReadOnlySpan<byte> rune, ExecutionContext context, LythonSourceSpan span)
        {
            for (var offset = 0; offset < terminator.Length; offset += Math.Min(4096, terminator.Length - offset))
            {
                context.CheckExecution(span);
                if (terminator.Slice(offset, Math.Min(4096 + rune.Length - 1, terminator.Length - offset)).IndexOf(rune) >= 0) return true;
            }
            return false;
        }

        private static void AppendField(GovernedByteBuilder builder, PyString field, bool numeric, CsvOptions options,
            ExecutionContext context, LythonSourceSpan span)
        {
            var bytes = field.Utf8Bytes.Span;
            var delimiter = Character(options.Delimiter); var quote = Character(options.QuoteChar); var escape = Character(options.EscapeChar);
            var quoted = options.Quoting == CsvQuotingMode.All || options.Quoting == CsvQuotingMode.NonNumeric && !numeric;
            if (bytes.IsEmpty && delimiter == ' ' && options.SkipInitialSpace)
            {
                if (options.Quoting == CsvQuotingMode.None) throw CsvError("empty field must be quoted with a space delimiter and skipinitialspace", span);
                quoted = true;
            }
            // Decide quoting and validate escaping before emitting this field.
            for (var offset = 0; offset < bytes.Length;)
            {
                context.CheckExecution(span);
                Rune.DecodeFromUtf8(bytes[offset..], out var rune, out var consumed);
                var special = rune.Value == delimiter || rune.Value == quote || rune.Value == escape || rune.Value is '\n' or '\r' ||
                    TerminatorContains(options.LineTerminator.Utf8Bytes.Span, bytes.Slice(offset, consumed), context, span);
                if (special)
                {
                    var escaping = options.Quoting == CsvQuotingMode.None || rune.Value == escape || rune.Value == quote && !options.DoubleQuote;
                    if (escaping && escape < 0) throw CsvError("need to escape, but no escapechar set", span);
                    if (!escaping) quoted = true;
                }
                offset += consumed;
            }
            if (quoted)
            {
                if (options.QuoteChar is null) throw CsvError("quotechar must be set when quoting is enabled", span);
                builder.Append(options.QuoteChar);
            }
            for (var offset = 0; offset < bytes.Length;)
            {
                context.CheckExecution(span);
                Rune.DecodeFromUtf8(bytes[offset..], out var rune, out var consumed);
                var special = rune.Value == delimiter || rune.Value == quote || rune.Value == escape || rune.Value is '\n' or '\r' ||
                    TerminatorContains(options.LineTerminator.Utf8Bytes.Span, bytes.Slice(offset, consumed), context, span);
                if (special)
                {
                    if (options.Quoting == CsvQuotingMode.None || rune.Value == escape || rune.Value == quote && !options.DoubleQuote)
                        builder.Append(options.EscapeChar!);
                    else if (rune.Value == quote) builder.Append(options.QuoteChar!);
                }
                builder.Append(bytes.Slice(offset, consumed)); offset += consumed;
            }
            if (quoted) builder.Append(options.QuoteChar!);
        }
    }
}
