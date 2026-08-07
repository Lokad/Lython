using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyFunction : IPyRenderableValue, IPyBindableCallable, IClassOwnedMember, IPyDynamicAttributes
{
    private readonly FunctionBindingPlan _bindingPlan;
    private readonly IReadOnlyList<LoweredStatement> _body;
    private readonly LythonRuntime.ExecutionContext _closure;
    private readonly ScopeDirectiveFacts _scopeFacts;
    private readonly Dictionary<string, object> _metadata = new(StringComparer.Ordinal);

    public PyFunction(
        string name,
        IReadOnlyList<LoweredFunctionParameter> parameters,
        IReadOnlyList<LoweredStatement> body,
        LythonRuntime.ExecutionContext closure,
        Dictionary<string, object> defaultValues,
        ScopeDirectiveFacts scopeFacts)
    {
        Name = name;
        _body = body;
        _closure = closure;
        _bindingPlan = new FunctionBindingPlan(name, PythonCallableKind.Function, parameters, defaultValues);
        _scopeFacts = scopeFacts;
    }

    public string Name { get; }

    public PyType? OwnerType { get; private set; }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        var boundArguments = LythonRuntime.BindFunctionArguments(arguments, span, _bindingPlan, context);

        var frame = PyFunctionBinding.EnterInvocationFrame(
            _closure,
            _scopeFacts,
            _bindingPlan,
            boundArguments,
            mirrorBoundArguments: true,
            OwnerType,
            span);
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
            PyFunctionBinding.AnnotateException(ex, frame, Name, span);
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
        var boundArguments = LythonRuntime.BindFunctionArguments(arguments, span, _bindingPlan, context);

        var frame = PyFunctionBinding.EnterInvocationFrame(
            _closure,
            _scopeFacts,
            _bindingPlan,
            boundArguments,
            mirrorBoundArguments: true,
            OwnerType,
            span);
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
            PyFunctionBinding.AnnotateException(ex, frame, Name, span);
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
