using Lokad.Lython.Frontend;
using System.Collections;
using Lokad.Lython.Runtime.Text;
using System.Text.RegularExpressions;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object LoadLocal(ExecutableCodeObject codeObject, object[] locals, int slot, LythonSourceSpan span)
    {
        var value = locals[slot];
        if (ReferenceEquals(value, UninitializedLocal))
        {
            throw RuntimeErrors.NameNotDefined(codeObject.LocalNames[slot], span);
        }

        return value;
    }

    private static object LoadClosure(ExecutableCodeObject codeObject, ExecutionContext context, int slot, LythonSourceSpan span)
    {
        var frame = context.CurrentExecutableFrame;
        if (frame is null || !frame.TryGetClosureCell(slot, out var cell))
        {
            throw RuntimeErrors.FreeVariableNotAssociated(codeObject.ClosureNames[slot], span);
        }

        if (ReferenceEquals(cell.Value, UninitializedLocal))
        {
            throw RuntimeErrors.FreeVariableNotAssociated(codeObject.ClosureNames[slot], span);
        }

        return cell.Value;
    }

    private static object ResolveExecutableGlobal(string name, LythonSourceSpan span, ExecutionContext context)
    {
        var globalContext = GetGlobalContext(context);
        if (globalContext.CurrentExecutableFrame is not null &&
            globalContext.CurrentExecutableFrame.TryResolveLocalOrClosure(name, out var executableValue))
        {
            return executableValue;
        }

        if (globalContext.Variables.TryGetValue(name, out var value))
        {
            return value;
        }

        throw RuntimeErrors.NameNotDefined(name, span);
    }

    private static void StoreExecutableClosure(ExecutableCodeObject codeObject, ExecutionContext context, int slot, object value, LythonSourceSpan span)
    {
        var frame = context.CurrentExecutableFrame;
        if (frame is null || !frame.TryGetClosureCell(slot, out var cell))
        {
            throw RuntimeErrors.NameNotDefined(codeObject.ClosureNames[slot], span);
        }

        cell.Value = value;
        StoreName(codeObject.ClosureNames[slot], value, context, span);
    }

    private static object ResolveExecutableName(string name, LythonSourceSpan span, ExecutionContext context)
    {
        for (var current = context; current is not null; current = current.ParentContext)
        {
            if (current.CurrentExecutableFrame is not null &&
                current.CurrentExecutableFrame.TryResolveLocalOrClosure(name, out var executableValue))
            {
                return executableValue;
            }

            if (current.Variables.TryGetValue(name, out var value))
            {
                return value;
            }
        }

        throw RuntimeErrors.NameNotDefined(name, span);
    }

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
        ExecutableValueStack stack,
        IReadOnlyList<int?> blockEntryStackDepths,
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
            if (currentBlockIndex < region.ProtectedStartBlockIndex ||
                currentBlockIndex > region.ProtectedEndBlockIndex)
            {
                continue;
            }

            if (abrupt is PendingException { Exception: var exception } &&
                region.ExceptBlockIndex is int exceptBlock &&
                MatchesCaughtException(region.ExceptionTypeNames, exception, context, span))
            {
                RestoreExecutableStackForHandler(region, stack, blockEntryStackDepths, span);
                pendingAbrupt = null;
                matchedRegion = region;
                var pyException = CreatePythonExceptionInstance(exception);
                UnwindAbandonedHandlerVars(context, span, exceptBlock);
                if (region.ExceptionVariableName is not null)
                {
                    ChargeBoundException(context.MemoryGovernor, span);
                    StoreName(region.ExceptionVariableName, pyException, context, span);
                    PushActiveHandlerVar(context, region);
                }

                context.Services.SetCurrentException(pyException);
                nextBlockIndex = exceptBlock;
                return true;
            }

            if (region.FinallyBlockIndex is int finallyBlock)
            {
                RestoreExecutableStackForHandler(region, stack, blockEntryStackDepths, span);
                pendingAbrupt = abrupt;
                nextBlockIndex = finallyBlock;
                matchedRegion = region;
                UnwindAbandonedHandlerVars(context, span, finallyBlock);
                return true;
            }
            // A non-matching inner handler does not intercept the exception. Continue with
            // the next enclosing protected range, just as CPython unwinds nested try suites.
        }

        return false;
    }

    // Handler variables die with their suite like CPython deleting them on
    // exit: pushes track entries with their suite range, and unwinding pops
    // every entry whose suite the propagation target leaves.
    private static void PushActiveHandlerVar(ExecutionContext context, ExecutableExceptionRegion region)
    {
        context.ActiveHandlerVariables ??= new Stack<(string Name, int SuiteStart, int SuiteEnd)>();
        context.ActiveHandlerVariables.Push((
            region.ExceptionVariableName!,
            region.SuiteStartBlockIndex ?? 0,
            region.SuiteEndBlockIndex ?? int.MaxValue));
    }

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
        ExecutableValueStack stack,
        IReadOnlyList<int?> blockEntryStackDepths,
        LythonSourceSpan span)
    {
        var targetDepth = blockEntryStackDepths[region.ProtectedStartBlockIndex]
            ?? throw RuntimeErrors.Runtime("executable exception region has no entry stack depth", span);
        if (stack.Count < targetDepth)
        {
            throw RuntimeErrors.Runtime("executable exception handler stack is invalid", span);
        }

        stack.RemoveTail(stack.Count - targetDepth);
    }

    private static bool TryExecuteExecutableMatchCase(
        ExecutableMatchCaseBinding matchCase,
        object subject,
        ExecutionContext context,
        object[] locals,
        IReadOnlyList<ExecutableCell?>? localCells)
    {
        var bindings = new Dictionary<string, object>(StringComparer.Ordinal);
        if (!TryMatchPattern(matchCase.Case.Pattern, subject, context, bindings))
        {
            return false;
        }

        if (matchCase.Case.Guard is not null)
        {
            var guardContext = new ExecutionContext(context);
            foreach (var pair in bindings)
            {
                guardContext.Variables[pair.Key] = pair.Value;
            }

            if (!IsTruthy(EvaluateExpression(matchCase.Case.Guard, guardContext)))
            {
                return false;
            }
        }

        foreach (var pair in bindings)
        {
            StoreName(pair.Key, pair.Value, context, matchCase.Case.Span);
            if (matchCase.LocalBindingSlots.TryGetValue(pair.Key, out var slot))
            {
                locals[slot] = pair.Value;
                if (localCells?[slot] is ExecutableCell localCell)
                {
                    localCell.Value = pair.Value;
                }
            }
        }

        return true;
    }

    private static void ExecuteExecutableImport(
        ExecutableCodeObject codeObject,
        ExecutableImportBinding importBinding,
        object[] locals,
        ExecutableCell?[]? localCells,
        ExecutionContext context)
    {
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
            foreach (var name in importedModule.ExportedNames)
            {
                if (!TryResolveImportedMember(importedModule, name, context, importBinding.Span, out var value))
                {
                    throw RuntimeErrors.CannotImportMember(importBinding.ModuleName, name, importBinding.Span);
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

    private static void ExecuteExecutableFunctionDefinition(
        ExecutableCodeObject codeObject,
        ExecutableFunctionBinding functionBinding,
        object[] locals,
        ExecutableCell?[]? localCells,
        ExecutionContext context)
    {
        context.EnterInterpreterFrame(functionBinding.Function.Span);
        try
        {
            var (closureCells, closureCellBytes) = functionBinding.CodeObject is null
                ? ([], 0L)
                : CaptureExecutableClosures(functionBinding.CodeObject, context, functionBinding.Function.Span);
            var function = functionBinding.CodeObject is null
                ? (object)new PyFunction(
                    functionBinding.Function.Syntax.Name,
                    functionBinding.Function.Parameters,
                    functionBinding.Function.Body,
                    context.FunctionClosureContext,
                    BuildDefaultArgumentMap(functionBinding.Function.Parameters, expression => EvaluateLoweredExpression(expression, context)),
                    ScopeDirectiveFactsCollector.ForFunction(functionBinding.Function.Syntax))
                : new PyExecutableFunction(
                    functionBinding.Function.Syntax.Name,
                    functionBinding.Function.Parameters,
                    functionBinding.CodeObject,
                    context.FunctionClosureContext,
                    closureCells,
                    MaterializeExecutableDefaultValues(functionBinding.DefaultValues, context),
                    functionBinding.CodeObject.ScopeFacts);
            ChargeFunctionValue(context, functionBinding.Function.Span);
            ChargeDefaultArguments(functionBinding.DefaultValues.Count, context.MemoryGovernor, functionBinding.Function.Span);
            var closureRetentionBytes = ChargeClosureRetention(
                context.FunctionClosureContext,
                context.MemoryGovernor,
                functionBinding.Function.Span);
            var docstringBytes = function is PyFunctionBase defined
                ? PyFunctionBase.CaptureFunctionDocstring(defined, functionBinding.Function.Body, context, functionBinding.Function.Span)
                : 0;
            TrackFunctionValue(function, functionBinding.DefaultValues.Count, closureRetentionBytes + closureCellBytes, docstringBytes, context, functionBinding.Function.Span);
            var decorated = ApplyDecorators(function, functionBinding.Function.Decorators, functionBinding.Function.Span, context);
            AssignExecutableBoundName(codeObject, locals, localCells, functionBinding.Function.Syntax.Name, decorated, context, functionBinding.Function.Span);
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
    }

    private static object ExecuteExecutableCall(
        ExecutableCallSite callSite,
        ExecutableValueStack stack,
        ExecutionContext context,
        ref ExecutableCallCache? cache)
    {
        var valueCount = callSite.ArgumentCount + 1;
        if (valueCount < 0 || stack.Count < valueCount)
        {
            throw RuntimeErrors.Runtime("executable stack underflow", callSite.CallSpan);
        }

        var start = stack.Count - valueCount;
        var target = stack[start];
        var arguments = RentCallArguments(callSite.ArgumentCount);
        for (var i = 0; i < callSite.ArgumentCount; i++)
        {
            var spec = callSite.Arguments[i];
            if (spec.Kind is CallArgumentKind.StarredList or CallArgumentKind.StarredDictionary)
            {
                throw new InvalidOperationException($"Executable call site contains unsupported argument kind {spec.Kind}.");
            }

            arguments[i] = spec.Kind == CallArgumentKind.Keyword
                ? CallArgumentValue.Keyword(spec.KeywordName, stack[start + i + 1])
                : CallArgumentValue.Positional(stack[start + i + 1]);
        }

        stack.RemoveTail(valueCount);

        try
        {
            if (cache is not null && ReferenceEquals(cache.Target, target))
            {
                return InvokeCallableTarget(cache.Callable, callSite.TargetSpan, callSite.CallSpan, context, arguments);
            }

            if (target is ICallable callable)
            {
                cache = new ExecutableCallCache(target, callable);
                return InvokeCallableTarget(callable, callSite.TargetSpan, callSite.CallSpan, context, arguments);
            }

            cache = null;
            return RuntimeValue(InvokeCallableTarget(target, callSite.TargetSpan, callSite.CallSpan, context, arguments));
        }
        finally
        {
            ReturnCallArguments(arguments);
        }
    }

    private static (ExecutableCell[] Cells, long RetainedBytes) CaptureExecutableClosures(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (codeObject.ClosureNames.Count == 0)
        {
            return ([], 0);
        }

        var frame = context.CurrentExecutableFrame;
        if (frame is null)
        {
            throw RuntimeErrors.NameNotDefined(codeObject.ClosureNames[0], span);
        }

        var cells = new ExecutableCell[codeObject.ClosureNames.Count];
        for (var i = 0; i < codeObject.ClosureNames.Count; i++)
        {
            var name = codeObject.ClosureNames[i];
            if (!frame.TryGetCell(name, out var cell))
            {
                throw RuntimeErrors.NameNotDefined(name, span);
            }

            cells[i] = cell;
        }

        // MG11: captured cells outlive the invocation frame through the new
        // function value, so the cells array plus each newly retained cell is
        // owned here. Marks commit after the charge, so a tripped attempt pays
        // nothing and a retry fails closed again instead of slipping through.
        var uncharged = 0;
        foreach (var cell in cells)
        {
            if (!cell.RetentionCharged)
            {
                uncharged++;
            }
        }

        var retainedBytes = checked(
            ExecutableCellsArrayBaseBytes +
            ExecutableCellsArraySlotBytes * (long)cells.Length +
            ClosureCellSlotBytes * (long)uncharged);
        context.MemoryGovernor.Reserve(retainedBytes, span);
        context.MemoryGovernor.Commit(retainedBytes);
        // The array travels on the new function value (same lifetime, same first-wins
        // aliasing as contexts above), so the caller folds the delta into the coupon.
        foreach (var cell in cells)
        {
            cell.RetentionCharged = true;
        }

        return (cells, retainedBytes);
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

        SyncExecutableLocalFromValue(codeObject, locals, localCells, name, value);
        if (codeObject.RequiresLocalVariableMirroring || context.MirrorLocalStores || !codeObject.LocalNameToSlot.ContainsKey(name))
        {
            context.Variables[name] = value;
        }
    }

    private static Dictionary<string, object> MaterializeExecutableDefaultValues(
        IReadOnlyDictionary<string, LoweredExpression> defaultValues,
        ExecutionContext context)
    {
        var result = new Dictionary<string, object>(defaultValues.Count, StringComparer.Ordinal);
        foreach (var pair in defaultValues)
        {
            result[pair.Key] = RuntimeValue(EvaluateLoweredExpression(pair.Value, context));
        }

        return result;
    }

}
