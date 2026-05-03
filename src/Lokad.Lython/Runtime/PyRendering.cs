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
                double floating => PyString.FromString(floating.ToString(System.Globalization.CultureInfo.InvariantCulture)),
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
                double floating => PyString.FromString(floating.ToString(System.Globalization.CultureInfo.InvariantCulture)),
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
