using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyBoundMethod : IPyRenderableValue, LythonRuntime.ICallable, IPyDynamicAttributes
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

    // Bound methods expose the wrapped __self__/__func__ plus
    // function.__name__/__module__ like CPython bound methods; engine method
    // objects without names stay missing, matching method-wrapper surface
    // (no __module__ there).
    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        if (name is "__name__" or "__qualname__")
        {
            var functionName = _function switch
            {
                PyFunctionBase function => function.Name,
                INamedRuntimeCallable named => named.Name,
                _ => null,
            };

            if (functionName is null)
            {
                value = PyNone.Instance;
                return false;
            }

            value = PyString.FromString(functionName);
            return true;
        }

        if (name == "__self__")
        {
            value = _self;
            return true;
        }

        if (name == "__func__")
        {
            value = _function;
            return true;
        }

        if (name == "__module__")
        {
            if (_function is PyFunctionBase function &&
                function.TryGetModuleName(out var moduleName))
            {
                value = moduleName;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        value = PyNone.Instance;
        return false;
    }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var bound = new CallArgumentValue[arguments.Length + 1];
        bound[0] = CallArgumentValue.Positional(_self);
        Array.Copy(arguments, 0, bound, 1, arguments.Length);
        return _function.Invoke(bound, span, context);
    }

    public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var bound = new CallArgumentValue[arguments.Length + 1];
        bound[0] = CallArgumentValue.Positional(_self);
        Array.Copy(arguments, 0, bound, 1, arguments.Length);
        return _function.InvokeAsync(bound, span, context);
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString($"<bound method {_displayName}>");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public override string ToString() => $"<bound method {_displayName}>";
}
