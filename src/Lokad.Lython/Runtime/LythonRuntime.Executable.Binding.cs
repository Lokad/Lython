using Lokad.Lython.Frontend;
using System.Collections;
using Lokad.Lython.Runtime.Text;
using System.Text.RegularExpressions;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object LoadLocal(ExecutableCodeObject codeObject, object?[] locals, int slot, LythonSourceSpan span)
    {
        var value = locals[slot];
        if (ReferenceEquals(value, UninitializedLocal))
        {
            throw RuntimeErrors.NameNotDefined(codeObject.LocalNames[slot], span);
        }

        return value.RequireNotNull();
    }

    private static object LoadClosure(ExecutableCodeObject codeObject, ExecutionContext context, int slot, LythonSourceSpan span)
    {
        var frame = context.CurrentExecutableFrame;
        if (frame is null || !frame.TryGetClosureCell(slot, out var cell))
        {
            throw RuntimeErrors.NameNotDefined(codeObject.ClosureNames[slot], span);
        }

        if (ReferenceEquals(cell.Value, UninitializedLocal))
        {
            throw RuntimeErrors.NameNotDefined(codeObject.ClosureNames[slot], span);
        }

        return cell.Value.RequireNotNull();
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
        for (var current = context; current is not null; current = current.Parent)
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
                throw returned.Return;
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
        ref int nextBlockIndex)
    {
        var region = FindExecutableExceptionRegion(codeObject, currentBlockIndex);
        if (region is null)
        {
            return false;
        }

        RestoreExecutableStackForHandler(region, stack, blockEntryStackDepths, span);

        if (abrupt is PendingException { Exception: var exception } &&
            region.ExceptBlockIndex is int exceptBlock &&
            MatchesExecutableExceptionType(region.ExceptionTypeNames, exception.ExceptionType))
        {
            pendingAbrupt = null;
            var pyException = new PyException(
                exception.ExceptionType,
                exception.Message,
                exception.Payload ?? PyNone.Instance);
            if (region.ExceptionVariableName is not null)
            {
                StoreName(region.ExceptionVariableName, pyException, context, span);
            }

            context.Services.SetCurrentException(pyException);
            nextBlockIndex = exceptBlock;
            return true;
        }

        if (region.FinallyBlockIndex is not int finallyBlock)
        {
            return false;
        }

        pendingAbrupt = abrupt;
        nextBlockIndex = finallyBlock;
        return true;
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

    private static ExecutableExceptionRegion? FindExecutableExceptionRegion(ExecutableCodeObject codeObject, int blockIndex)
    {
        ExecutableExceptionRegion? best = null;
        var bestWidth = int.MaxValue;
        foreach (var region in codeObject.ExceptionRegions)
        {
            if (blockIndex < region.ProtectedStartBlockIndex || blockIndex > region.ProtectedEndBlockIndex)
            {
                continue;
            }

            var width = region.ProtectedEndBlockIndex - region.ProtectedStartBlockIndex;
            if (width < bestWidth)
            {
                best = region;
                bestWidth = width;
            }
        }

        return best;
    }

    private static bool MatchesExecutableExceptionType(IReadOnlyList<string>? exceptionTypes, string exceptionType)
        => exceptionTypes is null || exceptionTypes.Any(name => MatchesExceptionTypeName(name, exceptionType));

    private static bool TryExecuteExecutableMatchCase(
        ExecutableMatchCaseBinding matchCase,
        object subject,
        ExecutionContext context,
        object?[] locals,
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
        object?[] locals,
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

    private static void ExecuteExecutableFunctionDefinition(
        ExecutableCodeObject codeObject,
        ExecutableFunctionBinding functionBinding,
        object?[] locals,
        ExecutableCell?[]? localCells,
        ExecutionContext context)
    {
        context.EnterInterpreterFrame(functionBinding.Function.Span);
        try
        {
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
                    CaptureExecutableClosures(functionBinding.CodeObject, context, functionBinding.Function.Span),
                    MaterializeExecutableDefaultValues(functionBinding.DefaultValues, context),
                    functionBinding.CodeObject.ScopeFacts);
            var decorated = ApplyDecorators(function, functionBinding.Function.Decorators, functionBinding.Function.Span, context);
            AssignExecutableBoundName(codeObject, locals, localCells, functionBinding.Function.Syntax.Name, decorated, context, functionBinding.Function.Span);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static object ExecuteExecutableCall(ExecutableCallSite callSite, ExecutableValueStack stack, ExecutionContext context, ExecutableCallCache cache)
    {
        var valueCount = callSite.ArgumentCount + 1;
        if (valueCount < 0 || stack.Count < valueCount)
        {
            throw RuntimeErrors.Runtime("executable stack underflow", callSite.CallSpan);
        }

        var start = stack.Count - valueCount;
        var target = stack[start];
        var arguments = new CallArgumentValue[callSite.ArgumentCount];
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

        if (cache.Callable is not null && ReferenceEquals(cache.Target, target))
        {
            return InvokeExecutableCachedCallable(cache.Callable, callSite.CallSpan, context, arguments);
        }

        if (target is ICallable callable)
        {
            cache.Target = target;
            cache.Callable = callable;
            return InvokeExecutableCachedCallable(callable, callSite.CallSpan, context, arguments);
        }

        cache.Target = null;
        cache.Callable = null;
        return RuntimeValue(InvokeCallableTarget(target, callSite.TargetSpan, callSite.CallSpan, context, arguments));
    }

    private static object InvokeExecutableCachedCallable(
        ICallable callable,
        LythonSourceSpan callSpan,
        ExecutionContext context,
        CallArgumentValue[] arguments)
    {
        context.EnterInterpreterFrame(callSpan);
        try
        {
            context.CheckExecutionBudget(callSpan);
            return RuntimeValue(callable.Invoke(arguments, callSpan, context));
        }
        catch (RegexParseException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, callSpan);
        }
        catch (LythonRuntimeException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            throw RuntimeErrors.Runtime(ex.Message, callSpan);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

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

        return cells;
    }

    private static void SyncExecutableLocalsFromContext(
        ExecutableCodeObject codeObject,
        object?[] locals,
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
        object?[] locals,
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
        object?[] locals,
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
                return;
            }

            StoreName(name, value, context, span);
            return;
        }

        SyncExecutableLocalFromValue(codeObject, locals, localCells, name, value);
        if (codeObject.RequiresLocalVariableMirroring || !codeObject.LocalNameToSlot.ContainsKey(name))
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
