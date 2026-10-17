using Lokad.Lython.Frontend;
using System.Collections;
using Lokad.Lython.Runtime.Text;
using System.Text.RegularExpressions;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Internal operand only: its receiver occupies a separate slot. A guest
    // list-valued attribute can never be confused with this resolved method.
    private static readonly object DirectListAppend = new();
    private static readonly object DirectDictionaryGet = new();
    private static readonly object DirectStringStartsWith = new();
    private static readonly object DirectStringReplace = new();

    private static object LoadLocal(ExecutableCodeObject codeObject, object[] locals, int slot, LythonSourceSpan span, ExecutionContext context)
    {
        var value = locals[slot];
        if (ReferenceEquals(value, UninitializedLocal))
        {
            if (!codeObject.IsFunctionScope && !codeObject.LocalNames[slot].StartsWith('<') &&
                context.Variables.TryGetValue(codeObject.LocalNames[slot], out var globalValue))
                return globalValue;
            throw codeObject.IsFunctionScope
                ? RuntimeErrors.UnboundLocalVariable(codeObject.LocalNames[slot], span)
                : RuntimeErrors.NameNotDefined(codeObject.LocalNames[slot], span);
        }

        return value;
    }

    private static object LoadClosure(ExecutableCodeObject codeObject, ExecutionContext context, int slot, LythonSourceSpan span)
    {
        var frame = context.CurrentExecutableFrame;
        if (frame is null || !frame.TryGetClosureCell(slot, out var cell))
        {
            if (codeObject.ScopeFacts.IsNonlocal(codeObject.ClosureNames[slot]))
                return ResolveName(codeObject.ClosureNames[slot], span, context);
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
        ref ExecutableValueStack stack,
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
        int? failedHandlerStart = null;
        int? failedHandlerEnd = null;
        for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
        {
            var region = regions[regionIndex];
            if (currentBlockIndex < region.ProtectedStartBlockIndex ||
                currentBlockIndex > region.ProtectedEndBlockIndex)
            {
                continue;
            }

            if (abrupt is PendingJump jump && jump.TargetBlock >= region.ProtectedStartBlockIndex && jump.TargetBlock <= region.ProtectedEndBlockIndex)
                continue;

            if (abrupt is PendingException { Exception: var exception } &&
                region.ExceptBlockIndex is int exceptBlock)
            {
                if (region.ProtectedStartBlockIndex == failedHandlerStart &&
                    region.ProtectedEndBlockIndex == failedHandlerEnd)
                    continue;

                bool matches;
                try
                {
                    matches = MatchesCaughtException(region.ExceptionTypeNames, region.ExceptionTypesAreTuple, exception, context, span);
                }
                catch (LythonRuntimeException handlerError)
                {
                    // A failed header replaces the in-flight exception and bypasses
                    // the remaining handlers of this try, while its finally and
                    // enclosing handlers still run.
                    handlerError.PythonContext ??= CreatePythonExceptionInstance(exception, context);
                    abrupt = new PendingException(handlerError);
                    failedHandlerStart = region.ProtectedStartBlockIndex;
                    failedHandlerEnd = region.ProtectedEndBlockIndex;
                    continue;
                }

                if (!matches)
                    continue;

                RestoreExecutableStackForHandler(region, ref stack, blockEntryStackDepths, span);
                pendingAbrupt = null;
                matchedRegion = region;
                var pyException = CreatePythonExceptionInstance(exception, context);
                UnwindAbandonedHandlerVars(context, span, exceptBlock);
                if (region.ExceptionVariableName is not null)
                {
                    pyException.Adopt(context, span);
                    StoreName(region.ExceptionVariableName, pyException, context, span);
                    PushActiveHandlerVar(context, region);
                }

                context.Services.SetCurrentException(pyException);
                nextBlockIndex = exceptBlock;
                return true;
            }

            if (region.FinallyBlockIndex is int finallyBlock)
            {
                RestoreExecutableStackForHandler(region, ref stack, blockEntryStackDepths, span);
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
        ref ExecutableValueStack stack,
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
        if (!TryMatchRootPattern(matchCase.Case.Pattern, subject, context, bindings))
        {
            return false;
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
        if (importBinding.ModuleName == "__future__" && importBinding.ImportedMembers is not null &&
            ImportsOnlyFutureAnnotations(importBinding.ImportedMembers))
        {
            ExecuteImport(new ImportStatementSyntax(importBinding.ModuleName, importBinding.BindingName,
                importBinding.BoundModuleName, importBinding.ImportedMembers, importBinding.Span), context);
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

    private static async ValueTask ExecuteExecutableFunctionDefinitionAsync(
        ExecutableCodeObject codeObject,
        ExecutableFunctionBinding functionBinding,
        object[] locals,
        ExecutableCell?[]? localCells,
        ExecutionContext context,
        bool asynchronous)
    {
        var hasAnnotations = functionBinding.Function.ReturnAnnotation is not null ||
            functionBinding.Function.Parameters.Any(p => p.Annotation is not null);
        if (functionBinding.Function.TypeParameters is not null || HasTypeParameterScope(context) ||
            hasAnnotations && (functionBinding.CodeObject is null || functionBinding.Function.GeneratorCode is not null))
        {
            if (asynchronous) await ExecuteLoweredFunctionDefinitionAsync(functionBinding.Function, context).ConfigureAwait(false);
            else ExecuteLoweredFunctionDefinition(functionBinding.Function, context);
            SyncExecutableLocalsFromContext(codeObject, locals, localCells, context);
            return;
        }

        context.EnterInterpreterFrame(functionBinding.Function.Span);
        try
        {
            using var decoratorStorage = context.MemoryGovernor.ReserveTemporary(EstimateObjectArrayBytes(functionBinding.Function.Decorators.Count), functionBinding.Function.Span);
            var decorators = asynchronous
                ? await EvaluateDecoratorsAsync(functionBinding.Function.Decorators, context).ConfigureAwait(false)
                : EvaluateDecorators(functionBinding.Function.Decorators, context);
            var closureCells = functionBinding.CodeObject is null
                ? []
                : CaptureExecutableClosures(functionBinding.CodeObject, context, functionBinding.Function.Span);
            using var defaultStorage = ReserveDefaultArgumentsWorkingStorage(functionBinding.DefaultValues.Count, context.MemoryGovernor, functionBinding.Function.Span);
            var defaults = asynchronous
                ? await BuildDefaultArgumentMapAsync(functionBinding.Function.Parameters,
                    expression => EvaluateLoweredExpressionAsync(expression, context)).ConfigureAwait(false)
                : MaterializeExecutableDefaultValues(functionBinding.DefaultValues, context);
            var function = functionBinding.Function.GeneratorCode is not null
                ? (object)new PyGeneratorFunction(functionBinding.Function, context.FunctionClosureContext,
                    defaults, closureCells)
                : functionBinding.CodeObject is null
                ? new PyFunction(
                    functionBinding.Function.Syntax.DeclaredName,
                    functionBinding.Function.Parameters,
                    functionBinding.Function.Body,
                    context.FunctionClosureContext,
                    defaults,
                    ScopeDirectiveFactsCollector.ForFunction(functionBinding.Function.Syntax))
                : new PyExecutableFunction(
                    functionBinding.Function.Syntax.DeclaredName,
                    functionBinding.Function.Parameters,
                    functionBinding.CodeObject,
                    context.FunctionClosureContext,
                    closureCells,
                    defaults,
                    functionBinding.CodeObject.ScopeFacts);
            ChargeFunctionValue(context, functionBinding.Function.Span);
            ChargeClosureRetention(
                context.FunctionClosureContext,
                context.MemoryGovernor,
                functionBinding.Function.Span);
            if (function is PyFunctionBase defined)
            {
                PyFunctionBase.CaptureFunctionDocstring(defined, functionBinding.Function.Body, context, functionBinding.Function.Span);
                if (hasAnnotations)
                {
                    // Metadata does not require interpreting an otherwise compiled body.
                    // Keep its existing scope, evaluation order and ownership before publication.
                    var typeScope = CreateTypeScope(null, context, functionBinding.Function.Span);
                    await AttachTypeMetadataAsync(defined, functionBinding.Function, typeScope, context, asynchronous).ConfigureAwait(false);
                }
            }
            var decorated = asynchronous
                ? await ApplyDecoratorsAsync(function, decorators, functionBinding.Function.Span, context).ConfigureAwait(false)
                : ApplyDecorators(function, decorators, functionBinding.Function.Span, context);
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
        else if (arguments.Length > 3)
        {
            // These buffers are not pooled, but the completed call helper can
            // still hold the array while observing its result. Withdraw every
            // input reference at the same boundary as the small pooled arities.
            arguments.AsSpan().Fill(CallArgumentValue.Positional(PyNone.Instance));
        }
    }

    private static int ReadExecutableCallTarget(
        ExecutableCallSite callSite,
        ref ExecutableValueStack stack,
        out object target)
    {
        var valueCount = callSite.ArgumentCount + callSite.ArgumentOffset;
        if (valueCount < 0 || stack.Count < valueCount)
        {
            throw RuntimeErrors.Runtime("executable stack underflow", callSite.CallSpan);
        }

        var start = stack.Count - valueCount;
        target = stack[start + callSite.TargetOffset];
        return start;
    }

    private static CallArgumentValue[] PrepareExecutableCallArguments(
        ExecutableCallSite callSite, ref ExecutableValueStack stack, int start)
    {
        var arguments = RentCallArguments(callSite.ArgumentCount);
        try
        {
            for (var i = 0; i < callSite.ArgumentCount; i++)
            {
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

    private static CallArgumentValue CreateExecutableCallArgument(ExecutableCallArgumentSpec spec, object value)
        => spec.Kind switch
        {
            CallArgumentKind.Positional => CallArgumentValue.Positional(value),
            CallArgumentKind.Keyword => CallArgumentValue.Keyword(spec.KeywordName, value),
            _ => throw new InvalidOperationException($"Executable call site contains unsupported argument kind {spec.Kind}.")
        };

    private static void CompleteExecutableValueTransfer(
        ref ExecutableValueStack stack,
        int start,
        object result,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        // The operation has completed: inputs are no longer live unless retained
        // by the result. Reuse the first input slot before result admission.
        // No extra slot, hidden retained extent or per-opcode cleanup is needed.
        stack.RemoveTail(stack.Count - start - 1);
        stack[start] = result;
        context.ObserveValue(stack[start], span);
    }

    private static void ExecuteExecutableCall(
        ExecutableCallSite callSite,
        ref ExecutableValueStack stack,
        ExecutionContext context)
    {
        var start = ReadExecutableCallTarget(callSite, ref stack, out var target);

        try
        {
            object result;
            if (callSite.HasReceiver && ReferenceEquals(target, DirectListAppend))
            {
                result = InvokeListAppend((PyList)stack[start], stack[start + callSite.ArgumentOffset], callSite.CallSpan, context);
            }
            else if (callSite.HasReceiver && stack[start] is PyString stringReceiver &&
                target is Func<PyString, LythonSourceSpan, ExecutionContext, object> stringOperation)
            {
                result = InvokeSingleInput(stringReceiver, stringOperation, callSite.CallSpan, context);
            }
            else if (callSite.HasReceiver && ReferenceEquals(target, DirectStringStartsWith))
            {
                result = InvokeStringStartsWith((PyString)stack[start],
                    stack.Slice(start + callSite.ArgumentOffset, callSite.ArgumentCount), callSite.CallSpan, context);
            }
            else if (callSite.HasReceiver && ReferenceEquals(target, DirectStringReplace))
            {
                result = InvokeStringReplace((PyString)stack[start], stack[start + callSite.ArgumentOffset],
                    stack[start + callSite.ArgumentOffset + 1],
                    callSite.ArgumentCount == 3 ? stack[start + callSite.ArgumentOffset + 2] : null,
                    callSite.CallSpan, context);
            }
            else if (callSite.HasReceiver && ReferenceEquals(target, DirectDictionaryGet))
            {
                result = InvokeDictionaryGet((PyDict)stack[start], stack[start + callSite.ArgumentOffset],
                    callSite.ArgumentCount == 2 ? stack[start + callSite.ArgumentOffset + 1] : PyNone.Instance,
                    callSite.CallSpan, context);
            }
            else if (callSite.IsPositionalOnly && callSite.ArgumentCount == 1 && ExecutionContext.IsLengthBuiltin(target) &&
                stack[start + callSite.ArgumentOffset] is not PyInstance)
            {
                result = InvokeSingleInput(stack[start + callSite.ArgumentOffset], LengthOperation, callSite.CallSpan, context);
            }
            else if (callSite.IsPositionalOnly && callSite.ArgumentCount == 1 && target is BuiltinCallable { Name: "int" } &&
                stack[start + callSite.ArgumentOffset] is PyString or PyBytes)
            {
                result = InvokeSingleInput(stack[start + callSite.ArgumentOffset], IntegerTextOperation, callSite.CallSpan, context);
            }
            else if (target is PyFunctionBase function)
            {
                result = InvokeFunctionFromOperands(function, stack.Slice(start + callSite.ArgumentOffset, callSite.ArgumentCount).Span,
                    callSite.CallSpan, context, callSite: callSite.IsPositionalOnly && function is PyExecutableFunction { SupportsDirectPositional: true }
                        ? null : callSite);
            }
            else if (target is PyBoundMethod { Function: PyFunctionBase method } bound)
            {
                result = InvokeFunctionFromOperands(method, stack.Slice(start + callSite.ArgumentOffset, callSite.ArgumentCount).Span,
                    callSite.CallSpan, context, bound.Receiver,
                    callSite.IsPositionalOnly && method is PyFunction { SupportsPositionalBinding: true } ? null : callSite);
            }
            else
            {
                var arguments = PrepareExecutableCallArguments(callSite, ref stack, start);
                try
                {
                    result = InvokeCallableTarget(target, callSite.TargetSpan, callSite.CallSpan, context, arguments);
                }
                finally
                {
                    // Withdraw finished inputs before result admission.
                    ReturnCallArguments(arguments);
                }
            }

            CompleteExecutableValueTransfer(ref stack, start, result, context, callSite.CallSpan);
        }
        catch
        {
            // The tail contains inputs or the denied result, depending on how
            // far the transfer progressed. Surrounding operands stay intact.
            stack.RemoveTail(stack.Count - start);
            throw;
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
        NoteModuleBuiltinAlias(context, name, value, span);
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
