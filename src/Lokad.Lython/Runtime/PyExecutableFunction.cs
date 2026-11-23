using Lokad.Lython.Frontend;
namespace Lokad.Lython.Runtime;

internal sealed class PyExecutableFunction : PyFunctionBase
{
    private readonly LythonRuntime.ExecutableCaptures _closureCells;
    private LythonRuntime.ExecutableLookupCache[]? _lookupCaches;

    // Facts belong to this execution-owned definition, not each invocation.
    // Reentrant admission can establish the same backing before we return.
    internal LythonRuntime.ExecutableLookupCache[] GetLookupCaches(LythonSourceSpan span)
    {
        if (_lookupCaches is { } cached) return cached;
        var bytes = LythonRuntime.LookupCacheBytes(CodeObject.LookupCacheCount);
        using var storage = Closure.MemoryGovernor.ReserveTemporary(bytes, span);
        Closure.CheckExecution(span);
        if (_lookupCaches is { } established) return established;
        var caches = new LythonRuntime.ExecutableLookupCache[CodeObject.LookupCacheCount];
        storage.Commit(bytes);
        return _lookupCaches = caches;
    }

    internal void ClearLookupCache(int index)
    {
        if (_lookupCaches is { } caches) caches[index] = default;
    }

    public PyExecutableFunction(
        FunctionParameterLayout parameterLayout,
        ExecutableCodeObject codeObject,
        LythonRuntime.ExecutionContext closure,
        LythonRuntime.ExecutableCaptures closureCells,
        FunctionDefaults defaults)
        : base(codeObject, parameterLayout, closure, defaults)
    {
        _closureCells = closureCells;
    }

    // Compiler parameter slots are the binding-layout prefix. The invocation
    // owns the full local-capacity array through completion. Captures use
    // separate cells; mirrored namespaces and exposed locals copy values,
    // never this array.

    internal LythonRuntime.ExecutableCaptures Captures => _closureCells;
    internal LythonRuntime.ExecutionContext DefinitionContext => Closure;

    // Standalone local cells belong to the work. Scope-sensitive operations
    // still promote before observing their namespace.
    // Owner binding alone adds no namespace requirement; lexical class-cell
    // facts below retain the context needed by __class__ and implicit super.
    internal bool CanDeferInvocationContext => !CodeObject.RequiresLocalVariableMirroring &&
        !CodeObject.ScopeFacts.RequiresClassCell &&
        (CodeObject.CapturedLocalSlots.Count == 0 || CodeObject.UsesOnlyLexicalSlots) &&
        CodeObject.ExceptionRegions.Count == 0 &&
        !Closure.IsClassBody && !Closure.IsAnnotationScope && !Closure.IsTypeParameterScope && Closure.ClassCell is null &&
        Closure.ActiveHandlerVariables is not { Count: > 0 };

    // The comprehension lookup can only be present in a class or annotation scope.
    private bool CanRunNativePrefix => CodeObject.CanRunNativeInstructions
        && !Closure.IsClassBody && !Closure.IsAnnotationScope && !Closure.IsTypeParameterScope
        && Closure.ClassCell is null;

    internal bool CanStartInline => CanRunNativePrefix
        && CodeObject.ValueStorageCapacity <= LythonRuntime.NativeValueCapacity;

    // Both executable callers prove the prefix once. Only calls that need
    // binding inspect the current default containers and parameter shape.
    internal bool TrySelectNativeCall(ExecutableCallSite call, object? receiver, out bool completePositional)
    {
        completePositional = false;
        if (!CanStartInline) return false;
        completePositional = call.IsPositionalOnly && HasCompletePositionalSupply(call.ArgumentCount, receiver);
        if (completePositional) return true;
        return ParameterCount <= LythonRuntime.NativeValueCapacity
            && VariadicListIndex < 0 && VariadicDictionaryIndex < 0
            && PositionalDefaults is null or PyTuple
            && KeywordDefaultStorage is null or { SupportsNativeStringReads: true };
    }

