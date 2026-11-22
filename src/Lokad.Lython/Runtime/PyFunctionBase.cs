using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;
using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal abstract class PyFunctionBase : FunctionBindingPlan, IPyRenderableValue, IPyBindableCallable, LythonRuntime.ISpanCallable, IClassOwnedMember, IPyMutableDynamicAttributes, IPyDeletableDynamicAttributes, IPyHashableValue
{
    private readonly LythonRuntime.ExecutionContext _closure;
    // One optional value can live directly here: a custom dictionary or
    // a type-parameter tuple. Overrides or both values need the full record.
    private object? _attributes;
    // Resetting metadata needs no replacement storage.
    private bool _moduleCleared;
    private bool _docstringCleared;
    private bool _repeatedAnnotation;
    private object? _annotations;

    private sealed class FunctionAttributes
    {
        internal object? Name;
        internal object? QualifiedName;
        internal object? Module;
        internal object? Docstring;
        internal object? TypeParameters;
        internal PyDict? Custom;
    }

    protected PyFunctionBase(
        ExecutableCodeObject codeObject,
        FunctionParameterLayout parameterLayout,
        LythonRuntime.ExecutionContext closure,
        FunctionDefaults defaults)
        : base(parameterLayout, defaults)
    {
        CodeObject = codeObject;
        _closure = closure;
    }

    internal ExecutableCodeObject CodeObject { get; }

    public string Name => CodeObject.Name;

    public sealed override string CallableName => Name;
    public sealed override PythonCallableKind CallableKind
        => Name == "<lambda>" ? PythonCallableKind.Lambda : PythonCallableKind.Function;

    public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

    protected LythonRuntime.ExecutionContext Closure => _closure;

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
        => LythonRuntime.BindFunctionArguments([], span, this, context, receiver, callSite, arguments, storageCapacity: CodeObject.ValueStorageCapacity);

    internal ValueTask<BoundFunctionArguments> BindCallOperandsAsync(ReadOnlySpan<LythonRuntime.ExecutableSlot> arguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, object? receiver, ExecutableCallSite? callSite)
        => LythonRuntime.BindFunctionArgumentsAsync([], span, this, context, receiver, callSite, arguments, CodeObject.ValueStorageCapacity);

    // A complete positional supply needs no live defaults or dynamic binding.
    internal bool HasCompletePositionalSupply(int count, object? receiver)
        => SupportsPositionalBinding && count == PositionalCount - (receiver is null ? 0 : 1);

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
        var storage = LythonRuntime.RentFunctionArgumentStorage(this, span, context, CodeObject.ValueStorageCapacity);
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

        var boundArguments = LythonRuntime.BindFunctionArguments(arguments, span, this, context, receiver, storageCapacity: CodeObject.ValueStorageCapacity);
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
                    LythonRuntime.ReturnBoundArguments(boundArguments, CodeObject.ValueStorageCapacity);
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

            var binding = LythonRuntime.BindFunctionArgumentsAsync(arguments, span, this, context, receiver, storageCapacity: CodeObject.ValueStorageCapacity);
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
                    LythonRuntime.ReturnBoundArguments(boundArguments, CodeObject.ValueStorageCapacity);
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
        if (name == "__dict__") { value = GetCustomAttributes(); return true; }
        var storage = _attributes;
        var attributes = storage as FunctionAttributes;
        value = name switch
        {
            "__name__" => attributes?.Name ?? CodeObject.FunctionName.RequireNotNull(),
            "__qualname__" => attributes?.QualifiedName ?? CodeObject.QualifiedName ?? CodeObject.FunctionName.RequireNotNull(),
            "__module__" => attributes?.Module ??
                (!_moduleCleared && TryGetModuleName(out var moduleName) ? moduleName : (object)PyNone.Instance),
            "__doc__" => attributes?.Docstring ??
                (_docstringCleared ? PyNone.Instance : CodeObject.Docstring ?? (object)PyNone.Instance),
            "__annotations__" => ReadAnnotations(),
            "__type_params__" => storage switch
            {
                FunctionAttributes record => record.TypeParameters ?? PyTuple.Empty,
                null or PyDict => PyTuple.Empty,
                _ => storage,
            },
            _ => null,
        };
        return value is not null || (storage as PyDict ?? attributes?.Custom) is { } custom &&
            custom.TryGetStringValue(name, _closure.MemoryGovernor, null, null, out value);
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
                var dictionary = value as PyDict ?? throw new LythonRuntimeException("TypeError", "__dict__ must be set to a dictionary", null);
                if (_attributes is null or PyDict) _attributes = dictionary;
                else GetFunctionAttributes().Custom = dictionary;
                break;
            case "__name__": GetFunctionAttributes().Name = value; break;
            case "__qualname__": GetFunctionAttributes().QualifiedName = value; break;
            case "__module__":
                if (value is PyNone)
                {
                    _moduleCleared = true;
                    if (_attributes is FunctionAttributes attributes) attributes.Module = null;
                }
                else GetFunctionAttributes().Module = value;
                break;
            case "__doc__":
                if (value is PyNone)
                {
                    _docstringCleared = true;
                    if (_attributes is FunctionAttributes attributes) attributes.Docstring = null;
                }
                else GetFunctionAttributes().Docstring = value;
                break;
            case "__annotations__":
                if (value is not (PyNone or PyDict or PyDefaultDict or PyCounter))
                    throw new LythonRuntimeException("TypeError", "__annotations__ must be set to a dict object", null);
                ReplaceAnnotations(value is PyNone ? null : value);
                break;
            case "__type_params__":
                if (!PyTupleLike.TryGetItems(value, out _)) throw TypeParametersMustBeTuple(PyStructuralGuard.AmbientSpan);
                if (_attributes is PyDict or FunctionAttributes) GetFunctionAttributes().TypeParameters = value;
                else _attributes = value;
                break;
            default:
                GetCustomAttributes().SetStringItem(name, value, _closure.MemoryGovernor, null);
                break;
        }
        return true;
    }

    private FunctionAttributes GetFunctionAttributes()
    {
        if (_attributes is FunctionAttributes existing) return existing;
        const long bytes = 80;
        var span = PyStructuralGuard.AmbientSpan;
        var governor = _closure.MemoryGovernor;
        governor.Reserve(bytes, span);
        try
        {
            _closure.CheckExecution(span);
            var current = _attributes;
            if (current is not FunctionAttributes)
            {
                var attributes = new FunctionAttributes
                {
                    Custom = current as PyDict,
                    TypeParameters = current is PyDict ? null : current,
                };
                governor.Commit(bytes);
                _attributes = attributes;
                return attributes;
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
        return (FunctionAttributes)_attributes.RequireNotNull();
    }

    private PyDict? ReadCustomAttributes() => _attributes as PyDict ?? (_attributes as FunctionAttributes)?.Custom;

    private PyDict GetCustomAttributes()
    {
        if (ReadCustomAttributes() is { } existing) return existing;
        var span = PyStructuralGuard.AmbientSpan;
        var dictionary = new PyDict(_closure.MemoryGovernor, span);
        try
        {
            _closure.CheckExecution(span);
            if (_attributes is null)
            {
                _attributes = dictionary;
                return dictionary;
            }
            if (ReadCustomAttributes() is null)
            {
                var attributes = GetFunctionAttributes();
                if (attributes.Custom is null) return attributes.Custom = dictionary;
            }
        }
        finally
        {
            if (!ReferenceEquals(ReadCustomAttributes(), dictionary)) dictionary.DiscardUnpublishedStorage();
        }
        // During admission a callback can publish a dictionary of its own.
        _closure.CheckExecution(span);
        return ReadCustomAttributes().RequireNotNull();
    }

    public bool TryDeleteMember(string name, LythonSourceSpan? span)
    {
        if (TrySetDefaultMetadata(name, PyNone.Instance, _closure.MemoryGovernor, span)) return true;
        if (name is "__name__" or "__qualname__" or "__dict__")
            throw new LythonRuntimeException("TypeError", $"cannot delete {name}", span);
        if (name == "__type_params__") throw TypeParametersMustBeTuple(span);
        if (name is "__doc__" or "__module__") return TrySetMember(name, PyNone.Instance);

        // Values previously returned to Python can remain aliased after deletion.
        // Removing the field keeps those values funded by their existing owners.
        var removed = false;
        if (name == "__annotations__")
        {
            removed = _annotations is not null;
            ReplaceAnnotations(null);
        }
        if (!removed && ReadCustomAttributes() is { } custom)
        {
            var governor = _closure.MemoryGovernor;
            var key = PyString.FromString(name, governor, span);
            var mayEscape = custom.HasProtocolKeys;
            try { removed = custom.Remove(key); }
            finally { if (!mayEscape) governor.RefundUnpublishedValue(key); }
        }
        return removed || name == "__annotations__";
    }

    private static LythonRuntimeException TypeParametersMustBeTuple(LythonSourceSpan? span)
        => RuntimeErrors.Type("__type_params__ must be set to a tuple", span);

    internal void AttachAdmittedAnnotations(object values, LythonRuntime.ExecutionContext context, bool repeated)
    {
        System.Diagnostics.Debug.Assert(_annotations is null && ReferenceEquals(_closure.MemoryGovernor, context.MemoryGovernor));
        System.Diagnostics.Debug.Assert(repeated || values is object?[]);
        _annotations = values;
        _repeatedAnnotation = repeated;
    }

    private object ReadAnnotations()
    {
        var repeated = _repeatedAnnotation;
        var prepared = _annotations;
        var values = prepared as object?[];
        if (repeated || values is not null)
        {
            var span = PyStructuralGuard.AmbientSpan;
            _closure.CheckExecution(span);
            var keys = CodeObject.DefinitionAnnotationKeys.RequireNotNull();
            if (repeated || values is PyString[])
            {
                // Both forms already fund the complete mutable record.
                var observed = new object?[keys.PreparedValueCapacity];
                for (var i = 0; i < keys.Count; i++)
                {
                    if ((i & 63) == 0) _closure.CheckExecution(span);
                    observed[i] = repeated ? prepared : values![i];
                }
                _closure.CheckExecution(span);
                values = observed;
            }
            _annotations = keys.AdoptPreparedValues(values.RequireNotNull(), _closure.MemoryGovernor, span);
            _repeatedAnnotation = false;
        }
        return _annotations ??= new PyDict(_closure.MemoryGovernor);
    }

    private void ReplaceAnnotations(object? value)
    {
        var previous = _annotations;
        var repeated = _repeatedAnnotation;
        _annotations = value;
        _repeatedAnnotation = false;
        if (repeated || previous is PyString[])
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
