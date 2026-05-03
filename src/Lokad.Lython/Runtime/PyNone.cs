using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyNone : IPyTruthyValue, IPyRenderableValue, IPyHashableValue
{
    public static readonly PyNone Instance = new();

    private PyNone()
    {
    }

    public bool IsTruthy() => false;

    public int GetPyHashCode() => 0;

    public PyString RenderPython(PyRenderingContext context) => PyStringOps.NoneLiteral;

    public PyString RenderInterpolated(PyRenderingContext context) => PyStringOps.NoneLiteral;

    public override string ToString() => "None";
}
