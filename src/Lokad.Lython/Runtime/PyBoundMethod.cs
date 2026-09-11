using Lokad.Lython.Runtime.Calls;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyBoundMethod : IPyRenderableValue, LythonRuntime.ICallable, IPyDynamicAttributes, IPyHashableValue
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

    // Exposes the wrapped callable so class resolution can report slot
    // method-wrapper and bound-builtin types like CPython.
    internal LythonRuntime.ICallable Function => _function;

    public int GetPyHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(_self), RuntimeHelpers.GetHashCode(_function));

    // Bound methods mirror the wrapped callable.__name__/__qualname__/__module__/__objclass__
    // like CPython (including user-assigned overrides), alongside the bound
    // __self__/__func__ pair; engine objects without member handling stay
    // missing, matching method-wrapper surface (no __module__ there).
    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        if (name is "__name__" or "__qualname__" or "__module__" or "__doc__" or "__objclass__" &&
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

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString(DisplayText());

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public override string ToString() => DisplayText();

    private string DisplayText()
    {
        // Bound slot wrappers render like CPython method-wrappers (minus the
        // address suffix), reusing the wrapped slot __name__/__qualname__ so
        // the display stays in sync with the mirrored members; this also keeps
        // CLR type names out of guest-visible output.
        if (_function is IPySlotWrapper &&
            _function is IPyDynamicAttributes attributes &&
            attributes.TryGetMember("__name__", out var rawName) &&
            attributes.TryGetMember("__qualname__", out var rawQualname) &&
            rawName is PyString shortName &&
            rawQualname is PyString qualifiedName)
        {
            var qualified = qualifiedName.AsString();
            var dot = qualified.LastIndexOf(".");
            var owner = dot < 0 ? qualified : qualified.Substring(0, dot);
            return "<method-wrapper '" + shortName.AsString() + "' of " + owner + " object>";
        }

        return $"<bound method {_displayName}>";
    }
}
