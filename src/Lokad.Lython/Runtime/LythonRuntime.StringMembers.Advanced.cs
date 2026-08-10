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
        private sealed class AdvancedStringMemberProvider : IStringMemberProvider
        {
            public static readonly AdvancedStringMemberProvider Instance = new();

            public bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {                "join" => new BoundCallable((arguments, span, _) =>
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
        }
    }
}