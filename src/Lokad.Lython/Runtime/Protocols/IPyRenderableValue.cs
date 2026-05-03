using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal interface IPyRenderableValue
{
    PyString RenderPython(PyRenderingContext context);

    PyString RenderInterpolated(PyRenderingContext context);
}
