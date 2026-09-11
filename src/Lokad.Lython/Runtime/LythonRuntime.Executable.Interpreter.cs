using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    /// <summary>
    /// Owns the mutable stack, caches, and control-flow state for one executable
    /// code-object invocation. Keeping this state together makes opcode handlers
    /// independently reviewable without extending the runtime entry-point method.
    /// </summary>
    private sealed class ExecutableFrameInterpreter(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        object[] locals,
        ExecutableCell?[]? localCells)
    {
        private readonly ExecutableValueStack _stack = new(Math.Max(8, codeObject.LocalNames.Count));
        private int _currentBlockIndex = codeObject.EntryBlockIndex;
        private PendingAbruptSignal? _pendingAbrupt;
        private readonly Stack<ActiveExceptionSave> _savedActiveExceptions = new();
        private readonly PyException? _entryActiveException = context.Services.CurrentException;

        private sealed record ActiveExceptionSave(
            PyException? SavedException,
            int? SuiteStartBlockIndex,
            int? SuiteEndBlockIndex);

        // Abandoning the frame drops its saved handler chain and restores the
        // active exception from frame entry, like CPython deleting handler
        // names and restoring the previous exception on frame exit.
        private void AbandonFrame()
        {
            _savedActiveExceptions.Clear();
            context.Services.SetCurrentException(_entryActiveException);
        }

        // Pops saves for suites this propagation abandoned: a handler stays
        // live exactly while execution resumes inside its suite. Entries
        // without a suite range are kept, erring toward a stale value rather
        // than dropping a live save.
        private void UnwindAbandonedHandlers(int targetBlockIndex)
        {
            while (_savedActiveExceptions.Count > 0 &&
                _savedActiveExceptions.Peek() is { SuiteStartBlockIndex: int start, SuiteEndBlockIndex: int end } &&
                (targetBlockIndex < start || targetBlockIndex > end))
            {
                context.Services.SetCurrentException(_savedActiveExceptions.Pop().SavedException);
            }
        }

        private readonly ExecutableMemberCache?[] _memberCaches = new ExecutableMemberCache?[codeObject.MemberCacheCount];
        private readonly ExecutableCallCache?[] _callCaches = new ExecutableCallCache?[codeObject.CallCacheCount];
        private readonly int?[] _blockEntryStackDepths = new int?[codeObject.Blocks.Count];

        private void PushObserved(object value, LythonSourceSpan span)
        {
            context.ObserveValue(value, span);
            _stack.Push(value);
        }

        private void ExecuteDefinitionOrFallback(ExecutableInstruction instruction)
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

        private void ExecuteStackTransfer(ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.LoadConst:
                    var constant = RuntimeValue(codeObject.Constants[instruction.ConstantIndex]);
                    context.ObserveValue(constant, instruction.Span);
                    _stack.Push(constant);
                    break;

                case ExecutableOpCode.LoadLocal:
                    _stack.Push(LoadLocal(codeObject, locals, instruction.LocalSlot, instruction.Span));
                    break;

                case ExecutableOpCode.LoadClosure:
                    _stack.Push(LoadClosure(codeObject, context, instruction.ClosureSlot, instruction.Span));
                    break;

                case ExecutableOpCode.LoadGlobal:
                    _stack.Push(ResolveExecutableGlobal(codeObject.Names[instruction.NameIndex], instruction.Span, context));
                    break;

                case ExecutableOpCode.LoadName:
                    _stack.Push(ResolveExecutableName(codeObject.Names[instruction.NameIndex], instruction.Span, context));
                    break;

                case ExecutableOpCode.EvaluateFallbackExpression:
                    var fallback = EvaluateLoweredExpression(codeObject.ExpressionFallbacks[instruction.ExpressionFallbackIndex].Expression, context);
                    context.ObserveValue(fallback, instruction.Span);
                    _stack.Push(fallback);
                    break;

                case ExecutableOpCode.LoadMember:
                    var target = Pop(_stack, instruction.Span);
                    var memberName = codeObject.Names[instruction.NameIndex];
                    var cachedMember = _memberCaches[instruction.MemberCacheIndex];
                    object memberValue;
                    if (cachedMember is not null && ReferenceEquals(cachedMember.Target, target))
                    {
                        // Identity is required: equal mutable Python values can expose different
                        // instance members, while cacheable builtin targets have stable lookup rules.
                        memberValue = cachedMember.Value;
                    }
                    else
                    {
                        if (!TryResolveRuntimeMember(target, memberName, context, instruction.Span, out var resolvedMember))
                        {
                            throw PyMemberAccess.CreateMissingMemberError(target, memberName, instruction.Span, context);
                        }

                        memberValue = resolvedMember;
                        _memberCaches[instruction.MemberCacheIndex] = CanCacheRuntimeMemberValue(target, memberValue)
                            ? new ExecutableMemberCache(target, memberValue)
                            : null;
                    }

                    context.ObserveValue(memberValue, instruction.Span);
                    _stack.Push(memberValue);
                    break;

                case ExecutableOpCode.StoreLocal:
                    var local = Pop(_stack, instruction.Span);
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
                        Pop(_stack, instruction.Span),
                        instruction.Span);
                    break;

                case ExecutableOpCode.StoreGlobal:
                    StoreName(
                        codeObject.Names[instruction.NameIndex],
                        Pop(_stack, instruction.Span),
                        context,
                        instruction.Span);
                    break;

                case ExecutableOpCode.StoreName:
                    AssignExecutableBoundName(
                        codeObject,
                        locals,
                        localCells,
                        codeObject.Names[instruction.NameIndex],
                        Pop(_stack, instruction.Span),
                        context,
                        instruction.Span);
                    break;

                case ExecutableOpCode.Dup:
                    _stack.Push(Peek(_stack, instruction.Span));
                    break;

                case ExecutableOpCode.PopTop:
                    _ = Pop(_stack, instruction.Span);
                    break;
            }
        }

        private bool ExecuteStructure(ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.MakeList:
                    PushObserved(CreateListFromStack(_stack, instruction.ItemCount, instruction.Span, context), instruction.Span);
                    break;

                case ExecutableOpCode.MakeTuple:
                    PushObserved(CreateTupleFromStack(_stack, instruction.ItemCount, instruction.Span, context), instruction.Span);
                    break;

                case ExecutableOpCode.MakeSet:
                    PushObserved(ExecuteExecutableMakeSet(_stack, instruction.ItemCount, instruction.Span, context), instruction.Span);
                    break;

                case ExecutableOpCode.MakeDict:
                    PushObserved(ExecuteExecutableMakeDict(_stack, instruction.PairCount, instruction.Span, context), instruction.Span);
                    break;

                case ExecutableOpCode.ResolveContextManager:
                    var managerValue = Pop(_stack, instruction.Span);
                    _stack.Push(PyContextManagers.Resolve(managerValue, instruction.Span, context));
                    break;

                case ExecutableOpCode.EnterContextManager:
                    var enteringManager = PopContextManager(_stack, instruction.Span);
                    PushObserved(enteringManager.Enter(), instruction.Span);
                    break;

                case ExecutableOpCode.ExitContextManager:
                    var exitingManager = PopContextManager(_stack, instruction.Span);
                    // Only exceptions are suppressible. Return/break/continue
                    // are normal exits to __exit__ and remain pending afterward.
                    if (_pendingAbrupt is PendingException { Exception: var exception })
                    {
                        if (exitingManager.Exit(
                                ResolvePythonExceptionType(exception, context),
                                CreatePythonExceptionInstance(exception),
                                PyNone.Instance))
                        {
                            _pendingAbrupt = null;
                            // Suppression completes the cleanup suite: restore
                            // the active exception saved on entry.
                            context.Services.SetCurrentException(
                                _savedActiveExceptions.Count > 0 ? _savedActiveExceptions.Pop().SavedException : null);
                        }
                    }
                    else
                    {
                        _ = exitingManager.Exit(PyNone.Instance, PyNone.Instance, PyNone.Instance);
                    }
                    break;

                case ExecutableOpCode.MatchCase:
                    var subject = Pop(_stack, instruction.Span);
                    if (!TryExecuteExecutableMatchCase(
                            codeObject.MatchCases[instruction.MatchCaseIndex],
                            subject,
                            context,
                            locals,
                            localCells))
                    {
                        _currentBlockIndex = instruction.FailureBlockIndex;
                        return true;
                    }
                    break;
            }

            return false;
        }

        private bool ExecuteValueOperation(ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.GetIter:
                    var iterable = Pop(_stack, instruction.Span);
                    _stack.Push(ToSequence(iterable, instruction.Span, context).GetEnumerator());
                    break;

                case ExecutableOpCode.ForNext:
                    var iterator = PeekIterator(_stack, instruction.Span);
                    if (!iterator.MoveNext())
                    {
                        _ = Pop(_stack, instruction.Span);
                        _currentBlockIndex = instruction.TargetBlockIndex;
                        return true;
                    }

                    _stack.Push(iterator.Current);
                    break;

                case ExecutableOpCode.AssignLoopTarget:
                    var loopBinding = codeObject.LoopTargets[instruction.LoopTargetIndex];
                    AssignLoopTarget(loopBinding.Target, Pop(_stack, instruction.Span), loopBinding.Span, context);
                    break;

                case ExecutableOpCode.AssignUnpackingTargets:
                    var unpackingBinding = codeObject.UnpackingTargets[instruction.UnpackingTargetIndex];
                    AssignTargets(unpackingBinding.Targets, Pop(_stack, instruction.Span), unpackingBinding.Span, context);
                    break;

                case ExecutableOpCode.Call:
                    var callResult = ExecuteExecutableCall(
                        codeObject.CallSites[instruction.CallSiteIndex],
                        _stack,
                        context,
                        ref _callCaches[instruction.CallCacheIndex]);
                    PushObserved(callResult, instruction.Span);
                    break;

                case ExecutableOpCode.Subscript:
                    var index = Pop(_stack, instruction.Span);
                    var target = Pop(_stack, instruction.Span);
                    PushObserved(ReadSubscriptValue(target, index, instruction.Span, context), instruction.Span);
                    break;

                case ExecutableOpCode.Slice:
                    PushObserved(ExecuteExecutableSlice(_stack, instruction.SliceParts, instruction.Span, context), instruction.Span);
                    break;

                case ExecutableOpCode.Binary:
                    var binaryRight = Pop(_stack, instruction.Span);
                    var binaryLeft = Pop(_stack, instruction.Span);
                    PushObserved(EvaluateExecutableBinary(
                        instruction.BinaryOperator,
                        binaryLeft,
                        binaryRight,
                        instruction.Span,
                        context), instruction.Span);
                    break;

                case ExecutableOpCode.Augmented:
                    var augmentedRight = Pop(_stack, instruction.Span);
                    var augmentedLeft = Pop(_stack, instruction.Span);
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
                        Pop(_stack, instruction.Span),
                        context,
                        instruction.Span), instruction.Span);
                    break;
            }

            return false;
        }

        private bool ExecuteControlFlow(ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.JumpIfFalse:
                    if (!IsTruthy(Pop(_stack, instruction.Span), context, instruction.Span))
                    {
                        _currentBlockIndex = instruction.TargetBlockIndex;
                        return true;
                    }
                    return false;

                case ExecutableOpCode.Jump:
                    _currentBlockIndex = instruction.TargetBlockIndex;
                    return true;

                case ExecutableOpCode.ClearException:
                    if (instruction.ExceptionNameIndex >= 0)
                    {
                        _ = DeleteName(codeObject.Names[instruction.ExceptionNameIndex], context, instruction.Span);
                    }
                    context.Services.SetCurrentException(
                        _savedActiveExceptions.Count > 0 ? _savedActiveExceptions.Pop().SavedException : null);
                    return false;

                case ExecutableOpCode.EndFinally:
                    if (_pendingAbrupt is PendingException)
                    {
                        // An exception-routed finally suite exits: restore the
                        // active exception saved when the suite was entered.
                        context.Services.SetCurrentException(
                            _savedActiveExceptions.Count > 0 ? _savedActiveExceptions.Pop().SavedException : null);
                    }

                    if (_pendingAbrupt is not null)
                    {
                        PropagatePendingAbrupt(_pendingAbrupt);
                    }

                    _currentBlockIndex = instruction.TargetBlockIndex;
                    return true;

                case ExecutableOpCode.Return:
                    throw new ReturnSignal(Pop(_stack, instruction.Span));

                case ExecutableOpCode.ReturnNone:
                    throw new ReturnSignal(PyNone.Instance);

                default:
                    throw new InvalidOperationException($"Unknown executable opcode: {instruction.OpCode}");
            }
        }

        public void Execute()
        {
            // Every control-flow edge must arrive at a block with the same value
            // stack depth. The first arrival records that depth; edge handling
            // below validates later arrivals. Abrupt signals remain separate so
            // finally blocks can run before return/break/continue is rethrown.
            while (true)
            {
                var block = codeObject.Blocks[_currentBlockIndex];
                _blockEntryStackDepths[_currentBlockIndex] ??= _stack.Count;
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
                        if (!TryHandleAbrupt(codeObject, context, _stack, _blockEntryStackDepths, _currentBlockIndex, new PendingReturn(signal), instruction.Span, ref _pendingAbrupt, ref _currentBlockIndex, out _))
                        {
                            AbandonFrame();
                            throw;
                        }

                        jumped = true;
                    }
                    catch (ControlSignal signal)
                    {
                        if (!TryHandleAbrupt(codeObject, context, _stack, _blockEntryStackDepths, _currentBlockIndex, new PendingControl(signal), instruction.Span, ref _pendingAbrupt, ref _currentBlockIndex, out _))
                        {
                            AbandonFrame();
                            throw;
                        }

                        jumped = true;
                    }
                    catch (LythonRuntimeException ex)
                    {
                        var previousActive = context.Services.CurrentException;
                        if (!TryHandleAbrupt(codeObject, context, _stack, _blockEntryStackDepths, _currentBlockIndex, new PendingException(ex), instruction.Span, ref _pendingAbrupt, ref _currentBlockIndex, out var matchedRegion))
                        {
                            AbandonFrame();
                            throw;
                        }

                        var routedToHandler = _pendingAbrupt is null;
                        var routedToCleanup = !routedToHandler &&
                            _pendingAbrupt is PendingException pending &&
                            ReferenceEquals(pending.Exception, ex);
                        if (routedToHandler || routedToCleanup)
                        {
                            // The except route already installed the handler
                            // exception; hold it aside and reseed the displaced
                            // live nesting while abandoned suites unwind
                            // beneath it. The current block now targets the
                            // handler or cleanup suite.
                            var installed = routedToHandler ? context.Services.CurrentException : null;
                            context.Services.SetCurrentException(previousActive);
                            UnwindAbandonedHandlers(_currentBlockIndex);
                            _savedActiveExceptions.Push(new ActiveExceptionSave(
                                context.Services.CurrentException,
                                matchedRegion?.SuiteStartBlockIndex,
                                matchedRegion?.SuiteEndBlockIndex));
                            context.Services.SetCurrentException(
                                routedToHandler ? installed : CreatePythonExceptionInstance(ex));
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
    }
}
