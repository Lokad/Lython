using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal readonly record struct PyRenderingContext(LythonRuntime.ExecutionContext Context);

internal static class PyRendering
{
    private static readonly PyString TrueLiteral = PyString.FromString("True");
    private static readonly PyString FalseLiteral = PyString.FromString("False");
    private static readonly PyString EmptySetLiteral = PyString.FromString("set()");
    private static readonly PyString FileLiteral = PyString.FromString("<file>");
    private static readonly PyString ObjectLiteral = PyString.FromString("<object>");

    // Mapping misses carry their key as the payload; like CPython, the key
    // renders through repr. Constructed values and domain errors without a
    // payload keep the stored message.
    private static PyString RenderExceptionMessage(PyException exception, PyRenderingContext context)
    {
        // Assigned args recompute the message like CPython instead of serving
        // the construction text: empty for no args, str of the single arg
        // (repr for KeyError), repr of the tuple otherwise.
        if (exception.ArgsOverride is not null)
        {
            var overrideArgs = exception.ArgsOverride;
            if (overrideArgs.Count == 0)
            {
                return PyString.FromString(string.Empty, context.Context.MemoryGovernor);
            }

            if (overrideArgs.Count == 1 &&
                string.Equals(exception.TypeName, "KeyError", StringComparison.Ordinal) &&
                exception.Identity.IsBuiltin)
            {
                return ToReprPyString(overrideArgs[0], context);
            }

            if (overrideArgs.Count == 1)
            {
                return ToInterpolatedPyString(overrideArgs[0], context);
            }

            return ToReprPyString(overrideArgs, context);
        }

        if (string.Equals(exception.TypeName, "KeyError", StringComparison.Ordinal) &&
            exception.Identity.IsBuiltin &&
            exception.ExplicitArgs is null &&
            !ReferenceEquals(exception.Value, PyNone.Instance))
        {
            return ToReprPyString(exception.Value, context);
        }

        return PyString.FromString(exception.Message, context.Context.MemoryGovernor);
    }

    public static PyString ToInterpolatedPyString(object value, PyRenderingContext context)
    {
        context.Context.EnterInterpreterFrame(null);
        try
        {
            if (value is IPyRenderableValue renderable)
            {
                return renderable.RenderInterpolated(context);
            }

            return value switch
            {
                PyNone => PyStringOps.NoneLiteral,
                bool boolean => boolean ? TrueLiteral : FalseLiteral,
                BigInteger integer => PyString.FromString(integer.ToString(), context.Context.MemoryGovernor),
                double floating => PyString.FromString(Numbers.PyNumberOps.RenderFloat(floating), context.Context.MemoryGovernor),
                LythonRuntime.DictKeysView view => JoinRenderedReprValues("dict_keys([", view, "])", context),
                LythonRuntime.DictValuesView view => JoinRenderedReprValues("dict_values([", view, "])", context),
                LythonRuntime.DictItemsView view => JoinRenderedReprValues("dict_items([", view, "])", context),
                PyException exception => RenderExceptionMessage(exception, context),
                // Like CPython, str(match) renders the repr form, not the text.
                LythonRuntime.ReMatchObject match => ToReprPyString(match, context),
                LythonRuntime.ExecutionContext.TextFileHandle => FileLiteral,
                _ => RenderOpaqueObject()
            };
        }
        finally
        {
            context.Context.LeaveInterpreterFrame();
        }
    }

    public static PyString ToPythonPyString(object value, PyRenderingContext context)
    {
        context.Context.EnterInterpreterFrame(null);
        try
        {
            if (value is IPyRenderableValue renderable)
            {
                return renderable.RenderPython(context);
            }

            return value switch
            {
                PyNone => PyStringOps.NoneLiteral,
                bool boolean => boolean ? TrueLiteral : FalseLiteral,
                BigInteger integer => PyString.FromString(integer.ToString(), context.Context.MemoryGovernor),
                double floating => PyString.FromString(Numbers.PyNumberOps.RenderFloat(floating), context.Context.MemoryGovernor),
                LythonRuntime.DictKeysView view => JoinRenderedReprValues("dict_keys([", view, "])", context),
                LythonRuntime.DictValuesView view => JoinRenderedReprValues("dict_values([", view, "])", context),
                LythonRuntime.DictItemsView view => JoinRenderedReprValues("dict_items([", view, "])", context),
                PyException exception => RenderExceptionMessage(exception, context),
                // Like CPython, str(match) renders the repr form, not the text.
                LythonRuntime.ReMatchObject match => ToReprPyString(match, context),
                LythonRuntime.ExecutionContext.TextFileHandle => FileLiteral,
                _ => RenderOpaqueObject()
            };
        }
        finally
        {
            context.Context.LeaveInterpreterFrame();
        }
    }

