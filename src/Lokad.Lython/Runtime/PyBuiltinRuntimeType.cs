using Lokad.Lython.Runtime.Text;
using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal sealed class PyBuiltinRuntimeType : LythonRuntime.ICallable, IPyRenderableValue, IPyHashableValue, IPyDynamicAttributes, IPyContextualDynamicAttributes
{
    private readonly Func<CallArgumentValue[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> _constructor;
    private readonly Func<string, object?>? _memberFactory;

    public PyBuiltinRuntimeType(string name, Func<CallArgumentValue[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> constructor) : this(name, constructor, null) { }

    public PyBuiltinRuntimeType(
        string name,
        Func<CallArgumentValue[], LythonSourceSpan, LythonRuntime.ExecutionContext, object> constructor,
        Func<string, object?>? memberFactory)
    {
        Name = name;
        _constructor = constructor;
        _memberFactory = memberFactory;
        _shortName = PyString.FromString(name.Substring(name.LastIndexOf((char)46) + 1));
    }

    public string Name { get; }

    // Short type names are fixed per runtime type object, so reads alias
    // stably like CPython instead of rebuilding per read.
    private readonly PyString _shortName;

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => _constructor(arguments, span, context);

    // Runtime types resolve __module__ through the shared label catalog and
    // __bases__/__mro__ per read: base objects mix global type singletons with
    // the run builtins table, so tuples cannot be shared across runs.
    public bool TryGetMember(string memberName, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        if (memberName == "__module__")
        {
            value = LythonRuntime.ExceptionTypeValue.SharedModuleLabel(ModulePart(Name));
            return true;
        }

        if (memberName == "__bases__" || memberName == "__mro__")
        {
            var bases = GetBaseObjects(Name, context);
            if (bases is null)
            {
                value = PyNone.Instance;
                return false;
            }

            if (memberName == "__bases__")
            {
                value = new PyTuple(bases, context.MemoryGovernor, span);
                return true;
            }

            var mro = new object[bases.Length + 1];
            mro[0] = this;
            Array.Copy(bases, 0, mro, 1, bases.Length);
            value = new PyTuple(mro, context.MemoryGovernor, span);
            return true;
        }

        return TryGetMember(memberName, out value);
    }

    private static string ModulePart(string name)
    {
        var dot = name.LastIndexOf((char)46);
        return dot < 0 ? "builtins" : name.Substring(0, dot);
    }

    private static object[]? GetBaseObjects(string name, LythonRuntime.ExecutionContext context)
    {
        if (string.Equals(name, "datetime.datetime", StringComparison.Ordinal))
        {
            return [PyDateTimeOps.DateType];
        }

        return context.TryGetBuiltin("object", out var obj) && obj is not null ? [obj] : null;
    }

    public bool TryGetMember(string memberName, [MaybeNullWhen(false)] out object value)
    {
        if (memberName == "__name__")
        {
            value = _shortName;
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

    public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

    public override string ToString() => $"<class '{Name}'>";
}