    [System.Runtime.CompilerServices.InlineArray(LythonRuntime.NativeValueCapacity)]
    private struct NativeValues { private LythonRuntime.ExecutableSlot _first; }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal void StartNativeCall(ExecutableCallSite call, ReadOnlySpan<LythonRuntime.ExecutableSlot> arguments, object? receiver,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool consumedByIteration,
        bool completePositional, Span<LythonRuntime.ExecutableSlot> unpackedReturn, out bool unpacked,
        out LythonRuntime.ExecutableSlot result, out LythonRuntime.ExecutionContext? frame)
    {
        if (receiver is null && completePositional && arguments.Length == 1 &&
            CodeObject.DirectDefinitionReturn is { } definition && !Closure.IsTypeParameterScope &&
            TryReturnDefinition(definition, arguments[0], span, out result))
        {
            unpacked = false;
            frame = null;
            return;
        }

        if (!completePositional && receiver is null && CodeObject.HasDirectIntegerReturn &&
            TryStartPreparedIntegerReturn(call, arguments, context, span, out result))
        {
            unpacked = false;
            frame = null;
            return;
        }

        // Both executable callers select eligibility before entry. Starting a
        // selected call completes or transfers its exact work; it never declines
        // after binding and asks the caller to try another execution route.
        NativeValues buffer = default;
        Span<LythonRuntime.ExecutableSlot> values = buffer;
        values = values[..CodeObject.ValueStorageCapacity];
        // This span is private to the synchronous prefix and cannot escape. Its
        // live values transfer before suspension; stack exit ends the remaining
        // references. Owned callback arrays keep their ordinary full clear.
        if (completePositional)
        {
            var first = 0;
            if (receiver is not null) values[first++].Object = receiver;
            arguments.CopyTo(values[first..]);
        }
        else LythonRuntime.BindFunctionArgumentsInto(values[..ParameterCount],
            [], span, this, context, receiver: receiver, callSite: call, operands: arguments);
        StartInline(values, null, span, consumedByIteration, unpackedReturn,
            out unpacked, out result, out frame, out _);
    }

    // This shape consumes the argument only as an integer. The caller can
    // keep its canonical slot private through the definition's admission.
    internal bool TryStartForwardedComputedDefinition(ExecutableCallSite call, LythonRuntime.ExecutableSlot argument,
        LythonRuntime.ExecutionContext scope, out LythonRuntime.ExecutableSlot result)
    {
        result = default;
        if (CodeObject.DirectDefinitionReturn is not { InputCount: > 0 } definition ||
            !TrySelectNativeCall(call, null, out var complete) || !complete) return false;
        scope.EnterInterpreterFrame(call.CallSpan);
        try
        {
            scope.CheckExecution(call.CallSpan);
            return TryReturnDefinition(definition, argument, call.CallSpan, out result);
        }
        catch (Exception error) when (LythonRuntime.TryTranslateCallableException(error, call.CallSpan, out var translated))
        { throw translated; }
        finally { scope.LeaveInterpreterFrame(); }
    }

    private bool TryReturnDefinition(ExecutableCreateFunction definition,
        LythonRuntime.ExecutableSlot argument, LythonSourceSpan span, out LythonRuntime.ExecutableSlot result)
    {
        result = default;
        object? keyword = null;
        long positional = 0;
        if (definition.InputCount != 0 &&
            ((keyword = _closureCells.FixedValue) is null ||
             !LythonRuntime.TryReadClosedDefinitionDefault(CodeObject, argument, out positional))) return false;
        try
        {
            Closure.EnterFunctionCall(span);
            var previous = Closure.State.CurrentException;
            try
            {
                var function = keyword is null
                    ? LythonRuntime.CreateClosedFunctionDefinition(definition, Closure, argument, CodeObject.Instructions[0].Span)
                    : LythonRuntime.CreateClosedComputedDefinition(definition, CodeObject, Closure, positional, keyword);
                Closure.CheckExecution(CodeObject.Instructions[^1].Span);
                result = LythonRuntime.ExecutableSlot.FromObject(function);
                return true;
            }
            catch (LythonRuntimeException error)
            {
                if (previous is not null && !ReferenceEquals(error.OriginalPythonException, previous))
                    error.PythonContext ??= previous;
                throw;
            }
            finally
            {
                Closure.State.SetCurrentException(previous);
                Closure.LeaveFunctionCall();
            }
        }
        catch (LythonRuntimeException error)
        {
            PyFunctionBinding.AnnotateException(error, Closure, Name, span);
            throw;
        }
    }

