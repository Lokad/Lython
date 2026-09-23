using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class StringMembers
    {
        private sealed class StringSegmentationMemberProvider : IStringMemberProvider
        {
            // N17: hot fixed signatures hoisted per family.
        private static readonly LythonCallableSignature StrStripSignature = LythonCallableSignature.Create("str.strip", ["chars"], 0);
        private static readonly LythonCallableSignature StrLstripSignature = LythonCallableSignature.Create("str.lstrip", ["chars"], 0);
        private static readonly LythonCallableSignature StrRstripSignature = LythonCallableSignature.Create("str.rstrip", ["chars"], 0);
            private static readonly LythonCallableSignature StrSplitSignature = LythonCallableSignature.Create("str.split", ["sep", "maxsplit"], 0);
            private static readonly LythonCallableSignature StrRSplitSignature = LythonCallableSignature.Create("str.rsplit", ["sep", "maxsplit"], 0);
            private static readonly LythonCallableSignature StrExpandTabsSignature = LythonCallableSignature.Create("str.expandtabs", ["tabsize"], 0);
            public static readonly StringSegmentationMemberProvider Instance = new();

            public bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "split" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length == 0)
                        {
                            return OwnSplitListResult(PyStringOps.SplitWhitespace(text, context.MemoryGovernor, span), span, context.Services.State.CallTemporaries);
                        }

                        int maxSplit;
                        if (arguments[0] is PyNone)
                        {
                            maxSplit = arguments.Length == 2 ? ParseStringOptionalInt(arguments[1], "maxsplit", "str.split([sep[, maxsplit]])", span, context) : -1;
                            return OwnSplitListResult(PyStringOps.SplitWhitespace(text, maxSplit, context.MemoryGovernor, span), span, context.Services.State.CallTemporaries);
                        }

                        if (arguments.Length > 2)
                        {
                            throw new LythonRuntimeException("TypeError", "str.split([sep[, maxsplit]]) expects zero, one, or two arguments with string separator and optional integer maxsplit.", span);
                        }

                        if (!PyStringOps.TryAsString(arguments[0], out var separator))
                        {
                            throw new LythonRuntimeException("TypeError", "must be str or None, not " + RuntimeErrors.OperandTypeName(arguments[0]), span);
                        }

                        maxSplit = arguments.Length == 2 ? ParseStringOptionalInt(arguments[1], "maxsplit", "str.split([sep[, maxsplit]])", span, context) : -1;
                        try
                        {
                            return OwnSplitListResult(PyStringOps.Split(text, separator, maxSplit, context.MemoryGovernor, span), span, context.Services.State.CallTemporaries);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("ValueError", ex.Message, span);
                        }
                    }, StrSplitSignature),
                    "rsplit" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length == 0)
                        {
                            return OwnSplitListResult(PyStringOps.RSplitWhitespace(text, -1, context.MemoryGovernor, span), span, context.Services.State.CallTemporaries);
                        }

                        int maxSplit;
                        if (arguments[0] is PyNone)
                        {
                            maxSplit = arguments.Length == 2 ? ParseStringOptionalInt(arguments[1], "maxsplit", "str.rsplit([sep[, maxsplit]])", span, context) : -1;
                            return OwnSplitListResult(PyStringOps.RSplitWhitespace(text, maxSplit, context.MemoryGovernor, span), span, context.Services.State.CallTemporaries);
                        }

                        if (arguments.Length > 2)
                        {
                            throw new LythonRuntimeException("TypeError", "str.rsplit([sep[, maxsplit]]) expects zero, one, or two arguments with string separator and optional integer maxsplit.", span);
                        }

                        if (!PyStringOps.TryAsString(arguments[0], out var separator))
                        {
                            throw new LythonRuntimeException("TypeError", "must be str or None, not " + RuntimeErrors.OperandTypeName(arguments[0]), span);
                        }

                        maxSplit = arguments.Length == 2 ? ParseStringOptionalInt(arguments[1], "maxsplit", "str.rsplit([sep[, maxsplit]])", span, context) : -1;
                        try
                        {
                            return OwnSplitListResult(PyStringOps.RSplit(text, separator, maxSplit, context.MemoryGovernor, span), span, context.Services.State.CallTemporaries);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("ValueError", ex.Message, span);
                        }
                    }, StrRSplitSignature),
                    "splitlines" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length > 1)
                        {
                            throw new LythonRuntimeException("TypeError", "str.splitlines([keepends]) expects zero or one bool argument.", span);
                        }

                        var keepEnds = arguments.Length == 1 && IsTruthy(arguments[0]);
                        return OwnSplitListResult(PyStringOps.SplitLines(text, keepEnds, context.MemoryGovernor, span), span, context.Services.State.CallTemporaries);
                    }, LythonCallableSignature.Create("str.splitlines", ["keepends"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 0)),
                    "expandtabs" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length > 1)
                        {
                            throw new LythonRuntimeException("TypeError", "str.expandtabs([tabsize]) expects zero or one integer argument.", span);
                        }

                        var tabSize = arguments.Length == 1 ? ParseStringOptionalInt(arguments[0], "tabsize", "str.expandtabs([tabsize])", span, context) : 8;
                        return OwnMethodResult(PyStringOps.ExpandTabs(text, tabSize), text, context.MemoryGovernor, span, context.Services.State.CallTemporaries);
                    }, StrExpandTabsSignature),
                    "strip" => CreateStripMethod(text, name, PyStringOps.Strip, StrStripSignature),
                    "lstrip" => CreateStripMethod(text, name, PyStringOps.LStrip, StrLstripSignature),
                    "rstrip" => CreateStripMethod(text, name, PyStringOps.RStrip, StrRstripSignature),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);

                static BoundCallable CreateStripMethod(PyString target, string methodName, Func<PyString, PyString> whitespaceOperation, LythonCallableSignature signature)
                    => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length > 1)
                        {
                            throw new LythonRuntimeException("TypeError", $"str.{methodName}([chars]) expects zero or one string argument.", span);
                        }

                        PyString result;
                        if (arguments.Length == 0 || ReferenceEquals(arguments[0], PyNone.Instance))
                        {
                            result = whitespaceOperation(target);
                        }
                        else if (!PyStringOps.TryAsString(arguments[0], out var chars))
                        {
                            throw new LythonRuntimeException("TypeError", $"{methodName} arg must be None or str", span);
                        }
                        else
                        {
                            result = methodName switch
                            {
                                "strip" => PyStringOps.Strip(target, chars),
                                "lstrip" => PyStringOps.LStrip(target, chars),
                                _ => PyStringOps.RStrip(target, chars),
                            };
                        }

                        return OwnMethodResult(result, target, context.MemoryGovernor, span, context.Services.State.CallTemporaries);
                    }, signature);
            }
        }
    }
}
