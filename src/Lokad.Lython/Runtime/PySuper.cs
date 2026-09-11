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
        // CPython spells the anchor via repr and abbreviates the bound object
        // as <{Type} object>, ignoring custom __repr__ (probed); the anchor
        // follows Lython class-rendering shapes (short, not __main__-qualified).
        return PyString.FromString($"<super: {PyRendering.ToPythonString(AnchorType, context)}, <{BoundType.Name} object>>");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}
