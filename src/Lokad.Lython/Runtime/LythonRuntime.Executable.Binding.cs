using Lokad.Lython.Frontend;
using System.Collections;
using Lokad.Lython.Runtime.Text;
using System.Text.RegularExpressions;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object LoadLocal(ExecutableCodeObject codeObject, object[] locals, int slot, LythonSourceSpan span, ExecutionContext context)
    {
        var value = locals[slot];
        if (ReferenceEquals(value, UninitializedLocal))
        {
            if (!codeObject.IsFunctionScope && !codeObject.LocalNames[slot].StartsWith('<'))
            {
                if (context.Variables.TryGetValue(codeObject.LocalNames[slot], out var globalValue)) return globalValue;
                if (context.TryGetBuiltin(codeObject.LocalNames[slot], out var builtin)) return builtin;
            }
            throw codeObject.IsFunctionScope
                ? RuntimeErrors.UnboundLocalVariable(codeObject.LocalNames[slot], span)
                : RuntimeErrors.NameNotDefined(codeObject.LocalNames[slot], span);
        }

        return value;
    }

    private static bool TryLoadClosure(in ExecutableWorkState execution, ExecutionContext? context,
        int slot, LythonSourceSpan span, out object value)
    {
        var codeObject = execution.Code;
        if (execution.ClosureCells is not { } cells || (uint)slot >= (uint)cells.Count)
        {
            // Missing-cell/nonlocal resolution needs the actual invocation's
            // lexical scope. Present cells belong to the work, not its frame.
            if (context is null)
            {
                value = PyNone.Instance;
                return false;
            }
            if (codeObject.ScopeFacts.IsNonlocal(codeObject.ClosureNames[slot]))
            {
                value = ResolveName(codeObject.ClosureNames[slot], span, context);
                return true;
            }
            throw RuntimeErrors.FreeVariableNotAssociated(codeObject.ClosureNames[slot], span);
        }

        value = cells[slot].Value;
        if (ReferenceEquals(value, UninitializedLocal))
        {
            throw RuntimeErrors.FreeVariableNotAssociated(codeObject.ClosureNames[slot], span);
        }

        return true;
    }

    private static object ResolveExecutableGlobal(string name, LythonSourceSpan span, ExecutionContext context)
        => TryResolveExecutableGlobal(name, context, out var value)
            ? value : throw RuntimeErrors.NameNotDefined(name, span);

    private static bool TryResolveExecutableGlobal(string name, ExecutionContext context, [MaybeNullWhen(false)] out object value)
    {
        var globalContext = GetGlobalContext(context);
        if (globalContext.CurrentExecutableFrame is not null &&
            globalContext.CurrentExecutableFrame.TryResolveLocalOrClosure(name, out value))
        {
            return true;
        }

        if (globalContext.Variables.TryGetValue(name, out value))
        {
            return true;
        }

        return context.TryGetBuiltin(name, out value);
    }

    private static void StoreExecutableClosure(ExecutableCodeObject codeObject, ExecutionContext context, int slot, object value, LythonSourceSpan span)
    {
        var frame = context.CurrentExecutableFrame;
        if (frame is null || !frame.TryGetClosureCell(slot, out var cell))
        {
            if (codeObject.ScopeFacts.IsNonlocal(codeObject.ClosureNames[slot]))
            {
                StoreName(codeObject.ClosureNames[slot], value, context, span);
                return;
            }
            throw RuntimeErrors.NameNotDefined(codeObject.ClosureNames[slot], span);
        }

        cell.Value = value;
        StoreName(codeObject.ClosureNames[slot], value, context, span);
    }

    private static object ResolveExecutableName(string name, LythonSourceSpan span, ExecutionContext context)
        => ResolveName(name, span, context);

    private static void PropagatePendingAbrupt(PendingAbruptSignal pending)
    {
        switch (pending)
        {
            case PendingException exception:
                throw exception.Exception;
            case PendingReturn returned:
                throw new ReturnSignal(returned.Value);
            case PendingControl control:
                throw control.Control;
            default:
                throw new InvalidOperationException($"Unknown pending abrupt signal: {pending.GetType().Name}");
        }
    }

    private static bool TryHandleAbrupt(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        ref ExecutableValueStorage stack,
        int currentBlockIndex,
        PendingAbruptSignal abrupt,
        LythonSourceSpan span,
        ref PendingAbruptSignal? pendingAbrupt,
        ref int nextBlockIndex,
        out ExecutableExceptionRegion? matchedRegion)
    {
        matchedRegion = null;
        // Indexed to avoid boxing the region-list enumerator on every
        // routing scan (same order, no disposal semantics).
        var regions = codeObject.ExceptionRegions;
        for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
        {
            var region = regions[regionIndex];
            if (currentBlockIndex < region.ProtectedStartIndex ||
                currentBlockIndex > region.ProtectedEndIndex)
            {
                continue;
            }

            if (abrupt is PendingJump jump && jump.TargetIndex >= region.ProtectedStartIndex && jump.TargetIndex <= region.ProtectedEndIndex)
                continue;

            if (abrupt is PendingException { Exception: var exception } &&
                region.ExceptIndex is int exceptBlock)
            {
                RestoreExecutableStackForHandler(region, ref stack, span);
                pendingAbrupt = null;
                matchedRegion = region;
                var pyException = CreatePythonExceptionInstance(exception);
                UnwindAbandonedHandlerVars(context, span, exceptBlock);

                context.State.SetCurrentException(pyException);
                nextBlockIndex = exceptBlock;
                return true;
            }

            if (region.FinallyIndex is int finallyBlock)
            {
                RestoreExecutableStackForHandler(region, ref stack, span);
                abrupt.CleanupId = region.CleanupId;
                pendingAbrupt = abrupt;
                nextBlockIndex = finallyBlock;
                matchedRegion = region;
                UnwindAbandonedHandlerVars(context, span, finallyBlock);
                return true;
            }
            // A non-matching inner handler does not intercept the exception. Continue with
            // the next enclosing protected range, just as CPython unwinds nested try suites.
        }

        if (abrupt is PendingException)
            pendingAbrupt = abrupt;
        return false;
    }

    // BindException tracks handler variables with their suite range. Unwinding pops
    // every entry whose suite the propagation target leaves.
    private static void UnwindAbandonedHandlerVars(ExecutionContext context, LythonSourceSpan span, int targetBlockIndex)
    {
        while (context.ActiveHandlerVariables is { Count: > 0 } stack)
        {
            var top = stack.Peek();
            if (top.SuiteStart <= targetBlockIndex && targetBlockIndex <= top.SuiteEnd)
            {
                break;
            }

            _ = DeleteName(stack.Pop().Name, context, span);
        }
    }

    internal static void AbandonActiveHandlerVars(ExecutionContext context, LythonSourceSpan span)
    {
        while (context.ActiveHandlerVariables is { Count: > 0 } stack)
        {
            _ = DeleteName(stack.Pop().Name, context, span);
        }
    }

    private static void RestoreExecutableStackForHandler(
        ExecutableExceptionRegion region,
        ref ExecutableValueStorage stack,
        LythonSourceSpan span)
    {
        var targetDepth = region.StackDepth;
        if (stack.LiveExtent < targetDepth)
        {
            throw RuntimeErrors.Runtime("executable exception handler stack is invalid", span);
        }

        stack.RetireTo(targetDepth);
    }

    private static bool TryExecuteExecutableMatchCase(
        ExecutableMatchCaseBinding matchCase, object subject, ExecutionContext context,
        object[] locals, IReadOnlyList<ExecutableCell?>? localCells, Span<object> storage)
    {
        var pattern = matchCase.Pattern;
        storage = storage[..pattern.StorageCount];
        try
        {
            if (!TryMatchRootPattern(pattern, subject, context, storage)) return false;
            for (var i = 0; i < pattern.Bindings.Length; i++)
            {
                var value = storage[i];
                StoreName(pattern.Bindings[i].Name, value, context, matchCase.Span);
                var slot = matchCase.LocalBindingSlots[i];
                if (slot < 0) continue;
                locals[slot] = value;
                if (localCells?[slot] is ExecutableCell localCell) localCell.Value = value;
            }
            return true;
        }
        finally { storage.Clear(); }
    }

    private static void ExecuteExecutableImport(
        ExecutableCodeObject codeObject,
        ImportStatementSyntax importBinding,
        object[] locals,
        ExecutableCell?[]? localCells,
        ExecutionContext context)
    {
        if (importBinding.ModuleName == "__future__" && importBinding.ImportedMembers is not null &&
            ImportsOnlyFutureAnnotations(importBinding.ImportedMembers))
        {
            ExecuteImport(importBinding, context);
            return;
        }
        if (importBinding.ImportedMembers is null)
        {
            var module = ResolveImportedModuleHierarchy(importBinding.ModuleName, context, importBinding.Span);
            var boundModule = string.Equals(importBinding.BoundModuleName, importBinding.ModuleName, StringComparison.Ordinal)
                ? module
                : ResolveImportedModule(importBinding.BoundModuleName, context, importBinding.Span);
            AssignExecutableBoundName(codeObject, locals, localCells, importBinding.BindingName, boundModule, context, importBinding.Span);
            return;
        }

        var importedModule = ResolveImportedModuleHierarchy(importBinding.ModuleName, context, importBinding.Span);
        if (ImportSyntaxFacts.IsStarImport(importBinding.ImportedMembers))
        {
            foreach (var name in StarImportNames(importedModule, context, importBinding.Span))
            {
                if (!TryResolveImportedMember(importedModule, name, context, importBinding.Span, out var value))
                {
                    throw PyMemberAccess.CreateMissingMemberError(importedModule, name, importBinding.Span, context);
                }

                AssignExecutableBoundName(codeObject, locals, localCells, name, value, context, importBinding.Span);
            }

            return;
        }

        foreach (var importedMember in importBinding.ImportedMembers)
        {
            if (!TryResolveImportedMember(importedModule, importedMember.Name, context, importBinding.Span, out var value))
            {
                throw RuntimeErrors.CannotImportMember(importBinding.ModuleName, importedMember.Name, importBinding.Span);
            }

            AssignExecutableBoundName(codeObject, locals, localCells, importedMember.BindingName, value, context, importBinding.Span);
        }
    }

    // MG11: one cells-array object plus one reference slot per captured name.
    private const long ExecutableCellsArrayBaseBytes = 32;
    private const long ExecutableCellsArraySlotBytes = 8;

    private static async ValueTask ExecuteExecutableFunctionDefinitionAsync(
        ExecutableCodeObject codeObject,
        ExecutableFunctionBinding functionBinding,
        object[] locals,
        ExecutableCell?[]? localCells,
        ExecutionContext context,
        bool asynchronous)
    {
        var needsTypeMetadata = functionBinding.HasAnnotations || functionBinding.TypeParameters is not null || HasTypeParameterScope(context);
        context.EnterInterpreterFrame(functionBinding.Span);
        try
        {
            using var decoratorStorage = context.MemoryGovernor.ReserveTemporary(EstimateObjectArrayBytes(functionBinding.Decorators.Count), functionBinding.Span);
            var decorators = asynchronous
                ? await EvaluateDecoratorsAsync(functionBinding.Decorators, context).ConfigureAwait(false)
                : EvaluateDecorators(functionBinding.Decorators, context);
            var closureCells = CaptureExecutableClosures(functionBinding.CodeObject, context, functionBinding.Span);
            using var defaultStorage = ReserveDefaultArgumentsWorkingStorage(functionBinding.DefaultCount, context.MemoryGovernor, functionBinding.Span);
            var defaults = await EvaluateFunctionDefaults(functionBinding.Parameters, functionBinding.DefaultCount,
                context, asynchronous).ConfigureAwait(false);
            var typeScope = CreateTypeScope(functionBinding.TypeParameters, context, functionBinding.Span);
            var closure = typeScope.Closure.FunctionClosureContext;
            var function = functionBinding.IsGenerator
                ? (object)new PyGeneratorFunction(functionBinding.DeclaredName, functionBinding.ParameterLayout,
                    functionBinding.CodeObject, closure, defaults, functionBinding.CodeObject.ScopeFacts, closureCells)
                : new PyExecutableFunction(
                    functionBinding.DeclaredName,
                    functionBinding.ParameterLayout,
                    functionBinding.CodeObject,
                    closure,
                    closureCells,
                    defaults,
                    functionBinding.CodeObject.ScopeFacts);
            ChargeFunctionValue(context, functionBinding.Span);
            ChargeClosureRetention(
                closure,
                context.MemoryGovernor,
                functionBinding.Span);
            if (function is PyFunctionBase defined)
            {
                PyFunctionBase.CaptureFunctionDocstring(defined, functionBinding.Docstring, context, functionBinding.Span);
                if (needsTypeMetadata)
                {
                    // Metadata does not require interpreting an otherwise compiled body.
                    // Keep its existing scope, evaluation order and ownership before publication.
                    await AttachTypeMetadataAsync(defined, functionBinding.Parameters, functionBinding.ReturnAnnotation,
                        functionBinding.TypeParameters is not null, functionBinding.Span, typeScope, context, asynchronous).ConfigureAwait(false);
                }
            }
            var decorated = asynchronous
                ? await ApplyDecoratorsAsync(function, decorators, functionBinding.Span, context).ConfigureAwait(false)
                : ApplyDecorators(function, decorators, functionBinding.Span, context);
            AssignExecutableBoundName(codeObject, locals, localCells, functionBinding.Name, decorated, context, functionBinding.Span);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    // Pooled per-call argument boxes for the common arities, confined per
    // thread: tests run test classes in parallel, so a process-wide box
    // could be taken twice at once. The retention audit shows no live
    // caller-provided array reaches a retainer (the only storing callable,
    // PyMethodCaller, is built from a range slice), so take-null with
    // keep-first stays safe under recursion and suspension: each live use
    // exclusively owns its array. Boxes are scrubbed before caching so
    // reuse never leaks values.
    [ThreadStatic]
    private static CallArgumentValue[]? _pooledOneCallArgument;
    [ThreadStatic]
    private static CallArgumentValue[]? _pooledTwoCallArguments;
    [ThreadStatic]
    private static CallArgumentValue[]? _pooledThreeCallArguments;

    private static CallArgumentValue[] RentCallArguments(int count)
    {
        if (count == 1)
        {
            var rented = _pooledOneCallArgument;
            if (rented is not null)
            {
                _pooledOneCallArgument = null;
                return rented;
            }
            return new CallArgumentValue[1];
        }
        if (count == 2)
        {
            var rented = _pooledTwoCallArguments;
            if (rented is not null)
            {
                _pooledTwoCallArguments = null;
                return rented;
            }
            return new CallArgumentValue[2];
        }
        if (count == 3)
        {
            var rentedThree = _pooledThreeCallArguments;
            if (rentedThree is not null)
            {
                _pooledThreeCallArguments = null;
                return rentedThree;
            }
            return new CallArgumentValue[3];
        }
        return count == 0 ? Array.Empty<CallArgumentValue>() : new CallArgumentValue[count];
    }

    private static void ReturnCallArguments(CallArgumentValue[] arguments)
    {
        if (arguments.Length == 1)
        {
            arguments[0] = CallArgumentValue.Positional(PyNone.Instance);
            _pooledOneCallArgument ??= arguments;
        }
        else if (arguments.Length == 2)
        {
            arguments[0] = CallArgumentValue.Positional(PyNone.Instance);
            arguments[1] = CallArgumentValue.Positional(PyNone.Instance);
            _pooledTwoCallArguments ??= arguments;
        }
        else if (arguments.Length == 3)
        {
            arguments[0] = CallArgumentValue.Positional(PyNone.Instance);
            arguments[1] = CallArgumentValue.Positional(PyNone.Instance);
            arguments[2] = CallArgumentValue.Positional(PyNone.Instance);
            _pooledThreeCallArguments ??= arguments;
        }
        else if (arguments.Length > 3)
        {
            // These buffers are not pooled, but the completed call helper can
            // still hold the array until handing off its result. Withdraw every
            // input reference at the same boundary as the small pooled arities.
            arguments.AsSpan().Fill(CallArgumentValue.Positional(PyNone.Instance));
        }
    }

    private static CallArgumentValue[] PrepareExecutableCallArguments(
        ExecutableCallSite callSite, ref ExecutableValueStorage stack, int start, ExecutionContext context)
    {
        var arguments = RentCallArguments(callSite.ArgumentCount);
        try
        {
            for (var i = 0; i < callSite.ArgumentCount; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(callSite.CallSpan);
                arguments[i] = CreateExecutableCallArgument(callSite.Arguments[i], stack[start + i + callSite.ArgumentOffset]);
            }
            return arguments;
        }
        catch
        {
            ReturnCallArguments(arguments);
            throw;
        }
    }

    // Tiny cleared pools are control headroom. Larger native-call packets are
    // substantial private storage, distinct from the already-owned operands.
    // Keep their admission through the callable's actual completion and cleanup.
    private static MemoryGovernor.TemporaryMemoryReservation? ReserveExecutableCallArguments(
        ExecutableCallSite callSite, ExecutionContext context)
        => callSite.ArgumentCount <= 3 ? null : context.MemoryGovernor.ReserveTemporary(
            checked(32L + 32L * callSite.ArgumentCount), callSite.CallSpan);

    private static CallArgumentValue CreateExecutableCallArgument(ExecutableCallArgument spec, object value)
        => spec.Kind switch
        {
            CallArgumentKind.Positional => CallArgumentValue.Positional(value),
            CallArgumentKind.Keyword => CallArgumentValue.Keyword(spec.KeywordName, value),
            _ => throw new InvalidOperationException($"Executable call site contains unsupported argument kind {spec.Kind}.")
        };

    private static ExecutableCell[] CaptureExecutableClosures(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (codeObject.ClosureNames.Count == 0)
        {
            return [];
        }

        var frame = context.CurrentExecutableFrame;
        ExecutableCell Resolve(string name)
        {
            ExecutableCell? cell = null;
            if (frame?.TryGetCell(name, out cell) != true &&
                !(name == "__class__" && context.TryGetLexicalClassCell(out cell)))
            {
                throw RuntimeErrors.NameNotDefined(name, span);
            }

            return cell.RequireNotNull();
        }

        // Captured cells outlive the invocation frame through the function.
        // A comprehension already owns a live binding namespace. Identify that
        // owner without allocating cells before the array/cell preflight.
        var uncharged = 0;
        foreach (var name in codeObject.ClosureNames)
        {
            if (context.TryGetInlinedBinding(name, out _) || !Resolve(name).RetentionCharged)
            {
                uncharged++;
            }
        }

        var retainedBytes = checked(
            ExecutableCellsArrayBaseBytes +
            ExecutableCellsArraySlotBytes * (long)codeObject.ClosureNames.Count +
            48L * uncharged);
        context.MemoryGovernor.Reserve(retainedBytes, span);
        context.MemoryGovernor.Commit(retainedBytes);
        try
        {
            var cells = new ExecutableCell[codeObject.ClosureNames.Count];
            for (var i = 0; i < cells.Length; i++)
            {
                var name = codeObject.ClosureNames[i];
                cells[i] = context.TryGetInlinedBinding(name, out var binding)
                    ? new ExecutableCell(UninitializedLocal, binding, name) : Resolve(name);
            }
            // Lookups above have no callbacks or mutation. Publish only after
            // every allocation succeeds. Namespace-backed cells observe later
            // loop stores without mirroring the enclosing caller or a registry.
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i].RetentionCharged = true;
                if (context.TryGetInlinedBinding(codeObject.ClosureNames[i], out var binding))
                    binding.MirrorLocalStores = true;
            }
            return cells;
        }
        catch
        {
            context.MemoryGovernor.Release(retainedBytes);
            throw;
        }
    }

    // Lambdas resolve free names by walking parent Variables, but executable
    // frames keep locals in slots that vanish on return. Flagging the chain
    // mirrors slot stores into Variables (catch-up below seeds current values,
    // preferring shared cells), so escaped lambdas keep reading finals.
    internal static void RetainLocalsForLambda(ExecutionContext definingContext)
    {
        for (var current = definingContext; current is not null; current = current.ParentContext)
        {
            current.MirrorLocalStores = true;
            var frame = current.CurrentExecutableFrame;
            if (frame is null)
            {
                continue;
            }

            foreach (var pair in frame.EnumerateLocals())
            {
                if (frame.TryGetCell(pair.Key, out var cell) &&
                    cell is not null &&
                    !ReferenceEquals(cell.Value, UninitializedLocal))
                {
                    current.Variables[pair.Key] = cell.Value;
                }
                else
                {
                    current.Variables[pair.Key] = pair.Value;
                }
            }
        }
    }

    private static void SyncExecutableLocalsFromContext(
        ExecutableCodeObject codeObject,
        object[] locals,
        ExecutableCell?[]? localCells,
        ExecutionContext context)
    {
        for (var i = 0; i < codeObject.LocalNames.Count; i++)
        {
            // Compiler slots belong to this frame, rather than the guest
            // namespace. Fallback statements cannot rebind or delete them.
            if (codeObject.LocalNames[i].StartsWith('<')) continue;
            if (!context.Variables.TryGetValue(codeObject.LocalNames[i], out var value))
            {
                locals[i] = UninitializedLocal;
                if (localCells?[i] is ExecutableCell localCell)
                {
                    localCell.Value = UninitializedLocal;
                }

                continue;
            }

            locals[i] = value;
            if (localCells?[i] is ExecutableCell existingCell)
            {
                existingCell.Value = value;
            }
        }
    }

    private static void SyncExecutableLocalFromValue(
        ExecutableCodeObject codeObject,
        object[] locals,
        ExecutableCell?[]? localCells,
        string name,
        object value)
    {
        if (!codeObject.LocalNameToSlot.TryGetValue(name, out var slot))
        {
            return;
        }

        locals[slot] = value;
        if (localCells?[slot] is ExecutableCell localCell)
        {
            localCell.Value = value;
        }
    }

    private static void AssignExecutableBoundName(
        ExecutableCodeObject codeObject,
        object[] locals,
        ExecutableCell?[]? localCells,
        string name,
        object value,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (codeObject.ScopeFacts.IsGlobal(name))
        {
            StoreName(name, value, context, span);
            return;
        }

        if (codeObject.ScopeFacts.IsNonlocal(name))
        {
            if (codeObject.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                StoreExecutableClosure(codeObject, context, closureSlot, value, span);
                if (context.TryGetNonlocalTarget(name, out var nonlocalTarget) && nonlocalTarget.MirrorLocalStores)
                {
                    nonlocalTarget.Variables[name] = value;
                }
                return;
            }

            StoreName(name, value, context, span);
            return;
        }

        if (context.GlobalNamespace is not null)
        {
            StoreGlobalNamespaceName(context, name, value);
            return;
        }
        SyncExecutableLocalFromValue(codeObject, locals, localCells, name, value);
        // Module declarations must survive this executable frame: retained
        // functions/imports and later code in the same namespace still read them.
        // Their slots accelerate live execution without replacing that ownership.
        if (!codeObject.IsFunctionScope || codeObject.RequiresLocalVariableMirroring || context.MirrorLocalStores || !codeObject.LocalNameToSlot.ContainsKey(name))
        {
            context.Variables[name] = value;
        }
    }

}
