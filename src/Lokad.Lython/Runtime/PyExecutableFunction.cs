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

    internal override bool TryExecuteWithoutFrame(ReadOnlySpan<object?> arguments, LythonSourceSpan span, out object result)
        => TryExecuteWithoutFrame(arguments, span, false, out result);

    internal bool TryExecuteWithoutFrame(ReadOnlySpan<object?> arguments, LythonSourceSpan span,
        bool consumedByIteration, out object result)
    {
        // Proven bodies cannot inspect their invocation namespace or suspend.
        // Incoming reads/writes retain their definition-owned cell identities.
        // Class/annotation contexts keep their existing lexical machinery.
        // Handler aliases belong to the caller: this closed body never binds or
        // unwinds them, and exception state is shared with the ordinary frame.
        if (!CanExecuteNativeBody)
        {
            result = PyNone.Instance;
            return false;
        }
        // Parameters already have their caller/binder storage. Cells retain the
        // definition's existing owner; native evaluation borrows both directly.
        return LythonRuntime.TryExecuteNativeFunction(_codeObject, Closure, arguments, _closureCells,
            Name, span, out result, consumedByIteration);
    }

    private bool CanExecuteNativeBody => _codeObject.NativeBody is not null
        && !Closure.IsClassBody && !Closure.IsAnnotationScope && !Closure.IsTypeParameterScope
        && Closure.ClassCell is null && Closure.InlinedComprehensionLookup is null;

    // An optimization threshold, never a Python argument/execution limit.
    // Wide/external-default/protocol calls keep their admitted owned binding.
    private const int NativeParameterCapacity = 16;
    internal bool CanBindNativeArguments => CanExecuteNativeBody
        && BindingPlan.ParameterCount <= NativeParameterCapacity
        && BindingPlan.VariadicListIndex < 0 && BindingPlan.VariadicDictionaryIndex < 0
        && BindingPlan.PositionalDefaults is null or PyTuple
        && (BindingPlan.KeywordDefaultStorage is null || BindingPlan.KeywordDefaultStorage.SupportsNativeStringReads);

    [System.Runtime.CompilerServices.InlineArray(NativeParameterCapacity)]
    private struct NativeParameters { private object _first; }

    // Eligibility precedes this frame: filling these slots cannot allocate,
    // admit storage or invoke default/key protocols. Arguments/defaults remain
    // funded by their existing caller/definition owners through completion.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal bool TryExecuteNativeCall(ExecutableCallSite call, ReadOnlySpan<object?> arguments, object? receiver,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool consumedByIteration, out object result)
    {
        NativeParameters buffer = default;
        Span<object> values = buffer;
        values = values[..BindingPlan.ParameterCount];
        try
        {
            var assigned = new ArgumentPresence(BindingPlan.ParameterCount);
            LythonRuntime.BindFunctionArgumentsInto(values, ref assigned, [], span, BindingPlan,
                context, receiver: receiver, callSite: call, operands: arguments);
            return TryExecuteWithoutFrame(values, span, consumedByIteration, out result);
        }
        finally { values.Clear(); }
    }

    protected override object ExecuteBody(
        LythonRuntime.ExecutionContext frame,
        BoundCallArguments boundArguments,
        LythonSourceSpan span)
    {
        if (LythonRuntime.TryExecuteNativeReturn(_codeObject, frame, boundArguments.Values.AsSpan(0, boundArguments.Assigned.Length), span, out var result))
            return result;
        return LythonRuntime.ExecuteExecutableCodeObject(_codeObject, frame, boundArguments, _closureCells, true, span, this)
            ?? PyNone.Instance;
    }

    protected override async ValueTask<object> ExecuteBodyAsync(
        LythonRuntime.ExecutionContext frame,
        BoundCallArguments boundArguments,
        LythonSourceSpan span)
    {
        // Only statically proven bodies without callbacks use this shared shortcut.
        if (LythonRuntime.TryExecuteNativeReturn(_codeObject, frame, boundArguments.Values.AsSpan(0, boundArguments.Assigned.Length), span, out var result))
            return result;
        return await LythonRuntime.ExecuteExecutableCodeObjectAsync(
            _codeObject, frame, boundArguments, _closureCells, true, span, this).ConfigureAwait(false)
            ?? PyNone.Instance;
    }
}
