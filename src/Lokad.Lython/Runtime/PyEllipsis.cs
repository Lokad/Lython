using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyEllipsis : IPyTruthyValue, IPyRenderableValue, IPyHashableValue
{
    public static readonly PyEllipsis Instance = new();

    private PyEllipsis()
    {
    }

    public bool IsTruthy() => true;

    public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

    public PyString RenderPython(PyRenderingContext context) => PyStringOps.EllipsisLiteral;

    public PyString RenderInterpolated(PyRenderingContext context) => PyStringOps.EllipsisLiteral;

    public override string ToString() => "Ellipsis";
}
