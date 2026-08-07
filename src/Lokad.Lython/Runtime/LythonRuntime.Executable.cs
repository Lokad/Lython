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

        public bool TryResolveLocalOrClosure(string name, [MaybeNullWhen(false)] out object value)
        {
            if (_codeObject.LocalNameToSlot.TryGetValue(name, out var localSlot))
            {
                var local = _locals[localSlot];
                if (!ReferenceEquals(local, UninitializedLocal))
                {
                    value = local.RequireNotNull();
                    return true;
                }
            }

            if (_closureCells is not null && _codeObject.ClosureNameToSlot.TryGetValue(name, out var closureSlot))
            {
                var closure = _closureCells[closureSlot].Value;
                if (!ReferenceEquals(closure, UninitializedLocal))
                {
                    value = closure.RequireNotNull();
                    return true;
                }
            }

            value = PyNone.Instance;
            return false;
        }

        public IEnumerable<KeyValuePair<string, object>> EnumerateLocals()
        {
            foreach (var pair in _codeObject.LocalNameToSlot)
            {
                var value = _locals[pair.Value];
                if (!ReferenceEquals(value, UninitializedLocal))
                {
                    yield return new KeyValuePair<string, object>(pair.Key, value.RequireNotNull());
                }
            }
        }

        public bool TryGetCell(string name, [MaybeNullWhen(false)] out ExecutableCell cell)
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

            cell = null;
            return false;
        }

        public bool TryGetClosureCell(int slot, [MaybeNullWhen(false)] out ExecutableCell cell)
        {
            if (_closureCells is not null && slot >= 0 && slot < _closureCells.Count)
            {
                cell = _closureCells[slot];
                return true;
            }

            cell = null;
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
        LythonRuntimeException? Exception,
        ReturnSignal? Return,
        ControlSignal? Control)
    {
        public PendingAbruptSignal()
            : this(null, null, null)
        {
        }

        public PendingAbruptSignal(LythonRuntimeException? Exception)
            : this(Exception, null, null)
        {
        }

        public PendingAbruptSignal(LythonRuntimeException? Exception, ReturnSignal? Return)
            : this(Exception, Return, null)
        {
        }

        public PendingAbruptSignal(ReturnSignal? Return)
            : this(null, Return, null)
        {
        }

        public PendingAbruptSignal(ControlSignal? Control)
            : this(null, null, Control)
        {
        }
    }

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

        public object this[int index] => _items[index].RequireNotNull();

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
            var value = _items[index].RequireNotNull();
            _items[index] = null;
            Count = index;
            return value;
        }

        public object Peek() => _items[Count - 1].RequireNotNull();

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

        ExecutionContext? context = null;
        try
        {
            context = new ExecutionContext(host, options);
            ExecuteExecutableCodeObject(script.EntryPoint, context);

            return CreateSuccessfulResult(context, null);
        }
        catch (ReturnSignal signal)
        {
            return CreateReturnedResult(signal, context, options);
        }
        catch (LythonRuntimeException ex)
        {
            return CreateRuntimeFailureResult(ex, context);
        }
    }

    internal static void ExecuteExecutableCodeObject(ExecutableCodeObject codeObject, ExecutionContext context)
        => ExecuteExecutableCodeObject(codeObject, context, null, null);

    internal static void ExecuteExecutableCodeObject(ExecutableCodeObject codeObject, ExecutionContext context, IReadOnlyDictionary<string, object>? initialLocals)
        => ExecuteExecutableCodeObject(codeObject, context, initialLocals, null);

    internal static void ExecuteExecutableCodeObject(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        IReadOnlyDictionary<string, object>? initialLocals,
        IReadOnlyList<ExecutableCell>? closureCells)
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
            var blockEntryStackDepths = new int?[codeObject.Blocks.Count];

            void PushObserved(object value, LythonSourceSpan span)
            {
                context.ObserveValue(value, span);
                stack.Push(value);
            }

            void ExecuteDefinitionOrFallback(ExecutableInstruction instruction)
            {
                switch (instruction.OpCode)
                {
                    case ExecutableOpCode.Import:
                        ExecuteExecutableImport(codeObject, codeObject.Imports[instruction.ImportIndex], locals, localCells, context);
                        break;

                    case ExecutableOpCode.DefineFunction:
                        ExecuteExecutableFunctionDefinition(codeObject, codeObject.Functions[instruction.FunctionIndex], locals, localCells, context);
                        break;

                    case ExecutableOpCode.ExecuteFallbackStatement:
                        DispatchLoweredStatementAsync(
                            codeObject.StatementFallbacks[instruction.StatementFallbackIndex].Statement,
                            context,
                            SynchronousLoweredStatementExecution.Instance).GetAwaiter().GetResult();
                        SyncExecutableLocalsFromContext(codeObject, locals, localCells, context);
                        break;
                }
            }

            void ExecuteStackTransfer(ExecutableInstruction instruction)
            {
                switch (instruction.OpCode)
                {
                    case ExecutableOpCode.LoadConst:
                        var constant = RuntimeValue(codeObject.Constants[instruction.ConstantIndex]);
                        context.ObserveValue(constant, instruction.Span);
                        stack.Push(constant);
                        break;

                    case ExecutableOpCode.LoadLocal:
                        stack.Push(LoadLocal(codeObject, locals, instruction.LocalSlot, instruction.Span));
                        break;

                    case ExecutableOpCode.LoadClosure:
                        stack.Push(LoadClosure(codeObject, context, instruction.ClosureSlot, instruction.Span));
                        break;

                    case ExecutableOpCode.LoadGlobal:
                        stack.Push(ResolveExecutableGlobal(codeObject.Names[instruction.NameIndex], instruction.Span, context));
                        break;

                    case ExecutableOpCode.LoadName:
                        stack.Push(ResolveExecutableName(codeObject.Names[instruction.NameIndex], instruction.Span, context));
                        break;

                    case ExecutableOpCode.EvaluateFallbackExpression:
                        var fallback = EvaluateLoweredExpression(codeObject.ExpressionFallbacks[instruction.ExpressionFallbackIndex].Expression, context);
                        context.ObserveValue(fallback, instruction.Span);
                        stack.Push(fallback);
                        break;

                    case ExecutableOpCode.LoadMember:
                        var target = Pop(stack, instruction.Span);
                        var memberName = codeObject.Names[instruction.NameIndex];
                        var memberCache = memberCaches[instruction.MemberCacheIndex] ??= new ExecutableMemberCache();
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

                    case ExecutableOpCode.StoreLocal:
                        var local = Pop(stack, instruction.Span);
                        locals[instruction.LocalSlot] = local;
                        if (localCells?[instruction.LocalSlot] is ExecutableCell localCell)
                        {
                            localCell.Value = local;
                        }

                        if (codeObject.RequiresLocalVariableMirroring)
                        {
                            context.Variables[codeObject.LocalNames[instruction.LocalSlot]] = local;
                        }
                        break;

                    case ExecutableOpCode.StoreClosure:
                        StoreExecutableClosure(
                            codeObject,
                            context,
                            instruction.ClosureSlot,
                            Pop(stack, instruction.Span),
                            instruction.Span);
                        break;

                    case ExecutableOpCode.StoreGlobal:
                        StoreName(
                            codeObject.Names[instruction.NameIndex],
                            Pop(stack, instruction.Span),
                            context,
                            instruction.Span);
                        break;

                    case ExecutableOpCode.StoreName:
                        AssignExecutableBoundName(
                            codeObject,
                            locals,
                            localCells,
                            codeObject.Names[instruction.NameIndex],
                            Pop(stack, instruction.Span),
                            context,
                            instruction.Span);
                        break;

                    case ExecutableOpCode.Dup:
                        stack.Push(Peek(stack, instruction.Span));
                        break;

                    case ExecutableOpCode.PopTop:
                        _ = Pop(stack, instruction.Span);
                        break;
                }
            }

            bool ExecuteStructure(ExecutableInstruction instruction)
            {
                switch (instruction.OpCode)
                {
                    case ExecutableOpCode.MakeList:
                        PushObserved(CreateListFromStack(stack, instruction.ItemCount, instruction.Span, context), instruction.Span);
                        break;

                    case ExecutableOpCode.MakeTuple:
                        PushObserved(CreateTupleFromStack(stack, instruction.ItemCount, instruction.Span, context), instruction.Span);
                        break;

                    case ExecutableOpCode.MakeSet:
                        PushObserved(ExecuteExecutableMakeSet(stack, instruction.ItemCount, instruction.Span, context), instruction.Span);
                        break;

                    case ExecutableOpCode.MakeDict:
                        PushObserved(ExecuteExecutableMakeDict(stack, instruction.PairCount, instruction.Span, context), instruction.Span);
                        break;

                    case ExecutableOpCode.ResolveContextManager:
                        var managerValue = Pop(stack, instruction.Span);
                        stack.Push(PyContextManagers.Resolve(managerValue, instruction.Span, context));
                        break;

                    case ExecutableOpCode.EnterContextManager:
                        var enteringManager = PopContextManager(stack, instruction.Span);
                        PushObserved(enteringManager.Enter(), instruction.Span);
                        break;

                    case ExecutableOpCode.ExitContextManager:
                        var exitingManager = PopContextManager(stack, instruction.Span);
                        // Only exceptions are suppressible. Return/break/continue
                        // are normal exits to __exit__ and remain pending afterward.
                        if (pendingAbrupt?.Exception is { } exception)
                        {
                            if (exitingManager.Exit(exception.ExceptionType, exception, PyNone.Instance))
                            {
                                pendingAbrupt = null;
                            }
                        }
                        else
                        {
                            _ = exitingManager.Exit(PyNone.Instance, PyNone.Instance, PyNone.Instance);
                        }
                        break;

                    case ExecutableOpCode.MatchCase:
                        var subject = Pop(stack, instruction.Span);
                        if (!TryExecuteExecutableMatchCase(
                                codeObject.MatchCases[instruction.MatchCaseIndex],
                                subject,
                                context,
                                locals,
                                localCells))
                        {
                            currentBlockIndex = instruction.FailureBlockIndex;
                            return true;
                        }
                        break;
                }

                return false;
            }

            bool ExecuteValueOperation(ExecutableInstruction instruction)
            {
                switch (instruction.OpCode)
                {
                    case ExecutableOpCode.GetIter:
                        var iterable = Pop(stack, instruction.Span);
                        stack.Push(ToSequence(iterable, instruction.Span, context).GetEnumerator());
                        break;

                    case ExecutableOpCode.ForNext:
                        var iterator = PeekIterator(stack, instruction.Span);
                        if (!iterator.MoveNext())
                        {
                            _ = Pop(stack, instruction.Span);
                            currentBlockIndex = instruction.TargetBlockIndex;
                            return true;
                        }

                        stack.Push(iterator.Current);
                        break;

                    case ExecutableOpCode.AssignLoopTarget:
                        var loopBinding = codeObject.LoopTargets[instruction.LoopTargetIndex];
                        AssignLoopTarget(loopBinding.Target, Pop(stack, instruction.Span), loopBinding.Span, context);
                        break;

                    case ExecutableOpCode.AssignUnpackingTargets:
                        var unpackingBinding = codeObject.UnpackingTargets[instruction.UnpackingTargetIndex];
                        AssignTargets(unpackingBinding.Targets, Pop(stack, instruction.Span), unpackingBinding.Span, context);
                        break;

                    case ExecutableOpCode.Call:
                        PushObserved(ExecuteExecutableCall(
                            codeObject.CallSites[instruction.CallSiteIndex],
                            stack,
                            context,
                            callCaches[instruction.CallCacheIndex] ??= new ExecutableCallCache()), instruction.Span);
                        break;

                    case ExecutableOpCode.Subscript:
                        var index = Pop(stack, instruction.Span);
                        var target = Pop(stack, instruction.Span);
                        PushObserved(ExecuteExecutableSubscript(target, index, instruction.Span, context), instruction.Span);
                        break;

                    case ExecutableOpCode.Slice:
                        PushObserved(ExecuteExecutableSlice(stack, instruction.SliceParts, instruction.Span, context), instruction.Span);
                        break;

                    case ExecutableOpCode.Binary:
                        var binaryRight = Pop(stack, instruction.Span);
                        var binaryLeft = Pop(stack, instruction.Span);
                        PushObserved(EvaluateExecutableBinary(
                            instruction.BinaryOperator,
                            binaryLeft,
                            binaryRight,
                            instruction.Span,
                            context), instruction.Span);
                        break;

                    case ExecutableOpCode.Augmented:
                        var augmentedRight = Pop(stack, instruction.Span);
                        var augmentedLeft = Pop(stack, instruction.Span);
                        PushObserved(EvaluateExecutableAugmented(
                            instruction.AugmentedOperator,
                            augmentedLeft,
                            augmentedRight,
                            context,
                            instruction.Span), instruction.Span);
                        break;

                    case ExecutableOpCode.Unary:
                        PushObserved(EvaluateExecutableUnary(
                            instruction.UnaryOperator,
                            Pop(stack, instruction.Span),
                            context,
                            instruction.Span), instruction.Span);
                        break;
                }

                return false;
            }

            bool ExecuteControlFlow(ExecutableInstruction instruction)
            {
                switch (instruction.OpCode)
                {
                    case ExecutableOpCode.JumpIfFalse:
                        if (!IsTruthy(Pop(stack, instruction.Span), context, instruction.Span))
                        {
                            currentBlockIndex = instruction.TargetBlockIndex;
                            return true;
                        }
                        return false;

                    case ExecutableOpCode.Jump:
                        currentBlockIndex = instruction.TargetBlockIndex;
                        return true;

                    case ExecutableOpCode.ClearException:
                        if (instruction.ExceptionNameIndex >= 0)
                        {
                            _ = DeleteName(codeObject.Names[instruction.ExceptionNameIndex], context, instruction.Span);
                        }
                        context.Services.SetCurrentException(null);
                        return false;

                    case ExecutableOpCode.EndFinally:
                        if (pendingAbrupt is not null)
                        {
                            PropagatePendingAbrupt(pendingAbrupt);
                        }

                        currentBlockIndex = instruction.TargetBlockIndex;
                        return true;

                    case ExecutableOpCode.Return:
                        throw new ReturnSignal(Pop(stack, instruction.Span));

                    case ExecutableOpCode.ReturnNone:
                        throw new ReturnSignal(PyNone.Instance);

                    default:
                        throw new InvalidOperationException($"Unknown executable opcode: {instruction.OpCode}");
                }
            }

            // Every control-flow edge must arrive at a block with the same value
            // stack depth. The first arrival records that depth; edge handling
            // below validates later arrivals. Abrupt signals remain separate so
            // finally blocks can run before return/break/continue is rethrown.
            while (true)
            {
                var block = codeObject.Blocks[currentBlockIndex];
                blockEntryStackDepths[currentBlockIndex] ??= stack.Count;
                var jumped = false;

                foreach (var instruction in block.Instructions)
                {
                    context.Services.CheckExecutionBudget(instruction.Span);

                    try
                    {
                        switch (instruction.OpCode)
                        {
                            case ExecutableOpCode.Import or
                                 ExecutableOpCode.DefineFunction or
                                 ExecutableOpCode.ExecuteFallbackStatement:
                                ExecuteDefinitionOrFallback(instruction);
                                break;

                            case ExecutableOpCode.LoadConst or
                                 ExecutableOpCode.LoadLocal or
                                 ExecutableOpCode.LoadClosure or
                                 ExecutableOpCode.LoadGlobal or
                                 ExecutableOpCode.LoadName or
                                 ExecutableOpCode.EvaluateFallbackExpression or
                                 ExecutableOpCode.LoadMember or
                                 ExecutableOpCode.StoreLocal or
                                 ExecutableOpCode.StoreClosure or
                                 ExecutableOpCode.StoreGlobal or
                                 ExecutableOpCode.StoreName or
                                 ExecutableOpCode.Dup or
                                 ExecutableOpCode.PopTop:
                                ExecuteStackTransfer(instruction);
                                break;

                            case ExecutableOpCode.MakeList or
                                 ExecutableOpCode.MakeTuple or
                                 ExecutableOpCode.MakeSet or
                                 ExecutableOpCode.MakeDict or
                                 ExecutableOpCode.ResolveContextManager or
                                 ExecutableOpCode.EnterContextManager or
                                 ExecutableOpCode.ExitContextManager or
                                 ExecutableOpCode.MatchCase:
                                jumped = ExecuteStructure(instruction);
                                break;

                            case ExecutableOpCode.GetIter or
                                 ExecutableOpCode.ForNext or
                                 ExecutableOpCode.AssignLoopTarget or
                                 ExecutableOpCode.AssignUnpackingTargets or
                                 ExecutableOpCode.Call or
                                 ExecutableOpCode.Subscript or
                                 ExecutableOpCode.Slice or
                                 ExecutableOpCode.Binary or
                                 ExecutableOpCode.Augmented or
                                 ExecutableOpCode.Unary:
                                jumped = ExecuteValueOperation(instruction);
                                break;

                            case ExecutableOpCode.JumpIfFalse or
                                 ExecutableOpCode.Jump or
                                 ExecutableOpCode.ClearException or
                                 ExecutableOpCode.EndFinally or
                                 ExecutableOpCode.Return or
                                 ExecutableOpCode.ReturnNone:
                                jumped = ExecuteControlFlow(instruction);
                                break;

                            default:
                                throw new InvalidOperationException($"Unknown executable opcode: {instruction.OpCode}");
                        }
                    }
                    catch (ReturnSignal signal)
                    {
                        if (!TryHandleAbrupt(codeObject, context, stack, blockEntryStackDepths, currentBlockIndex, new PendingAbruptSignal(Return: signal), instruction.Span, ref pendingAbrupt, ref currentBlockIndex))
                        {
                            throw;
                        }

                        jumped = true;
                    }
                    catch (ControlSignal signal)
                    {
                        if (!TryHandleAbrupt(codeObject, context, stack, blockEntryStackDepths, currentBlockIndex, new PendingAbruptSignal(Control: signal), instruction.Span, ref pendingAbrupt, ref currentBlockIndex))
                        {
                            throw;
                        }

                        jumped = true;
                    }
                    catch (LythonRuntimeException ex)
                    {
                        if (!TryHandleAbrupt(codeObject, context, stack, blockEntryStackDepths, currentBlockIndex, new PendingAbruptSignal(Exception: ex), instruction.Span, ref pendingAbrupt, ref currentBlockIndex))
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

}
