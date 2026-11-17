using Lokad.Lython.Frontend;
namespace Lokad.Lython.Runtime;

internal sealed class PyExecutableFunction : PyFunctionBase
{

    private readonly ExecutableCodeObject _codeObject;
    private readonly IReadOnlyList<LythonRuntime.ExecutableCell> _closureCells;
    private LythonRuntime.ExecutableLookupCache[]? _lookupCaches;

    // Facts belong to this execution-owned definition, not each invocation.
    // Reentrant admission can establish the same backing before we return.
    internal LythonRuntime.ExecutableLookupCache[] GetLookupCaches(LythonSourceSpan span)
    {
        if (_lookupCaches is { } cached) return cached;
        var bytes = LythonRuntime.LookupCacheBytes(_codeObject.LookupCacheCount);
        using var storage = Closure.MemoryGovernor.ReserveTemporary(bytes, span);
        Closure.CheckExecution(span);
        if (_lookupCaches is { } established) return established;
        var caches = new LythonRuntime.ExecutableLookupCache[_codeObject.LookupCacheCount];
        storage.Commit(bytes);
        return _lookupCaches = caches;
    }

    internal void ClearLookupCache(int index)
    {
        if (_lookupCaches is { } caches) caches[index] = default;
    }

    public PyExecutableFunction(
        string name,
        FunctionParameterLayout parameterLayout,
        ExecutableCodeObject codeObject,
        LythonRuntime.ExecutionContext closure,
        IReadOnlyList<LythonRuntime.ExecutableCell> closureCells,
        IReadOnlyDictionary<string, object> defaultValues,
        ScopeDirectiveFacts scopeFacts,
        PythonCallableKind callableKind = PythonCallableKind.Function)
        : base(name, parameterLayout, closure, defaultValues, scopeFacts, callableKind,
            storageCapacity: codeObject.InvocationStorageCapacity)
    {
        _codeObject = codeObject;
        _closureCells = closureCells;
        SupportsDirectPositional = SupportsPositionalBinding;
    }

    protected override bool RequiresArgumentMirroring => _codeObject.RequiresLocalVariableMirroring;

    internal bool SupportsDirectPositional { get; }
    // Compiler parameter slots are the binding-layout prefix. The invocation
    // owns the full local-capacity array through completion. Captures use
    // separate cells; mirrored namespaces and exposed locals copy values,
    // never this array.

    internal ExecutableCodeObject CodeObject => _codeObject;
    internal IReadOnlyList<LythonRuntime.ExecutableCell> ClosureCells => _closureCells;
    internal LythonRuntime.ExecutionContext DefinitionContext => Closure;

    // Incoming cells already belong to the work; new local cells still need a frame.
    // Scope-sensitive operations still promote before observing their namespace.
    // Owner binding alone adds no namespace requirement; lexical class-cell
    // facts below retain the context needed by __class__ and implicit super.
    internal bool CanDeferInvocationContext => !_codeObject.RequiresLocalVariableMirroring &&
        !_codeObject.ScopeFacts.RequiresClassCell &&
        _codeObject.CapturedLocalSlots.Count == 0 &&
        _codeObject.ExceptionRegions.Count == 0 &&
        !Closure.IsClassBody && !Closure.IsAnnotationScope && !Closure.IsTypeParameterScope && Closure.ClassCell is null &&
        Closure.InlinedComprehensionLookup is null && Closure.ActiveHandlerVariables is not { Count: > 0 };

    private bool CanRunNativePrefix => _codeObject.CanRunNativeInstructions
        && !Closure.IsClassBody && !Closure.IsAnnotationScope && !Closure.IsTypeParameterScope
        && Closure.ClassCell is null && Closure.InlinedComprehensionLookup is null;

    internal bool CanStartInline => CanRunNativePrefix
        && _codeObject.ValueStorageCapacity <= LythonRuntime.NativeValueCapacity;

    internal bool CanBindNativeArguments => CanStartInline
        && BindingPlan.ParameterCount <= LythonRuntime.NativeValueCapacity
        && BindingPlan.VariadicListIndex < 0 && BindingPlan.VariadicDictionaryIndex < 0
        && BindingPlan.PositionalDefaults is null or PyTuple
        && (BindingPlan.KeywordDefaultStorage is null || BindingPlan.KeywordDefaultStorage.SupportsNativeStringReads);

