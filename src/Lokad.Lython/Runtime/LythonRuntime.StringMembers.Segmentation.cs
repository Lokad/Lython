using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class StringMembers
    {
        private sealed class StringSegmentationMemberProvider : IStringMemberProvider
        {
            public static readonly StringSegmentationMemberProvider Instance = new();

            public bool TryGetMember(PyString text, string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "split" => BoundCallable.Create((arguments, span, context) =>
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
                    "rsplit" => BoundCallable.Create((arguments, span, context) =>
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
                    "splitlines" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length > 1)
                        {
                            throw new LythonRuntimeException("TypeError", "str.splitlines([keepends]) expects zero or one bool argument.", span);
                        }

                        var keepEnds = arguments.Length == 1 && IsTruthy(arguments[0]);
                        return PyStringOps.SplitLines(text, keepEnds, context.MemoryGovernor, span);
                    }, LythonCallableSignature.Create("str.splitlines", ["keepends"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1)),
                    "expandtabs" => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length > 1)
                        {
                            throw new LythonRuntimeException("TypeError", "str.expandtabs([tabsize]) expects zero or one integer argument.", span);
                        }

                        var tabSize = arguments.Length == 1 ? ParseStringOptionalInt(arguments[0], "tabsize", "str.expandtabs([tabsize])", span) : 8;
                        return PyStringOps.ExpandTabs(text, tabSize);
                    }, "str.expandtabs", ["tabsize"], 0),
                    "strip" => CreateStripMethod(text, name, PyStringOps.Strip),
                    "lstrip" => CreateStripMethod(text, name, PyStringOps.LStrip),
                    "rstrip" => CreateStripMethod(text, name, PyStringOps.RStrip),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);

                static BoundCallable CreateStripMethod(PyString target, string methodName, Func<PyString, PyString> whitespaceOperation)
                    => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length > 1)
                        {
                            throw new LythonRuntimeException("TypeError", $"str.{methodName}([chars]) expects zero or one string argument.", span);
                        }

                        if (arguments.Length == 0 || ReferenceEquals(arguments[0], PyNone.Instance))
                        {
                            return whitespaceOperation(target);
                        }

                        if (!PyStringOps.TryAsString(arguments[0], out var chars))
                        {
                            throw new LythonRuntimeException("TypeError", $"str.{methodName}([chars]) expects zero or one string argument.", span);
                        }

                        return methodName switch
                        {
                            "strip" => PyStringOps.Strip(target, chars),
                            "lstrip" => PyStringOps.LStrip(target, chars),
                            _ => PyStringOps.RStrip(target, chars),
                        };
                    }, $"str.{methodName}", ["chars"], 0);
            }
        }
    }
}
