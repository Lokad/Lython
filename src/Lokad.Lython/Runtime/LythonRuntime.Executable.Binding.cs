using Lokad.Lython.Frontend;
using System.Collections;
using Lokad.Lython.Runtime.Text;
using System.Text.RegularExpressions;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Captured local slots hold their authoritative cell. Cells are internal
    // storage and never guest values; temporary operands always hold the value.
    private static object? ReadLocalValue(ExecutableSlot[] locals, int slot)
    {
        var value = locals[slot].Object;
        return value is ExecutableCell cell ? cell.Value : value;
    }

    private static void WriteLocalValue(ExecutableSlot[] locals, int slot, object? value)
    {
        if (locals[slot].Reference is ExecutableCell cell) cell.Value = value;
        else locals[slot].Object = value!;
    }

    private static object LoadLocal(ExecutableCodeObject codeObject, ExecutableSlot[] locals, int slot, LythonSourceSpan span, ExecutionContext context)
    {
        var value = ReadLocalValue(locals, slot);
        return value ?? LoadMissingLocal(codeObject, slot, span, context);
    }

    private static object LoadMissingLocal(ExecutableCodeObject codeObject, int slot,
        LythonSourceSpan span, ExecutionContext context)
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

    // Executable captures carry either the singleton cell itself or the
    // owned array. Both interpreters read that storage directly.
    private static ExecutableCell? ReadCapturedCell(IReadOnlyList<ExecutableCell>? cells, int slot)
    {
        if (cells is ExecutableCell singleton) return slot == 0 ? singleton : null;
        return cells is ExecutableCell[] array && (uint)slot < (uint)array.Length ? array[slot] : null;
    }

    private static bool TryLoadClosure(in ExecutableWorkState execution, ExecutionContext? context,
        int slot, LythonSourceSpan span, out object value)
    {
        var codeObject = execution.Code;
        if (ReadCapturedCell(execution.ClosureCells, slot) is not { } cell)
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

        var captured = cell.Value;
        if (captured is null)
        {
            throw RuntimeErrors.FreeVariableNotAssociated(codeObject.ClosureNames[slot], span);
        }

        value = captured;
        return true;
    }

    private static object ResolveExecutableGlobal(ExecutableCodeObject code, int nameIndex, LythonSourceSpan span, ExecutionContext context)
        => TryResolveExecutableGlobal(code, nameIndex, context, out var value)
            ? value : throw RuntimeErrors.NameNotDefined(code.Names[nameIndex], span);

    private static bool TryResolveExecutableGlobal(ExecutableCodeObject code, int nameIndex, ExecutionContext context,
        [MaybeNullWhen(false)] out object value)
    {
        var name = code.Names[nameIndex];
        var globalContext = GetGlobalContext(context);
        if (globalContext.CurrentExecutableFrame is { } frame)
        {
            if (ReferenceEquals(frame._execution.Code, code.GlobalModule))
            {
                var slot = code.ModuleGlobalSlots[nameIndex];
                if (slot >= 0 && ReadLocalValue(frame._execution.Locals, slot) is { } current)
                { value = current; return true; }
            }
            else if (frame.TryResolveLocalOrClosure(name, out value)) return true;
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
        if (ReadCapturedCell(execution.ClosureCells, slot) is { CanStoreDirectly: true } cell)
        {
            var value = execution.Stack.TakeValue(instruction.InputStart, instruction.LiveExtent);
            cell.Value = value;
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

        if (cell.CanStoreDirectly) cell.Value = value;
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
        ExecutableSlot[] locals, Span<ExecutableSlot> storage)
    {
        var pattern = matchCase.Pattern;
        storage = storage[..pattern.StorageCount];
        try
        {
            if (!TryMatchRootPattern(pattern, subject, context, storage)) return false;
            for (var i = 0; i < pattern.Bindings.Length; i++)
            {
                var value = storage[i].Object.RequireNotNull();
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
        ExecutableSlot[] locals,
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

    // Small copied packets use minor control headroom. Wider copies are admitted
    // through completion, independently of the original evaluated operands.
    internal static MemoryGovernor.TemporaryMemoryReservation? ReserveCallArgumentStorage(
        int count, ExecutionContext context, LythonSourceSpan span)
        => count <= 3 ? null : context.MemoryGovernor.ReserveTemporary(
            checked(32L + 32L * count), span);

    // Bound wrappers supply fresh retainable prefixes. Unlike invocation scratch,
    // their major capacity stays charged until the execution/result owner ends.
    internal static CallArgumentValue[] AllocateRetainedBoundArguments(
        int count, ExecutionContext context, LythonSourceSpan span)
    {
        context.CheckExecution(span);
        var bytes = count <= 3 ? 0 : checked(32L + 32L * count);
        context.MemoryGovernor.Reserve(bytes, span);
        try
        {
            context.CheckExecution(span);
            var arguments = new CallArgumentValue[count];
            context.MemoryGovernor.Commit(bytes);
            return arguments;
        }
        catch { context.MemoryGovernor.ReleaseReserved(bytes); throw; }
    }

    private static CallArgumentValue CreateExecutableCallArgument(CallArgumentForm spec, object value)
        => spec.Kind switch
        {
            CallArgumentKind.Positional => CallArgumentValue.Positional(value),
            CallArgumentKind.Keyword => CallArgumentValue.Keyword(spec.KeywordName, value),
            _ => throw new InvalidOperationException($"Executable call site contains unsupported argument kind {spec.Kind}.")
        };

    private static IReadOnlyList<ExecutableCell> CaptureExecutableClosures(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (codeObject.ClosureNames.Count == 0)
        {
            return [];
        }

        var frame = context.CurrentExecutableFrame;
        var slots = frame is not null && ReferenceEquals(context, context.FunctionClosureContext) &&
            codeObject.CaptureSources is { } sources && ReferenceEquals(sources.Parent, frame._execution.Code)
            ? sources.Slots : null;
        var names = codeObject.ClosureNames;
        ExecutableCell Resolve(int index)
        {
            if (slots is not null)
            {
                var slot = slots[index];
                return slot >= 0 ? (ExecutableCell)frame!._execution.Locals[slot].Reference!
                    : frame!._execution.ClosureCells![~slot];
            }
            var name = names[index];
            ExecutableCell? cell = null;
            if (frame?.TryGetCell(name, out cell) != true &&
                !(name == "__class__" && context.TryGetLexicalClassCell(out cell)))
            {
                throw RuntimeErrors.NameNotDefined(name, span);
            }

            return cell.RequireNotNull();
        }

        // Existing locals are funded by their frame. Only a comprehension's
        // new namespace-backed cells and the capture array need admission.
        var newCells = 0;
        if (slots is null)
        {
            for (var i = 0; i < names.Count; i++)
            {
                if (i != 0 && (i & 255) == 0) context.CheckExecution(span);
                if (context.TryGetInlinedBinding(names[i], out _)) newCells++;
                else _ = Resolve(i);
            }
        }

        var arrayBytes = names.Count == 1 ? 0 : checked(ExecutableCellsArrayBaseBytes +
            ExecutableCellsArraySlotBytes * (long)names.Count);
        var retainedBytes = checked(arrayBytes + ExecutableCell.StorageBytes * newCells);
        context.MemoryGovernor.Reserve(retainedBytes, span);
        context.MemoryGovernor.Commit(retainedBytes);
        try
        {
            context.CheckExecution(span);
            if (names.Count == 1)
            {
                var cell = Capture(0);
                Publish(0, cell);
                return cell;
            }

            var cells = new ExecutableCell[names.Count];
            for (var i = 0; i < cells.Length; i++)
            {
                if (i != 0 && (i & 255) == 0) context.CheckExecution(span);
                cells[i] = Capture(i);
            }
            // Publish only after every allocation succeeds. Namespace-backed
            // cells observe later loop stores through the existing owner.
            for (var i = 0; i < cells.Length; i++)
                Publish(i, cells[i]);
            return cells;
        }
        catch
        {
            context.MemoryGovernor.Release(retainedBytes);
            throw;
        }

        ExecutableCell Capture(int index)
            => slots is null && context.TryGetInlinedBinding(names[index], out var binding)
                ? new ExecutableCell(null, binding, names[index]) : Resolve(index);

        void Publish(int index, ExecutableCell cell)
        {
            if (cell.RetentionCharged) return;
            if (slots is null && context.TryGetInlinedBinding(names[index], out var binding))
                binding.MirrorLocalStores = true;
            else
                frame!._execution.Stack.TransferCapturedCellCharge();
            cell.RetentionCharged = true;
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
        ExecutableSlot[] locals,
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
        ExecutableSlot[] locals,
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
        ExecutableSlot[] locals,
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
