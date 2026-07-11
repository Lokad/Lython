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
                BigInteger integer => PyString.FromString(integer.ToString()),
                double floating => PyString.FromString(Numbers.PyNumberOps.RenderFloat(floating)),
                LythonRuntime.DictKeysView view => JoinRenderedSequence("dict_keys([", new RenderedSequence(view, context, interpolated: true), "])", context),
                LythonRuntime.DictValuesView view => JoinRenderedSequence("dict_values([", new RenderedSequence(view, context, interpolated: true), "])", context),
                LythonRuntime.DictItemsView view => JoinRenderedSequence("dict_items([", new RenderedSequence(view, context, interpolated: true), "])", context),
                PyException exception => PyString.FromString($"{exception.TypeName}({exception.Message})"),
                LythonRuntime.ReMatchObject match => match.Value,
                LythonRuntime.ExecutionContext.TextFileHandle => PyString.FromString("<file>"),
                _ => PyString.FromString(value.ToString() ?? string.Empty)
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
                BigInteger integer => PyString.FromString(integer.ToString()),
                double floating => PyString.FromString(Numbers.PyNumberOps.RenderFloat(floating)),
                LythonRuntime.DictKeysView view => JoinRenderedSequence("dict_keys([", new RenderedSequence(view, context, interpolated: false), "])", context),
                LythonRuntime.DictValuesView view => JoinRenderedSequence("dict_values([", new RenderedSequence(view, context, interpolated: false), "])", context),
                LythonRuntime.DictItemsView view => JoinRenderedSequence("dict_items([", new RenderedSequence(view, context, interpolated: false), "])", context),
                PyException exception => PyString.FromString($"{exception.TypeName}({exception.Message})"),
                LythonRuntime.ReMatchObject match => match.Value,
                LythonRuntime.ExecutionContext.TextFileHandle => PyString.FromString("<file>"),
                _ => PyString.FromString(value.ToString() ?? string.Empty)
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

    public static PyString JoinRenderedSequence(string prefix, IEnumerable<PyString> items, string suffix)
    {
        var builder = new Utf8ValueBuilder();
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
        return builder.ToPyString();
    }

    public static PyString JoinRenderedSequence(string prefix, IEnumerable<PyString> items, string suffix, PyRenderingContext context)
    {
        var builder = new Utf8ValueBuilder(context.Context.MemoryGovernor);
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
        return builder.ToPyString();
    }

    public static PyString RenderSingletonTuple(PyString item)
    {
        var builder = new Utf8ValueBuilder();
        builder.AppendAscii("(");
        builder.Append(item);
        builder.Append(PyStringOps.CommaLiteral);
        builder.AppendAscii(")");
        return builder.ToPyString();
    }

    public static PyString JoinRenderedDictionary(PyDict dict, PyRenderingContext context, bool interpolated)
    {
        var builder = new Utf8ValueBuilder(context.Context.MemoryGovernor);
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
        return builder.ToPyString();
    }

    public static PyString RenderDictionaryKey(object key, PyRenderingContext context, bool interpolated)
    {
        if (key is PyString text)
        {
            return JoinRenderedSequence("'", [text], "'", context);
        }

        return interpolated ? ToInterpolatedPyString(key, context) : ToPythonPyString(key, context);
    }

    public static PyString RenderSet(PySet set, PyRenderingContext context, bool interpolated)
    {
        if (set.Count == 0)
        {
            return EmptySetLiteral;
        }

        var rendered = new List<PyString>(set.Count);
        foreach (var item in set)
        {
            rendered.Add(interpolated ? ToInterpolatedPyString(item, context) : ToPythonPyString(item, context));
        }

        rendered.Sort(PyStringOrdinalComparer.Instance);
        return JoinRenderedSequence(
            "{",
            rendered,
            "}",
            context);
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
            BigInteger integer => PyString.FromString(integer.ToString()),
            double floating => PyString.FromString(Numbers.PyNumberOps.RenderFloat(floating)),
            LythonRuntime.DictKeysView view => RenderReprSequence(view, "dict_keys([", "])", "dict_keys([...])", context, activeContainers),
            LythonRuntime.DictValuesView view => RenderReprSequence(view, "dict_values([", "])", "dict_values([...])", context, activeContainers),
            LythonRuntime.DictItemsView view => RenderReprSequence(view, "dict_items([", "])", "dict_items([...])", context, activeContainers),
            PyException exception => RenderExceptionRepr(exception, context),
            IPyRenderableValue renderable => renderable.RenderPython(context),
            _ => PyString.FromString(value.ToString() ?? string.Empty)
        };
    }

    private static PyString RenderStringLiteral(string text, PyRenderingContext context)
    {
        var builder = new Utf8ValueBuilder(context.Context.MemoryGovernor);
        builder.AppendAscii("'");
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            switch (ch)
            {
                case '\\':
                    builder.AppendAscii("\\\\");
                    break;
                case '\'':
                    builder.AppendAscii("\\'");
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

        builder.AppendAscii("'");
        return builder.ToPyString();
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
                ? RenderSingletonTuple(ToReprPyStringCore(tuple[0], context, activeContainers))
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
        var builder = new Utf8ValueBuilder(context.Context.MemoryGovernor);
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
        return builder.ToPyString();
    }

    private static PyString RenderReprDictionary(PyDict dict, PyRenderingContext context, HashSet<object> activeContainers)
    {
        if (!activeContainers.Add(dict))
        {
            return PyString.FromString("{...}");
        }

        try
        {
            var builder = new Utf8ValueBuilder(context.Context.MemoryGovernor);
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
            return builder.ToPyString();
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
        var builder = new Utf8ValueBuilder(context.Context.MemoryGovernor);
        builder.AppendString(exception.TypeName);
        builder.AppendAscii("(");
        builder.Append(RenderStringLiteral(exception.Message, context));
        builder.AppendAscii(")");
        return builder.ToPyString();
    }

    private sealed class RenderedSequence : IEnumerable<PyString>
    {
        private readonly IEnumerable<object> _items;
        private readonly PyRenderingContext _context;
        private readonly bool _interpolated;

        public RenderedSequence(IEnumerable<object> items, PyRenderingContext context, bool interpolated)
        {
            _items = items;
            _context = context;
            _interpolated = interpolated;
        }

        public IEnumerator<PyString> GetEnumerator()
        {
            foreach (var item in _items)
            {
                yield return _interpolated ? ToInterpolatedPyString(item, _context) : ToPythonPyString(item, _context);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
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
