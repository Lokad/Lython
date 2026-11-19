using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;
using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal abstract class PyFunctionBase : IPyRenderableValue, IPyBindableCallable, IClassOwnedMember, IPyMutableDynamicAttributes, IPyDeletableDynamicAttributes, IPyHashableValue
{
    private readonly FunctionBindingPlan _bindingPlan;
    private readonly int? _storageCapacity;
    private readonly LythonRuntime.ExecutionContext _closure;
    // Standard attributes have fixed storage in the funded function wrapper.
    // Explicit overrides shadow the compiled defaults.
    private object? _assignedName;
    private object? _assignedQualName;
    private object? _assignedModule;
    private object? _docstring;
    private object? _annotations;
    private object? _typeParameters;
    private PyDict? _customAttributes;

    protected PyFunctionBase(
        ExecutableCodeObject codeObject,
        FunctionParameterLayout parameterLayout,
        LythonRuntime.ExecutionContext closure,
        FunctionDefaults defaults,
        PythonCallableKind callableKind = PythonCallableKind.Function,
        int? storageCapacity = null)
    {
        CodeObject = codeObject;
        _storageCapacity = storageCapacity;
        _closure = closure;
        _docstring = codeObject.Docstring;
        _bindingPlan = new FunctionBindingPlan(codeObject.Name, callableKind, parameterLayout, defaults);
        SupportsPositionalBinding = _bindingPlan.KeywordOnlyCount == 0 &&
            _bindingPlan.VariadicListIndex < 0 && _bindingPlan.VariadicDictionaryIndex < 0;
    }

    internal ExecutableCodeObject CodeObject { get; }

    public string Name => CodeObject.Name;

    public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

    protected FunctionBindingPlan BindingPlan => _bindingPlan;

    protected LythonRuntime.ExecutionContext Closure => _closure;

    internal bool SupportsPositionalBinding { get; }

    // Lowered opcode calls and callbacks bind invocation-owned values.
    // Executable calls use the linear runner, including its native
    // operand borrow; callbacks can still complete natively after binding below.
    internal object InvokeFromOperands(ReadOnlySpan<LythonRuntime.ExecutableSlot> arguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, object? receiver = null, ExecutableCallSite? callSite = null)
    {
        context.CheckExecution(span);
        var bound = BindCallOperands(arguments, span, context, receiver, callSite);
        return InvokeBound(bound, span);
    }

    internal ValueTask<object> InvokeFromOperandsAsync(ReadOnlySpan<LythonRuntime.ExecutableSlot> arguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, object? receiver = null, ExecutableCallSite? callSite = null)
    {
        context.CheckExecution(span);
        var binding = BindCallOperandsAsync(arguments, span, context, receiver, callSite);
        return binding.IsCompletedSuccessfully ? InvokeBoundAsync(binding.Result, span) : BindAndInvokeAsync(binding, span);
    }

    public PyType? OwnerType { get; private set; }

    internal BoundFunctionArguments BindCallOperands(ReadOnlySpan<LythonRuntime.ExecutableSlot> arguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, object? receiver, ExecutableCallSite? callSite)
        => LythonRuntime.BindFunctionArguments([], span, _bindingPlan, context, receiver, callSite, arguments, storageCapacity: _storageCapacity);

    internal ValueTask<BoundFunctionArguments> BindCallOperandsAsync(ReadOnlySpan<LythonRuntime.ExecutableSlot> arguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, object? receiver, ExecutableCallSite? callSite)
        => LythonRuntime.BindFunctionArgumentsAsync([], span, _bindingPlan, context, receiver, callSite, arguments, _storageCapacity);

    // No defaults, receiver insertion or dynamic binding during the borrow.
    internal bool CanBorrowPositionalArguments(int count, object? receiver)
        => receiver is null && SupportsPositionalBinding && count == _bindingPlan.PositionalCount;

    internal bool CanTakeExpandedKeywords(int positionalCount, object? receiver)
        => _bindingPlan.KeywordOnlyCount == 0 && _bindingPlan.VariadicListIndex < 0 &&
            _bindingPlan.VariadicDictionaryIndex >= 0 &&
            positionalCount == _bindingPlan.PositionalCount - (receiver is null ? 0 : 1);

    internal PyDict? PrepareExpandedKeywords(PyDict source, LythonRuntime.ExecutionContext context,
        LythonSourceSpan mappingSpan, LythonSourceSpan callSpan, bool freshMapping)
        => source.PrepareExpandedKeywords(_bindingPlan, context, mappingSpan, callSpan, freshMapping);

    internal BoundFunctionArguments BindPreparedKeywords(ReadOnlySpan<LythonRuntime.ExecutableSlot> positional, object? receiver,
        PyDict keywords, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        var storage = LythonRuntime.RentFunctionArgumentStorage(_bindingPlan, span, context, _storageCapacity);
        try
        {
            var slot = 0;
            if (receiver is not null) storage.Values[slot++].Object = receiver;
            for (var offset = 0; offset < positional.Length;)
            {
                context.CheckExecution(span);
                var length = Math.Min(1024, positional.Length - offset);
                positional.Slice(offset, length).CopyTo(storage.Values.AsSpan(slot + offset, length));
                offset += length;
            }
            storage.Values[_bindingPlan.VariadicDictionaryIndex].Object = keywords;
            return storage;
        }
        catch
        {
            LythonRuntime.ReturnBoundArguments(storage);
            throw;
        }
    }

    public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => InvokeBorrowed(arguments, span, context);

    public object InvokeBorrowed(ReadOnlySpan<CallArgumentValue> arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => Invoke(arguments, span, context);

    // A separate receiver entry keeps the existing invocation depth and guards;
    // the binder consumes self first without materializing a prefixed array.
    internal object Invoke(ReadOnlySpan<CallArgumentValue> arguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, object? receiver = null)
    {
        context.CheckExecution(span);
        var boundArguments = LythonRuntime.BindFunctionArguments(arguments, span, _bindingPlan, context, receiver, storageCapacity: _storageCapacity);
        return InvokeBound(boundArguments, span);
    }

    internal object InvokeBound(BoundFunctionArguments boundArguments, LythonSourceSpan span)
    {
        LythonRuntime.ExecutionContext? frame = null;
        try
        {
            var prepared = TryStartBoundInvocation(boundArguments, span, out var result, out frame);
            if (prepared && frame is null) return result.Object.RequireNotNull();
            frame ??= EnterInvocationFrame(boundArguments, span);
            try { return prepared ? LythonRuntime.ExecuteInitializedFrame(frame) ?? PyNone.Instance
                : ExecuteBody(frame, boundArguments, span); }
            catch (LythonRuntime.ReturnSignal signal) { return signal.Value; }
            catch (LythonRuntimeException ex)
            {
                PyFunctionBinding.AnnotateException(ex, frame, Name, span);
                throw;
            }
        }
        finally
        {
            try { frame?.LeaveFunctionCall(); }
            finally
            {
                // One relinquishment covers native success, entry failure and
                // a framed body. A throwing refund must never return it again.
                if (frame?.RetainsBoundWorkspace(boundArguments.Values) != true)
                    LythonRuntime.ReturnBoundArguments(boundArguments, _storageCapacity ?? boundArguments.ParameterCount);
            }
        }
    }

    public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => InvokeBorrowedAsync(arguments, span, context);

    public ValueTask<object> InvokeBorrowedAsync(ReadOnlySpan<CallArgumentValue> arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => InvokeAsync(arguments, span, context);

    internal ValueTask<object> InvokeAsync(ReadOnlySpan<CallArgumentValue> arguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, object? receiver = null)
    {
        try
        {
            context.CheckExecution(span);
            var binding = LythonRuntime.BindFunctionArgumentsAsync(arguments, span, _bindingPlan, context, receiver, storageCapacity: _storageCapacity);
            return binding.IsCompletedSuccessfully ? InvokeBoundAsync(binding.Result, span) : BindAndInvokeAsync(binding, span);
        }
        catch (Exception error) { return ValueTask.FromException<object>(error); }
    }

    private async ValueTask<object> BindAndInvokeAsync(ValueTask<BoundFunctionArguments> binding, LythonSourceSpan span)
        => await InvokeBoundAsync(await binding.ConfigureAwait(false), span).ConfigureAwait(false);

    internal async ValueTask<object> InvokeBoundAsync(BoundFunctionArguments boundArguments, LythonSourceSpan span)
    {
        LythonRuntime.ExecutionContext? frame = null;
        try
        {
            var prepared = TryStartBoundInvocation(boundArguments, span, out var result, out frame);
            if (prepared && frame is null) return result.Object.RequireNotNull();
            frame ??= EnterInvocationFrame(boundArguments, span);
            try { return prepared ? await LythonRuntime.ExecuteInitializedFrameAsync(frame).ConfigureAwait(false) ?? PyNone.Instance
                : await ExecuteBodyAsync(frame, boundArguments, span).ConfigureAwait(false); }
            catch (LythonRuntime.ReturnSignal signal) { return signal.Value; }
            catch (LythonRuntimeException ex)
            {
                PyFunctionBinding.AnnotateException(ex, frame, Name, span);
                throw;
            }
        }
        finally
        {
            try { frame?.LeaveFunctionCall(); }
            finally
            {
                if (frame?.RetainsBoundWorkspace(boundArguments.Values) != true)
                    LythonRuntime.ReturnBoundArguments(boundArguments, _storageCapacity ?? boundArguments.ParameterCount);
            }
        }
    }

    public object Bind(object self) => new PyBoundMethod(self, this);

    public void BindOwner(PyType owner)
    {
        if (OwnerType is null)
        {
            OwnerType = owner;
        }
    }

    public object Get(object? instance, PyType owner, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
        => instance is null ? this : Bind(instance);

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        if (_bindingPlan.TryGetDefaultMetadata(name, out value)) return true;
        if (name == "__dict__") { value = _customAttributes ??= new PyDict(_closure.MemoryGovernor); return true; }
        value = name switch
        {
            "__name__" => _assignedName ?? CodeObject.FunctionName.RequireNotNull(),
            "__qualname__" => _assignedQualName ?? CodeObject.QualifiedName ?? CodeObject.FunctionName.RequireNotNull(),
            "__module__" => _assignedModule ?? (TryGetModuleName(out var moduleName) ? moduleName : (object)PyNone.Instance),
            "__doc__" => _docstring ?? PyNone.Instance,
            "__annotations__" => _annotations ??= new PyDict(_closure.MemoryGovernor),
            "__type_params__" => _typeParameters,
            _ => null,
        };
        return value is not null || _customAttributes is not null &&
            _customAttributes.TryGetStringValue(name, _closure.MemoryGovernor, null, null, out value);
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
        if (_bindingPlan.TrySetDefaultMetadata(name, value)) return true;
        switch (name)
        {
            case "__dict__":
                _customAttributes = value as PyDict ?? throw new LythonRuntimeException("TypeError", "__dict__ must be set to a dictionary", null);
                break;
            case "__name__": _assignedName = value; break;
            case "__qualname__": _assignedQualName = value; break;
            case "__module__": _assignedModule = value; break;
            case "__doc__": _docstring = value; break;
            case "__annotations__": _annotations = value; break;
            case "__type_params__": _typeParameters = value; break;
            default:
                (_customAttributes ??= new PyDict(_closure.MemoryGovernor)).SetStringItem(name, value, _closure.MemoryGovernor, null);
                break;
        }
        return true;
    }

    public bool TryDeleteMember(string name, LythonSourceSpan? span)
    {
        if (_bindingPlan.TrySetDefaultMetadata(name, PyNone.Instance, span)) return true;
        if (name is "__name__" or "__qualname__" or "__dict__")
            throw new LythonRuntimeException("TypeError", $"cannot delete {name}", span);
        if (name is "__doc__" or "__module__") return TrySetMember(name, PyNone.Instance);

        // Values previously returned to Python can remain aliased after deletion.
        // Removing the field keeps those values funded by their existing owners.
        var removed = false;
        if (name == "__annotations__")
        {
            removed = _annotations is not null;
            _annotations = null;
        }
        else if (name == "__type_params__")
        {
            removed = _typeParameters is not null;
            _typeParameters = null;
        }
        if (!removed && _customAttributes is not null)
        {
            var governor = _closure.MemoryGovernor;
            var key = PyString.FromString(name, governor, span);
            var mayEscape = _customAttributes.HasProtocolKeys;
            try { removed = _customAttributes.Remove(key); }
            finally { if (!mayEscape) governor.RefundUnpublishedValue(key); }
        }
        return removed || name == "__annotations__";
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString($"<function {Name}>");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public override string ToString() => $"<function {Name}>";

    /// <summary>Executes the function body after argument binding and returns its fall-through result.</summary>
    protected abstract object ExecuteBody(
        LythonRuntime.ExecutionContext frame,
        BoundFunctionArguments boundArguments,
        LythonSourceSpan span);

    /// <summary>Executes the function body asynchronously with the same return and exception semantics as <see cref="ExecuteBody"/>.</summary>
    protected virtual ValueTask<object> ExecuteBodyAsync(
        LythonRuntime.ExecutionContext frame,
        BoundFunctionArguments boundArguments,
        LythonSourceSpan span)
        => ValueTask.FromResult(ExecuteBody(frame, boundArguments, span));

    // Generators create their suspension state in ExecuteBody. Ordinary compiled
    // functions can complete this prefix or hand back an initialized frame.
    internal virtual bool TryStartBoundInvocation(in BoundFunctionArguments bound, LythonSourceSpan span,
        out LythonRuntime.ExecutableSlot result, out LythonRuntime.ExecutionContext? frame)
    {
        result = LythonRuntime.ExecutableSlot.FromObject(PyNone.Instance);
        frame = null;
        return false;
    }

    internal LythonRuntime.ExecutionContext EnterInvocationFrame(
        BoundFunctionArguments boundArguments,
        LythonSourceSpan span,
        bool enterFunctionCall = true)
        => PyFunctionBinding.EnterInvocationFrame(
            _closure,
            CodeObject.ScopeFacts,
            _bindingPlan,
            boundArguments,
            CodeObject.RequiresLocalVariableMirroring,
            OwnerType,
            span,
            enterFunctionCall,
            nonlocalsAreCaptured: CodeObject.UsesOnlyLexicalSlots && _closure.ParentContext is null);
}