    public static string ToPythonString(object value, PyRenderingContext context) => ToPythonPyString(value, context).AsString();

    public static string ToInterpolatedString(object value, PyRenderingContext context) => ToInterpolatedPyString(value, context).AsString();

    public static PyString ToReprPyString(object value, PyRenderingContext context)
    {
        context.Context.EnterInterpreterFrame(null);
        try
        {
            return ToReprPyStringCore(value, context, new HashSet<object>(ReferenceEqualityComparer.Instance));
        }
        finally
        {
            context.Context.LeaveInterpreterFrame();
        }
    }


    public static PyString JoinRenderedReprValues(
        string prefix,
        IEnumerable<object> values,
        string suffix,
        PyRenderingContext context)
    {
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
        builder.AppendString(prefix);
        var first = true;
        foreach (var value in values)
        {
            if (!first)
            {
                builder.AppendAscii(", ");
            }

            builder.Append(ToReprPyString(value, context));
            first = false;
        }

        builder.AppendString(suffix);
        return builder.ToPyStringAndRelease();
    }

    public static PyString JoinRenderedSequence(string prefix, IEnumerable<PyString> items, string suffix, PyRenderingContext context)
    {
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
        builder.AppendString(prefix);
        var first = true;
        foreach (var item in items)
        {
            if (!first)
            {
                builder.AppendAscii(", ");
            }

            builder.Append(item);
            first = false;
        }

        builder.AppendString(suffix);
        return builder.ToPyStringAndRelease();
    }

    public static PyString JoinRenderedValues(
        string prefix,
        IEnumerable<object> values,
        string suffix,
        PyRenderingContext context,
        bool interpolated)
    {
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
        builder.AppendString(prefix);
        var first = true;
        foreach (var value in values)
        {
            if (!first)
            {
                builder.AppendAscii(", ");
            }

            builder.Append(interpolated ? ToInterpolatedPyString(value, context) : ToPythonPyString(value, context));
            first = false;
        }

        builder.AppendString(suffix);
        return builder.ToPyStringAndRelease();
    }

    public static PyString RenderSingletonTuple(PyString item, MemoryGovernor governor)
    {
        var builder = new GovernedByteBuilder(governor);
        builder.AppendAscii("(");
        builder.Append(item);
        builder.Append(PyStringOps.CommaLiteral);
        builder.AppendAscii(")");
        return builder.ToPyStringAndRelease();
    }

    public static PyString JoinRenderedDictionary(PyDict dict, PyRenderingContext context, bool interpolated)
    {
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
        builder.AppendAscii("{");
        var first = true;
        foreach (var pair in dict)
        {
            if (!first)
            {
                builder.AppendAscii(", ");
            }

            builder.Append(RenderDictionaryKey(pair.Key, context, interpolated));
            builder.AppendAscii(": ");
            builder.Append(interpolated ? ToInterpolatedPyString(pair.Value, context) : ToPythonPyString(pair.Value, context));
            first = false;
        }

        builder.AppendAscii("}");
        return builder.ToPyStringAndRelease();
    }

    public static PyString RenderDictionaryKey(object key, PyRenderingContext context, bool interpolated)
    {
        // String keys render through repr like CPython on the
        // non-interpolated path (both live callers pass false), so quoting
        // and escapes match plain-dict rendering; the interpolated path
        // keeps its historical raw form.
        if (key is PyString text)
        {
            return interpolated
                ? JoinRenderedSequence("'", [text], "'", context)
                : ToReprPyString(text, context);
        }

        return interpolated ? ToInterpolatedPyString(key, context) : ToPythonPyString(key, context);
    }

