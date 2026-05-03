using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyBoundMethod : IPyRenderableValue, LythonRuntime.ICallable
{
    private readonly object _self;
    private readonly LythonRuntime.ICallable _function;
    private readonly string _displayName;

    public PyBoundMethod(object self, LythonRuntime.ICallable function)
    {
        _self = self;
        _function = function;
        _displayName = function switch
        {
            PyFunction pyFunction => pyFunction.Name,
            _ => function.ToString() ?? "<callable>"
        };
    }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var bound = new CallArgumentValue[arguments.Length + 1];
        bound[0] = new CallArgumentValue(null, _self);
        Array.Copy(arguments, 0, bound, 1, arguments.Length);
        return _function.Invoke(bound, span, context);
    }

    public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var bound = new CallArgumentValue[arguments.Length + 1];
        bound[0] = new CallArgumentValue(null, _self);
        Array.Copy(arguments, 0, bound, 1, arguments.Length);
        return _function.InvokeAsync(bound, span, context);
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString($"<bound method {_displayName}>");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public override string ToString() => $"<bound method {_displayName}>";
}
