using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class TextwrapModule : PyModule
    {
        internal static readonly TextwrapModule Instance = new();
        private TextwrapModule() : base("textwrap") { }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "dedent" => BuiltinCallable.Create(LythonKnownCallableSignatures.TextwrapDedent, Dedent),
                "indent" => BuiltinCallable.Create(LythonKnownCallableSignatures.TextwrapIndent,
                    (args, span, context) => IndentAsync(args, span, context, false).GetAwaiter().GetResult(),
                    (args, span, context) => IndentAsync(args, span, context, true)),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object Dedent(object[] args, LythonSourceSpan span, ExecutionContext context)
        {
            if (!PyStringOps.TryAsString(args[0], out var text))
                throw new LythonRuntimeException("TypeError", "textwrap.dedent() expects string text", span);
            return DedentText(text, span, context.Services);
        }

        private static ValueTask<object> IndentAsync(object[] args, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var predicate = args.Length == 3 ? args[2] : PyNone.Instance;
            return PyStringOps.TryAsString(args[0], out var text)
                ? IndentTextAsync(text, args[1], predicate, span, context, asynchronous)
                : IndentOtherAsync(args[0], args[1], predicate, span, context, asynchronous);
        }

        internal static PyString DedentText(PyString text, LythonSourceSpan span, ExecutionServices services)
        {
            var source = text.Utf8Bytes.Span;
            var marginStart = 0;
            var marginLength = -1;
            var normalizeBlankLines = false;
            for (var start = 0; start < source.Length;)
            {
                services.CheckExecution(span);
                var end = FindLf(source, start, span, services);
                var indent = LeadingIndent(source, start, end, span, services);
                if (indent == end - start)
                {
                    normalizeBlankLines |= indent != 0;
                }
                else if (marginLength < 0)
                {
                    marginStart = start;
                    marginLength = indent;
                }
                else
                {
                    marginLength = Math.Min(marginLength, indent);
                    var shared = 0;
                    while (shared < marginLength && source[marginStart + shared] == source[start + shared])
                    {
                        if ((shared & 1023) == 0) services.CheckExecution(span);
                        shared++;
                    }
                    marginLength = shared;
                }
                start = end == source.Length ? end : end + 1;
            }
            if (marginLength <= 0 && !normalizeBlankLines) return text;
            marginLength = Math.Max(0, marginLength);
            var builder = new GovernedByteBuilder(services.MemoryGovernor, span);
            try
            {
                for (var start = 0; start < source.Length;)
                {
                    services.CheckExecution(span);
                    var end = FindLf(source, start, span, services);
                    var indent = LeadingIndent(source, start, end, span, services);
                    if (indent != end - start) builder.Append(source[(start + marginLength)..end]);
                    if (end != source.Length) builder.Append((byte)'\n');
                    start = end == source.Length ? end : end + 1;
                }
                return Finish(builder, span, services);
            }
            finally { builder.Release(); }
        }

        // Dedent's multiline regex rules use LF anchors, unlike splitlines.
        private static int FindLf(ReadOnlySpan<byte> source, int start, LythonSourceSpan span, ExecutionServices services)
        {
            var end = start;
            while (end < source.Length && source[end] != (byte)'\n')
            {
                if ((end & 1023) == 0) services.CheckExecution(span);
                end++;
            }
            return end;
        }

        private static int LeadingIndent(ReadOnlySpan<byte> source, int start, int end, LythonSourceSpan span, ExecutionServices services)
        {
            var cursor = start;
            while (cursor < end && source[cursor] is (byte)' ' or (byte)'\t')
            {
                if ((cursor & 1023) == 0) services.CheckExecution(span);
                cursor++;
            }
            return cursor - start;
        }

        internal static async ValueTask<object> IndentTextAsync(PyString text, object prefix, object predicate,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var services = context.Services;
            var builder = new GovernedByteBuilder(services.MemoryGovernor, span);
            object? invalidItem = null;
            long outputScalars = 0;
            var prefixScalars = -1;
            try
            {
                for (var start = 0; start < text.Utf8Bytes.Length;)
                {
                    var line = ScanLine(text.Utf8Bytes.Span, start, span, services);
                    var selected = line.HasNonWhitespace;
                    PyString? lineText = null;
                    if (predicate is not PyNone)
                    {
                        lineText = PyString.FromUtf8(text.Utf8Bytes.Slice(start, line.EndByte - start), services.MemoryGovernor, span);
                        services.State.CallTemporaries.TrackFreshString(lineText, span);
                        selected = await SelectLineAsync(lineText, predicate, span, context, asynchronous).ConfigureAwait(false);
                    }
                    services.CheckExecution(span);
                    if (selected && prefix is not PyString)
                    {
                        invalidItem ??= prefix;
                    }
                    else if (invalidItem is null)
                    {
                        if (selected && prefixScalars < 0)
                            prefixScalars = CountScalars(((PyString)prefix).Utf8Bytes.Span, span, services);
                        outputScalars += line.Scalars + (selected ? prefixScalars : 0L);
                        CheckOutputLength(outputScalars, span, services);
                        if (selected) builder.Append(((PyString)prefix).Utf8Bytes.Span);
                        builder.Append(text.Utf8Bytes.Span[start..line.EndByte]);
                    }
                    start = line.EndByte;
                }
                // Python 3.13 joins prefixes as separate items, without an
                // addition operator, and validates after every predicate ran.
                if (invalidItem is not null)
                    throw new LythonRuntimeException("TypeError", "indent produced a non-string item", span);
                return Finish(builder, span, services);
            }
            finally { builder.Release(); }
        }

        private static async ValueTask<object> IndentOtherAsync(object text, object prefix, object predicate,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var lines = await CallMemberAsync(text, "splitlines", [CallArgumentValue.Positional(true)],
                span, context, asynchronous).ConfigureAwait(false);
            var services = context.Services;
            var builder = new GovernedByteBuilder(services.MemoryGovernor, span);
            var invalidItem = false;
            long outputScalars = 0;
            var prefixScalars = -1;
            try
            {
                if (asynchronous)
                {
                    await foreach (var line in PyIteration.ToSequenceAsync(lines, span, context).ConfigureAwait(false))
                        await AppendLineAsync(line).ConfigureAwait(false);
                }
                else
                {
                    foreach (var line in PyIteration.ToSequence(lines, span, context))
                        await AppendLineAsync(line).ConfigureAwait(false);
                }
                if (invalidItem)
                    throw new LythonRuntimeException("TypeError", "indent produced a non-string item", span);
                return Finish(builder, span, services);
            }
            finally { builder.Release(); }

            async ValueTask AppendLineAsync(object line)
            {
                services.CheckExecution(span);
                var selected = await SelectLineAsync(line, predicate, span, context, asynchronous).ConfigureAwait(false);
                services.CheckExecution(span);
                if (line is not PyString lineText || (selected && prefix is not PyString))
                    invalidItem = true;
                else if (!invalidItem)
                {
                    if (selected && prefixScalars < 0)
                        prefixScalars = CountScalars(((PyString)prefix).Utf8Bytes.Span, span, services);
                    outputScalars += CountScalars(lineText.Utf8Bytes.Span, span, services) + (selected ? prefixScalars : 0L);
                    CheckOutputLength(outputScalars, span, services);
                    if (selected) builder.Append(((PyString)prefix).Utf8Bytes.Span);
                    builder.Append(lineText.Utf8Bytes.Span);
                }
            }
        }

        private static async ValueTask<bool> SelectLineAsync(object line, object predicate,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            object result;
            if (predicate is PyNone)
                result = await CallMemberAsync(line, "isspace", [], span, context, asynchronous).ConfigureAwait(false);
            else
            {
                if (predicate is not ICallable callable)
                    throw new LythonRuntimeException("TypeError", "predicate is not callable", span);
                result = asynchronous
                    ? await CallableInvocation.InvokeUnaryAsync(callable, line, span, context).ConfigureAwait(false)
                    : CallableInvocation.InvokeUnary(callable, line, span, context);
                context.Services.State.CallTemporaries.TrackCallResult(result, span);
            }
            var truth = asynchronous
                ? await IsTruthyAsync(result, context, span).ConfigureAwait(false)
                : IsTruthy(result, context, span);
            return predicate is PyNone ? !truth : truth;
        }

        private static async ValueTask<object> CallMemberAsync(object target, string name, CallArgumentValue[] arguments,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var member = asynchronous
                ? await PyTextStream.ResolveMemberAsync(target, name, span, context).ConfigureAwait(false)
                : PyTextStream.ResolveMember(target, name, span, context);
            var result = asynchronous
                ? await InvokeCallableTargetAsync(member, span, span, context,
                    () => ValueTask.FromResult(arguments)).ConfigureAwait(false)
                : InvokeCallableTarget(member, span, span, context, () => arguments);
            context.Services.State.CallTemporaries.TrackCallResult(result, span);
            return result;
        }

        private readonly record struct LineFacts(int EndByte, int Scalars, bool HasNonWhitespace);

        private static LineFacts ScanLine(ReadOnlySpan<byte> source, int start, LythonSourceSpan span, ExecutionServices services)
        {
            var cursor = start;
            var scalars = 0;
            var nonWhitespace = false;
            long nextCheck = start;
            while (cursor < source.Length)
            {
                if (cursor >= nextCheck) { services.CheckExecution(span); nextCheck = (long)cursor + 1024; }
                if (PyStringOps.TryGetLineBreakByteLength(source, cursor, out var endingBytes))
                {
                    scalars += source[cursor] == (byte)'\r' && endingBytes == 2 ? 2 : 1;
                    return new LineFacts(cursor + endingBytes, scalars, nonWhitespace);
                }
                Rune.DecodeFromUtf8(source[cursor..], out var rune, out var width);
                nonWhitespace |= !PyStringOps.IsPythonWhitespace(rune);
                scalars++;
                cursor += width;
            }
            return new LineFacts(cursor, scalars, nonWhitespace);
        }

        private static int CountScalars(ReadOnlySpan<byte> source, LythonSourceSpan span, ExecutionServices services)
        {
            var count = 0;
            long nextCheck = 0;
            for (var cursor = 0; cursor < source.Length;)
            {
                if (cursor >= nextCheck) { services.CheckExecution(span); nextCheck = (long)cursor + 1024; }
                Rune.DecodeFromUtf8(source[cursor..], out _, out var width);
                count++;
                cursor += width;
            }
            return count;
        }

        private static void CheckOutputLength(long scalars, LythonSourceSpan span, ExecutionServices services)
        {
            if (services.Limits.MaxStringLength is { } maximum && scalars > maximum)
                throw RuntimeErrors.Runtime($"maximum string length exceeded ({maximum})", span);
        }

        private static PyString Finish(GovernedByteBuilder builder, LythonSourceSpan span, ExecutionServices services)
        {
            var result = builder.ToPyStringAndRelease();
            services.State.CallTemporaries.TrackFreshString(result, span);
            return result;
        }
    }
}
