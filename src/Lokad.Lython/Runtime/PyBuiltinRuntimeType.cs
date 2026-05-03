using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyBuiltinRuntimeType : LythonRuntime.ICallable, IPyRenderableValue
{
    private readonly Func<CallArgumentValue[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> _constructor;
    private readonly Func<string, object?>? _memberFactory;

    public PyBuiltinRuntimeType(
        string name,
        Func<CallArgumentValue[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> constructor,
        Func<string, object?>? memberFactory = null)
    {
        Name = name;
        _constructor = constructor;
        _memberFactory = memberFactory;
    }

    public string Name { get; }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => _constructor(arguments, span, context);

    public bool TryGetMember(string memberName, out object value)
    {
        if (memberName == "__name__")
        {
            value = PyString.FromString(Name.Split('.').Last());
            return true;
        }

        if (_memberFactory is not null)
        {
            var produced = _memberFactory(memberName);
            if (produced is not null)
            {
                value = produced;
                return true;
            }
        }

        value = PyNone.Instance;
        return false;
    }

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString($"<class '{Name}'>");
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public override string ToString() => $"<class '{Name}'>";
}
