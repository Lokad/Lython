using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyStaticMethod : IPyRenderableValue, IPyBindableCallable, IClassOwnedMember
{
    private readonly LythonRuntime.ICallable _callable;

    public PyStaticMethod(LythonRuntime.ICallable callable)
    {
        _callable = callable;
    }

    public object Bind(object self) => _callable;

    public void BindOwner(PyType owner)
    {
        if (_callable is IClassOwnedMember owned)
        {
            owned.BindOwner(owner);
        }
    }

    public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
        => _callable;

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => _callable.Invoke(arguments, span, context);

    public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => _callable.InvokeAsync(arguments, span, context);

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<staticmethod>");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}

internal sealed class PyClassMethod : IPyRenderableValue, IPyBindableCallable, IClassOwnedMember
{
    private readonly LythonRuntime.ICallable _callable;

    public PyClassMethod(LythonRuntime.ICallable callable)
    {
        _callable = callable;
    }

    public object Bind(object self)
    {
        return self switch
        {
            PyInstance instance => new PyBoundMethod(instance.Type, _callable),
            PyType type => new PyBoundMethod(type, _callable),
            _ => new PyBoundMethod(self, _callable)
        };
    }

    public void BindOwner(PyType owner)
    {
        if (_callable is IClassOwnedMember owned)
        {
            owned.BindOwner(owner);
        }
    }

    public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        return instance switch
        {
            null => new PyBoundMethod(owner, _callable),
            _ => Bind(instance)
        };
    }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => _callable.Invoke(arguments, span, context);

    public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => _callable.InvokeAsync(arguments, span, context);

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<classmethod>");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}