    internal bool TryStartPreparedIntegerReturn(ExecutableCallSite call, ReadOnlySpan<LythonRuntime.ExecutableSlot> arguments,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span, out LythonRuntime.ExecutableSlot result)
    {
        result = default;
        // An exposed keyword mapping can run comparisons during binding.
        // Decline before counting or reading the positional defaults.
        if (!call.IsPositionalOnly || ParameterCount != CodeObject.FirstTemporarySlot ||
            !HasPreparedOmittedArguments(arguments.Length)) return false;
        context.CheckExecution(span);
        return TryStartDirectIntegerReturn(new LythonRuntime.DefaultIntegerOperands(arguments, this), span, out result);
    }

    internal bool TryStartSuppliedIntegerReturn(ReadOnlySpan<LythonRuntime.ExecutableSlot> arguments,
        object? receiver, LythonSourceSpan span, out LythonRuntime.ExecutableSlot result)
    {
        result = default;
        if (!CodeObject.HasDirectIntegerReturn ||
            arguments.Length + (receiver is null ? 0 : 1) != CodeObject.FirstTemporarySlot) return false;
        return receiver is null
            ? TryStartDirectIntegerReturn(new LythonRuntime.SlotIntegerOperands(arguments), span, out result)
            : TryStartDirectIntegerReturn(new LythonRuntime.BoundIntegerOperands(receiver, arguments), span, out result);
    }

    private bool TryStartDirectIntegerReturn<T>(T operands, LythonSourceSpan span, out LythonRuntime.ExecutableSlot result)
        where T : LythonRuntime.IIntegerOperands, allows ref struct
    {
        try
        {
            Closure.EnterFunctionCall(span);
            try
            {
                return LythonRuntime.TryEvaluateDirectIntegerReturn(CodeObject, operands, Closure, out result);
            }
            catch (LythonRuntimeException error)
            {
                var previous = Closure.State.CurrentException;
                if (previous is not null && !ReferenceEquals(error.OriginalPythonException, previous))
                    error.PythonContext ??= previous;
                throw;
            }
            finally { Closure.LeaveFunctionCall(); }
        }
        catch (LythonRuntimeException error)
        {
            PyFunctionBinding.AnnotateException(error, Closure, Name, span);
            throw;
        }
    }

