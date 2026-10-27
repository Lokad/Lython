using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

// A declaration is metadata plus the ordinary dict constructor, with no
// instance representation, field binder or runtime schema enforcement.
internal sealed class PyTypedDictType : LythonRuntime.ICallable, IPyDynamicAttributes, IPyRenderableValue, INamedRuntimeCallable
{
    private readonly PyString _name;
    private readonly PyDict _annotations;
    private readonly bool _total;
    internal object ModuleName { get; set; }
    internal object Docstring { get; set; } = PyNone.Instance;
    public string Name { get; }

    internal PyTypedDictType(PyString name, PyDict annotations, bool total,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var bytes = 128L + 2L * name.Utf8Bytes.Length;
        using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
        context.CheckExecution(span);
        _name = name;
        Name = name.AsString();
        _annotations = annotations;
        _total = total;
        ModuleName = PyFunctionBase.TryGetModuleName(context, out var module) ? module : PyNone.Instance;
        storage.Commit(bytes);
    }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        return LythonRuntime.Dict(arguments, span, context);
    }

    public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        return LythonRuntime.DictAsync(arguments, span, context);
    }

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        value = name switch
        {
            "__name__" or "__qualname__" => _name,
            "__module__" => ModuleName,
            "__doc__" => Docstring,
            "__annotations__" => _annotations,
            "__total__" => _total,
            "__required_keys__" or "__optional_keys__" => throw new LythonRuntimeException(
                "NotImplementedError", "TypedDict immutable key-set inspection is not supported in Lython.", null),
            _ => MissingMemberValue.Instance,
        };
        return !ReferenceEquals(value, MissingMemberValue.Instance);
    }

    public PyString RenderPython(PyRenderingContext context)
    {
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor);
        try
        {
            builder.AppendAscii("<class '");
            builder.Append(_name);
            builder.AppendAscii("'>");
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}
