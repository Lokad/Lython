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
                            var index = 0;
                            foreach (var part in ToSequence(arguments[0], span, context))
                            {
                                if (!PyStringOps.TryAsString(part, out var partText))
                                {
                                    throw new LythonRuntimeException("TypeError", "sequence item " + index + ": expected str instance, " + RuntimeErrors.OperandTypeName(part) + " found", span);
                                }

                                yield return partText;
                                index++;
                            }
                        }
                    }, async (arguments, span, context) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "str.join(iterable) expects one argument.", span);
                        }

                        // Async twin of the drain above: parts await each pull
                        // while validation order and messages stay identical.
                        var parts = await PyIteration.MaterializeAsync(arguments[0], span, context).ConfigureAwait(false);
                        return JoinStrings(text, EnumerateParts(parts), context.MemoryGovernor, span);

                        IEnumerable<PyString> EnumerateParts(List<object> parts)
                        {
                            var index = 0;
                            foreach (var part in parts)
                            {
                                if (!PyStringOps.TryAsString(part, out var partText))
                                {
                                    throw new LythonRuntimeException("TypeError", "sequence item " + index + ": expected str instance, " + RuntimeErrors.OperandTypeName(part) + " found", span);
                                }

                                yield return partText;
                                index++;
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

                        var width = ParseStringOptionalInt(arguments[0], "width", "str.zfill(width)", span, context);
                        return OwnMethodResult(PyStringOps.ZFill(text, width), text, context.MemoryGovernor, span, context.Services.State.CallTemporaries);
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

                        var width = ParseStringOptionalInt(arguments[0], "width", signature, span, context);
                        var fill = arguments.Length == 2 ? RequireFillChar(arguments[1], span) : null;
                        try
                        {
                            return OwnMethodResult(operation(text, width, fill), text, context.MemoryGovernor, span, context.Services.State.CallTemporaries);
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
                    => BoundCallable.Create((arguments, span, context) =>
                    {
                        var signature = $"str.{methodName}(sub[, start[, end]])";
                        if (arguments.Length is < 1 or > 3)
                        {
                            throw new LythonRuntimeException("TypeError", $"{signature} expects one string argument plus optional integer bounds.", span);
                        }

                        if (!PyStringOps.TryAsString(arguments[0], out var needle))
                        {
                            // CPython names the offending type, spelling a None needle bare.
                            var needleType = arguments[0] is PyNone ? "None" : RuntimeErrors.OperandTypeName(arguments[0]);
                            throw new LythonRuntimeException("TypeError", $"{methodName}() argument 1 must be str, not {needleType}", span);
                        }

                        var (start, end, _) = ParseStringBounds(text.Length, arguments, span, context, signature);
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
