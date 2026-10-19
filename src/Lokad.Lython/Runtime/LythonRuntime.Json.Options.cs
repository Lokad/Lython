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
        ICallable? ObjectHook,
        ICallable? ParseFloat,
        ICallable? ParseInt,
        ICallable? ParseConstant,
        ICallable? ObjectPairsHook, object Strict)
    {
        // Fixed runtime metadata: no guest callbacks, values or execution owners.
        internal static readonly JsonLoadOptions Default = new(null, null, null, null, null, true);

        internal static JsonLoadOptions Create(ICallable? objectHook, ICallable? parseFloat,
            ICallable? parseInt, ICallable? parseConstant, ICallable? objectPairsHook, object strict)
            => objectHook is null && parseFloat is null && parseInt is null
                && parseConstant is null && objectPairsHook is null && strict is true
                ? Default
                : new(objectHook, parseFloat, parseInt, parseConstant, objectPairsHook, strict);
    }

    internal sealed record JsonDumpOptions(
        bool SkipKeys,
        bool EnsureAscii,
        bool CheckCircular,
        bool AllowNan,
        string? IndentUnit,
        string ItemSeparator,
        string KeySeparator,
        ICallable? DefaultCallable,
        bool SortKeys)
    {
        // Immutable builtin metadata; no guest values, receiver or execution owner.
        internal static readonly JsonDumpOptions Default = new(false, true, true, true,
            null, ", ", ": ", BoundCallable.Create(JsonEncoderObject.Default,
                LythonKnownCallableSignatures.JsonEncoderDefault), false);
    }

    internal readonly record struct JsonSeparators(string ItemSeparator, string KeySeparator);

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

        internal static object InvokeJsonCallback(ICallable callable, object argument, ExecutionContext context, LythonSourceSpan span)
        {
            var result = InvokeCallableTarget(callable, span, span, context, [CallArgumentValue.Positional(argument)]);
            return result;
        }

        // The parser/encoder caller owns frame and scratch cleanup through its
        // await. This helper forwards the operation without another state machine.
        internal static ValueTask<object> InvokeJsonCallbackCoreAsync(ICallable callable, object argument, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
            => asynchronous
                ? InvokeCallableTargetAsync(callable, span, span, context,
                    () => ValueTask.FromResult(new[] { CallArgumentValue.Positional(argument) }))
                : new(InvokeJsonCallback(callable, argument, context, span));

        internal static ICallable? OptionalJsonCallable(object value, string parameterName, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return null;
            }

            if (value is ICallable callable)
            {
                return callable;
            }

            throw new LythonRuntimeException("TypeError", $"json option {parameterName}=... expects a callable or None.", span);
        }

        internal static string? ParseJsonIndent(object value, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return null;
            }

            if (PyStringOps.TryAsString(value, out var text))
            {
                return text.AsString();
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

                if (integer > 32)
                {
                    throw new LythonRuntimeException("OverflowError", "json.dumps(indent=...) is too large for Lython.", span);
                }

                return new string(' ', (int)integer);
            }

            throw new LythonRuntimeException("TypeError", "json.dumps(indent=...) expects an integer, string, or None.", span);
        }

        internal static JsonSeparators ParseJsonSeparators(object value, bool pretty, LythonSourceSpan span, ExecutionContext context)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return pretty ? new JsonSeparators(",", ": ") : new JsonSeparators(", ", ": ");
            }

            if (value is not PyTuple and not PyList)
            {
                throw new LythonRuntimeException("TypeError", "json.dumps(separators=...) expects a two-item tuple/list of strings or None.", span);
            }

            // Fixed-arity input: check the concrete count before copying so an
            // oversized tuple/list fails without a full materialization.
            var knownCount = value switch
            {
                PyTuple tuple => tuple.Count,
                PyList list => list.Count,
                _ => -1,
            };
            if (knownCount != -1 && knownCount != 2)
            {
                throw new LythonRuntimeException("TypeError", "json.dumps(separators=...) expects a two-item tuple/list of strings.", span);
            }

            var items = ToSequence(value, span, context).ToArray();
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
