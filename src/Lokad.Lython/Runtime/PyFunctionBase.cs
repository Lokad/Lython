using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;
using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal abstract class PyFunctionBase : FunctionBindingPlan, IPyRenderableValue, IPyBindableCallable, LythonRuntime.ISpanCallable, IClassOwnedMember, IPyMutableDynamicAttributes, IPyDeletableDynamicAttributes, IPyHashableValue
{
    private readonly int? _storageCapacity;
    private readonly LythonRuntime.ExecutionContext _closure;
    // Compiled names and docstrings can be read directly. Explicit overrides
    // use separately admitted storage and stay separate from __dict__.
    private StandardOverrides? _standardOverrides;
    private object? _annotations;
    private object? _typeParameters;
    private PyDict? _customAttributes;

    private sealed class StandardOverrides
    {
        internal object? Name;
        internal object? QualifiedName;
        internal object? Module;
        internal object? Docstring;
    }

    protected PyFunctionBase(
        ExecutableCodeObject codeObject,
        FunctionParameterLayout parameterLayout,
        LythonRuntime.ExecutionContext closure,
        FunctionDefaults defaults,
        PythonCallableKind callableKind = PythonCallableKind.Function,
        int? storageCapacity = null)
        : base(codeObject.Name, callableKind, parameterLayout, defaults)
    {
        CodeObject = codeObject;
        _storageCapacity = storageCapacity;
        _closure = closure;
        SupportsPositionalBinding = KeywordOnlyCount == 0 &&
            VariadicListIndex < 0 && VariadicDictionaryIndex < 0;
    }

    internal ExecutableCodeObject CodeObject { get; }

    public string Name => CallableName;

    public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

    protected LythonRuntime.ExecutionContext Closure => _closure;

    internal bool SupportsPositionalBinding { get; }

    // Lowered operand calls bind invocation-owned values. The executable
    // runner and exact positional callback entry can start in native slots.
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
        => LythonRuntime.BindFunctionArguments([], span, this, context, receiver, callSite, arguments, storageCapacity: _storageCapacity);

    internal ValueTask<BoundFunctionArguments> BindCallOperandsAsync(ReadOnlySpan<LythonRuntime.ExecutableSlot> arguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, object? receiver, ExecutableCallSite? callSite)
        => LythonRuntime.BindFunctionArgumentsAsync([], span, this, context, receiver, callSite, arguments, _storageCapacity);

    // No defaults, receiver insertion or dynamic binding during the borrow.
    internal bool CanBorrowPositionalArguments(int count, object? receiver)
        => receiver is null && SupportsPositionalBinding && count == PositionalCount;

    internal bool CanTakeExpandedKeywords(int positionalCount, object? receiver)
        => KeywordOnlyCount == 0 && VariadicListIndex < 0 &&
            VariadicDictionaryIndex >= 0 &&
            positionalCount == PositionalCount - (receiver is null ? 0 : 1);

    internal PyDict? PrepareExpandedKeywords(PyDict source, LythonRuntime.ExecutionContext context,
        LythonSourceSpan mappingSpan, LythonSourceSpan callSpan, bool freshMapping)
        => source.PrepareExpandedKeywords(this, context, mappingSpan, callSpan, freshMapping);

    internal BoundFunctionArguments BindPreparedKeywords(ReadOnlySpan<LythonRuntime.ExecutableSlot> positional, object? receiver,
        PyDict keywords, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        context.CheckExecution(span);
        var storage = LythonRuntime.RentFunctionArgumentStorage(this, span, context, _storageCapacity);
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
            storage.Values[VariadicDictionaryIndex].Object = keywords;
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
        if (this is PyExecutableFunction executable &&
            executable.TryStartPositionalCallback(arguments, receiver, span, out var result, out var frame, out var owned))
            return frame is null ? result.Object.RequireNotNull() : InvokeBound(owned, span, frame);

        var boundArguments = LythonRuntime.BindFunctionArguments(arguments, span, this, context, receiver, storageCapacity: _storageCapacity);
        return InvokeBound(boundArguments, span);
    }

    internal object InvokeBound(BoundFunctionArguments boundArguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext? initializedFrame = null)
    {
        var frame = initializedFrame;
        try
        {
            LythonRuntime.ExecutableSlot result = default;
            var prepared = frame is not null || TryStartBoundInvocation(boundArguments, span, out result, out frame);
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
            if (this is PyExecutableFunction executable &&
                executable.TryStartPositionalCallback(arguments, receiver, span, out var result, out var frame, out var owned))
                return frame is null ? new(result.Object.RequireNotNull()) : InvokeBoundAsync(owned, span, frame);

            var binding = LythonRuntime.BindFunctionArgumentsAsync(arguments, span, this, context, receiver, storageCapacity: _storageCapacity);
            return binding.IsCompletedSuccessfully ? InvokeBoundAsync(binding.Result, span) : BindAndInvokeAsync(binding, span);
        }
        catch (Exception error) { return ValueTask.FromException<object>(error); }
    }

    private async ValueTask<object> BindAndInvokeAsync(ValueTask<BoundFunctionArguments> binding, LythonSourceSpan span)
        => await InvokeBoundAsync(await binding.ConfigureAwait(false), span).ConfigureAwait(false);

    internal async ValueTask<object> InvokeBoundAsync(BoundFunctionArguments boundArguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext? initializedFrame = null)
    {
        var frame = initializedFrame;
        try
        {
            LythonRuntime.ExecutableSlot result = default;
            var prepared = frame is not null || TryStartBoundInvocation(boundArguments, span, out result, out frame);
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
        if (TryGetDefaultMetadata(name, _closure, out value)) return true;
        if (name == "__dict__") { value = _customAttributes ??= new PyDict(_closure.MemoryGovernor); return true; }
        value = name switch
        {
            "__name__" => _standardOverrides?.Name ?? CodeObject.FunctionName.RequireNotNull(),
            "__qualname__" => _standardOverrides?.QualifiedName ?? CodeObject.QualifiedName ?? CodeObject.FunctionName.RequireNotNull(),
            "__module__" => _standardOverrides?.Module ?? (TryGetModuleName(out var moduleName) ? moduleName : (object)PyNone.Instance),
            "__doc__" => _standardOverrides?.Docstring ?? CodeObject.Docstring ?? (object)PyNone.Instance,
            "__annotations__" => ReadAnnotations(),
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
        if (TrySetDefaultMetadata(name, value, _closure.MemoryGovernor)) return true;
        switch (name)
        {
            case "__dict__":
                _customAttributes = value as PyDict ?? throw new LythonRuntimeException("TypeError", "__dict__ must be set to a dictionary", null);
                break;
            case "__name__": GetStandardOverrides().Name = value; break;
            case "__qualname__": GetStandardOverrides().QualifiedName = value; break;
            case "__module__": GetStandardOverrides().Module = value; break;
            case "__doc__": GetStandardOverrides().Docstring = value; break;
            case "__annotations__":
                if (value is not (PyNone or PyDict or PyDefaultDict or PyCounter))
                    throw new LythonRuntimeException("TypeError", "__annotations__ must be set to a dict object", null);
                ReplaceAnnotations(value is PyNone ? null : value);
                break;
            case "__type_params__": _typeParameters = value; break;
            default:
                (_customAttributes ??= new PyDict(_closure.MemoryGovernor)).SetStringItem(name, value, _closure.MemoryGovernor, null);
                break;
        }
        return true;
    }

    private StandardOverrides GetStandardOverrides()
    {
        if (_standardOverrides is { } existing) return existing;
        const long bytes = 64;
        var span = PyStructuralGuard.AmbientSpan;
        var governor = _closure.MemoryGovernor;
        governor.Reserve(bytes, span);
        try
        {
            _closure.CheckExecution(span);
            if (_standardOverrides is null)
            {
                var overrides = new StandardOverrides();
                governor.Commit(bytes);
                return _standardOverrides = overrides;
            }
        }
        catch
        {
            governor.ReleaseReserved(bytes);
            throw;
        }
        // A reentrant caller established the record while admission ran.
        // Refund the redundant allowance before changing an existing value.
        governor.ReleaseReserved(bytes);
        _closure.CheckExecution(span);
        return _standardOverrides.RequireNotNull();
    }

    public bool TryDeleteMember(string name, LythonSourceSpan? span)
    {
        if (TrySetDefaultMetadata(name, PyNone.Instance, _closure.MemoryGovernor, span)) return true;
        if (name is "__name__" or "__qualname__" or "__dict__")
            throw new LythonRuntimeException("TypeError", $"cannot delete {name}", span);
        if (name is "__doc__" or "__module__") return TrySetMember(name, PyNone.Instance);

        // Values previously returned to Python can remain aliased after deletion.
        // Removing the field keeps those values funded by their existing owners.
        var removed = false;
        if (name == "__annotations__")
        {
            removed = _annotations is not null;
            ReplaceAnnotations(null);
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

    internal void AttachAdmittedAnnotations(object?[] values, LythonRuntime.ExecutionContext context)
    {
        System.Diagnostics.Debug.Assert(_annotations is null && ReferenceEquals(_closure.MemoryGovernor, context.MemoryGovernor));
        _annotations = values;
    }

    private object ReadAnnotations()
    {
        if (_annotations is object?[] prepared)
        {
            var span = PyStructuralGuard.AmbientSpan;
            _closure.CheckExecution(span);
            var keys = CodeObject.DefinitionAnnotationKeys!;
            var values = prepared;
            if (prepared is PyString[] postponed)
            {
                // The definition has funded the final record. The compiler's
                // immutable payload never becomes guest-owned mutable storage.
                values = new object?[keys.PreparedValueCapacity];
                for (var i = 0; i < postponed.Length; i++)
                {
                    if ((i & 63) == 0) _closure.CheckExecution(span);
                    values[i] = postponed[i];
                }
                _closure.CheckExecution(span);
            }
            _annotations = keys.AdoptPreparedValues(values, _closure.MemoryGovernor, span);
        }
        return _annotations ??= new PyDict(_closure.MemoryGovernor);
    }

    private void ReplaceAnnotations(object? value)
    {
        var previous = _annotations;
        _annotations = value;
        if (previous is PyString[])
            _closure.MemoryGovernor.Release(PyDict.RecordKeys.RecordBytes(CodeObject.DefinitionAnnotationKeys!.PreparedValueCapacity));
        else if (previous is object?[] values)
            PyDict.RecordKeys.RefundPreparedValues(values, _closure.MemoryGovernor);
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
            this,
            boundArguments,
            CodeObject.RequiresLocalVariableMirroring,
            OwnerType,
            span,
            enterFunctionCall,
            nonlocalsAreCaptured: CodeObject.UsesOnlyLexicalSlots && _closure.ParentContext is null);
}
