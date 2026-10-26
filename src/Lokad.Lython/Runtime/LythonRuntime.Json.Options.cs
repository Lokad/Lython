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
    internal sealed record JsonLoadOptions(
        object? ObjectHook,
        object? ParseFloat,
        object? ParseInt,
        object? ParseConstant,
        object? ObjectPairsHook, object Strict, bool Prepared = false)
    {
        // Fixed runtime metadata: no guest callbacks, values or execution owners.
        internal static readonly JsonLoadOptions Default = new(null, null, null, null, null, true, Prepared: true);

        internal static JsonLoadOptions Create(object? objectHook, object? parseFloat,
            object? parseInt, object? parseConstant, object? objectPairsHook, object strict)
            => objectHook is null && parseFloat is null && parseInt is null
                && parseConstant is null && objectPairsHook is null && strict is true
                ? Default
                : new(objectHook, parseFloat, parseInt, parseConstant, objectPairsHook, strict);

        internal async ValueTask<JsonLoadOptions> PrepareAsync(ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            if (Prepared) return this;
            var floating = ParseFloat is not null && await JsonOptionTruthAsync(ParseFloat, context, span, asynchronous).ConfigureAwait(false) ? ParseFloat : null;
            var integer = ParseInt is not null && await JsonOptionTruthAsync(ParseInt, context, span, asynchronous).ConfigureAwait(false) ? ParseInt : null;
            var constant = ParseConstant is not null && await JsonOptionTruthAsync(ParseConstant, context, span, asynchronous).ConfigureAwait(false) ? ParseConstant : null;
            return this with { ParseFloat = floating, ParseInt = integer, ParseConstant = constant,
                Strict = await JsonOptionTruthAsync(Strict, context, span, asynchronous).ConfigureAwait(false), Prepared = true };
        }
    }

    internal sealed record JsonDumpOptions(
        object SkipKeys,
        bool EnsureAscii,
        bool CheckCircular,
        object AllowNan,
        string? IndentUnit,
        PyString ItemSeparator,
        PyString KeySeparator,
        object? DefaultCallable,
        object SortKeys)
    {
        // Immutable builtin metadata; no guest values, receiver or execution owner.
        internal static readonly JsonDumpOptions Default = new(false, true, true, true,
            null, PyString.FromString(", "), PyString.FromString(": "), BoundCallable.Create(JsonEncoderObject.Default,
                LythonKnownCallableSignatures.JsonEncoderDefault), false);
    }

    internal readonly record struct JsonSeparators(object ItemSeparator, object KeySeparator);

    private static ValueTask<bool> JsonOptionTruthAsync(object value, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        => value is bool flag ? new(flag) : asynchronous ? IsTruthyAsync(value, context, span) : new(IsTruthy(value, context, span));

    private sealed partial class JsonModule : PyModule
    {
        private static LythonRuntimeException CreateJsonDecodeError(PyString input, JsonException exception, LythonSourceSpan span, ExecutionContext context)
        {
            var reportedBytePosition = ComputeJsonErrorBytePosition(
                input.Utf8Bytes.Span,
                exception.LineNumber.GetValueOrDefault(),
                exception.BytePositionInLine.GetValueOrDefault());
            var bytePosition = NormalizeJsonErrorBytePosition(input.Utf8Bytes.Span, reportedBytePosition, exception.Message);
            return NewJsonDecodeFailure(input, exception.Message, bytePosition, exception, span, context);
        }

        // Builds the catchable JSONDecodeError value (msg/doc/pos/lineno/colno)
        // for an explicit message and parse-space byte position: used both for
        // backend errors above and for positions the core detects itself
        // (truncation, trailing data, strict violations, index bounds). Positions
        // always refer to the original document; token-local backend errors
        // are translated before reaching this shared payload builder.
        private static LythonRuntimeException NewJsonDecodeFailure(PyString document, string message, int bytePosition, Exception? innerException, LythonSourceSpan span, ExecutionContext context)
        {
            var position = document.ByteIndexToRuneIndex(bytePosition);
            var location = ComputeJsonErrorLocation(document, bytePosition);
            var payload = new PyDict(context.MemoryGovernor, span);
            var errorMessage = CreateString(message, context, span);
            payload.SetItem(PyString.FromString("msg"), errorMessage);
            payload.SetItem(PyString.FromString("doc"), document);
            payload.SetItem(PyString.FromString("pos"), new BigInteger(position));
            payload.SetItem(PyString.FromString("lineno"), new BigInteger(location.Line));
            payload.SetItem(PyString.FromString("colno"), new BigInteger(location.Column));
            var summary = string.Create(CultureInfo.InvariantCulture,
                $"{message}: line {location.Line} column {location.Column} (char {position})");
            return new LythonRuntimeException(ModuleException("json", "JSONDecodeError"), summary, span, innerException, payload);
        }

        // Builds the catchable JSONDecodeError for an explicit Python string
        // index (used when the index lies past the end, where no parse byte
        // position maps to it). Lines and columns count like the byte path.
        private static LythonRuntimeException NewJsonDecodeFailureAtRune(PyString input, string message, long runePosition, LythonSourceSpan span, ExecutionContext context)
        {
            var location = ComputeJsonRuneLocation(input, runePosition);
            var payload = new PyDict(context.MemoryGovernor, span);
            var errorMessage = CreateString(message, context, span);
            payload.SetItem(PyString.FromString("msg"), errorMessage);
            payload.SetItem(PyString.FromString("doc"), input);
            payload.SetItem(PyString.FromString("pos"), new BigInteger(runePosition));
            payload.SetItem(PyString.FromString("lineno"), new BigInteger(location.Line));
            payload.SetItem(PyString.FromString("colno"), new BigInteger(location.Column));
            var summary = string.Create(CultureInfo.InvariantCulture,
                $"{message}: line {location.Line} column {location.Column} (char {runePosition})");
            return new LythonRuntimeException(ModuleException("json", "JSONDecodeError"), summary, span, innerException: null, payload);
        }

        // Reports a prefix index past the end with CPython coordinates: every
        // newline in the document precedes it.
        private static LythonRuntimeException CreateJsonExpectingErrorAtRune(PyString input, long runePosition, LythonSourceSpan span, ExecutionContext context)
            => NewJsonDecodeFailureAtRune(input, "Expecting value", runePosition, span, context);

        private static JsonSourceLocation ComputeJsonRuneLocation(PyString document, long runePosition)
        {
            var line = 1;
            var lineStart = 0L;
            var index = 0L;
            foreach (var rune in document.EnumerateRunes())
            {
                if (index >= runePosition)
                {
                    break;
                }

                // Newlines are single-byte, so no multi-byte sequence can
                // start with this byte.
                if (rune.Utf8Bytes.Span[0] == (byte)'\n')
                {
                    line++;
                    lineStart = index + 1;
                }

                index++;
            }

            return new JsonSourceLocation(line, (int)(runePosition - lineStart + 1));
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

        internal static object InvokeJsonCallback(object callable, object argument, ExecutionContext context, LythonSourceSpan span)
        {
            var result = InvokeCallableTarget(callable, span, span, context, [CallArgumentValue.Positional(argument)]);
            return result;
        }

        // The parser/encoder caller owns frame and scratch cleanup through its
        // await. This helper forwards the operation without another state machine.
        internal static ValueTask<object> InvokeJsonCallbackCoreAsync(object callable, object argument, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
            => asynchronous
                ? InvokeCallableTargetAsync(callable, span, span, context,
                    () => ValueTask.FromResult(new[] { CallArgumentValue.Positional(argument) }))
                : new(InvokeJsonCallback(callable, argument, context, span));

        internal static object? OptionalJsonValue(object value) => value is PyNone ? null : value;

        internal static string? ParseJsonIndent(object value, LythonSourceSpan span, ExecutionContext context)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return null;
            }

            if (PyStringOps.TryAsString(value, out var text))
            {
                var characters = StrictUtf8.GetCharCount(text.Utf8Bytes.Span);
                if (characters == 0) return string.Empty;
                var bytes = 32L + 2L * characters;
                using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
                context.CheckExecution(span);
                var result = text.AsString();
                storage.Commit(bytes);
                return result;
            }

            // R18: Python bools are integers (True == 1); accept them exactly
            // like their numeric value instead of rejecting them outright.
            if (value is bool boolean)
            {
                return boolean ? " " : string.Empty;
            }

            if (value is BigInteger integer)
            {
                if (integer <= BigInteger.Zero)
                {
                    return string.Empty;
                }

                if (integer > int.MaxValue)
                {
                    throw new LythonRuntimeException("OverflowError", "json indentation exceeds string capacity.", span);
                }

                var bytes = 32L + 2L * (int)integer;
                using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
                context.CheckExecution(span);
                var result = new string(' ', (int)integer);
                storage.Commit(bytes);
                return result;
            }

            throw new LythonRuntimeException("TypeError", "json.dumps(indent=...) expects an integer, string, or None.", span);
        }

        internal static readonly PyString PrettyItemSeparator = PyString.FromString(",");

        internal static PyString RequireJsonSeparator(object value, LythonSourceSpan span)
            => PyStringOps.TryAsString(value, out var text) ? text : throw RuntimeErrors.Type("JSON separator must be a string", span);

        internal static async ValueTask<JsonSeparators> ParseJsonSeparatorsAsync(object value, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            using var values = asynchronous
                ? await PyIteration.MaterializeLeasedAsync(value, span, context, maximumItems: 3).ConfigureAwait(false)
                : MaterializeUnpackingSequence(value, span, context, maximumItems: 3);
            if (values.Items.Count != 2) throw new LythonRuntimeException("ValueError", DescribeLoopArityMismatch(2, values.Items.Count), span);
            return new(values.Items[0], values.Items[1]);
        }

        private static object GetOptional(object[] arguments, int index)
            => index < arguments.Length ? arguments[index] : PyNone.Instance;


        private enum JsonDumpCallForm
        {
            Dump,
            Dumps
        }


        private readonly record struct JsonSourceLocation(int Line, int Column);


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
                context.CheckExecution(span);
                throw new LythonRuntimeException("NotImplementedError", $"{Name} custom classes are not supported by Lython's JSON subset.", span);
            }

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString("<class '" + Name + "'>");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        }

    }
}
