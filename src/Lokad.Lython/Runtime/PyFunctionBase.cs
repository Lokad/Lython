using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal abstract class PyFunctionBase : IPyRenderableValue, IPyBindableCallable, IClassOwnedMember, IPyMutableDynamicAttributes
{
    private readonly FunctionBindingPlan _bindingPlan;
    private readonly LythonRuntime.ExecutionContext _closure;
    private readonly Dictionary<string, object> _metadata = new(StringComparer.Ordinal);
    private readonly ScopeDirectiveFacts _scopeFacts;

    protected PyFunctionBase(
        string name,
        IReadOnlyList<LoweredFunctionParameter> parameters,
        LythonRuntime.ExecutionContext closure,
        Dictionary<string, object> defaultValues,
        ScopeDirectiveFacts scopeFacts)
    {
        Name = name;
        _closure = closure;
        _bindingPlan = new FunctionBindingPlan(name, PythonCallableKind.Function, parameters, defaultValues);
        _scopeFacts = scopeFacts;
    }

    public string Name { get; }

    public PyType? OwnerType { get; private set; }

    /// <summary>States whether bound arguments must also be materialized in the frame's name dictionary.</summary>
    protected abstract bool RequiresArgumentMirroring { get; }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        var boundArguments = LythonRuntime.BindFunctionArguments(arguments, span, _bindingPlan, context);
        var frame = EnterInvocationFrame(boundArguments, span);
        try
        {
            return ExecuteBody(frame, boundArguments, span);
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
        var frame = EnterInvocationFrame(boundArguments, span);
        try
        {
            return await ExecuteBodyAsync(frame, boundArguments, span).ConfigureAwait(false);
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
            _ => PyNone.Instance,
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

    /// <summary>Executes the function body after argument binding and returns its fall-through result.</summary>
    protected abstract object ExecuteBody(
        LythonRuntime.ExecutionContext frame,
        IReadOnlyDictionary<string, object> boundArguments,
        LythonSourceSpan span);

    /// <summary>Executes the function body asynchronously with the same return and exception semantics as <see cref="ExecuteBody"/>.</summary>
    protected virtual ValueTask<object> ExecuteBodyAsync(
        LythonRuntime.ExecutionContext frame,
        IReadOnlyDictionary<string, object> boundArguments,
        LythonSourceSpan span)
        => ValueTask.FromResult(ExecuteBody(frame, boundArguments, span));

    private LythonRuntime.ExecutionContext EnterInvocationFrame(
        IReadOnlyDictionary<string, object> boundArguments,
        LythonSourceSpan span)
        => PyFunctionBinding.EnterInvocationFrame(
            _closure,
            _scopeFacts,
            _bindingPlan,
            boundArguments,
            RequiresArgumentMirroring,
            OwnerType,
            span);
}
