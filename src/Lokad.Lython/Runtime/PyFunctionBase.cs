using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal abstract class PyFunctionBase : IPyRenderableValue, IPyBindableCallable, IClassOwnedMember, IPyMutableDynamicAttributes
{
    private readonly FunctionBindingPlan _bindingPlan;
    private readonly LythonRuntime.ExecutionContext _closure;
    // Metadata tables grow one CLR entry per guest attribute name; charge
    // each new key so retained attributes accumulate. The key strings
    // themselves are caller-owned; only the table slot is charged here.
    // Functions have no attribute delete path, so nothing is released.
    private const long AttributeSlotBytes = 64;
    private MemoryGovernor? _memoryGovernor;
    private readonly PyString _nameValue;
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
        _memoryGovernor = closure.MemoryGovernor;
        _nameValue = PyString.FromString(name, closure.MemoryGovernor, null);
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
            "__name__" => _nameValue,
            "__qualname__" => OwnerType is not null
                ? PyString.FromString(OwnerType.Name + "." + Name)
                : _nameValue,
            "__module__" => TryGetModuleName(out var moduleName) ? moduleName : PyNone.Instance,
            _ => PyNone.Instance,
        };
        return !ReferenceEquals(value, PyNone.Instance);
    }

    // The defining module is resolved per read by walking the closure frame
    // chain to its module root and aliasing that frame.__name__ string, so
    // reads cost no retained storage and alias stably like CPython.
    internal bool TryGetModuleName([MaybeNullWhen(false)] out PyString moduleName)
        => TryGetModuleName(_closure, out moduleName);

    internal static bool TryGetModuleName(LythonRuntime.ExecutionContext context, [MaybeNullWhen(false)] out PyString moduleName)
    {
        var frame = context.Frame;
        while (frame.Parent is not null)
        {
            frame = frame.Parent;
        }

        if (frame.Variables.TryGetValue("__name__", out var name) && name is PyString module)
        {
            moduleName = module;
            return true;
        }

        moduleName = null;
        return false;
    }

    public bool TrySetMember(string name, object value)
    {
        if (_memoryGovernor is not null && !_metadata.ContainsKey(name))
        {
            _memoryGovernor.Reserve(AttributeSlotBytes, null);
            _memoryGovernor.Commit(AttributeSlotBytes);
        }

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
