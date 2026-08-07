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
    private sealed class PathlibModule : PyModule
    {
        public static readonly PathlibModule Instance = new();
        private static readonly PathlibPathType PathType = new("Path", isSupported: true);
        private static readonly PathlibPathType PurePathType = new("PurePath", isSupported: true);
        private static readonly PathlibPathType PurePosixPathType = new("PurePosixPath", isSupported: true);
        private static readonly PathlibPathType PosixPathType = new("PosixPath", isSupported: true);
        private static readonly PathlibPathType PureWindowsPathType = new("PureWindowsPath", isSupported: false);
        private static readonly PathlibPathType WindowsPathType = new("WindowsPath", isSupported: false);

        private PathlibModule() : base("pathlib")
        {
        }

        public override IReadOnlyList<string> ExportedNames
            => ["Path", "PurePath", "PurePosixPath", "PosixPath", "PureWindowsPath", "WindowsPath"];

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "Path" => PathType,
                "PurePath" => PurePathType,
                "PurePosixPath" => PurePosixPathType,
                "PosixPath" => PosixPathType,
                "PureWindowsPath" => PureWindowsPathType,
                "WindowsPath" => WindowsPathType,
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object CreatePath(IReadOnlyList<object> arguments, LythonSourceSpan span)
        {
            if (arguments.Count == 0)
            {
                return new PyPath(PyStringOps.DotLiteral);
            }

            PyString? path = null;
            foreach (var argument in arguments)
            {
                PyString segment;
                if (argument is PyPath pyPath)
                {
                    segment = pyPath.Value;
                }
                else if (!PyStringOps.TryAsString(argument, out segment))
                {
                    throw new LythonRuntimeException("TypeError", "pathlib.Path([path][, ...]) expects string or Path arguments.", span);
                }

                path = path is null ? segment : PathOps.Join(path, segment);
            }

            return new PyPath(PathOps.NormalizeLexical(path.RequireNotNull()));
        }

        private sealed class PathlibPathType : ICallable, IPyDynamicAttributes, IPyRenderableValue, INamedRuntimeCallable
        {
            private readonly bool _isSupported;

            public PathlibPathType(string shortName, bool isSupported)
            {
                ShortName = shortName;
                _isSupported = isSupported;
            }

            public string ShortName { get; }

            public string Name => $"pathlib.{ShortName}";

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                if (!_isSupported)
                {
                    throw UnsupportedWindowsPath(span);
                }

                var values = new object[arguments.Length];
                for (var i = 0; i < arguments.Length; i++)
                {
                    if (arguments[i].IsKeyword)
                    {
                        throw CallErrors.NoKeywordArguments("Builtin", Name, span);
                    }

                    values[i] = arguments[i].Value;
                }

                return CreatePath(values, span);
            }

            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "cwd" when _isSupported => new BoundCallable((arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", $"{Name}.cwd() expects no arguments.", span);
                        }

                        context.RegisterHostCall(span);
                        return new PyPath(PyString.FromString(PathOps.Normalize(context.Host.Cwd)));
                    }, $"{Name}.cwd", []),
                    "home" when _isSupported => new BoundCallable((arguments, span, _) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", $"{Name}.home() expects no arguments.", span);
                        }

                        throw new LythonRuntimeException("NotImplementedError", $"{Name}.home() is not supported by Lython; the host does not expose an ambient user home directory.", span);
                    }, $"{Name}.home", []),
                    "__name__" => PyString.FromString(ShortName),
                    _ => MissingMemberValue.Instance
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }

            public bool TrySetMember(string name, object value)
            {
                _ = name;
                _ = value;
                return false;
            }

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString($"<class 'pathlib.{ShortName}'>");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

            private LythonRuntimeException UnsupportedWindowsPath(LythonSourceSpan span)
                => new(
                    "NotImplementedError",
                    $"{Name} is not supported by Lython's normalized POSIX-like path model.",
                    span);
        }
    }

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
                                throw CallErrors.MultipleValues("Method", "str.format", argument.KeywordName, span);
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

    internal static class BytesMembers
    {
        public static bool TryGetMember(PyBytes bytes, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "decode" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length > 2)
                    {
                        throw new LythonRuntimeException("TypeError", "bytes.decode([encoding][, errors]) expects zero to two arguments.", span);
                    }

                    var encoding = arguments.Length >= 1
                        ? ParseTextEncoding(arguments[0], "bytes.decode()", span)
                        : TextEncodingMode.Utf8;
                    var errors = arguments.Length == 2
                        ? ParseTextErrors(arguments[1], "bytes.decode()", span)
                        : TextErrorMode.Strict;
                    return DecodeText(bytes.ToArray(), encoding, context, span, errors, TextNewlineMode.PreserveUniversal);
                }, "bytes.decode", ["encoding", "errors"], 0),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    private sealed class BoundCallable : ICallable
    {
        private readonly Func<object[], LythonSourceSpan, ExecutionContext, object> _implementation;
        private readonly Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? _asyncImplementation;
        private readonly LythonCallableSignature _signature;

        public BoundCallable(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, LythonCallableSignature signature) : this(implementation, signature, null) { }

        public BoundCallable(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            LythonCallableSignature signature,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation)
        {
            _implementation = implementation;
            _asyncImplementation = asyncImplementation;
            _signature = signature;
        }

        public BoundCallable(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation) : this(implementation, new LythonCallableSignature("bound method")) { }

        public BoundCallable(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, string? name) : this(implementation, name, null, null) { }

        public BoundCallable(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, string? name, string[]? parameterNames) : this(implementation, name, parameterNames, null) { }

        public BoundCallable(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            string? name,
            string[]? parameterNames,
            int? requiredCount)
            : this(implementation, new LythonCallableSignature(name ?? "bound method", parameterNames, requiredCount))
        {
        }

        public BoundCallable(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation) : this(implementation, asyncImplementation, null, null, null) { }

        public BoundCallable(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation, string? name) : this(implementation, asyncImplementation, name, null, null) { }

        public BoundCallable(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation, string? name, string[]? parameterNames) : this(implementation, asyncImplementation, name, parameterNames, null) { }

        public BoundCallable(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation,
            string? name,
            string[]? parameterNames,
            int? requiredCount)
            : this(implementation, new LythonCallableSignature(name ?? "bound method", parameterNames, requiredCount), asyncImplementation)
        {
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var positional = CallBinder.BindNamedArguments(arguments, span, _signature, "Method");
            return _implementation(positional, span, context);
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var positional = CallBinder.BindNamedArguments(arguments, span, _signature, "Method");
            return _asyncImplementation is null
                ? _implementation(positional, span, context)
                : await _asyncImplementation(positional, span, context).ConfigureAwait(false);
        }
    }

    private sealed class RawBoundCallable(
        Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> implementation) : ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return implementation(arguments, span, context);
        }
    }

    internal sealed class FnMatchModule : PyModule
    {
        public static readonly FnMatchModule Instance = new();

        private FnMatchModule() : base("fnmatch")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "fnmatch" => new BuiltinCallable(LythonKnownCallableSignatures.FnMatch, Match),
                "fnmatchcase" => new BuiltinCallable(LythonKnownCallableSignatures.FnMatchCase, MatchCase),
                "filter" => new BuiltinCallable(LythonKnownCallableSignatures.FnMatchFilter, Filter),
                "translate" => new BuiltinCallable(LythonKnownCallableSignatures.FnMatchTranslate, Translate),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private object Match(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2 ||
                !PyStringOps.TryAsString(arguments[0], out var name) ||
                !PyStringOps.TryAsString(arguments[1], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", "fnmatch.fnmatch(name, pattern) expects two string arguments.", span);
            }

            return MatchSimple(name, pattern);
        }

        private object MatchCase(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2 ||
                !PyStringOps.TryAsString(arguments[0], out var name) ||
                !PyStringOps.TryAsString(arguments[1], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", "fnmatch.fnmatchcase(name, pattern) expects two string arguments.", span);
            }

            return MatchSimple(name, pattern);
        }

        private object Filter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2 || !PyStringOps.TryAsString(arguments[1], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", "fnmatch.filter(names, pattern) expects an iterable and a string pattern.", span);
            }

            var result = new PyList([], context.MemoryGovernor, span);
            foreach (var item in ToSequence(arguments[0], span))
            {
                if (!PyStringOps.TryAsString(item, out var name))
                {
                    throw new LythonRuntimeException("TypeError", "fnmatch.filter(names, pattern) expects an iterable of strings.", span);
                }

                if (MatchSimple(name, pattern))
                {
                    result.Add(name);
                }
            }

            return result;
        }

        private object Translate(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var pattern))
            {
                throw new LythonRuntimeException("TypeError", "fnmatch.translate(pattern) expects a string pattern.", span);
            }

            return CreateString(TranslatePattern(pattern.AsString()), context, span);
        }

        internal static bool MatchSimple(PyString name, PyString pattern)
        {
            var nameRunes = MaterializeRunes(name);
            var patternRunes = MaterializeRunes(pattern);
            var memo = new Dictionary<(int Name, int Pattern), bool>();
            return MatchSimple(nameRunes, 0, patternRunes, 0, memo);
        }

        private static PyString[] MaterializeRunes(PyString value)
        {
            var runes = new PyString[value.Length];
            var index = 0;
            foreach (var rune in value.EnumerateRunes())
            {
                runes[index++] = rune;
            }

            return runes;
        }

        private static bool MatchSimple(
            IReadOnlyList<PyString> name,
            int nameIndex,
            IReadOnlyList<PyString> pattern,
            int patternIndex,
            Dictionary<(int Name, int Pattern), bool> memo)
        {
            if (memo.TryGetValue((nameIndex, patternIndex), out var cached))
            {
                return cached;
            }

            bool result;
            if (patternIndex == pattern.Count)
            {
                result = nameIndex == name.Count;
            }
            else
            {
                var token = pattern[patternIndex];
                if (token.Utf8Bytes.Length == 1 && token.Utf8Bytes.Span[0] == (byte)'*')
                {
                    result = MatchSimple(name, nameIndex, pattern, patternIndex + 1, memo);
                    for (var next = nameIndex; !result && next < name.Count; next++)
                    {
                        result = MatchSimple(name, next + 1, pattern, patternIndex + 1, memo);
                    }
                }
                else if (token.Utf8Bytes.Length == 1 && token.Utf8Bytes.Span[0] == (byte)'?')
                {
                    result = nameIndex < name.Count && MatchSimple(name, nameIndex + 1, pattern, patternIndex + 1, memo);
                }
                else if (IsAsciiRune(token, '[') &&
                         nameIndex < name.Count &&
                         TryMatchCharacterClass(name[nameIndex], pattern, patternIndex, out var classCloseIndex, out var classMatches))
                {
                    result = classMatches && MatchSimple(name, nameIndex + 1, pattern, classCloseIndex + 1, memo);
                }
                else
                {
                    result = nameIndex < name.Count &&
                        token.Equals(name[nameIndex]) &&
                        MatchSimple(name, nameIndex + 1, pattern, patternIndex + 1, memo);
                }
            }

            memo[(nameIndex, patternIndex)] = result;
            return result;
        }

        private static bool TryMatchCharacterClass(
            PyString value,
            IReadOnlyList<PyString> pattern,
            int openIndex,
            out int closeIndex,
            out bool matches)
        {
            closeIndex = -1;
            matches = false;

            var contentStart = openIndex + 1;
            if (contentStart >= pattern.Count)
            {
                return false;
            }

            var negated = IsAsciiRune(pattern[contentStart], '!');
            if (negated)
            {
                contentStart++;
            }

            if (contentStart >= pattern.Count)
            {
                return false;
            }

            var searchStart = contentStart;
            if (IsAsciiRune(pattern[searchStart], ']'))
            {
                searchStart++;
            }

            for (var index = searchStart; index < pattern.Count; index++)
            {
                if (IsAsciiRune(pattern[index], ']'))
                {
                    closeIndex = index;
                    break;
                }
            }

            if (closeIndex < 0)
            {
                return false;
            }

            var valueScalar = RuneScalarValue(value);
            var included = CharacterClassIncludes(valueScalar, pattern, contentStart, closeIndex);
            matches = negated ? !included : included;
            return true;
        }

        private static bool CharacterClassIncludes(int valueScalar, IReadOnlyList<PyString> pattern, int start, int closeIndex)
        {
            for (var index = start; index < closeIndex; index++)
            {
                if (index + 2 < closeIndex && IsAsciiRune(pattern[index + 1], '-'))
                {
                    var rangeStart = RuneScalarValue(pattern[index]);
                    var rangeEnd = RuneScalarValue(pattern[index + 2]);
                    if (rangeStart <= rangeEnd && valueScalar >= rangeStart && valueScalar <= rangeEnd)
                    {
                        return true;
                    }

                    index += 2;
                    continue;
                }

                if (RuneScalarValue(pattern[index]) == valueScalar)
                {
                    return true;
                }
            }

            return false;
        }

        private static string TranslatePattern(string pattern)
        {
            var builder = new StringBuilder(pattern.Length + 2);
            builder.Append('^');
            for (var index = 0; index < pattern.Length; index++)
            {
                var ch = pattern[index];
                switch (ch)
                {
                    case '*':
                        builder.Append(".*");
                        break;
                    case '?':
                        builder.Append('.');
                        break;
                    case '[':
                        index = AppendTranslatedCharacterClass(builder, pattern, index);
                        break;
                    default:
                        AppendEscapedRegexLiteral(builder, ch);
                        break;
                }
            }

            builder.Append('$');
            return builder.ToString();
        }

        private static int AppendTranslatedCharacterClass(StringBuilder builder, string pattern, int openIndex)
        {
            var contentStart = openIndex + 1;
            if (contentStart >= pattern.Length)
            {
                builder.Append("\\[");
                return openIndex;
            }

            var negated = pattern[contentStart] == '!';
            if (negated)
            {
                contentStart++;
            }

            if (contentStart >= pattern.Length)
            {
                builder.Append("\\[");
                return openIndex;
            }

            var searchStart = contentStart;
            if (pattern[searchStart] == ']')
            {
                searchStart++;
            }

            var closeIndex = -1;
            for (var index = searchStart; index < pattern.Length; index++)
            {
                if (pattern[index] == ']')
                {
                    closeIndex = index;
                    break;
                }
            }

            if (closeIndex < 0)
            {
                builder.Append("\\[");
                return openIndex;
            }

            var classBuilder = new StringBuilder(closeIndex - contentStart);
            for (var index = contentStart; index < closeIndex; index++)
            {
                if (index + 2 < closeIndex && pattern[index + 1] == '-')
                {
                    if (char.ConvertToUtf32(pattern, index) <= char.ConvertToUtf32(pattern, index + 2))
                    {
                        AppendEscapedRegexClassCharacter(classBuilder, pattern[index], allowRangeHyphen: true);
                        classBuilder.Append('-');
                        AppendEscapedRegexClassCharacter(classBuilder, pattern[index + 2], allowRangeHyphen: true);
                    }

                    index += 2;
                    continue;
                }

                AppendEscapedRegexClassCharacter(classBuilder, pattern[index], allowRangeHyphen: false);
            }

            if (classBuilder.Length == 0)
            {
                builder.Append(negated ? "." : "(?!)");
                return closeIndex;
            }

            builder.Append('[');
            if (negated)
            {
                builder.Append('^');
            }

            builder.Append(classBuilder);
            builder.Append(']');
            return closeIndex;
        }

        private static void AppendEscapedRegexClassCharacter(StringBuilder builder, char ch, bool allowRangeHyphen)
        {
            if (ch is '\\' or ']' or '^' || ch == '-' && !allowRangeHyphen)
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        private static bool IsAsciiRune(PyString value, char ch)
            => value.Utf8Bytes.Length == 1 && value.Utf8Bytes.Span[0] == (byte)ch;

        private static int RuneScalarValue(PyString value)
        {
            var text = value.AsString();
            return char.ConvertToUtf32(text, 0);
        }
    }

}
