using Lokad.Lython.Frontend;
using System.Collections;
using Lokad.Lython.Runtime.Text;
using System.Text.RegularExpressions;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static readonly object UninitializedLocal = new();

    internal sealed class ExecutableCell
    {
        public ExecutableCell(object? value) => Value = value;

        public object? Value { get; set; }
    }

    internal sealed class ExecutableFrameState
    {
        private readonly ExecutableCodeObject _codeObject;
        private readonly object?[] _locals;
        private readonly ExecutableCell?[]? _localCells;
        private readonly IReadOnlyList<ExecutableCell>? _closureCells;

        public ExecutableFrameState(
            ExecutableCodeObject codeObject,
            object?[] locals,
            ExecutableCell?[]? localCells,
            IReadOnlyList<ExecutableCell>? closureCells)
        {
            _codeObject = codeObject;
            _locals = locals;
            _localCells = localCells;
            _closureCells = closureCells;
        }

        public bool TryResolveLocalOrClosure(string name, out object value)
        {
            if (_codeObject.LocalNameToSlot.TryGetValue(name, out var localSlot))
            {
                var local = _locals[localSlot];
                if (!ReferenceEquals(local, UninitializedLocal))
                {
                    value = local!;
                    return true;
                }
            }

            if (_closureCells is not null && _codeObject.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                var closure = _closureCells[closureSlot].Value;
                if (!ReferenceEquals(closure, UninitializedLocal))
                {
                    value = closure!;
                    return true;
                }
            }

            value = PyNone.Instance;
            return false;
        }

        public bool TryGetCell(string name, out ExecutableCell cell)
        {
            if (_localCells is not null && _codeObject.LocalNameToSlot.TryGetValue(name, out var localSlot))
            {
                if (_localCells[localSlot] is ExecutableCell localCell)
                {
                    cell = localCell;
                    return true;
                }
            }

            if (_closureCells is not null && _codeObject.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                cell = _closureCells[closureSlot];
                return true;
            }

            cell = null!;
            return false;
        }

        public bool TryGetClosureCell(int slot, out ExecutableCell cell)
        {
            if (_closureCells is not null && slot >= 0 && slot < _closureCells.Count)
            {
                cell = _closureCells[slot];
                return true;
            }

            cell = null!;
            return false;
        }

        public bool TryStoreLocalOrClosure(string name, object value)
        {
            if (_codeObject.LocalNameToSlot.TryGetValue(name, out var localSlot))
            {
                _locals[localSlot] = value;
                if (_localCells?[localSlot] is ExecutableCell localCell)
                {
                    localCell.Value = value;
                }

                return true;
            }

            if (_closureCells is not null && _codeObject.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                _closureCells[closureSlot].Value = value;
                return true;
            }

            return false;
        }

        public bool TryDeleteLocalOrClosure(string name)
        {
            if (_codeObject.LocalNameToSlot.TryGetValue(name, out var localSlot))
            {
                var removed = !ReferenceEquals(_locals[localSlot], UninitializedLocal);
                _locals[localSlot] = UninitializedLocal;
                if (_localCells?[localSlot] is ExecutableCell localCell)
                {
                    removed |= !ReferenceEquals(localCell.Value, UninitializedLocal);
                    localCell.Value = UninitializedLocal;
                }

                return removed;
            }

            if (_closureCells is not null && _codeObject.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                var cell = _closureCells[closureSlot];
                var removed = !ReferenceEquals(cell.Value, UninitializedLocal);
                cell.Value = UninitializedLocal;
                return removed;
            }

            return false;
        }
    }

    private sealed record PendingAbruptSignal(
        LythonRuntimeException? Exception = null,
        ReturnSignal? Return = null,
        ControlSignal? Control = null);

    private sealed class ExecutableMemberCache
    {
        public object? Target { get; set; }

        public object? Value { get; set; }
    }

    private sealed class ExecutableCallCache
    {
        public object? Target { get; set; }

        public ICallable? Callable { get; set; }
    }

    private sealed class ExecutableValueStack
    {
        private object?[] _items;

        public ExecutableValueStack(int capacity)
        {
            _items = new object[Math.Max(4, capacity)];
        }

        public int Count { get; private set; }

        public object this[int index] => _items[index]!;

        public void Push(object value)
        {
            if (Count == _items.Length)
            {
                Array.Resize(ref _items, checked(_items.Length * 2));
            }

            _items[Count++] = value;
        }

        public object Pop()
        {
            var index = Count - 1;
            var value = _items[index]!;
            _items[index] = null;
            Count = index;
            return value;
        }

        public object Peek() => _items[Count - 1]!;

        public void RemoveTail(int count)
        {
            if (count == 0)
            {
                return;
            }

            var newCount = Count - count;
            Array.Clear(_items, newCount, count);
            Count = newCount;
        }
    }

    public LythonExecutionResult Run(
        ExecutableScript script,
        ILythonHost host,
        LythonRunOptions? options)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(host);

        ExecutionContext? context = null;
        try
        {
            context = new ExecutionContext(host, options);
            ExecuteExecutableCodeObject(script.EntryPoint, context);

            return new LythonExecutionResult(
                success: true,
                returnValue: null,
                standardOutput: CaptureStandardOutput(context),
                standardError: CaptureStandardError(context),
                exitCode: null,
                diagnostics: Array.Empty<LythonDiagnostic>(),
                failure: null);
        }
        catch (ReturnSignal signal)
        {
            try
            {
                return new LythonExecutionResult(
                    success: true,
                    returnValue: NormalizePublicValue(signal.Value, options),
                    standardOutput: context is null ? string.Empty : CaptureStandardOutput(context),
                    standardError: context is null ? string.Empty : CaptureStandardError(context),
                    exitCode: null,
                    diagnostics: Array.Empty<LythonDiagnostic>(),
                    failure: null);
            }
            catch (ProjectionException ex)
            {
                return new LythonExecutionResult(
                    success: false,
                    returnValue: null,
                    standardOutput: context is null ? string.Empty : CaptureStandardOutput(context),
                    standardError: context is null ? string.Empty : CaptureStandardError(context),
                    exitCode: null,
                    diagnostics: Array.Empty<LythonDiagnostic>(),
                    failure: new LythonRuntimeFailure("ProjectionError", ex.Message, null, Array.Empty<LythonStackFrame>(), context?.SourcePath));
            }
        }
        catch (LythonRuntimeException ex)
        {
            ex.SetSourcePathIfMissing(context?.SourcePath);
            return new LythonExecutionResult(
                success: false,
                returnValue: null,
                standardOutput: context is null ? string.Empty : CaptureStandardOutput(context),
                standardError: context is null ? string.Empty : CaptureStandardError(context),
                exitCode: GetExitCode(ex),
                diagnostics: Array.Empty<LythonDiagnostic>(),
                failure: RuntimeFailureProjection.ToPublicFailure(ex));
        }
    }

    internal static void ExecuteExecutableCodeObject(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        IReadOnlyDictionary<string, object>? initialLocals = null,
        IReadOnlyList<ExecutableCell>? closureCells = null)
    {
        var previousExecutableFrame = context.CurrentExecutableFrame;
        context.EnterInterpreterFrame(null);
        try
        {
            var locals = new object?[codeObject.LocalNames.Count];
            Array.Fill(locals, UninitializedLocal);
            var needsLocalCells = codeObject.CapturedLocalSlots.Count != 0;
            ExecutableCell?[]? localCells = null;
            if (needsLocalCells)
            {
                localCells = new ExecutableCell[codeObject.LocalNames.Count];
                for (var i = 0; i < codeObject.CapturedLocalSlots.Count; i++)
                {
                    localCells[codeObject.CapturedLocalSlots[i]] = new ExecutableCell(UninitializedLocal);
                }
            }

            if (initialLocals is not null)
            {
                for (var i = 0; i < codeObject.LocalNames.Count; i++)
                {
                    var localName = codeObject.LocalNames[i];
                    if (initialLocals.TryGetValue(localName, out var value))
                    {
                        locals[i] = value;
                        if (localCells?[i] is ExecutableCell localCell)
                        {
                            localCell.Value = value;
                        }
                    }
                }
            }

            if (closureCells is not null || localCells is not null || codeObject.LocalNames.Count != 0)
            {
                context.EnterExecutableSlots(new ExecutableFrameState(codeObject, locals, localCells, closureCells));
            }

            var stack = new ExecutableValueStack(Math.Max(8, codeObject.LocalNames.Count));
            var currentBlockIndex = codeObject.EntryBlockIndex;
            PendingAbruptSignal? pendingAbrupt = null;
            var memberCaches = new ExecutableMemberCache[codeObject.MemberCacheCount];
            var callCaches = new ExecutableCallCache[codeObject.CallCacheCount];

            while (true)
            {
                var block = codeObject.Blocks[currentBlockIndex];
                var jumped = false;

                foreach (var instruction in block.Instructions)
                {
                    context.Services.CheckExecutionBudget(instruction.Span);

                    try
                    {
                        switch (instruction.OpCode)
                        {
                            case ExecutableOpCode.Import:
                                ExecuteExecutableImport(codeObject, codeObject.Imports[instruction.A], locals, localCells, context);
                                break;

                            case ExecutableOpCode.DefineFunction:
                                ExecuteExecutableFunctionDefinition(codeObject, codeObject.Functions[instruction.A], locals, localCells, context);
                                break;

                            case ExecutableOpCode.ExecuteFallbackStatement:
                                ExecuteLoweredStatement(codeObject.StatementFallbacks[instruction.A].Statement, context);
                                SyncExecutableLocalsFromContext(codeObject, locals, localCells, context);
                                break;

                            case ExecutableOpCode.LoadConst:
                            {
                                var value = RuntimeValue(codeObject.Constants[instruction.A]);
                                context.ObserveValue(value, instruction.Span);
                                stack.Push(value);
                                break;
                            }

                            case ExecutableOpCode.LoadLocal:
                                stack.Push(LoadLocal(codeObject, locals, instruction.A, instruction.Span));
                                break;

                            case ExecutableOpCode.LoadClosure:
                                stack.Push(LoadClosure(codeObject, context, instruction.A, instruction.Span));
                                break;

                            case ExecutableOpCode.LoadGlobal:
                                stack.Push(ResolveExecutableGlobal(codeObject.Names[instruction.A], instruction.Span, context));
                                break;

                            case ExecutableOpCode.LoadName:
                                stack.Push(ResolveExecutableName(codeObject.Names[instruction.A], instruction.Span, context));
                                break;

                            case ExecutableOpCode.EvaluateFallbackExpression:
                            {
                                var value = EvaluateLoweredExpression(codeObject.ExpressionFallbacks[instruction.A].Expression, context);
                                context.ObserveValue(value, instruction.Span);
                                stack.Push(value);
                                break;
                            }

                            case ExecutableOpCode.LoadMember:
                            {
                                var target = Pop(stack, instruction.Span);
                                var memberName = codeObject.Names[instruction.A];
                                var memberCache = memberCaches[instruction.B] ??= new ExecutableMemberCache();
                                if (!TryReadExecutableMemberCache(target, memberCache, out var memberValue))
                                {
                                    if (!TryResolveRuntimeMember(target, memberName, context, instruction.Span, out memberValue))
                                    {
                                        throw PyMemberAccess.CreateMissingMemberError(target, memberName, instruction.Span);
                                    }

                                    TryWriteExecutableMemberCache(target, memberValue, memberCache);
                                }

                                if (memberValue is null)
                                {
                                    throw PyMemberAccess.CreateMissingMemberError(target, memberName, instruction.Span);
                                }

                                context.ObserveValue(memberValue, instruction.Span);
                                stack.Push(memberValue);
                                break;
                            }

                            case ExecutableOpCode.StoreLocal:
                            {
                                var value = Pop(stack, instruction.Span);
                                locals[instruction.A] = value;
                                if (localCells?[instruction.A] is ExecutableCell localCell)
                                {
                                    localCell.Value = value;
                                }

                                if (codeObject.RequiresLocalVariableMirroring)
                                {
                                    context.Variables[codeObject.LocalNames[instruction.A]] = value;
                                }
                                break;
                            }

                            case ExecutableOpCode.StoreClosure:
                            {
                                var value = Pop(stack, instruction.Span);
                                StoreExecutableClosure(codeObject, context, instruction.A, value, instruction.Span);
                                break;
                            }

                            case ExecutableOpCode.StoreGlobal:
                            {
                                var value = Pop(stack, instruction.Span);
                                var name = codeObject.Names[instruction.A];
                                StoreName(name, value, context, instruction.Span);
                                break;
                            }

                            case ExecutableOpCode.StoreName:
                            {
                                var value = Pop(stack, instruction.Span);
                                var name = codeObject.Names[instruction.A];
                                AssignExecutableBoundName(codeObject, locals, localCells, name, value, context, instruction.Span);
                                break;
                            }

                            case ExecutableOpCode.Dup:
                                stack.Push(Peek(stack, instruction.Span));
                                break;

                            case ExecutableOpCode.PopTop:
                                _ = Pop(stack, instruction.Span);
                                break;

                            case ExecutableOpCode.MakeList:
                            {
                                var value = CreateListFromStack(stack, instruction.A, instruction.Span, context);
                                stack.Push(value);
                                break;
                            }

                            case ExecutableOpCode.MakeTuple:
                            {
                                var value = CreateTupleFromStack(stack, instruction.A, instruction.Span, context);
                                context.ObserveValue(value, instruction.Span);
                                stack.Push(value);
                                break;
                            }

                            case ExecutableOpCode.MakeSet:
                            {
                                var value = ExecuteExecutableMakeSet(stack, instruction.A, instruction.Span, context);
                                context.ObserveValue(value, instruction.Span);
                                stack.Push(value);
                                break;
                            }

                            case ExecutableOpCode.MakeDict:
                            {
                                var value = ExecuteExecutableMakeDict(stack, instruction.A, instruction.Span, context);
                                context.ObserveValue(value, instruction.Span);
                                stack.Push(value);
                                break;
                            }

                            case ExecutableOpCode.ResolveContextManager:
                            {
                                var manager = Pop(stack, instruction.Span);
                                stack.Push(PyContextManagers.Resolve(manager, instruction.Span, context));
                                break;
                            }

                            case ExecutableOpCode.EnterContextManager:
                            {
                                var manager = PopContextManager(stack, instruction.Span);
                                var entered = manager.Enter();
                                context.ObserveValue(entered, instruction.Span);
                                stack.Push(entered);
                                break;
                            }

                            case ExecutableOpCode.ExitContextManager:
                            {
                                var manager = PopContextManager(stack, instruction.Span);
                                if (pendingAbrupt?.Exception is not null &&
                                    manager.Exit(
                                        pendingAbrupt.Exception.ExceptionType,
                                        pendingAbrupt.Exception,
                                        PyNone.Instance))
                                {
                                    pendingAbrupt = null;
                                }
                                else
                                {
                                    _ = manager.Exit(PyNone.Instance, PyNone.Instance, PyNone.Instance);
                                }

                                break;
                            }

                            case ExecutableOpCode.MatchCase:
                            {
                                var subject = Pop(stack, instruction.Span);
                                if (!TryExecuteExecutableMatchCase(
                                        codeObject.MatchCases[instruction.A],
                                        subject,
                                        context,
                                        locals,
                                        localCells))
                                {
                                    currentBlockIndex = instruction.B;
                                    jumped = true;
                                }
                                break;
                            }

                            case ExecutableOpCode.GetIter:
                            {
                                var iterable = Pop(stack, instruction.Span);
                                stack.Push(ToSequence(iterable, instruction.Span).GetEnumerator());
                                break;
                            }

                            case ExecutableOpCode.ForNext:
                            {
                                var iterator = PeekIterator(stack, instruction.Span);
                                if (!iterator.MoveNext())
                                {
                                    _ = Pop(stack, instruction.Span);
                                    currentBlockIndex = instruction.A;
                                    jumped = true;
                                    break;
                                }

                                stack.Push(iterator.Current);
                                break;
                            }

                            case ExecutableOpCode.AssignLoopTarget:
                            {
                                var value = Pop(stack, instruction.Span);
                                var binding = codeObject.LoopTargets[instruction.A];
                                AssignLoopTarget(binding.Target, value, binding.Span, context);
                                break;
                            }

                            case ExecutableOpCode.AssignUnpackingTargets:
                            {
                                var value = Pop(stack, instruction.Span);
                                var binding = codeObject.UnpackingTargets[instruction.A];
                                AssignTargets(binding.Targets, value, binding.Span, context);
                                break;
                            }

                            case ExecutableOpCode.Call:
                            {
                                var value = ExecuteExecutableCall(codeObject.CallSites[instruction.A], stack, context, callCaches[instruction.B] ??= new ExecutableCallCache());
                                context.ObserveValue(value, instruction.Span);
                                stack.Push(value);
                                break;
                            }

                            case ExecutableOpCode.Subscript:
                            {
                                var index = Pop(stack, instruction.Span);
                                var target = Pop(stack, instruction.Span);
                                var value = ExecuteExecutableSubscript(target, index, instruction.Span, context);
                                context.ObserveValue(value, instruction.Span);
                                stack.Push(value);
                                break;
                            }

                            case ExecutableOpCode.Slice:
                            {
                                var value = ExecuteExecutableSlice(stack, instruction.A, instruction.Span, context);
                                context.ObserveValue(value, instruction.Span);
                                stack.Push(value);
                                break;
                            }

                            case ExecutableOpCode.Binary:
                            {
                                var right = Pop(stack, instruction.Span);
                                var left = Pop(stack, instruction.Span);
                                var value = instruction.B == 1
                                    ? EvaluateExecutableAugmented(instruction.AugmentedOperator, left, right, context, instruction.Span)
                                    : EvaluateExecutableBinary(instruction.BinaryOperator, left, right, instruction.Span, context);
                                context.ObserveValue(value, instruction.Span);
                                stack.Push(value);
                                break;
                            }

                            case ExecutableOpCode.Unary:
                            {
                                var operand = Pop(stack, instruction.Span);
                                var value = EvaluateExecutableUnary(instruction.UnaryOperator, operand, instruction.Span);
                                context.ObserveValue(value, instruction.Span);
                                stack.Push(value);
                                break;
                            }

                            case ExecutableOpCode.JumpIfFalse:
                            {
                                var condition = Pop(stack, instruction.Span);
                                if (!IsTruthy(condition))
                                {
                                    currentBlockIndex = instruction.A;
                                    jumped = true;
                                }
                                break;
                            }

                            case ExecutableOpCode.Jump:
                                currentBlockIndex = instruction.A;
                                jumped = true;
                                break;

                            case ExecutableOpCode.EndFinally:
                                if (pendingAbrupt is not null)
                                {
                                    PropagatePendingAbrupt(pendingAbrupt);
                                }

                                currentBlockIndex = instruction.A;
                                jumped = true;
                                break;

                            case ExecutableOpCode.Return:
                                throw new ReturnSignal(Pop(stack, instruction.Span));

                            case ExecutableOpCode.ReturnNone:
                                throw new ReturnSignal(PyNone.Instance);

                            default:
                                throw new InvalidOperationException($"Unknown executable opcode: {instruction.OpCode}");
                        }
                    }
                    catch (ReturnSignal signal)
                    {
                        if (!TryHandleAbrupt(codeObject, context, currentBlockIndex, new PendingAbruptSignal(Return: signal), instruction.Span, ref pendingAbrupt, ref currentBlockIndex))
                        {
                            throw;
                        }

                        jumped = true;
                    }
                    catch (ControlSignal signal)
                    {
                        if (!TryHandleAbrupt(codeObject, context, currentBlockIndex, new PendingAbruptSignal(Control: signal), instruction.Span, ref pendingAbrupt, ref currentBlockIndex))
                        {
                            throw;
                        }

                        jumped = true;
                    }
                    catch (LythonRuntimeException ex)
                    {
                        if (!TryHandleAbrupt(codeObject, context, currentBlockIndex, new PendingAbruptSignal(Exception: ex), instruction.Span, ref pendingAbrupt, ref currentBlockIndex))
                        {
                            throw;
                        }

                        jumped = true;
                    }

                    if (jumped)
                    {
                        break;
                    }
                }

                if (!jumped)
                {
                    return;
                }
            }
        }
        finally
        {
            context.LeaveExecutableSlots(previousExecutableFrame);
            context.LeaveInterpreterFrame();
        }
    }

    private static object LoadLocal(ExecutableCodeObject codeObject, object?[] locals, int slot, LythonSourceSpan span)
    {
        var value = locals[slot];
        if (ReferenceEquals(value, UninitializedLocal))
        {
            throw RuntimeErrors.NameNotDefined(codeObject.LocalNames[slot], span);
        }

        return value!;
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

        return cell.Value!;
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
        if (pending.Exception is not null)
        {
            throw pending.Exception;
        }

        if (pending.Return is not null)
        {
            throw pending.Return;
        }

        if (pending.Control is not null)
        {
            throw pending.Control;
        }
    }

    private static bool TryHandleAbrupt(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
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

        if (abrupt.Exception is not null &&
            region.ExceptBlockIndex is int exceptBlock &&
            MatchesExecutableExceptionType(region.ExceptionTypeNames, abrupt.Exception.ExceptionType))
        {
            pendingAbrupt = null;
            if (region.ExceptionVariableName is not null)
            {
                StoreName(region.ExceptionVariableName, new PyException(
                    abrupt.Exception.ExceptionType,
                    abrupt.Exception.Message,
                    abrupt.Exception.Payload ?? PyNone.Instance),
                    context,
                    span);
            }

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
        => exceptionTypes is null || exceptionTypes.Any(name => string.Equals(name, exceptionType, StringComparison.Ordinal));

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
            var module = ResolveImportedModule(importBinding.ModuleName, context, importBinding.Span);
            AssignExecutableBoundName(codeObject, locals, localCells, importBinding.BindingName, module, context, importBinding.Span);
            return;
        }

        var importedModule = ResolveImportedModule(importBinding.ModuleName, context, importBinding.Span);
        foreach (var importedMember in importBinding.ImportedMembers)
        {
            if (!importedModule.TryGetMember(importedMember.Name, out var value))
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
                throw new NotSupportedException($"Executable interpreter does not yet support call argument kind {spec.Kind}.");
            }

            arguments[i] = new CallArgumentValue(
                spec.Kind == CallArgumentKind.Keyword ? spec.Name : null,
                stack[start + i + 1]);
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

    private static PyDict ExecuteExecutableMakeDict(ExecutableValueStack stack, int pairCount, LythonSourceSpan span, ExecutionContext context)
    {
        var valueCount = checked(pairCount * 2);
        if (valueCount < 0 || stack.Count < valueCount)
        {
            throw RuntimeErrors.Runtime("executable stack underflow", span);
        }

        var start = stack.Count - valueCount;
        var dict = new PyDict(context.MemoryGovernor, span);
        for (var i = 0; i < valueCount; i += 2)
        {
            var key = ValidateDictionaryKey(stack[start + i], span);
            dict.SetItem(key, stack[start + i + 1]);
        }

        stack.RemoveTail(valueCount);

        context.ObserveCollectionCount(dict.Count, span);
        return dict;
    }

    private static PySet ExecuteExecutableMakeSet(ExecutableValueStack stack, int count, LythonSourceSpan span, ExecutionContext context)
    {
        if (count < 0 || stack.Count < count)
        {
            throw RuntimeErrors.Runtime("executable stack underflow", span);
        }

        var start = stack.Count - count;
        var set = new PySet(context.MemoryGovernor, span);
        for (var i = start; i < stack.Count; i++)
        {
            set.Add(ValidateSetItem(stack[i], span));
        }

        stack.RemoveTail(count);

        context.ObserveCollectionCount(set.Count, span);
        return set;
    }

    private static object ExecuteExecutableSubscript(object target, object index, LythonSourceSpan span, ExecutionContext context)
    {
        if (target is PyDefaultDict defaultDict)
        {
            return defaultDict.GetOrCreate(ValidateDictionaryKey(index, span), context, span);
        }

        return PyIndexing.ReadIndex(target, index, span);
    }

    private static IPyContextManager PopContextManager(ExecutableValueStack stack, LythonSourceSpan span)
    {
        var value = Pop(stack, span);
        if (value is IPyContextManager manager)
        {
            return manager;
        }

        throw RuntimeErrors.Type("Object does not support the context manager protocol.", span);
    }

    private static object ExecuteExecutableSlice(ExecutableValueStack stack, int flags, LythonSourceSpan span, ExecutionContext context)
    {
        object? step = null;
        object? end = null;
        object? start = null;

        if ((flags & 4) != 0)
        {
            step = Pop(stack, span);
        }
        if ((flags & 2) != 0)
        {
            end = Pop(stack, span);
        }
        if ((flags & 1) != 0)
        {
            start = Pop(stack, span);
        }

        var target = Pop(stack, span);
        return PyIndexing.ReadSlice(target, start, end, step, span);
    }

    private static bool TryReadExecutableMemberCache(object target, ExecutableMemberCache cache, out object? value)
    {
        if (ReferenceEquals(cache.Target, target))
        {
            value = cache.Value;
            return true;
        }

        value = null;
        return false;
    }

    private static void TryWriteExecutableMemberCache(object target, object value, ExecutableMemberCache cache)
    {
        if (!CanCacheExecutableMemberTarget(target))
        {
            cache.Target = null;
            cache.Value = null;
            return;
        }

        cache.Target = target;
        cache.Value = value;
    }

    private static bool CanCacheExecutableMemberTarget(object target)
        => target is PyString
            or PyBytes
            or PyList
            or PyTuple
            or PyDict
            or PySet
            or PyDecimal
            or PyDate
            or PyTime
            or PyDateTime
            or PyTimedelta
            or PyTimezone
            or RePatternObject
            or ReMatchObject
            or PyPath;

    private static object Pop(ExecutableValueStack stack, LythonSourceSpan span)
    {
        if (stack.Count == 0)
        {
            throw RuntimeErrors.Runtime("executable stack underflow", span);
        }

        return stack.Pop();
    }

    private static object Peek(ExecutableValueStack stack, LythonSourceSpan span)
    {
        if (stack.Count == 0)
        {
            throw RuntimeErrors.Runtime("executable stack underflow", span);
        }

        return stack.Peek();
    }

    private static IEnumerator<object> PeekIterator(ExecutableValueStack stack, LythonSourceSpan span)
    {
        if (stack.Count == 0 || stack.Peek() is not IEnumerator<object> iterator)
        {
            throw RuntimeErrors.Runtime("executable iterator stack is invalid", span);
        }

        return iterator;
    }

    private static PyList CreateListFromStack(ExecutableValueStack stack, int count, LythonSourceSpan span, ExecutionContext context)
    {
        if (count < 0 || stack.Count < count)
        {
            throw RuntimeErrors.Runtime("executable stack underflow", span);
        }

        var start = stack.Count - count;
        context.MemoryGovernor.EnsureCanReserve(EstimateObjectArrayBytes(count), span);
        var items = new object[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = stack[start + i];
        }

        stack.RemoveTail(count);
        return new PyList(items, context.MemoryGovernor, span);
    }

    private static PyTuple CreateTupleFromStack(ExecutableValueStack stack, int count, LythonSourceSpan span, ExecutionContext context)
    {
        if (count < 0 || stack.Count < count)
        {
            throw RuntimeErrors.Runtime("executable stack underflow", span);
        }

        var start = stack.Count - count;
        context.MemoryGovernor.EnsureCanReserve(PyTuple.EstimateApproximateBytes(count), span);
        var items = new object[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = stack[start + i];
        }

        stack.RemoveTail(count);
        return new PyTuple(items, context.MemoryGovernor, span);
    }

    private static object EvaluateExecutableBinary(ExecutableBinaryOperator op, object left, object right, LythonSourceSpan span, ExecutionContext context)
        => op switch
        {
            ExecutableBinaryOperator.Add => EvaluateAdd(left, right, context, span),
            ExecutableBinaryOperator.Subtract => EvaluateSubtract(left, right, span),
            ExecutableBinaryOperator.Multiply => EvaluateMultiply(left, right, context, span),
            ExecutableBinaryOperator.Divide => EvaluateDivide(left, right, span),
            ExecutableBinaryOperator.FloorDivide => EvaluateFloorDivide(left, right, span),
            ExecutableBinaryOperator.Modulo => EvaluateModulo(left, right, span),
            ExecutableBinaryOperator.Power => EvaluatePower(left, right, context, span),
            ExecutableBinaryOperator.BitwiseOr => EvaluateBitwiseOr(left, right, span),
            ExecutableBinaryOperator.BitwiseXor => EvaluateBitwiseXor(left, right, span),
            ExecutableBinaryOperator.BitwiseAnd => EvaluateBitwiseAnd(left, right, span),
            ExecutableBinaryOperator.LeftShift => EvaluateLeftShift(left, right, context, span),
            ExecutableBinaryOperator.RightShift => EvaluateRightShift(left, right, span),
            ExecutableBinaryOperator.Less => Compare(left, right, span) < 0,
            ExecutableBinaryOperator.LessEqual => Compare(left, right, span) <= 0,
            ExecutableBinaryOperator.Greater => Compare(left, right, span) > 0,
            ExecutableBinaryOperator.GreaterEqual => Compare(left, right, span) >= 0,
            ExecutableBinaryOperator.Is => ReferenceEquals(left, right),
            ExecutableBinaryOperator.IsNot => !ReferenceEquals(left, right),
            ExecutableBinaryOperator.In => Contains(right, left, span),
            ExecutableBinaryOperator.NotIn => !Contains(right, left, span),
            ExecutableBinaryOperator.Equal => AreEqual(left, right),
            ExecutableBinaryOperator.NotEqual => !AreEqual(left, right),
            _ => throw new NotSupportedException($"Executable interpreter does not yet support binary operator {op}."),
        };

    private static object EvaluateExecutableUnary(ExecutableUnaryOperator op, object operand, LythonSourceSpan span)
        => op switch
        {
            ExecutableUnaryOperator.Not => !IsTruthy(operand),
            ExecutableUnaryOperator.Plus => EvaluateUnaryPlus(operand, span),
            ExecutableUnaryOperator.Minus => EvaluateUnaryMinus(operand, span),
            ExecutableUnaryOperator.BitwiseNot => EvaluateBitwiseNot(operand, span),
            _ => throw new NotSupportedException($"Executable interpreter does not yet support unary operator {op}."),
        };

    private static object EvaluateExecutableAugmented(ExecutableAugmentedOperator op, object currentValue, object right, ExecutionContext context, LythonSourceSpan span)
        => EvaluateAugmentedAssignment(
            currentValue,
            right,
            op switch
            {
                ExecutableAugmentedOperator.Add => AugmentedAssignmentOperatorSyntax.Add,
                ExecutableAugmentedOperator.Subtract => AugmentedAssignmentOperatorSyntax.Subtract,
                ExecutableAugmentedOperator.Multiply => AugmentedAssignmentOperatorSyntax.Multiply,
                ExecutableAugmentedOperator.Divide => AugmentedAssignmentOperatorSyntax.Divide,
                ExecutableAugmentedOperator.FloorDivide => AugmentedAssignmentOperatorSyntax.FloorDivide,
                ExecutableAugmentedOperator.Modulo => AugmentedAssignmentOperatorSyntax.Modulo,
                ExecutableAugmentedOperator.Power => AugmentedAssignmentOperatorSyntax.Power,
                ExecutableAugmentedOperator.BitwiseOr => AugmentedAssignmentOperatorSyntax.BitwiseOr,
                ExecutableAugmentedOperator.BitwiseXor => AugmentedAssignmentOperatorSyntax.BitwiseXor,
                ExecutableAugmentedOperator.BitwiseAnd => AugmentedAssignmentOperatorSyntax.BitwiseAnd,
                ExecutableAugmentedOperator.LeftShift => AugmentedAssignmentOperatorSyntax.LeftShift,
                ExecutableAugmentedOperator.RightShift => AugmentedAssignmentOperatorSyntax.RightShift,
                _ => throw new NotSupportedException($"Executable interpreter does not yet support augmented operator {op}."),
            },
            context,
            span);
}
