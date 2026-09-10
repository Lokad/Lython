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
                    "encode" => BoundCallable.Create((arguments, span, context) =>
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
                    "replace" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length is < 2 or > 3 ||
                            !PyStringOps.TryAsString(arguments[0], out var oldValue) ||
                            !PyStringOps.TryAsString(arguments[1], out var newValue))
                        {
                            throw new LythonRuntimeException("TypeError", "str.replace(old, new[, count]) expects two string arguments and an optional integer count.", span);
                        }

                        var count = arguments.Length == 3 ? ParseStringOptionalInt(arguments[2], "count", "str.replace(old, new[, count])", span) : -1;
                        return OwnMethodResult(PyStringOps.Replace(text, oldValue, newValue, count), text, context.MemoryGovernor, span);
                    }, "str.replace", ["old", "new", "count"], 2),
                    "startswith" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length is < 1 or > 3)
                        {
                            throw new LythonRuntimeException("TypeError", "str.startswith(prefix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds.", span);
                        }

                        var (start, end, startBeyondLength) = ParseStringBounds(text.Length, arguments, span, "str.startswith(prefix[, start[, end]])");
                        return StartsOrEndsWith(text, arguments[0], start, end, startBeyondLength, isStart: true, span);
                    }, "str.startswith", ["prefix", "start", "end"], 1),
                    "endswith" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length is < 1 or > 3)
                        {
                            throw new LythonRuntimeException("TypeError", "str.endswith(suffix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds.", span);
                        }

                        var (start, end, startBeyondLength) = ParseStringBounds(text.Length, arguments, span, "str.endswith(suffix[, start[, end]])");
                        return StartsOrEndsWith(text, arguments[0], start, end, startBeyondLength, isStart: false, span);
                    }, "str.endswith", ["suffix", "start", "end"], 1),
                    "lower" => BoundCallable.CreateNoArguments(text, "str.lower", static (receiver, span, context) => OwnMethodResult(receiver.ToLowerInvariant(), receiver, context.MemoryGovernor, span)),
                    "capitalize" => BoundCallable.CreateNoArguments(text, "str.capitalize", static (receiver, span, context) => OwnMethodResult(PyStringOps.Capitalize(receiver), receiver, context.MemoryGovernor, span)),
                    "islower" => BoundCallable.CreateNoArguments(text, "str.islower", static (receiver, _, _) => PyStringOps.IsLower(receiver)),
                    "upper" => BoundCallable.CreateNoArguments(text, "str.upper", static (receiver, span, context) => OwnMethodResult(receiver.ToUpperInvariant(), receiver, context.MemoryGovernor, span)),
                    "swapcase" => BoundCallable.CreateNoArguments(text, "str.swapcase", static (receiver, span, context) => OwnMethodResult(PyStringOps.SwapCase(receiver), receiver, context.MemoryGovernor, span)),
                    "title" => BoundCallable.CreateNoArguments(text, "str.title", static (receiver, span, context) => OwnMethodResult(PyStringOps.Title(receiver), receiver, context.MemoryGovernor, span)),
                    "isupper" => BoundCallable.CreateNoArguments(text, "str.isupper", static (receiver, _, _) => PyStringOps.IsUpper(receiver)),
                    "isalpha" => BoundCallable.CreateNoArguments(text, "str.isalpha", static (receiver, _, _) => PyStringOps.IsAlpha(receiver)),
                    "isdigit" => BoundCallable.CreateNoArguments(text, "str.isdigit", static (receiver, _, _) => PyStringOps.IsDigit(receiver)),
                    "isalnum" => BoundCallable.CreateNoArguments(text, "str.isalnum", static (receiver, _, _) => PyStringOps.IsAlnum(receiver)),
                    "isspace" => BoundCallable.CreateNoArguments(text, "str.isspace", static (receiver, _, _) => PyStringOps.IsSpace(receiver)),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);

            }
        }
    }
}
