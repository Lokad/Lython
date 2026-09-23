using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class StringMembers
    {
        private sealed class StringLayoutSearchMemberProvider : IStringMemberProvider
        {
            // N17: hot fixed signatures hoisted per family.
            private static readonly LythonCallableSignature StrJoinSignature = LythonCallableSignature.Create("str.join", ["iterable"]);
            private static readonly LythonCallableSignature StrZfillSignature = LythonCallableSignature.Create("str.zfill", ["width"]);
        private static readonly LythonCallableSignature StrCenterSignature = LythonCallableSignature.Create("str.center", ["width", "fillchar"], 1);
        private static readonly LythonCallableSignature StrLjustSignature = LythonCallableSignature.Create("str.ljust", ["width", "fillchar"], 1);
        private static readonly LythonCallableSignature StrRjustSignature = LythonCallableSignature.Create("str.rjust", ["width", "fillchar"], 1);
        private static readonly LythonCallableSignature StrFindSignature = LythonCallableSignature.Create("str.find", ["sub", "start", "end"], 1);
        private static readonly LythonCallableSignature StrIndexSignature = LythonCallableSignature.Create("str.index", ["sub", "start", "end"], 1);
        private static readonly LythonCallableSignature StrRfindSignature = LythonCallableSignature.Create("str.rfind", ["sub", "start", "end"], 1);
        private static readonly LythonCallableSignature StrRindexSignature = LythonCallableSignature.Create("str.rindex", ["sub", "start", "end"], 1);
        private static readonly LythonCallableSignature StrCountSignature = LythonCallableSignature.Create("str.count", ["sub", "start", "end"], 1);
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
                    }, StrJoinSignature, async (arguments, span, context) =>
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
                    }),
                    "center" => CreatePaddingMethod("center", PyStringOps.Center, StrCenterSignature),
                    "ljust" => CreatePaddingMethod("ljust", PyStringOps.LJust, StrLjustSignature),
                    "rjust" => CreatePaddingMethod("rjust", PyStringOps.RJust, StrRjustSignature),
                    "zfill" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1)
                        {
                            throw new LythonRuntimeException("TypeError", "str.zfill(width) expects one integer width argument.", span);
                        }

                        var width = ParseStringOptionalInt(arguments[0], "width", "str.zfill(width)", span, context);
                        return OwnMethodResult(PyStringOps.ZFill(text, width), text, context.MemoryGovernor, span, context.Services.State.CallTemporaries);
                    }, StrZfillSignature),
                    "find" => CreateSearchMethod("find", PyStringOps.Find, throwWhenMissing: false, StrFindSignature),
                    "index" => CreateSearchMethod("index", PyStringOps.Find, throwWhenMissing: true, StrIndexSignature),
                    "rfind" => CreateSearchMethod("rfind", PyStringOps.RFind, throwWhenMissing: false, StrRfindSignature),
                    "rindex" => CreateSearchMethod("rindex", PyStringOps.RFind, throwWhenMissing: true, StrRindexSignature),
                    "count" => CreateSearchMethod("count", PyStringOps.Count, throwWhenMissing: false, StrCountSignature),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);

                BoundCallable CreatePaddingMethod(string methodName, Func<PyString, int, PyString?, PyString> operation, LythonCallableSignature signature)
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
                    }, signature);

                BoundCallable CreateSearchMethod(
                    string methodName,
                    Func<PyString, PyString, int, int, BigInteger> operation,
                    bool throwWhenMissing,
                    LythonCallableSignature signature)
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
                    }, signature);
            }
        }
    }
}
