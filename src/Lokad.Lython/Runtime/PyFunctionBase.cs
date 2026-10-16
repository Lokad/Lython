using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;
using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal abstract class PyFunctionBase : IPyRenderableValue, IPyBindableCallable, IClassOwnedMember, IPyMutableDynamicAttributes, IPyDeletableDynamicAttributes, IPyHashableValue
{
    private static readonly PyString LambdaName = PyString.FromString("<lambda>");

    private readonly FunctionBindingPlan _bindingPlan;
    private readonly int? _storageCapacity;
    private readonly LythonRuntime.ExecutionContext _closure;
    // Metadata tables grow one CLR entry per guest attribute name; charge
    // each new key so retained attributes accumulate. The key strings
    // themselves are caller-owned; only the table slot is charged here.
    // Storage remains charged to the function's execution/result owner.
    private const long AttributeSlotBytes = 64;
    private MemoryGovernor? _memoryGovernor;
    private readonly PyString _nameValue;
    // Computed qualnames are immutable for a bound owner/shape, so the first
    // qualified read caches its governed copy and every later read aliases it
    // (matching CPython identity). Explicit metadata assignments shadow through
    // _metadata before this cache is ever consulted, and binding an owner below
    // clears a premature ownerless computation.
    private PyString? _qualNameValue;
    private readonly Dictionary<string, object> _metadata = new(StringComparer.Ordinal);
    private readonly ScopeDirectiveFacts _scopeFacts;

    protected PyFunctionBase(
        string name,
        IReadOnlyList<LoweredFunctionParameter> parameters,
        LythonRuntime.ExecutionContext closure,
        Dictionary<string, object> defaultValues,
        ScopeDirectiveFacts scopeFacts,
        PythonCallableKind callableKind = PythonCallableKind.Function,
        int? storageCapacity = null)
    {
        Name = name;
        _storageCapacity = storageCapacity;
        _closure = closure;
        _memoryGovernor = closure.MemoryGovernor;
        _nameValue = callableKind == PythonCallableKind.Lambda
            ? LambdaName : PyString.FromString(name, closure.MemoryGovernor, null);
        _bindingPlan = new FunctionBindingPlan(name, callableKind, parameters, defaultValues, closure);
        SupportsPositionalBinding = _bindingPlan.KeywordOnlyParameters.Count == 0 &&
            _bindingPlan.VariadicList is null && _bindingPlan.VariadicDictionary is null;
        _scopeFacts = scopeFacts;
    }

    public string Name { get; }

    public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

    protected FunctionBindingPlan BindingPlan => _bindingPlan;

    internal bool SupportsPositionalBinding { get; }

    // Copy operands (and an optional bound receiver) straight into the existing
    // invocation-owned values. Frame entry, mirroring and body dispatch stay shared.
    internal object InvokeFromOperands(ReadOnlySpan<object?> arguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, object? receiver = null, ExecutableCallSite? callSite = null)
    {
        context.CheckExecution(span);
        var bound = callSite is null
            ? LythonRuntime.BindPositionalFunctionArguments(arguments, span, _bindingPlan, receiver, context.MemoryGovernor, _storageCapacity)
            : LythonRuntime.BindFunctionArguments([], span, _bindingPlan, context, receiver, callSite, arguments, storageCapacity: _storageCapacity);
        return InvokeBound(bound, span);
    }

    internal ValueTask<object> InvokeFromOperandsAsync(ReadOnlySpan<object?> arguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, object? receiver = null, ExecutableCallSite? callSite = null)
    {
        context.CheckExecution(span);
        var binding = callSite is null
            ? new ValueTask<BoundCallArguments>(LythonRuntime.BindPositionalFunctionArguments(arguments, span, _bindingPlan, receiver, context.MemoryGovernor, _storageCapacity))
            : LythonRuntime.BindFunctionArgumentsAsync([], span, _bindingPlan, context, receiver, callSite, arguments, _storageCapacity);
        return binding.IsCompletedSuccessfully ? InvokeBoundAsync(binding.Result, span) : BindAndInvokeAsync(binding, span);
    }

    public PyType? OwnerType { get; private set; }

    /// <summary>States whether bound arguments must also be materialized in the frame's name dictionary.</summary>
    protected abstract bool RequiresArgumentMirroring { get; }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        var boundArguments = LythonRuntime.BindFunctionArguments(arguments, span, _bindingPlan, context, storageCapacity: _storageCapacity);
        return InvokeBound(boundArguments, span);
    }

    // A separate receiver entry keeps the existing invocation depth and guards;
    // the binder consumes self first without materializing a prefixed array.
    internal object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, object receiver)
    {
        context.CheckExecution(span);
        var boundArguments = LythonRuntime.BindFunctionArguments(arguments, span, _bindingPlan, context, receiver, storageCapacity: _storageCapacity);
        return InvokeBound(boundArguments, span);
    }

    protected object InvokeBound(BoundCallArguments boundArguments, LythonSourceSpan span)
    {
        LythonRuntime.ExecutionContext frame;
        try
        {
            frame = EnterInvocationFrame(boundArguments, span);
        }
        catch
        {
            // Recursion or budget failure during frame entry must not strand
            // the rented bound-values box: hand it back before propagating.
            LythonRuntime.ReturnBoundArguments(boundArguments);
            throw;
        }

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
            LythonRuntime.ReturnBoundArguments(boundArguments);
        }
    }

    public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        try
        {
            context.CheckExecution(span);
            var binding = LythonRuntime.BindFunctionArgumentsAsync(arguments, span, _bindingPlan, context, storageCapacity: _storageCapacity);
            return binding.IsCompletedSuccessfully ? InvokeBoundAsync(binding.Result, span) : BindAndInvokeAsync(binding, span);
        }
        catch (Exception error) { return ValueTask.FromException<object>(error); }
    }

    internal ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, object receiver)
    {
        try
        {
            context.CheckExecution(span);
            var binding = LythonRuntime.BindFunctionArgumentsAsync(arguments, span, _bindingPlan, context, receiver, storageCapacity: _storageCapacity);
            return binding.IsCompletedSuccessfully ? InvokeBoundAsync(binding.Result, span) : BindAndInvokeAsync(binding, span);
        }
        catch (Exception error) { return ValueTask.FromException<object>(error); }
    }

    private async ValueTask<object> BindAndInvokeAsync(ValueTask<BoundCallArguments> binding, LythonSourceSpan span)
        => await InvokeBoundAsync(await binding.ConfigureAwait(false), span).ConfigureAwait(false);

    protected async ValueTask<object> InvokeBoundAsync(BoundCallArguments boundArguments, LythonSourceSpan span)
    {
        LythonRuntime.ExecutionContext frame;
        try
        {
            frame = EnterInvocationFrame(boundArguments, span);
        }
        catch
        {
            // Async twin of the entry cleanup above.
            LythonRuntime.ReturnBoundArguments(boundArguments);
            throw;
        }

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
            LythonRuntime.ReturnBoundArguments(boundArguments);
        }
    }

    public object Bind(object self) => new PyBoundMethod(self, this);

    public void BindOwner(PyType owner)
    {
        if (OwnerType is null)
        {
            OwnerType = owner;
            _qualNameValue = null;
        }
    }

    public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
        => instance is null ? this : Bind(instance);

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        if (_bindingPlan.TryGetDefaultMetadata(name, out value)) return true;
        if (_metadata.TryGetValue(name, out value))
        {
            return true;
        }

        if (name == "__annotations__")
        {
            var annotations = new PyDict(_closure.MemoryGovernor);
            TrySetMember(name, annotations);
            value = annotations;
            return true;
        }

        // Absent docstrings report None like CPython (nothing to store); present
        // ones are captured into metadata by CaptureFunctionDocstring.
        if (name == "__doc__")
        {
            value = PyNone.Instance;
            return true;
        }

        value = name switch
        {
            "__name__" => _nameValue,
            "__qualname__" => QualName(),
            "__module__" => TryGetModuleName(out var moduleName) ? moduleName : PyNone.Instance,
            _ => PyNone.Instance,
        };
        return !ReferenceEquals(value, PyNone.Instance);
    }

    // The defining module is resolved per read by walking the closure frame
    // chain to its module root and aliasing that frame.__name__ string, so
    // reads cost no retained storage and alias stably like CPython.
    // __qualname__ prefers the defining class (first-wins owner) and otherwise
    // prefixes the enclosing function path. A class scope between a function
    // scope and the definition stays invisible, so that segment is missing.
    private PyString QualName()
    {
        if (_qualNameValue is { } cached)
        {
            return cached;
        }

        var text = OwnerType is { } owner
            ? owner.Name + "." + Name
            : PyFunctionBinding.EnclosingFunctionPath(_closure) is { } path
                ? path + ".<locals>." + Name
                : null;
        if (text is null)
        {
            return _nameValue;
        }
        // Publish only a fully constructed copy: a denied reservation leaves
        // the field null so the next read retries instead of aliasing half
        // ownership. Governorless hosts keep the previous unowned behavior,
        // still bounded to one object by the cache above.
        var computed = _memoryGovernor is null
            ? PyString.FromString(text)
            : PyString.FromString(text, _memoryGovernor, null);
        _qualNameValue = computed;
        return computed;
    }

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

    // Docstrings are the leading string constant of the lowered body, stored as
    // governed metadata so reads report the text like CPython; absent stays None.
    // Only plain string literals count (f-strings and bytes do not document).
    internal static string? LeadingDocstring(IReadOnlyList<LoweredStatement> body)
    {
        if (body.Count == 0 ||
            body[0] is not LoweredExpressionStatement { Expression: LoweredStringLiteralExpression doc })
        {
            return null;
        }

        return doc.Literal.Value;
    }

    // Capture metadata under the execution owner before the function escapes.
    internal static void CaptureFunctionDocstring(
        PyFunctionBase function,
        IReadOnlyList<LoweredStatement> body,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        if (LeadingDocstring(body) is not { } text)
        {
            return;
        }

        var doc = PyString.FromString(text, context.MemoryGovernor, span);
        function.TrySetMember("__doc__", doc);
    }

    public bool TrySetMember(string name, object value)
    {
        if (_bindingPlan.TrySetDefaultMetadata(name, value)) return true;
        if (_memoryGovernor is not null && !_metadata.ContainsKey(name))
        {
            _memoryGovernor.Reserve(AttributeSlotBytes, null);
            _memoryGovernor.Commit(AttributeSlotBytes);
        }

        _metadata[name] = value;
        return true;
    }

    public bool TryDeleteMember(string name, LythonSourceSpan? span)
    {
        if (_bindingPlan.TrySetDefaultMetadata(name, PyNone.Instance, span)) return true;
        if (name is "__name__" or "__qualname__" or "__dict__")
            throw new LythonRuntimeException("TypeError", $"cannot delete {name}", span);
        if (name is "__doc__" or "__module__") return TrySetMember(name, PyNone.Instance);

        // Removing a key retains table capacity and funding for escaped values.
        // Annotations are recreated lazily, including after repeated deletion.
        var removed = _metadata.Remove(name);
        return removed || name == "__annotations__";
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString($"<function {Name}>");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public override string ToString() => $"<function {Name}>";

    /// <summary>Executes the function body after argument binding and returns its fall-through result.</summary>
    protected abstract object ExecuteBody(
        LythonRuntime.ExecutionContext frame,
        BoundCallArguments boundArguments,
        LythonSourceSpan span);

    /// <summary>Executes the function body asynchronously with the same return and exception semantics as <see cref="ExecuteBody"/>.</summary>
    protected virtual ValueTask<object> ExecuteBodyAsync(
        LythonRuntime.ExecutionContext frame,
        BoundCallArguments boundArguments,
        LythonSourceSpan span)
        => ValueTask.FromResult(ExecuteBody(frame, boundArguments, span));

    private LythonRuntime.ExecutionContext EnterInvocationFrame(
        BoundCallArguments boundArguments,
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