    private static PyString ToReprPyStringCore(object value, PyRenderingContext context, HashSet<object> activeContainers)
    {
        if (PyStringOps.TryAsString(value, out var text))
        {
            return RenderStringLiteral(text.AsString(), context);
        }

        return value switch
        {
            PyList list => RenderReprSequence(list, "[", "]", "[...]", context, activeContainers),
            PyTuple tuple => RenderReprTuple(tuple, context, activeContainers),
            PyDict dict => RenderReprDictionary(dict, context, activeContainers),
            PySet set => RenderReprSet(set, context, activeContainers),
            PyNone => PyStringOps.NoneLiteral,
            bool boolean => boolean ? TrueLiteral : FalseLiteral,
            BigInteger integer => PyString.FromString(integer.ToString(), context.Context.MemoryGovernor),
            double floating => PyString.FromString(Numbers.PyNumberOps.RenderFloat(floating), context.Context.MemoryGovernor),
            LythonRuntime.DictKeysView view => RenderReprSequence(view, "dict_keys([", "])", "dict_keys([...])", context, activeContainers),
            LythonRuntime.DictValuesView view => RenderReprSequence(view, "dict_values([", "])", "dict_values([...])", context, activeContainers),
            LythonRuntime.DictItemsView view => RenderReprSequence(view, "dict_items([", "])", "dict_items([...])", context, activeContainers),
            PyException exception => RenderExceptionRepr(exception, context),
            LythonRuntime.ReMatchObject match => RenderMatchRepr(match, context, activeContainers),
            IPyRenderableValue renderable => renderable.RenderPython(context),
            _ => RenderOpaqueObject()
        };
    }

    private static PyString RenderOpaqueObject() => ObjectLiteral;

    private static PyString RenderMatchRepr(
        LythonRuntime.ReMatchObject match,
        PyRenderingContext context,
        HashSet<object> activeContainers)
    {
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
        builder.AppendAscii("<re.Match object; span=(");
        builder.AppendString(match.Start.ToString());
        builder.AppendAscii(", ");
        builder.AppendString(match.End.ToString());
        builder.AppendAscii("), match=");
        builder.Append(ToReprPyStringCore(match.Value, context, activeContainers));
        builder.AppendAscii(">");
        return builder.ToPyStringAndRelease();
    }

    private static PyString RenderStringLiteral(string text, PyRenderingContext context)
    {
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
        // Like CPython, a string holding a single quote but no double quote
        // renders wrapped in double quotes with its singles left bare;
        // strings holding both, neither, or only doubles keep single quotes.
        var quote = text.IndexOf((char)39) >= 0 && text.IndexOf((char)34) < 0 ? (char)34 : (char)39;
        builder.Append((byte)quote);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == quote)
            {
                builder.Append((byte)92);
                builder.Append((byte)quote);
                continue;
            }

