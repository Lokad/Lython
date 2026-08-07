using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyFunction : IPyRenderableValue, IPyBindableCallable, IClassOwnedMember, IPyDynamicAttributes
{
    private readonly IReadOnlyList<LoweredFunctionParameter> _parameters;
    private readonly IReadOnlyList<LoweredStatement> _body;
    private readonly LythonRuntime.ExecutionContext _closure;
    private readonly Dictionary<string, object> _defaultValues;
    private readonly ScopeDirectiveFacts _scopeFacts;
    private readonly Dictionary<string, object> _metadata = new(StringComparer.Ordinal);

    public PyFunction(string name, IReadOnlyList<LoweredFunctionParameter> parameters, IReadOnlyList<LoweredStatement> body, LythonRuntime.ExecutionContext closure, Dictionary<string, object> defaultValues) : this(name, parameters, body, closure, defaultValues, null) { }

    public PyFunction(
        string name,
        IReadOnlyList<LoweredFunctionParameter> parameters,
        IReadOnlyList<LoweredStatement> body,
        LythonRuntime.ExecutionContext closure,
        Dictionary<string, object> defaultValues,
        ScopeDirectiveFacts? scopeFacts)
    {
        Name = name;
        _parameters = parameters;
        _body = body;
        _closure = closure;
        _defaultValues = defaultValues;
        _scopeFacts = scopeFacts ?? ScopeDirectiveFacts.Empty;
    }

    public string Name { get; }

    public PyType? OwnerType { get; private set; }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        var boundArguments = LythonRuntime.BindFunctionArguments(arguments, span, Name, "Function", _parameters, _defaultValues, context);

        var frame = new LythonRuntime.ExecutionContext(_closure, _scopeFacts);
        foreach (var pair in boundArguments)
        {
            frame.Variables[pair.Key] = pair.Value;
        }

        if (TryBuildImplicitSuperContext(boundArguments, out var anchorType, out var receiver))
        {
            frame.BindImplicitSuper(anchorType, receiver);
        }

        frame.EnterFunctionCall(span);
        try
        {
            var signal = LythonRuntime.ExecuteStatements(_body, frame);
            if (signal is LythonRuntime.BreakSignal or LythonRuntime.ContinueSignal)
            {
                throw new LythonRuntimeException("RuntimeError", "Loop control cannot escape a function body.", span);
            }

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

    public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        var boundArguments = LythonRuntime.BindFunctionArguments(arguments, span, Name, "Function", _parameters, _defaultValues, context);

        var frame = new LythonRuntime.ExecutionContext(_closure, _scopeFacts);
        foreach (var pair in boundArguments)
        {
            frame.Variables[pair.Key] = pair.Value;
        }

        if (TryBuildImplicitSuperContext(boundArguments, out var anchorType, out var receiver))
        {
            frame.BindImplicitSuper(anchorType, receiver);
        }

        frame.EnterFunctionCall(span);
        try
        {
            var signal = await LythonRuntime.ExecuteStatementsAsync(_body, frame).ConfigureAwait(false);
            if (signal is LythonRuntime.BreakSignal or LythonRuntime.ContinueSignal)
            {
                throw new LythonRuntimeException("RuntimeError", "Loop control cannot escape a function body.", span);
            }

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

    private bool TryBuildImplicitSuperContext(Dictionary<string, object> boundArguments, [MaybeNullWhen(false)] out PyType anchorType, [MaybeNullWhen(false)] out object receiver)
    {
        if (OwnerType is null || _parameters.Count == 0)
        {
            anchorType = null;
            receiver = null;
            return false;
        }

        var firstParameterName = _parameters[0].Name;
        if (!boundArguments.TryGetValue(firstParameterName, out receiver))
        {
            anchorType = null;
            receiver = null;
            return false;
        }

        switch (receiver)
        {
            case PyInstance instance when instance.Type.IsSubtypeOf(OwnerType):
                anchorType = OwnerType;
                return true;
            case PyType type when type.IsSubtypeOf(OwnerType):
                anchorType = OwnerType;
                return true;
            default:
                anchorType = null;
                receiver = null;
                return false;
        }
    }
}
