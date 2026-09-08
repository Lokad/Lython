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
            return new LythonRuntimeException(ModuleException("json", "JSONDecodeError"), exception.Message, span, exception, payload);
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

        private static JsonSeparators ParseJsonSeparators(object value, bool pretty, LythonSourceSpan span, ExecutionContext context)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return pretty ? new JsonSeparators(",", ": ") : new JsonSeparators(", ", ": ");
            }

            if (value is not PyTuple and not PyList)
            {
                throw new LythonRuntimeException("TypeError", "json.dumps(separators=...) expects a two-item tuple/list of strings or None.", span);
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
