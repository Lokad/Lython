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

    // Bound methods mirror the wrapped callable.__name__/__qualname__/__module__
    // like CPython (including user-assigned overrides), alongside the bound
    // __self__/__func__ pair; engine objects without member handling stay
    // missing, matching method-wrapper surface (no __module__ there).
    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        if (name is "__name__" or "__qualname__" or "__module__" &&
            _function is IPyDynamicAttributes attributes &&
            attributes.TryGetMember(name, out value))
        {
            return true;
        }

        // Engine slot-method objects carry names but no member handling; keep
        // reporting them like before.
        if (name is "__name__" or "__qualname__" &&
            _function is INamedRuntimeCallable named)
        {
            value = PyString.FromString(named.Name);
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
