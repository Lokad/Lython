using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyNotImplemented : IPyTruthyValue, IPyRenderableValue, IPyHashableValue
{
    public static readonly PyNotImplemented Instance = new();

    private PyNotImplemented()
    {
    }

    public bool IsTruthy() => true;

    public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

    public PyString RenderPython(PyRenderingContext context) => PyStringOps.NotImplementedLiteral;

    public PyString RenderInterpolated(PyRenderingContext context) => PyStringOps.NotImplementedLiteral;

    public override string ToString() => "NotImplemented";
}