    // The short callback argument span has already been evaluated. A complete
    // positional supply needs no defaults, collection construction or binding
    // workspace. Only a transferred body needs independently owned slots.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal bool TryStartPositionalCallback(ReadOnlySpan<CallArgumentValue> arguments, object? receiver,
        LythonSourceSpan span, out LythonRuntime.ExecutableSlot result, out LythonRuntime.ExecutionContext? frame,
        out BoundFunctionArguments owned)
    {
        result = default;
        frame = null;
        owned = default;
        if (!CanStartInline || !HasCompletePositionalSupply(arguments.Length, receiver)) return false;

        NativeValues buffer = default;
        Span<LythonRuntime.ExecutableSlot> values = buffer;
        var first = 0;
        if (receiver is not null) values[first++].Object = receiver;
        for (var i = 0; i < arguments.Length; i++)
        {
            if (!arguments[i].IsPositional) return false;
            values[first + i].Object = arguments[i].Value;
        }
        StartInline(values[..CodeObject.ValueStorageCapacity], null, span, false, default,
            out _, out result, out frame, out owned);
        return true;
    }

    internal override bool TryStartBoundInvocation(in BoundFunctionArguments bound, LythonSourceSpan span,
        out LythonRuntime.ExecutableSlot result, out LythonRuntime.ExecutionContext? frame)
        => TryStartBoundInvocation(bound, span, false, default, out _, out result, out frame);

    internal bool TryStartBoundInvocation(in BoundFunctionArguments bound, LythonSourceSpan span, bool consumedByIteration,
        Span<LythonRuntime.ExecutableSlot> unpackedReturn, out bool unpacked, out LythonRuntime.ExecutableSlot result, out LythonRuntime.ExecutionContext? frame)
    {
        if (CanRunNativePrefix)
        {
            StartInline(bound.Values.AsSpan(0, CodeObject.ValueStorageCapacity),
                bound, span, consumedByIteration, unpackedReturn, out unpacked, out result, out frame, out _);
            return true;
        }
        if (CanDeferInvocationContext)
        {
            frame = LythonRuntime.ExecutionContext.StartBorrowedInvocation(this, bound, span, consumedByIteration,
                unpackedReturn, out result, out unpacked);
            return true;
        }
        unpacked = false;
        result = default;
        frame = null;
        return false;
    }

    private void StartInline(Span<LythonRuntime.ExecutableSlot> values, BoundFunctionArguments? existing,
        LythonSourceSpan span, bool consumedByIteration, Span<LythonRuntime.ExecutableSlot> unpackedReturn, out bool unpacked,
        out LythonRuntime.ExecutableSlot result, out LythonRuntime.ExecutionContext? frame, out BoundFunctionArguments continuation)
    {
        unpacked = false;
        result = LythonRuntime.ExecutableSlot.FromObject(PyNone.Instance);
        frame = null;
        continuation = default;
        // Only this synchronous prefix can borrow primitive working storage.
        // Publication/transfer materializes stable guest objects before escape.
        // The common fixed buffer has a bounded managed stack cost; larger
        // bound calls borrow charged capacity until materialization and cleanup
        // finish. An idle loan stays funded until execution cleanup or eviction.
        var state = new LythonRuntime.NativeExecution(this, values, consumedByIteration, unpackedReturn.Length);
        BoundFunctionArguments owned = existing.GetValueOrDefault();
        var rented = false;
        try
        {
            if (state.Run(span, out result, unpackedReturn))
            {
                unpacked = state.ReturnedUnpacked;
                return;
            }
            state.PrepareTransfer();
            if (existing is null)
            {
                owned = LythonRuntime.RentFunctionArgumentStorage(this, span, Closure,
                    CodeObject.ValueStorageCapacity);
                rented = true;
                values[..state.LiveExtent].CopyTo(owned.Values);
            }
            frame = LythonRuntime.CreateNativeContinuation(this, owned, span, state.Index, state.LiveExtent,
                consumedByIteration, state.Failure);
            state.TransferOwnership(frame);
            continuation = owned;
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
                if (rented && !state.OwnershipTransferred)
                    LythonRuntime.ReturnBoundArguments(owned, CodeObject.ValueStorageCapacity);
            }
        }
    }

    protected override object ExecuteBody(
        LythonRuntime.ExecutionContext frame,
        BoundFunctionArguments boundArguments,
        LythonSourceSpan span)
    {
        return LythonRuntime.ExecuteExecutableCodeObject(CodeObject, frame, boundArguments, _closureCells, span, this)
            ?? PyNone.Instance;
    }

    protected override async ValueTask<object> ExecuteBodyAsync(
        LythonRuntime.ExecutionContext frame,
        BoundFunctionArguments boundArguments,
        LythonSourceSpan span)
    {
        return await LythonRuntime.ExecuteExecutableCodeObjectAsync(
            CodeObject, frame, boundArguments, _closureCells, span, this).ConfigureAwait(false)
            ?? PyNone.Instance;
    }
}
