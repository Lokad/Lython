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
            storageCapacity: codeObject.ValueStorageCapacity)
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

    [System.Runtime.CompilerServices.InlineArray(LythonRuntime.NativeValueCapacity)]
    private struct NativeValues { private object _first; }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal void StartNativeCall(ExecutableCallSite call, ReadOnlySpan<object?> arguments, object? receiver,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool consumedByIteration,
        bool borrow, Span<object> unpackedReturn, out bool unpacked,
        out object result, out LythonRuntime.ExecutionContext? frame)
    {
        // Both executable callers select eligibility before entry. Starting a
        // selected call completes or transfers its exact work; it never declines
        // after binding and asks the caller to try another execution route.
        NativeValues buffer = default;
        Span<object> values = buffer;
        values = values[.._codeObject.ValueStorageCapacity];
        // This span is private to the synchronous prefix and cannot escape. Its
        // live values transfer before suspension; stack exit ends the remaining
        // references. Owned callback arrays keep their ordinary full clear.
        var assigned = new ArgumentPresence(BindingPlan.ParameterCount);
        if (borrow)
        {
            arguments.CopyTo(values!);
            for (var i = 0; i < assigned.Length; i++) assigned[i] = true;
        }
        else LythonRuntime.BindFunctionArgumentsInto(values[..BindingPlan.ParameterCount], ref assigned,
            [], span, BindingPlan, context, receiver: receiver, callSite: call, operands: arguments);
        StartInline(values, assigned, null, span, consumedByIteration, unpackedReturn,
            out unpacked, out result, out frame);
    }

    internal override bool TryStartBoundInvocation(in BoundCallArguments bound, LythonSourceSpan span,
        out object result, out LythonRuntime.ExecutionContext? frame)
        => TryStartBoundInvocation(bound, span, false, default, out _, out result, out frame);

    internal bool TryStartBoundInvocation(in BoundCallArguments bound, LythonSourceSpan span, bool consumedByIteration,
        Span<object> unpackedReturn, out bool unpacked, out object result, out LythonRuntime.ExecutionContext? frame)
    {
        unpacked = false;
        result = PyNone.Instance;
        frame = null;
        if (!CanRunNativePrefix) return false;
        StartInline(bound.Values.AsSpan(0, _codeObject.ValueStorageCapacity),
            bound.Assigned, bound, span, consumedByIteration, unpackedReturn, out unpacked, out result, out frame);
        return true;
    }

    private void StartInline(Span<object> values, ArgumentPresence assigned, BoundCallArguments? existing,
        LythonSourceSpan span, bool consumedByIteration, Span<object> unpackedReturn, out bool unpacked,
        out object result, out LythonRuntime.ExecutionContext? frame)
    {
        unpacked = false;
        result = PyNone.Instance;
        frame = null;
        // Only this synchronous prefix can borrow primitive working storage.
        // Publication/transfer materializes stable guest objects before escape.
        // The common fixed buffer has a bounded managed stack cost; larger
        // bound calls borrow charged capacity until materialization and cleanup
        // finish. An idle loan stays funded until execution cleanup or eviction.
        var integerCapacity = _codeObject.NativeIntegerStorageCapacity;
        Span<long> fixedIntegers = integerCapacity == 0 ? default : stackalloc long[LythonRuntime.NativeValueCapacity];
        var integerStorage = integerCapacity > LythonRuntime.NativeValueCapacity
            ? Closure.MemoryGovernor.RentNativeIntegerWorkspace(integerCapacity, span, Closure) : null;
        Span<long> integers = integerCapacity <= LythonRuntime.NativeValueCapacity
            ? fixedIntegers[..integerCapacity] : integerStorage;
        var state = new LythonRuntime.NativeExecution(this, values, integers, consumedByIteration, unpackedReturn.Length);
        var entered = false;
        var transferred = false;
        BoundCallArguments owned = existing.GetValueOrDefault();
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
                owned = LythonRuntime.RentFunctionArgumentStorage(BindingPlan, span, Closure.MemoryGovernor,
                    _codeObject.ValueStorageCapacity) with { Assigned = assigned };
                rented = true;
                values[..state.LiveExtent].CopyTo(owned.Values);
            }
            frame = LythonRuntime.CreateNativeContinuation(this, owned, span, state.Index, state.LiveExtent, consumedByIteration);
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
                    try { if (rented && !transferred) LythonRuntime.ReturnBoundArguments(owned, _codeObject.ValueStorageCapacity); }
                    finally
                    {
                        if (integerStorage is not null) Closure.MemoryGovernor.ReturnNativeIntegerWorkspace(integerStorage);
                    }
                }
            }
        }
    }

    protected override object ExecuteBody(
        LythonRuntime.ExecutionContext frame,
        BoundCallArguments boundArguments,
        LythonSourceSpan span)
    {
        return LythonRuntime.ExecuteExecutableCodeObject(_codeObject, frame, boundArguments, _closureCells, true, span, this)
            ?? PyNone.Instance;
    }

    protected override async ValueTask<object> ExecuteBodyAsync(
        LythonRuntime.ExecutionContext frame,
        BoundCallArguments boundArguments,
        LythonSourceSpan span)
    {
        return await LythonRuntime.ExecuteExecutableCodeObjectAsync(
            _codeObject, frame, boundArguments, _closureCells, true, span, this).ConfigureAwait(false)
            ?? PyNone.Instance;
    }
}
