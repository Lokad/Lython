using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyExecutableFunction : IPyRenderableValue, IPyBindableCallable, IClassOwnedMember, IPyDynamicAttributes
{
    private readonly IReadOnlyList<LoweredFunctionParameter> _parameters;
    private readonly ExecutableCodeObject _codeObject;
    private readonly LythonRuntime.ExecutionContext _closure;
    private readonly IReadOnlyList<LythonRuntime.ExecutableCell> _closureCells;
    private readonly Dictionary<string, object> _defaultValues;
    private readonly ScopeDirectiveFacts _scopeFacts;
    private readonly Dictionary<string, object> _metadata = new(StringComparer.Ordinal);

    public PyExecutableFunction(
        string name,
        IReadOnlyList<LoweredFunctionParameter> parameters,
        ExecutableCodeObject codeObject,
        LythonRuntime.ExecutionContext closure,
        IReadOnlyList<LythonRuntime.ExecutableCell> closureCells,
        Dictionary<string, object> defaultValues,
        ScopeDirectiveFacts scopeFacts)
    {
        Name = name;
        _parameters = parameters;
        _codeObject = codeObject;
        _closure = closure;
        _closureCells = closureCells;
        _defaultValues = defaultValues;
        _scopeFacts = scopeFacts;
    }

    public string Name { get; }

    public PyType? OwnerType { get; private set; }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        var boundArguments = LythonRuntime.BindFunctionArguments(arguments, span, Name, "Function", _parameters, _defaultValues, context);

        var frame = new LythonRuntime.ExecutionContext(_closure, _scopeFacts);
        if (_codeObject.RequiresLocalVariableMirroring)
        {
            foreach (var pair in boundArguments)
            {
                frame.Variables[pair.Key] = pair.Value;
            }
        }

        if (PyFunctionBinding.TryBuildImplicitSuperContext(OwnerType, _parameters, boundArguments, out var anchorType, out var receiver))
        {
            frame.BindImplicitSuper(anchorType, receiver);
        }

        frame.EnterFunctionCall(span);
        try
        {
            LythonRuntime.ExecuteExecutableCodeObject(_codeObject, frame, boundArguments, _closureCells);
            return PyNone.Instance;
        }
        catch (LythonRuntime.ReturnSignal signal)
        {
            return signal.Value;
        }
        catch (LythonRuntimeException ex)
        {
            ex.SetSourcePathIfMissing(frame.SourcePath);
            ex.AddFrame(Name, span, frame.SourcePath);
            throw;
        }
        finally
        {
            frame.LeaveFunctionCall();
        }
    }

    public object Bind(object self) => new PyBoundMethod(self, this);

    public void BindOwner(PyType owner) => OwnerType ??= owner;

    public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
        => instance is null ? this : Bind(instance);

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        if (_metadata.TryGetValue(name, out value))
        {
            return true;
        }

        value = name switch
        {
            "__name__" => PyString.FromString(Name),
            "__qualname__" => PyString.FromString(Name),
            _ => PyNone.Instance
        };
        return !ReferenceEquals(value, PyNone.Instance);
    }

    public bool TrySetMember(string name, object value)
    {
        _metadata[name] = value;
        return true;
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString($"<function {Name}>");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public override string ToString() => $"<function {Name}>";

}
