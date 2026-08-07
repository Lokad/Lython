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
                            case ExecutableOpCode.Import:
                                ExecuteExecutableImport(codeObject, codeObject.Imports[instruction.ImportIndex], locals, localCells, context);
                                break;

                            case ExecutableOpCode.DefineFunction:
                                ExecuteExecutableFunctionDefinition(codeObject, codeObject.Functions[instruction.FunctionIndex], locals, localCells, context);
                                break;

                            case ExecutableOpCode.ExecuteFallbackStatement:
                                ExecuteLoweredStatement(codeObject.StatementFallbacks[instruction.StatementFallbackIndex].Statement, context);
                                SyncExecutableLocalsFromContext(codeObject, locals, localCells, context);
                                break;

                            case ExecutableOpCode.LoadConst:
                                {
                                    var value = RuntimeValue(codeObject.Constants[instruction.ConstantIndex]);
                                    context.ObserveValue(value, instruction.Span);
                                    stack.Push(value);
                                    break;
                                }

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
                                {
                                    var value = EvaluateLoweredExpression(codeObject.ExpressionFallbacks[instruction.ExpressionFallbackIndex].Expression, context);
                                    context.ObserveValue(value, instruction.Span);
                                    stack.Push(value);
                                    break;
                                }

                            case ExecutableOpCode.LoadMember:
                                {
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
                                }

                            case ExecutableOpCode.StoreLocal:
                                {
                                    var value = Pop(stack, instruction.Span);
                                    locals[instruction.LocalSlot] = value;
                                    if (localCells?[instruction.LocalSlot] is ExecutableCell localCell)
                                    {
                                        localCell.Value = value;
                                    }

                                    if (codeObject.RequiresLocalVariableMirroring)
                                    {
                                        context.Variables[codeObject.LocalNames[instruction.LocalSlot]] = value;
                                    }
                                    break;
                                }

                            case ExecutableOpCode.StoreClosure:
                                {
                                    var value = Pop(stack, instruction.Span);
                                    StoreExecutableClosure(codeObject, context, instruction.ClosureSlot, value, instruction.Span);
                                    break;
                                }

                            case ExecutableOpCode.StoreGlobal:
                                {
                                    var value = Pop(stack, instruction.Span);
                                    var name = codeObject.Names[instruction.NameIndex];
                                    StoreName(name, value, context, instruction.Span);
                                    break;
                                }

                            case ExecutableOpCode.StoreName:
                                {
                                    var value = Pop(stack, instruction.Span);
                                    var name = codeObject.Names[instruction.NameIndex];
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
                                    var value = CreateListFromStack(stack, instruction.ItemCount, instruction.Span, context);
                                    context.ObserveValue(value, instruction.Span);
                                    stack.Push(value);
                                    break;
                                }

                            case ExecutableOpCode.MakeTuple:
                                {
                                    var value = CreateTupleFromStack(stack, instruction.ItemCount, instruction.Span, context);
                                    context.ObserveValue(value, instruction.Span);
                                    stack.Push(value);
                                    break;
                                }

                            case ExecutableOpCode.MakeSet:
                                {
                                    var value = ExecuteExecutableMakeSet(stack, instruction.ItemCount, instruction.Span, context);
                                    context.ObserveValue(value, instruction.Span);
                                    stack.Push(value);
                                    break;
                                }

                            case ExecutableOpCode.MakeDict:
                                {
                                    var value = ExecuteExecutableMakeDict(stack, instruction.PairCount, instruction.Span, context);
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
                                    // Only exceptions are suppressible. Return/break/continue
                                    // are normal exits to __exit__ and remain pending afterward.
                                    if (pendingAbrupt?.Exception is { } exception)
                                    {
                                        if (manager.Exit(exception.ExceptionType, exception, PyNone.Instance))
                                        {
                                            pendingAbrupt = null;
                                        }
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
                                            codeObject.MatchCases[instruction.MatchCaseIndex],
                                            subject,
                                            context,
                                            locals,
                                            localCells))
                                    {
                                        currentBlockIndex = instruction.FailureBlockIndex;
                                        jumped = true;
                                    }
                                    break;
                                }

                            case ExecutableOpCode.GetIter:
                                {
                                    var iterable = Pop(stack, instruction.Span);
                                    stack.Push(ToSequence(iterable, instruction.Span, context).GetEnumerator());
                                    break;
                                }

                            case ExecutableOpCode.ForNext:
                                {
                                    var iterator = PeekIterator(stack, instruction.Span);
                                    if (!iterator.MoveNext())
                                    {
                                        _ = Pop(stack, instruction.Span);
                                        currentBlockIndex = instruction.TargetBlockIndex;
                                        jumped = true;
                                        break;
                                    }

                                    stack.Push(iterator.Current);
                                    break;
                                }

                            case ExecutableOpCode.AssignLoopTarget:
                                {
                                    var value = Pop(stack, instruction.Span);
                                    var binding = codeObject.LoopTargets[instruction.LoopTargetIndex];
                                    AssignLoopTarget(binding.Target, value, binding.Span, context);
                                    break;
                                }

                            case ExecutableOpCode.AssignUnpackingTargets:
                                {
                                    var value = Pop(stack, instruction.Span);
                                    var binding = codeObject.UnpackingTargets[instruction.UnpackingTargetIndex];
                                    AssignTargets(binding.Targets, value, binding.Span, context);
                                    break;
                                }

                            case ExecutableOpCode.Call:
                                {
                                    var value = ExecuteExecutableCall(codeObject.CallSites[instruction.CallSiteIndex], stack, context, callCaches[instruction.CallCacheIndex] ??= new ExecutableCallCache());
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
                                    var value = ExecuteExecutableSlice(stack, instruction.SliceParts, instruction.Span, context);
                                    context.ObserveValue(value, instruction.Span);
                                    stack.Push(value);
                                    break;
                                }

                            case ExecutableOpCode.Binary:
                                {
                                    var right = Pop(stack, instruction.Span);
                                    var left = Pop(stack, instruction.Span);
                                    var value = EvaluateExecutableBinary(instruction.BinaryOperator, left, right, instruction.Span, context);
                                    context.ObserveValue(value, instruction.Span);
                                    stack.Push(value);
                                    break;
                                }

                            case ExecutableOpCode.Augmented:
                                {
                                    var right = Pop(stack, instruction.Span);
                                    var left = Pop(stack, instruction.Span);
                                    var value = EvaluateExecutableAugmented(instruction.AugmentedOperator, left, right, context, instruction.Span);
                                    context.ObserveValue(value, instruction.Span);
                                    stack.Push(value);
                                    break;
                                }

                            case ExecutableOpCode.Unary:
                                {
                                    var operand = Pop(stack, instruction.Span);
                                    var value = EvaluateExecutableUnary(instruction.UnaryOperator, operand, context, instruction.Span);
                                    context.ObserveValue(value, instruction.Span);
                                    stack.Push(value);
                                    break;
                                }

                            case ExecutableOpCode.JumpIfFalse:
                                {
                                    var condition = Pop(stack, instruction.Span);
                                    if (!IsTruthy(condition, context, instruction.Span))
                                    {
                                        currentBlockIndex = instruction.TargetBlockIndex;
                                        jumped = true;
                                    }
                                    break;
                                }

                            case ExecutableOpCode.Jump:
                                currentBlockIndex = instruction.TargetBlockIndex;
                                jumped = true;
                                break;

                            case ExecutableOpCode.ClearException:
                                if (instruction.ExceptionNameIndex >= 0)
                                {
                                    _ = DeleteName(codeObject.Names[instruction.ExceptionNameIndex], context, instruction.Span);
                                }
                                context.Services.SetCurrentException(null);
                                break;

                            case ExecutableOpCode.EndFinally:
                                if (pendingAbrupt is not null)
                                {
                                    PropagatePendingAbrupt(pendingAbrupt);
                                }

                                currentBlockIndex = instruction.TargetBlockIndex;
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
