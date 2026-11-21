using System.Buffers;
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
        private static readonly SearchValues<byte> StandardFieldSearch = SearchValues.Create("\r\n,\""u8);
        private static readonly LythonCallableSignature RowsSignature = LythonCallableSignature.Create("csv.writerows", ["rows"]);

        public static bool TryGetMember(CsvWriterObject writer, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "writerow" => ReceiverMethods.CsvWriterow.Bind(writer),
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
                await foreach (var row in PyIteration.Cursor.ForAsyncEnumeration(rows, span, context).ConfigureAwait(false))
                    await WriteRowCoreAsync(writer, row, span, context, true).ConfigureAwait(false);
            else foreach (var row in PyIteration.Cursor.Create(rows, span, context))
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
                    await foreach (var item in PyIteration.Cursor.ForAsyncEnumeration(row, span, context).ConfigureAwait(false))
                        await AppendAsync(item).ConfigureAwait(false);
                else foreach (var item in PyIteration.Cursor.Create(row, span, context))
                         await AppendAsync(item).ConfigureAwait(false);
                if (count == 1 && builder.Length == 0)
                {
                    if (writer.Options.Quoting == CsvQuotingMode.None) throw CsvError("single empty field record must be quoted", span);
                    if (writer.Options.QuoteChar is null) throw CsvError("quotechar must be set when quoting is enabled", span);
                    builder.Append(writer.Options.QuoteChar); builder.Append(writer.Options.QuoteChar);
                }
                AppendBytes(builder, writer.Options.LineTerminator.Utf8Span, context, span);
                context.CheckExecution(span);
                if (writer.WriteTarget is ExecutionContext.TextFileHandle textFile)
                    return await InvokeFileWriteAsync(textFile, builder.WrittenMemory, span, context, asynchronous).ConfigureAwait(false);
                var rendered = builder.ToPyStringAndRelease();
                if (writer.WriteTarget is null)
                {
                    try { writer.AppendHistory(rendered, span); }
                    catch { context.MemoryGovernor.RefundUnpublishedValue(rendered); throw; }
                    return new BigInteger(rendered.Length);
                }
                context.CheckExecution(span);
                if (writer.WriteTarget is ExecutionContext.BinaryFileHandle)
                    return InvokeFileWrite(writer.WriteTarget, rendered, span, context);
                return asynchronous
                    ? await InvokeCallableTargetAsync(writer.WriteTarget, span, span, context,
                        () => ValueTask.FromResult<CallArgumentValue[]>([CallArgumentValue.Positional(rendered)])).ConfigureAwait(false)
                    : InvokeCallableTarget(writer.WriteTarget, span, span, context, () => [CallArgumentValue.Positional(rendered)]);
            }
            finally { builder.Release(); }

            async ValueTask AppendAsync(object item)
            {
                context.CheckExecution(span);
                var numeric = IsNumeric(item);
                var text = item is PyNone ? PyString.Empty
                    : await FormatSuspendedFieldAsync(item, 's', null, context, span, asynchronous).ConfigureAwait(false);
                if (count++ != 0) builder.Append(writer.Options.Delimiter);
                AppendField(builder, text, numeric, writer, context, span);
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
                    if (i == writer.Rows.Count - 1) bytes = bytes[..(bytes.Length - writer.Options.LineTerminator.ByteLength)];
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

        internal static bool UsesStandardFieldSearch(CsvOptions options)
            => options.EscapeChar is null && options.Delimiter.Utf8Span.SequenceEqual(","u8) &&
                options.QuoteChar is { } quote && quote.Utf8Span.SequenceEqual("\""u8) &&
                (options.LineTerminator.Utf8Span.SequenceEqual("\r\n"u8) ||
                    options.LineTerminator.Utf8Span.SequenceEqual("\n"u8));

        internal static SearchValues<byte> PrepareFieldSearch(CsvOptions options, bool standard,
            ExecutionContext context, LythonSourceSpan span)
        {
            if (standard) return StandardFieldSearch;

            Span<byte> search = stackalloc byte[256];
            search.Clear();
            search['\r'] = search['\n'] = search[options.Delimiter.Utf8Span[0]] = 1;
            if (options.QuoteChar is { } quote) search[quote.Utf8Span[0]] = 1;
            if (options.EscapeChar is { } escape) search[escape.Utf8Span[0]] = 1;
            var terminator = options.LineTerminator.Utf8Span;
            for (var i = 0; i < terminator.Length; i++)
            {
                if ((i & 4095) == 0) context.CheckExecution(span);
                var value = terminator[i];
                if ((value & 0xc0) != 0x80) search[value] = 1;
            }
            var count = 0;
            for (var i = 0; i < search.Length; i++)
                if (search[i] != 0) search[count++] = (byte)i;
            return SearchValues.Create(search[..count]);
        }

        internal static bool TerminatorContains(ReadOnlySpan<byte> terminator, ReadOnlySpan<byte> rune, ExecutionContext context, LythonSourceSpan span)
        {
            for (var offset = 0; offset < terminator.Length; offset += Math.Min(4096, terminator.Length - offset))
            {
                context.CheckExecution(span);
                if (terminator.Slice(offset, Math.Min(4096 + rune.Length - 1, terminator.Length - offset)).IndexOf(rune) >= 0) return true;
            }
            return false;
        }

        private static void AppendField(GovernedByteBuilder builder, PyString field, bool numeric,
            CsvWriterObject writer, ExecutionContext context, LythonSourceSpan span)
        {
            var bytes = field.Utf8Span;
            var options = writer.Options;
            var quoted = options.Quoting == CsvQuotingMode.All || options.Quoting == CsvQuotingMode.NonNumeric && !numeric;
            if (bytes.IsEmpty && options.SkipInitialSpace && options.Delimiter.Utf8Span.SequenceEqual(" "u8))
            {
                if (options.Quoting == CsvQuotingMode.None) throw CsvError("empty field must be quoted with a space delimiter and skipinitialspace", span);
                quoted = true;
            }

            // Validate escaping before asking for more row storage. Count the
            // final bytes so a long field needs at most one admitted growth.
            long length = bytes.Length;
            var firstPrefix = -1;
            for (var cursor = 0; TryScanField(writer, bytes, ref cursor, context, span,
                out var position, out var quotesField, out var prefix);)
            {
                quoted |= quotesField;
                if (prefix is null) continue;
                if (firstPrefix < 0) firstPrefix = position;
                length += prefix.ByteLength;
            }

            var quote = quoted ? options.QuoteChar ?? throw CsvError("quotechar must be set when quoting is enabled", span) : null;
            length += 2L * (quote?.ByteLength ?? 0);
            if (length > int.MaxValue) throw RuntimeErrors.Runtime("byte buffer allocation is too large.", span);
            var output = builder.GetWriteWindow((int)length, context.State.CancellationToken).Span;
            context.CheckExecution(span);
            var written = 0;
            if (quote is not null) CopyFieldBytes(quote.Utf8Span, output, ref written, context, span);
            var copied = 0;
            for (var cursor = firstPrefix; cursor >= 0 && TryScanField(writer, bytes, ref cursor, context, span,
                out var position, out _, out var prefix);)
            {
                if (prefix is null) continue;
                CopyFieldBytes(bytes[copied..position], output, ref written, context, span);
                CopyFieldBytes(prefix.Utf8Span, output, ref written, context, span);
                copied = position;
            }
            CopyFieldBytes(bytes[copied..], output, ref written, context, span);
            if (quote is not null) CopyFieldBytes(quote.Utf8Span, output, ref written, context, span);
            System.Diagnostics.Debug.Assert(written == length);
            builder.Advance(written);
        }

        private static bool TryScanField(CsvWriterObject writer, ReadOnlySpan<byte> bytes, ref int cursor,
            ExecutionContext context, LythonSourceSpan span, out int position, out bool quotesField, out PyString? prefix)
        {
            var options = writer.Options;
            while (cursor < bytes.Length)
            {
                context.CheckExecution(span);
                var count = Math.Min(4096, bytes.Length - cursor);
                var match = bytes.Slice(cursor, count).IndexOfAny(writer.FieldSearch);
                if (match < 0)
                {
                    cursor += count;
                    continue;
                }

                position = cursor + match;
                var width = 1;
                var ascii = bytes[position] < 0x80;
                if (!ascii) Rune.DecodeFromUtf8(bytes[position..], out _, out width);
                var scalar = bytes.Slice(position, width);
                cursor = position + width;
                var quote = options.QuoteChar is { } q && scalar.SequenceEqual(q.Utf8Span);
                var escape = options.EscapeChar is { } e && scalar.SequenceEqual(e.Utf8Span);
                // An ASCII lead match is exact. Non-ASCII lead bytes may
                // also identify an ordinary scalar with a different suffix.
                if (!ascii && !quote && !escape && !scalar.SequenceEqual(options.Delimiter.Utf8Span) &&
                    !TerminatorContains(options.LineTerminator.Utf8Span, scalar, context, span))
                    continue;

                var escaping = options.Quoting == CsvQuotingMode.None || escape || quote && !options.DoubleQuote;
                quotesField = !escaping;
                prefix = escaping ? options.EscapeChar ?? throw CsvError("need to escape, but no escapechar set", span)
                    : quote ? options.QuoteChar : null;
                return true;
            }
            position = -1;
            quotesField = false;
            prefix = null;
            return false;
        }

        private static void CopyFieldBytes(ReadOnlySpan<byte> source, Span<byte> output, ref int written,
            ExecutionContext context, LythonSourceSpan span)
        {
            while (!source.IsEmpty)
            {
                context.CheckExecution(span);
                var count = Math.Min(4096, source.Length);
                source[..count].CopyTo(output[written..]);
                written += count;
                source = source[count..];
            }
        }
    }
}