    // Disjoint windows still use ordinary native binding. An in-place positional
    // call already has its final parameter prefix and needs no preparation/copy.
    internal void BindNativeCallArguments(ExecutableCallSite call,
        ReadOnlySpan<LythonRuntime.ExecutableSlot> arguments, Span<LythonRuntime.ExecutableSlot> values,
        object? receiver, LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool borrow)
    {
        if (borrow) arguments.CopyTo(values);
        else
        {
            // A caught binding error can leave a partial prefix. The caller's
            // window is reusable storage, not a fresh default-initialized buffer.
            var parameters = values[..BindingPlan.ParameterCount];
            parameters.Clear();
            try { LythonRuntime.BindFunctionArgumentsInto(parameters, [], span, BindingPlan, context,
                receiver: receiver, callSite: call, operands: arguments); }
            catch { parameters.Clear(); throw; }
        }
    }

    internal override bool TryStartBoundInvocation(in BoundFunctionArguments bound, LythonSourceSpan span,
        out LythonRuntime.ExecutableSlot result, out LythonRuntime.ExecutionContext? frame)
        => TryStartBoundInvocation(bound, span, false, default, out _, out result, out frame);

    internal bool TryStartBoundInvocation(in BoundFunctionArguments bound, LythonSourceSpan span, bool consumedByIteration,
        Span<LythonRuntime.ExecutableSlot> unpackedReturn, out bool unpacked, out LythonRuntime.ExecutableSlot result, out LythonRuntime.ExecutionContext? frame)
    {
        unpacked = false;
        result = LythonRuntime.ExecutableSlot.FromObject(PyNone.Instance);
        frame = null;
        if (!CanRunNativePrefix) return false;
        StartInline(bound.Values,
            bound, span, consumedByIteration, unpackedReturn, out unpacked, out result, out frame);
        return true;
    }

    internal void StartInline(Span<LythonRuntime.ExecutableSlot> values, BoundFunctionArguments? existing,
        LythonSourceSpan span, bool consumedByIteration, Span<LythonRuntime.ExecutableSlot> unpackedReturn, out bool unpacked,
        out LythonRuntime.ExecutableSlot result, out LythonRuntime.ExecutionContext? frame)
    {
        unpacked = false;
        result = LythonRuntime.ExecutableSlot.FromObject(PyNone.Instance);
        frame = null;
        // Only this synchronous prefix can borrow primitive working storage.
        // Publication/transfer materializes stable guest objects before escape.
        // The caller owns a pre-admitted window through completion/transfer.
        // A recycled window must not expose stale unassigned locals. Bound
        // storage is already cleared by its existing workspace owner.
        if (existing is null) values[BindingPlan.ParameterCount.._codeObject.ValueStorageCapacity].Clear();
        var state = new LythonRuntime.NativeExecution(this, values, consumedByIteration, unpackedReturn.Length);
        var entered = false;
        var transferred = false;
        BoundFunctionArguments owned = existing.GetValueOrDefault();
        var rented = false;
        try
        {
            Closure.EnterFunctionCall(span);
            entered = true;
            if (state.Run(span, out result, unpackedReturn))
            {
                unpacked = state.ReturnedUnpacked;
                return;
            }
            state.PrepareTransfer();
            if (existing is null)
            {
                owned = LythonRuntime.RentFunctionArgumentStorage(BindingPlan, span, Closure,
                    _codeObject.InvocationStorageCapacity);
                rented = true;
                values[..state.LiveExtent].CopyTo(owned.Values);
            }
            frame = LythonRuntime.CreateNativeContinuation(this, owned, span, state.Index, state.LiveExtent,
                consumedByIteration, state.Failure);
            state.TransferOwnership(frame);
            transferred = true;
        }
        catch (LythonRuntimeException error)
        {
            PyFunctionBinding.AnnotateException(error, Closure, Name, span);
            throw;
        }
        finally
        {
            try { state.Dispose(); }
            finally
            {
                try { if (entered && !transferred) Closure.LeaveFunctionCall(); }
                finally
                {
                    try { if (rented && !transferred) LythonRuntime.ReturnBoundArguments(owned, _codeObject.InvocationStorageCapacity); }
                    finally { if (existing is null) values[.._codeObject.ValueStorageCapacity].Clear(); }
                }
            }
        }
    }

    protected override object ExecuteBody(
        LythonRuntime.ExecutionContext frame,
        BoundFunctionArguments boundArguments,
        LythonSourceSpan span)
    {
        return LythonRuntime.ExecuteExecutableCodeObject(_codeObject, frame, boundArguments, _closureCells, true, span, this)
            ?? PyNone.Instance;
    }

    protected override async ValueTask<object> ExecuteBodyAsync(
        LythonRuntime.ExecutionContext frame,
        BoundFunctionArguments boundArguments,
        LythonSourceSpan span)
    {
        return await LythonRuntime.ExecuteExecutableCodeObjectAsync(
            _codeObject, frame, boundArguments, _closureCells, true, span, this).ConfigureAwait(false)
            ?? PyNone.Instance;
    }
}
