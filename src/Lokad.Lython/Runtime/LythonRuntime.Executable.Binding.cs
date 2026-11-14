using Lokad.Lython.Frontend;
using System.Collections;
using Lokad.Lython.Runtime.Text;
using System.Text.RegularExpressions;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Captured local slots hold their authoritative cell. Cells are internal
    // storage and never guest values; temporary operands always hold the value.
    private static object? ReadLocalValue(object[] locals, int slot)
    {
        var value = locals[slot];
        return value is ExecutableCell cell ? cell.Value : value;
    }

    private static void WriteLocalValue(object[] locals, int slot, object? value)
    {
        if (locals[slot] is ExecutableCell cell) cell.Value = value;
        else locals[slot] = value!;
    }

    private static object LoadLocal(ExecutableCodeObject codeObject, object[] locals, int slot, LythonSourceSpan span, ExecutionContext context)
    {
        var value = ReadLocalValue(locals, slot);
        if (value is null)
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

        var captured = cells[slot].Value;
        if (captured is null)
        {
            throw RuntimeErrors.FreeVariableNotAssociated(codeObject.ClosureNames[slot], span);
        }

        value = captured;
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

    private static bool TryStoreClosure(ref ExecutableWorkState execution, ExecutionContext? context,
        in ExecutableInstruction instruction)
    {
        var slot = instruction.ClosureSlot;
        if (execution.ClosureCells is { } cells && (uint)slot < (uint)cells.Count && cells[slot].CanStoreThroughOwner)
        {
            var value = execution.Stack.TakeValue(instruction.InputStart, instruction.LiveExtent);
            cells[slot].Value = value;
            return true;
        }
        if (context is null) return false;
        StoreExecutableClosure(execution.Code, context, slot,
            execution.Stack.TakeValue(instruction.InputStart, instruction.LiveExtent), instruction.Span);
        return true;
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

        if (cell.CanStoreThroughOwner) cell.Value = value;
        else
        {
            cell.Value = value;
            StoreName(codeObject.ClosureNames[slot], value, context, span);
        }
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
        object[] locals, Span<object> storage)
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
                WriteLocalValue(locals, slot, value);
            }
            return true;
        }
        finally { storage.Clear(); }
    }

    private static void ExecuteExecutableImport(
        ExecutableCodeObject codeObject,
        ImportStatementSyntax importBinding,
        object[] locals,
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
            AssignExecutableBoundName(codeObject, locals, importBinding.BindingName, boundModule, context, importBinding.Span);
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

                AssignExecutableBoundName(codeObject, locals, name, value, context, importBinding.Span);
            }

            return;
        }

        foreach (var importedMember in importBinding.ImportedMembers)
        {
            if (!TryResolveImportedMember(importedModule, importedMember.Name, context, importBinding.Span, out var value))
            {
                throw RuntimeErrors.CannotImportMember(importBinding.ModuleName, importedMember.Name, importBinding.Span);
            }

            AssignExecutableBoundName(codeObject, locals, importedMember.BindingName, value, context, importBinding.Span);
        }
    }

    // MG11: one cells-array object plus one reference slot per captured name.
    private const long ExecutableCellsArrayBaseBytes = 32;
    private const long ExecutableCellsArraySlotBytes = 8;

    private static async ValueTask ExecuteExecutableFunctionDefinitionAsync(
        ExecutableCodeObject codeObject,
        ExecutableFunctionBinding functionBinding,
        object[] locals,
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
            AssignExecutableBoundName(codeObject, locals, functionBinding.Name, decorated, context, functionBinding.Span);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static void PrepareExecutableCallArguments(
        ExecutableCallSite callSite, ref ExecutableValueStorage stack, int start,
        Span<CallArgumentValue> arguments, ExecutionContext context)
    {
        for (var i = 0; i < callSite.ArgumentCount; i++)
        {
            if ((i & 1023) == 0) context.CheckExecution(callSite.CallSpan);
            arguments[i] = CreateExecutableCallArgument(callSite.Arguments[i], stack[start + i + callSite.ArgumentOffset]);
        }
    }

    // Short owned constructor packets use minor control headroom. Wider packets
    // are admitted through completion, independently of their existing operands.
    private static MemoryGovernor.TemporaryMemoryReservation? ReserveExecutableCallArguments(
        ExecutableCallSite callSite, ExecutionContext context)
        => callSite.ArgumentCount <= 3 ? null : context.MemoryGovernor.ReserveTemporary(
            checked(32L + 32L * callSite.ArgumentCount), callSite.CallSpan);

    private static CallArgumentValue CreateExecutableCallArgument(CallArgumentForm spec, object value)
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
            ExecutableCell.StorageBytes * uncharged);
        context.MemoryGovernor.Reserve(retainedBytes, span);
        context.MemoryGovernor.Commit(retainedBytes);
        try
        {
            var cells = new ExecutableCell[codeObject.ClosureNames.Count];
            for (var i = 0; i < cells.Length; i++)
            {
                var name = codeObject.ClosureNames[i];
                cells[i] = context.TryGetInlinedBinding(name, out var binding)
                    ? new ExecutableCell(null, binding, name) : Resolve(name);
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

    // Namespace-backed closures outlive executable workspaces. Seed the current
    // values before switching cells to namespace reads; there is no slot cache
    // to recover a value after the namespace becomes authoritative.
    internal static void RetainLocalsForLambda(ExecutionContext definingContext)
    {
        for (var current = definingContext; current is not null; current = current.ParentContext)
        {
            var frame = current.CurrentExecutableFrame;
            if (frame is not null)
            {
                foreach (var pair in frame.EnumerateLocals())
                    current.Variables[pair.Key] = pair.Value;
            }
            current.MirrorLocalStores = true;
        }
    }

    private static void SyncExecutableLocalsFromContext(
        ExecutableCodeObject codeObject,
        object[] locals,
        ExecutionContext context)
    {
        for (var i = 0; i < codeObject.LocalNames.Count; i++)
        {
            if ((i & 63) == 0) context.CheckExecution(context.CurrentInstructionSpan);
            // Compiler slots belong to this frame, rather than the guest
            // namespace. Fallback statements cannot rebind or delete them.
            if (codeObject.LocalNames[i].StartsWith('<')) continue;
            context.Variables.TryGetValue(codeObject.LocalNames[i], out var value);
            WriteLocalValue(locals, i, value);
        }
    }

    private static void SyncExecutableLocalFromValue(
        ExecutableCodeObject codeObject,
        object[] locals,
        string name,
        object value)
    {
        if (!codeObject.LocalNameToSlot.TryGetValue(name, out var slot))
        {
            return;
        }

        WriteLocalValue(locals, slot, value);
    }

    private static void AssignExecutableBoundName(
        ExecutableCodeObject codeObject,
        object[] locals,
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
        SyncExecutableLocalFromValue(codeObject, locals, name, value);
        // Module declarations must survive this executable frame: retained
        // functions/imports and later code in the same namespace still read them.
        // Their slots accelerate live execution without replacing that ownership.
        if (!codeObject.IsFunctionScope || codeObject.RequiresLocalVariableMirroring || context.MirrorLocalStores || !codeObject.LocalNameToSlot.ContainsKey(name))
        {
            context.Variables[name] = value;
        }
    }

}
