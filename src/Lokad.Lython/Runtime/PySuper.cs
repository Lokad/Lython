using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PySuper : IPyRenderableValue
{
    public PySuper(PyType anchorType, object boundObject, PyType boundType)
    {
        AnchorType = anchorType;
        BoundObject = boundObject;
        BoundType = boundType;
    }

    public PyType AnchorType { get; }

    public object BoundObject { get; }

    public PyType BoundType { get; }

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString($"<super: {BoundType.Name} after {AnchorType.Name}>");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}
