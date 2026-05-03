using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyPath : IPyTruthyValue, IPyHashableValue, IPyRenderableValue
{
    public PyPath(PyString value)
    {
        Value = value;
    }

    public PyString Value { get; }

    public bool IsTruthy() => Value.Length != 0;

    public int GetPyHashCode() => Value.GetPyHashCode();

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return Value;
    }

    public PyString RenderInterpolated(PyRenderingContext context)
    {
        _ = context;
        return Value;
    }

    public override string ToString() => Value.AsString();
}
