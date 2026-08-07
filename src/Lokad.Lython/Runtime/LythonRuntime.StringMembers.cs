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
    internal static class StringMembers
    {
        public static bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value)
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
                "join" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.join(iterable) expects one argument.", span);
                    }

                    IEnumerable<PyString> EnumerateParts()
                    {
                        foreach (var part in ToSequence(arguments[0], span))
                        {
                            if (!PyStringOps.TryAsString(part, out var partText))
                            {
                                throw new LythonRuntimeException("TypeError", "str.join(iterable) expects an iterable of strings.", span);
                            }

                            yield return partText;
                        }
                    }

                    return JoinStrings(text, EnumerateParts());
                }, "str.join", ["iterable"]),
                "center" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "str.center(width[, fillchar]) expects one integer width and an optional fill string.", span);
                    }

                    var width = ParseStringOptionalInt(arguments[0], "width", "str.center(width[, fillchar])", span);
                    var fill = arguments.Length == 2 ? RequireFillChar(arguments[1], "str.center(width[, fillchar])", span) : null;
                    try
                    {
                        return PyStringOps.Center(text, width, fill);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("TypeError", ex.Message, span);
                    }
                }, "str.center", ["width", "fillchar"], 1),
                "ljust" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "str.ljust(width[, fillchar]) expects one integer width and an optional fill string.", span);
                    }

                    var width = ParseStringOptionalInt(arguments[0], "width", "str.ljust(width[, fillchar])", span);
                    var fill = arguments.Length == 2 ? RequireFillChar(arguments[1], "str.ljust(width[, fillchar])", span) : null;
                    try
                    {
                        return PyStringOps.LJust(text, width, fill);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("TypeError", ex.Message, span);
                    }
                }, "str.ljust", ["width", "fillchar"], 1),
                "rjust" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "str.rjust(width[, fillchar]) expects one integer width and an optional fill string.", span);
                    }

                    var width = ParseStringOptionalInt(arguments[0], "width", "str.rjust(width[, fillchar])", span);
                    var fill = arguments.Length == 2 ? RequireFillChar(arguments[1], "str.rjust(width[, fillchar])", span) : null;
                    try
                    {
                        return PyStringOps.RJust(text, width, fill);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("TypeError", ex.Message, span);
                    }
                }, "str.rjust", ["width", "fillchar"], 1),
                "zfill" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "str.zfill(width) expects one integer width argument.", span);
                    }

                    var width = ParseStringOptionalInt(arguments[0], "width", "str.zfill(width)", span);
                    return PyStringOps.ZFill(text, width);
                }, "str.zfill", ["width"]),
                "find" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var needle))
                    {
                        throw new LythonRuntimeException("TypeError", "str.find(sub[, start[, end]]) expects one string argument plus optional integer bounds.", span);
                    }

                    var (start, end, _) = ParseStringBounds(text.Length, arguments, span, "str.find(sub[, start[, end]])");
                    return PyStringOps.Find(text, needle, start, end);
                }, "str.find", ["sub", "start", "end"], 1),
                "index" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var needle))
                    {
                        throw new LythonRuntimeException("TypeError", "str.index(sub[, start[, end]]) expects one string argument plus optional integer bounds.", span);
                    }

                    var (start, end, _) = ParseStringBounds(text.Length, arguments, span, "str.index(sub[, start[, end]])");
                    var result = PyStringOps.Find(text, needle, start, end);
                    if ((BigInteger)result < 0)
                    {
                        throw new LythonRuntimeException("ValueError", "substring not found", span);
                    }

                    return result;
                }, "str.index", ["sub", "start", "end"], 1),
                "rfind" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var needle))
                    {
                        throw new LythonRuntimeException("TypeError", "str.rfind(sub[, start[, end]]) expects one string argument plus optional integer bounds.", span);
                    }

                    var (start, end, _) = ParseStringBounds(text.Length, arguments, span, "str.rfind(sub[, start[, end]])");
                    return PyStringOps.RFind(text, needle, start, end);
                }, "str.rfind", ["sub", "start", "end"], 1),
                "rindex" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var needle))
                    {
                        throw new LythonRuntimeException("TypeError", "str.rindex(sub[, start[, end]]) expects one string argument plus optional integer bounds.", span);
                    }

                    var (start, end, _) = ParseStringBounds(text.Length, arguments, span, "str.rindex(sub[, start[, end]])");
                    var result = PyStringOps.RFind(text, needle, start, end);
                    if ((BigInteger)result < 0)
                    {
                        throw new LythonRuntimeException("ValueError", "substring not found", span);
                    }

                    return result;
                }, "str.rindex", ["sub", "start", "end"], 1),
                "count" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var needle))
                    {
                        throw new LythonRuntimeException("TypeError", "str.count(sub[, start[, end]]) expects one string argument plus optional integer bounds.", span);
                    }

                    var (start, end, _) = ParseStringBounds(text.Length, arguments, span, "str.count(sub[, start[, end]])");
                    return PyStringOps.Count(text, needle, start, end);
                }, "str.count", ["sub", "start", "end"], 1),
                "removeprefix" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var prefix))
                    {
                        throw new LythonRuntimeException("TypeError", "str.removeprefix(prefix) expects one string argument.", span);
                    }

                    return text.StartsWith(prefix)
                        ? SliceByByteCount(text, prefix.Utf8Bytes.Length, text.Utf8Bytes.Length - prefix.Utf8Bytes.Length)
                        : text;
                }, "str.removeprefix", ["prefix"]),
                "removesuffix" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var suffix))
                    {
                        throw new LythonRuntimeException("TypeError", "str.removesuffix(suffix) expects one string argument.", span);
                    }

                    return suffix.Length != 0 && text.EndsWith(suffix)
                        ? SliceByByteCount(text, 0, text.Utf8Bytes.Length - suffix.Utf8Bytes.Length)
                        : text;
                }, "str.removesuffix", ["suffix"]),
                "partition" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var separator))
                    {
                        throw new LythonRuntimeException("TypeError", "str.partition(sep) expects one string argument.", span);
                    }

                    try
                    {
                        return PyStringOps.Partition(text, separator, context.MemoryGovernor, span);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "str.partition", ["sep"]),
                "rpartition" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var separator))
                    {
                        throw new LythonRuntimeException("TypeError", "str.rpartition(sep) expects one string argument.", span);
                    }

                    try
                    {
                        return PyStringOps.RPartition(text, separator, context.MemoryGovernor, span);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                }, "str.rpartition", ["sep"]),
                "format" => new CustomMethodCallable("str.format", (arguments, span, context) =>
                {
                    try
                    {
                        var positionalCount = 0;
                        foreach (var argument in arguments)
                        {
                            if (argument.IsPositional)
                            {
                                positionalCount++;
                            }
                        }

                        var positional = new object[positionalCount];
                        var positionalIndex = 0;
                        foreach (var argument in arguments)
                        {
                            if (argument.IsPositional)
                            {
                                positional[positionalIndex++] = argument.Value;
                            }
                        }

                        var keywords = new Dictionary<string, object>(StringComparer.Ordinal);
                        foreach (var argument in arguments)
                        {
                            if (argument.IsPositional)
                            {
                                continue;
                            }

                            if (!keywords.TryAdd(argument.KeywordName, argument.Value))
                            {
                                throw CallErrors.MultipleValues(PythonCallableKind.Method, "str.format", argument.KeywordName, span);
                            }
                        }

                        return PyStringOps.Format(text, positional, keywords, field => ResolveFormatField(field, positional, keywords, span, context));
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                    catch (IndexOutOfRangeException ex)
                    {
                        throw new LythonRuntimeException("IndexError", ex.Message, span);
                    }
                    catch (KeyNotFoundException ex)
                    {
                        throw new LythonRuntimeException("KeyError", ex.Message, span);
                    }
                }),
                "format_map" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 1 || arguments[0] is not PyDict mapping)
                    {
                        throw new LythonRuntimeException("TypeError", "str.format_map(mapping) expects one dictionary argument.", span);
                    }

                    try
                    {
                        var keywords = PyStringOps.ExtractStringKeyDictionary(mapping);
                        return PyStringOps.Format(text, Array.Empty<object>(), keywords, field => ResolveFormatField(field, Array.Empty<object>(), keywords, span, context));
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new LythonRuntimeException("ValueError", ex.Message, span);
                    }
                    catch (IndexOutOfRangeException ex)
                    {
                        throw new LythonRuntimeException("IndexError", ex.Message, span);
                    }
                    catch (KeyNotFoundException ex)
                    {
                        throw new LythonRuntimeException("KeyError", ex.Message, span);
                    }
                }, "str.format_map", ["mapping"]),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static int ParseStringOptionalInt(object value, string name, string signature, LythonSourceSpan span)
        {
            return value switch
            {
                BigInteger integer => integer < int.MinValue || integer > int.MaxValue
                    ? throw new LythonRuntimeException("ValueError", $"{signature} {name} is out of range.", span)
                    : (int)integer,
                int integer => integer,
                _ => throw new LythonRuntimeException("TypeError", $"{signature} expects {name} to be an integer.", span)
            };
        }

        private static (int Start, int End, bool StartBeyondLength) ParseStringBounds(
            int textLength,
            object[] arguments,
            LythonSourceSpan span,
            string signature)
        {
            try
            {
                object? start = arguments.Length >= 2 ? arguments[1] : null;
                object? end = arguments.Length == 3 ? arguments[2] : null;
                var normalized = PyStringOps.NormalizeRange(textLength, start, end);
                var startBeyondLength = start switch
                {
                    BigInteger integer => integer > textLength,
                    int integer => integer > textLength,
                    _ => false
                };
                return (normalized.Start, normalized.End, startBeyondLength);
            }
            catch (InvalidOperationException)
            {
                throw new LythonRuntimeException("TypeError", "slice indices must be integers or None or have an __index__ method", span);
            }
        }

        private static bool StartsOrEndsWith(
            PyString text,
            object prefixOrTuple,
            int start,
            int end,
            bool startBeyondLength,
            bool isStart,
            LythonSourceSpan span)
        {
            if (PyStringOps.TryAsString(prefixOrTuple, out var single))
            {
                return !startBeyondLength &&
                    (isStart ? PyStringOps.StartsWith(text, single, start, end) : PyStringOps.EndsWith(text, single, start, end));
            }

            if (prefixOrTuple is not PyTuple tuple)
            {
                throw new LythonRuntimeException("TypeError",
                    isStart
                        ? "str.startswith(prefix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds."
                        : "str.endswith(suffix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds.",
                    span);
            }

            foreach (var item in tuple)
            {
                if (!PyStringOps.TryAsString(item, out var textItem))
                {
                    throw new LythonRuntimeException("TypeError",
                        isStart
                            ? $"tuple for startswith must only contain str, not {TypeName(item)}"
                            : $"tuple for endswith must only contain str, not {TypeName(item)}",
                        span);
                }

                if (!startBeyondLength &&
                    (isStart ? PyStringOps.StartsWith(text, textItem, start, end) : PyStringOps.EndsWith(text, textItem, start, end)))
                {
                    return true;
                }
            }

            return false;
        }

        private static PyString? RequireFillChar(object value, string signature, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(value, out var fill))
            {
                throw new LythonRuntimeException("TypeError", $"{signature} expects fillchar to be a string.", span);
            }

            return fill;
        }

        private static string TypeName(object value)
        {
            return value switch
            {
                BigInteger => "int",
                int => "int",
                bool => "bool",
                PyString => "str",
                PyTuple => "tuple",
                PyList => "list",
                PyDict => "dict",
                PyNone => "NoneType",
                _ => value.GetType().Name
            };
        }

        private static object ResolveFormatField(
            string field,
            IReadOnlyList<object> positional,
            IReadOnlyDictionary<string, object> keywords,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var index = 0;
            var current = ResolveFormatFieldRoot(field, positional, keywords, ref index);

            while (index < field.Length)
            {
                if (field[index] == '.')
                {
                    index++;
                    var start = index;
                    while (index < field.Length && field[index] is not '.' and not '[')
                    {
                        index++;
                    }

                    if (start == index)
                    {
                        throw new InvalidOperationException("Invalid format field.");
                    }

                    var memberName = field[start..index];
                    var memberTarget = current;
                    if (!PyMemberAccess.TryResolve(memberTarget, memberName, context, span, out current))
                    {
                        throw PyMemberAccess.CreateMissingMemberError(memberTarget, memberName, span);
                    }

                    continue;
                }

                if (field[index] == '[')
                {
                    index++;
                    var start = index;
                    while (index < field.Length && field[index] != ']')
                    {
                        index++;
                    }

                    if (index >= field.Length)
                    {
                        throw new InvalidOperationException("Invalid format field.");
                    }

                    var token = field[start..index];
                    index++;
                    object key = int.TryParse(token, out var intIndex)
                        ? new BigInteger(intIndex)
                        : PyString.FromString(token);
                    current = PyIndexing.ReadIndex(current, key, span);
                    continue;
                }

                throw new InvalidOperationException("Invalid format field.");
            }

            return current;
        }

        private static object ResolveFormatFieldRoot(
            string field,
            IReadOnlyList<object> positional,
            IReadOnlyDictionary<string, object> keywords,
            ref int index)
        {
            var start = index;
            while (index < field.Length && field[index] is not '.' and not '[')
            {
                index++;
            }

            var root = field[start..index];
            if (root.Length == 0)
            {
                throw new InvalidOperationException("Invalid format field.");
            }

            if (int.TryParse(root, out var intIndex))
            {
                if (intIndex < 0 || intIndex >= positional.Count)
                {
                    throw new IndexOutOfRangeException($"Replacement index {intIndex} out of range for positional args tuple");
                }

                return positional[intIndex];
            }

            if (!keywords.TryGetValue(root, out var value))
            {
                throw new KeyNotFoundException(root);
            }

            return value;
        }
    }
}
