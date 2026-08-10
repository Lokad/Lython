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
    internal static partial class StringMembers
    {
        private sealed class BasicStringMemberProvider : IStringMemberProvider
        {
            public static readonly BasicStringMemberProvider Instance = new();

            public bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {                "encode" => new BoundCallable((arguments, span, context) =>
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
                "lower" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.lower() expects no arguments.", span);
                    }

                    return text.ToLowerInvariant();
                }),
                "capitalize" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.capitalize() expects no arguments.", span);
                    }

                    return PyStringOps.Capitalize(text);
                }),
                "islower" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.islower() expects no arguments.", span);
                    }

                    return PyStringOps.IsLower(text);
                }),
                "upper" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.upper() expects no arguments.", span);
                    }

                    return text.ToUpperInvariant();
                }),
                "swapcase" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.swapcase() expects no arguments.", span);
                    }

                    return PyStringOps.SwapCase(text);
                }),
                "title" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.title() expects no arguments.", span);
                    }

                    return PyStringOps.Title(text);
                }),
                "isupper" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.isupper() expects no arguments.", span);
                    }

                    return PyStringOps.IsUpper(text);
                }),
                "isalpha" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.isalpha() expects no arguments.", span);
                    }

                    return PyStringOps.IsAlpha(text);
                }),
                "isdigit" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.isdigit() expects no arguments.", span);
                    }

                    return PyStringOps.IsDigit(text);
                }),
                "isalnum" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.isalnum() expects no arguments.", span);
                    }

                    return PyStringOps.IsAlnum(text);
                }),
                "isspace" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "str.isspace() expects no arguments.", span);
                    }

                    return PyStringOps.IsSpace(text);
                }),
                "split" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length == 0)
                    {
                        return PyStringOps.SplitWhitespace(text, context.MemoryGovernor, span);
                    }

                    int maxSplit;
                    if (arguments[0] is PyNone)
                    {
                        maxSplit = arguments.Length == 2 ? ParseStringOptionalInt(arguments[1], "maxsplit", "str.split([separator[, maxsplit]])", span) : -1;
                        return PyStringOps.SplitWhitespace(text, maxSplit, context.MemoryGovernor, span);
                    }

                    if (arguments.Length is < 1 or > 2 || !PyStringOps.TryAsString(arguments[0], out var separator))
                    {
                        throw new LythonRuntimeException("TypeError", "str.split([separator[, maxsplit]]) expects zero, one, or two arguments with string separator and optional integer maxsplit.", span);
                    }

                    maxSplit = arguments.Length == 2 ? ParseStringOptionalInt(arguments[1], "maxsplit", "str.split([separator[, maxsplit]])", span) : -1;
                    try
                    {
                        return PyStringOps.Split(text, separator, maxSplit, context.MemoryGovernor, span);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "str.split", ["separator", "maxsplit"], 0),
                "rsplit" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length == 0)
                    {
                        return PyStringOps.RSplitWhitespace(text, -1, context.MemoryGovernor, span);
                    }

                    int maxSplit;
                    if (arguments[0] is PyNone)
                    {
                        maxSplit = arguments.Length == 2 ? ParseStringOptionalInt(arguments[1], "maxsplit", "str.rsplit([separator[, maxsplit]])", span) : -1;
                        return PyStringOps.RSplitWhitespace(text, maxSplit, context.MemoryGovernor, span);
                    }

                    if (arguments.Length is < 1 or > 2 || !PyStringOps.TryAsString(arguments[0], out var separator))
                    {
                        throw new LythonRuntimeException("TypeError", "str.rsplit([separator[, maxsplit]]) expects zero, one, or two arguments with string separator and optional integer maxsplit.", span);
                    }

                    maxSplit = arguments.Length == 2 ? ParseStringOptionalInt(arguments[1], "maxsplit", "str.rsplit([separator[, maxsplit]])", span) : -1;
                    try
                    {
                        return PyStringOps.RSplit(text, separator, maxSplit, context.MemoryGovernor, span);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "str.rsplit", ["separator", "maxsplit"], 0),
                "splitlines" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.splitlines([keepends]) expects zero or one bool argument.", span);
                    }

                    var keepEnds = false;
                    if (arguments.Length == 1)
                    {
                        keepEnds = IsTruthy(arguments[0]);
                    }

                    return PyStringOps.SplitLines(text, keepEnds, context.MemoryGovernor, span);
                }, new LythonCallableSignature("str.splitlines", ["keepends"], RequiredCount: 0, MaxPositionalCount: null, VariadicParameters: LythonVariadicParameters.None, PositionalOnlyCount: 1)),
                "expandtabs" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.expandtabs([tabsize]) expects zero or one integer argument.", span);
                    }

                    var tabSize = arguments.Length == 1 ? ParseStringOptionalInt(arguments[0], "tabsize", "str.expandtabs([tabsize])", span) : 8;
                    return PyStringOps.ExpandTabs(text, tabSize);
                }, "str.expandtabs", ["tabsize"], 0),
                "strip" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.strip([chars]) expects zero or one string argument.", span);
                    }

                    if (arguments.Length == 0 || ReferenceEquals(arguments[0], PyNone.Instance))
                    {
                        return PyStringOps.Strip(text);
                    }

                    if (!PyStringOps.TryAsString(arguments[0], out var chars))
                    {
                        throw new LythonRuntimeException("TypeError", "str.strip([chars]) expects zero or one string argument.", span);
                    }

                    return PyStringOps.Strip(text, chars);
                }, "str.strip", ["chars"], 0),
                "lstrip" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.lstrip([chars]) expects zero or one string argument.", span);
                    }

                    if (arguments.Length == 0 || ReferenceEquals(arguments[0], PyNone.Instance))
                    {
                        return PyStringOps.LStrip(text);
                    }

                    if (!PyStringOps.TryAsString(arguments[0], out var chars))
                    {
                        throw new LythonRuntimeException("TypeError", "str.lstrip([chars]) expects zero or one string argument.", span);
                    }

                    return PyStringOps.LStrip(text, chars);
                }, "str.lstrip", ["chars"], 0),
                "rstrip" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.rstrip([chars]) expects zero or one string argument.", span);
                    }

                    if (arguments.Length == 0 || ReferenceEquals(arguments[0], PyNone.Instance))
                    {
                        return PyStringOps.RStrip(text);
                    }

                    if (!PyStringOps.TryAsString(arguments[0], out var chars))
                    {
                        throw new LythonRuntimeException("TypeError", "str.rstrip([chars]) expects zero or one string argument.", span);
                    }

                    return PyStringOps.RStrip(text, chars);
                }, "str.rstrip", ["chars"], 0),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }
        }
    }
}