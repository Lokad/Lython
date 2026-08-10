using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class StringMembers
    {
        private sealed class TextSemanticsMemberProvider : IStringMemberProvider
        {
            public static readonly TextSemanticsMemberProvider Instance = new();

            public bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "encode" => new BoundCallable((arguments, span, context) =>
                    {
                        if (arguments.Length > 2)
                        {
                            throw new LythonRuntimeException("TypeError", "str.encode([encoding][, errors]) expects zero to two arguments.", span);
                        }

                        var encoding = arguments.Length >= 1
                            ? ParseTextEncoding(arguments[0], "str.encode()", span)
                            : TextEncodingMode.Utf8;
                        var errors = arguments.Length == 2
                            ? ParseTextErrors(arguments[1], "str.encode()", span)
                            : TextErrorMode.Strict;
                        return CreateBytes(
                            EncodeText(text, encoding, errors, TextNewlineMode.PreserveUniversal, context, span),
                            context,
                            span);
                    }, "str.encode", ["encoding", "errors"], 0),
                    "replace" => new BoundCallable((arguments, span, _) =>
                    {
                        if (arguments.Length is < 2 or > 3 ||
                            !PyStringOps.TryAsString(arguments[0], out var oldValue) ||
                            !PyStringOps.TryAsString(arguments[1], out var newValue))
                        {
                            throw new LythonRuntimeException("TypeError", "str.replace(old, new[, count]) expects two string arguments and an optional integer count.", span);
                        }

                        var count = arguments.Length == 3 ? ParseStringOptionalInt(arguments[2], "count", "str.replace(old, new[, count])", span) : -1;
                        return PyStringOps.Replace(text, oldValue, newValue, count);
                    }, "str.replace", ["old", "new", "count"], 2),
                    "startswith" => new BoundCallable((arguments, span, _) =>
                    {
                        if (arguments.Length is < 1 or > 3)
                        {
                            throw new LythonRuntimeException("TypeError", "str.startswith(prefix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds.", span);
                        }

                        var (start, end, startBeyondLength) = ParseStringBounds(text.Length, arguments, span, "str.startswith(prefix[, start[, end]])");
                        return StartsOrEndsWith(text, arguments[0], start, end, startBeyondLength, isStart: true, span);
                    }, "str.startswith", ["prefix", "start", "end"], 1),
                    "endswith" => new BoundCallable((arguments, span, _) =>
                    {
                        if (arguments.Length is < 1 or > 3)
                        {
                            throw new LythonRuntimeException("TypeError", "str.endswith(suffix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds.", span);
                        }

                        var (start, end, startBeyondLength) = ParseStringBounds(text.Length, arguments, span, "str.endswith(suffix[, start[, end]])");
                        return StartsOrEndsWith(text, arguments[0], start, end, startBeyondLength, isStart: false, span);
                    }, "str.endswith", ["suffix", "start", "end"], 1),
                    "lower" => NoArgumentMethod("str.lower", text.ToLowerInvariant),
                    "capitalize" => NoArgumentMethod("str.capitalize", () => PyStringOps.Capitalize(text)),
                    "islower" => NoArgumentMethod("str.islower", () => PyStringOps.IsLower(text)),
                    "upper" => NoArgumentMethod("str.upper", text.ToUpperInvariant),
                    "swapcase" => NoArgumentMethod("str.swapcase", () => PyStringOps.SwapCase(text)),
                    "title" => NoArgumentMethod("str.title", () => PyStringOps.Title(text)),
                    "isupper" => NoArgumentMethod("str.isupper", () => PyStringOps.IsUpper(text)),
                    "isalpha" => NoArgumentMethod("str.isalpha", () => PyStringOps.IsAlpha(text)),
                    "isdigit" => NoArgumentMethod("str.isdigit", () => PyStringOps.IsDigit(text)),
                    "isalnum" => NoArgumentMethod("str.isalnum", () => PyStringOps.IsAlnum(text)),
                    "isspace" => NoArgumentMethod("str.isspace", () => PyStringOps.IsSpace(text)),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);

                static BoundCallable NoArgumentMethod(string methodName, Func<object> operation)
                    => new((arguments, span, _) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", $"{methodName}() expects no arguments.", span);
                        }

                        return operation();
                    });
            }
        }
    }
}
