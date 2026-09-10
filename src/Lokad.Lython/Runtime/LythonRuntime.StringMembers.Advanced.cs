using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class StringMembers
    {
        private sealed class StringLayoutSearchMemberProvider : IStringMemberProvider
        {
            public static readonly StringLayoutSearchMemberProvider Instance = new();

            public bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "join" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "str.join(iterable) expects one argument.", span);
                        }

                        return JoinStrings(text, EnumerateParts(), context.MemoryGovernor, span);

                        IEnumerable<PyString> EnumerateParts()
                        {
                            foreach (var part in ToSequence(arguments[0], span, context))
                            {
                                if (!PyStringOps.TryAsString(part, out var partText))
                                {
                                    throw new LythonRuntimeException("TypeError", "str.join(iterable) expects an iterable of strings.", span);
                                }

                                yield return partText;
                            }
                        }
                    }, "str.join", ["iterable"]),
                    "center" => CreatePaddingMethod("center", PyStringOps.Center),
                    "ljust" => CreatePaddingMethod("ljust", PyStringOps.LJust),
                    "rjust" => CreatePaddingMethod("rjust", PyStringOps.RJust),
                    "zfill" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "str.zfill(width) expects one integer width argument.", span);
                        }

                        var width = ParseStringOptionalInt(arguments[0], "width", "str.zfill(width)", span);
                        return OwnMethodResult(PyStringOps.ZFill(text, width), text, context.MemoryGovernor, span);
                    }, "str.zfill", ["width"]),
                    "find" => CreateSearchMethod("find", PyStringOps.Find, throwWhenMissing: false),
                    "index" => CreateSearchMethod("index", PyStringOps.Find, throwWhenMissing: true),
                    "rfind" => CreateSearchMethod("rfind", PyStringOps.RFind, throwWhenMissing: false),
                    "rindex" => CreateSearchMethod("rindex", PyStringOps.RFind, throwWhenMissing: true),
                    "count" => CreateSearchMethod("count", PyStringOps.Count, throwWhenMissing: false),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);

                BoundCallable CreatePaddingMethod(string methodName, Func<PyString, int, PyString?, PyString> operation)
                    => BoundCallable.Create((arguments, span, context) =>
                    {
                        var signature = $"str.{methodName}(width[, fillchar])";
                        if (arguments.Length is < 1 or > 2)
                        {
                            throw new LythonRuntimeException("TypeError", $"{signature} expects one integer width and an optional fill string.", span);
                        }

                        var width = ParseStringOptionalInt(arguments[0], "width", signature, span);
                        var fill = arguments.Length == 2 ? RequireFillChar(arguments[1], signature, span) : null;
                        try
                        {
                            return OwnMethodResult(operation(text, width, fill), text, context.MemoryGovernor, span);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("TypeError", ex.Message, span);
                        }
                    }, $"str.{methodName}", ["width", "fillchar"], 1);

                BoundCallable CreateSearchMethod(
                    string methodName,
                    Func<PyString, PyString, int, int, BigInteger> operation,
                    bool throwWhenMissing)
                    => BoundCallable.Create((arguments, span, _) =>
                    {
                        var signature = $"str.{methodName}(sub[, start[, end]])";
                        if (arguments.Length is < 1 or > 3 || !PyStringOps.TryAsString(arguments[0], out var needle))
                        {
                            throw new LythonRuntimeException("TypeError", $"{signature} expects one string argument plus optional integer bounds.", span);
                        }

                        var (start, end, _) = ParseStringBounds(text.Length, arguments, span, signature);
                        if (end < start)
                        {
                            if (methodName == "count")
                            {
                                return BigInteger.Zero;
                            }

                            if (throwWhenMissing)
                            {
                                throw new LythonRuntimeException("ValueError", "substring not found", span);
                            }

                            return new BigInteger(-1);
                        }

                        var result = operation(text, needle, start, end);
                        if (throwWhenMissing && result < 0)
                        {
                            throw new LythonRuntimeException("ValueError", "substring not found", span);
                        }

                        return result;
                    }, $"str.{methodName}", ["sub", "start", "end"], 1);
            }
        }
    }
}
