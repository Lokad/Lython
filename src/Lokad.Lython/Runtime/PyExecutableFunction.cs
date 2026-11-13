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

    internal bool CanStartInline => _codeObject.CanInlineInstructions
        && !Closure.IsClassBody && !Closure.IsAnnotationScope && !Closure.IsTypeParameterScope
        && Closure.ClassCell is null && Closure.InlinedComprehensionLookup is null;

    internal bool CanBindNativeArguments => CanStartInline
        && BindingPlan.ParameterCount <= LythonRuntime.NativeValueCapacity
        && BindingPlan.VariadicListIndex < 0 && BindingPlan.VariadicDictionaryIndex < 0
        && BindingPlan.PositionalDefaults is null or PyTuple
        && (BindingPlan.KeywordDefaultStorage is null || BindingPlan.KeywordDefaultStorage.SupportsNativeStringReads);

    [System.Runtime.CompilerServices.InlineArray(LythonRuntime.NativeValueCapacity)]
    private struct NativeValues { private object _first; }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal bool TryStartNativeCall(ExecutableCallSite call, ReadOnlySpan<object?> arguments, object? receiver,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool consumedByIteration,
        bool borrow, out object result, out LythonRuntime.ExecutionContext? frame)
    {
        result = PyNone.Instance;
        frame = null;
        if (!CanStartInline || !borrow && !CanBindNativeArguments) return false;
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
        return StartInline(values, assigned, null, span, consumedByIteration, out result, out frame);
    }

    internal override bool TryStartBoundInvocation(in BoundCallArguments bound, LythonSourceSpan span,
        out object result, out LythonRuntime.ExecutionContext? frame)
        => TryStartBoundInvocation(bound, span, false, out result, out frame);

    internal bool TryStartBoundInvocation(in BoundCallArguments bound, LythonSourceSpan span, bool consumedByIteration,
        out object result, out LythonRuntime.ExecutionContext? frame)
    {
        result = PyNone.Instance;
        frame = null;
        return CanStartInline && StartInline(bound.Values.AsSpan(0, _codeObject.ValueStorageCapacity),
            bound.Assigned, bound, span, consumedByIteration, out result, out frame);
    }

    private bool StartInline(Span<object> values, ArgumentPresence assigned, BoundCallArguments? existing,
        LythonSourceSpan span, bool consumedByIteration, out object result, out LythonRuntime.ExecutionContext? frame)
    {
        result = PyNone.Instance;
        frame = null;
        var state = new LythonRuntime.NativeExecution(this, values, consumedByIteration);
        var entered = false;
        var transferred = false;
        BoundCallArguments owned = existing.GetValueOrDefault();
        var rented = false;
        try
        {
            Closure.EnterFunctionCall(span);
            entered = true;
            if (state.Run(span, out result)) return true;
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
            return true;
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
                finally { if (rented && !transferred) LythonRuntime.ReturnBoundArguments(owned, _codeObject.ValueStorageCapacity); }
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