            switch (ch)
            {
                case '\\':
                    builder.AppendAscii("\\\\");
                    break;
                case '\n':
                    builder.AppendAscii("\\n");
                    break;
                case '\r':
                    builder.AppendAscii("\\r");
                    break;
                case '\t':
                    builder.AppendAscii("\\t");
                    break;
                case '\b':
                    builder.AppendAscii("\\x08");
                    break;
                case '\f':
                    builder.AppendAscii("\\x0c");
                    break;
                default:
                    if (char.IsControl(ch))
                    {
                        builder.AppendString(ch <= 0xff ? $"\\x{(int)ch:x2}" : $"\\u{(int)ch:x4}");
                    }
                    else if (char.IsHighSurrogate(ch) &&
                             i + 1 < text.Length &&
                             char.IsLowSurrogate(text[i + 1]))
                    {
                        builder.AppendString(text.Substring(i, 2));
                        i++;
                    }
                    else
                    {
                        builder.AppendString(ch.ToString());
                    }

                    break;
            }
        }

        builder.Append((byte)quote);
        return builder.ToPyStringAndRelease();
    }

    private static PyString RenderReprTuple(PyTuple tuple, PyRenderingContext context, HashSet<object> activeContainers)
    {
        if (!activeContainers.Add(tuple))
        {
            return PyString.FromString("(...)");
        }

        try
        {
            return tuple.Count == 1
                ? RenderSingletonTuple(ToReprPyStringCore(tuple[0], context, activeContainers), context.Context.MemoryGovernor)
                : RenderReprItems(tuple, "(", ")", context, activeContainers);
        }
        finally
        {
            activeContainers.Remove(tuple);
        }
    }

    private static PyString RenderReprSequence(
        IEnumerable<object> items,
        string prefix,
        string suffix,
        string recursiveLiteral,
        PyRenderingContext context,
        HashSet<object> activeContainers)
    {
        if (!activeContainers.Add(items))
        {
            return PyString.FromString(recursiveLiteral);
        }

        try
        {
            return RenderReprItems(items, prefix, suffix, context, activeContainers);
        }
        finally
        {
            activeContainers.Remove(items);
        }
    }

    private static PyString RenderReprItems(
        IEnumerable<object> items,
        string prefix,
        string suffix,
        PyRenderingContext context,
        HashSet<object> activeContainers)
    {
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
        builder.AppendString(prefix);
        var first = true;
        foreach (var item in items)
        {
            if (!first)
            {
                builder.AppendAscii(", ");
            }

            builder.Append(ToReprPyStringCore(item, context, activeContainers));
            first = false;
        }

        builder.AppendString(suffix);
        return builder.ToPyStringAndRelease();
    }

    private static PyString RenderReprDictionary(PyDict dict, PyRenderingContext context, HashSet<object> activeContainers)
    {
        if (!activeContainers.Add(dict))
        {
            return PyString.FromString("{...}");
        }

        try
        {
            var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
            builder.AppendAscii("{");
            var first = true;
            foreach (var pair in dict)
            {
                if (!first)
                {
                    builder.AppendAscii(", ");
                }

                builder.Append(ToReprPyStringCore(pair.Key, context, activeContainers));
                builder.AppendAscii(": ");
                builder.Append(ToReprPyStringCore(pair.Value, context, activeContainers));
                first = false;
            }

            builder.AppendAscii("}");
            return builder.ToPyStringAndRelease();
        }
        finally
        {
            activeContainers.Remove(dict);
        }
    }

    private static PyString RenderReprSet(PySet set, PyRenderingContext context, HashSet<object> activeContainers)
    {
        if (set.Count == 0)
        {
            return EmptySetLiteral;
        }

        if (!activeContainers.Add(set))
        {
            return PyString.FromString("{...}");
        }

        try
        {
            var rendered = new List<PyString>(set.Count);
            foreach (var item in set)
            {
                rendered.Add(ToReprPyStringCore(item, context, activeContainers));
            }

            rendered.Sort(PyStringOrdinalComparer.Instance);
            return JoinRenderedSequence("{", rendered, "}", context);
        }
        finally
        {
            activeContainers.Remove(set);
        }
    }

    private static PyString RenderExceptionRepr(PyException exception, PyRenderingContext context)
    {
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
        builder.AppendString(exception.TypeName);
        builder.AppendAscii("(");
        // Mirrors the .args fallback: message-carrying internal raises
        // render their lone argument instead of empty parens. The transient
        // string stays ungoverned like the neighboring read so rendering a
        // failure can never mask it with a budget error.
        var args = exception.ArgsOverride ?? exception.ExplicitArgs ?? (
            ReferenceEquals(exception.Value, PyNone.Instance)
                ? (string.IsNullOrEmpty(exception.Message)
                    ? PyTuple.Empty
                    : PyTuple.FromOwnedArray([PyString.FromString(exception.Message)]))
                : PyTuple.FromOwnedArray([exception.Value]));
        for (var i = 0; i < args.Count; i++)
        {
            if (i > 0)
            {
                builder.AppendAscii(", ");
            }

            builder.Append(ToReprPyString(args[i], context));
        }
        builder.AppendAscii(")");
        return builder.ToPyStringAndRelease();
    }

    internal sealed class PyStringOrdinalComparer : IComparer<PyString>
    {
        public static readonly PyStringOrdinalComparer Instance = new();

        public int Compare(PyString? x, PyString? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            return PyString.CompareOrdinal(x, y);
        }
    }
}
