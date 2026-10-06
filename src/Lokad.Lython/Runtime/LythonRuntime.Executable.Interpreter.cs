using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    /// <summary>
    /// Owns the mutable stack, caches, and control-flow state for one executable
    /// code-object invocation. Keeping this state together makes opcode handlers
    /// independently reviewable without extending the runtime entry-point method.
    /// </summary>
    private sealed partial class ExecutableFrameInterpreter(
        ExecutableCodeObject codeObject,
        ExecutionContext context,
        object[] locals,
        ExecutableCell?[]? localCells)
    {
        private readonly ExecutableValueStack _stack = new(Math.Max(8, codeObject.LocalNames.Count));
        private int _currentBlockIndex = codeObject.EntryBlockIndex;
        private PendingAbruptSignal? _pendingAbrupt;
        private int _instructionIndex;
        private bool _waitingForSend;
        private object _sentValue = PyNone.Instance;
        private LythonRuntimeException? _injectedException;
        private GeneratorDelegation? _delegation;
        internal bool HasYield { get; private set; }
        internal object YieldValue { get; private set; } = PyNone.Instance;

        internal void Resume(object sent, LythonRuntimeException? injected)
        {
            HasYield = false;
            _sentValue = sent;
            _injectedException = injected;
            if (_waitingForSend)
            {
                _waitingForSend = false;
                if (injected is null) _stack.Push(sent);
                _sentValue = PyNone.Instance;
            }
        }
        private object? _frameReturnValue;
        private bool _hasFrameReturn;
        // Handler-save chain, created on first except-route push: plain calls
        // never touch it, so the per-frame Stack box is skipped.
        private Stack<ActiveExceptionSave>? _savedActiveExceptions;

        private Stack<ActiveExceptionSave> SavedActiveExceptions() => _savedActiveExceptions ??= new();

        private PyException? _entryActiveException = context.Services.CurrentException;

        private sealed class ActiveExceptionSave(PyException? savedException, int? suiteStartBlockIndex, int? suiteEndBlockIndex, PendingAbruptSignal? savedPendingAbrupt = null)
        {
            public PyException? SavedException { get; set; } = savedException;
            public int? SuiteStartBlockIndex { get; } = suiteStartBlockIndex;
            public int? SuiteEndBlockIndex { get; } = suiteEndBlockIndex;
            public PendingAbruptSignal? SavedPendingAbrupt { get; } = savedPendingAbrupt;
        }

        internal bool HasActiveHandler => _savedActiveExceptions is { Count: > 0 };
        internal void SetCallerException(PyException? exception)
        {
            _entryActiveException = exception;
            if (_savedActiveExceptions is { Count: > 0 } saves)
            {
                ActiveExceptionSave? outermost = null;
                foreach (var save in saves) outermost = save;
                outermost!.SavedException = exception;
            }
        }

        // Abandoning the frame drops its saved handler chain and restores the
        // active exception from frame entry, like CPython deleting handler
        // names and restoring the previous exception on frame exit.
        private void AbandonFrame(LythonSourceSpan span)
        {
            _savedActiveExceptions?.Clear();
            AbandonActiveHandlerVars(context, span);
            context.Services.SetCurrentException(_entryActiveException);
        }

        // Pops saves for suites this propagation abandoned: a handler stays
        // live exactly while execution resumes inside its suite. Entries
        // without a suite range are kept, erring toward a stale value rather
        // than dropping a live save.
        private void UnwindAbandonedHandlers(int targetBlockIndex)
        {
            while (_savedActiveExceptions is { Count: > 0 } saves &&
                saves.Peek() is { SuiteStartBlockIndex: int start, SuiteEndBlockIndex: int end } &&
                (targetBlockIndex < start || targetBlockIndex > end))
            {
                context.Services.SetCurrentException(saves.Pop().SavedException);
            }
        }

        // Empty cache tables are shared: no instruction can name an index the
        // compiler never emitted (any such access already throws today).
        private readonly ExecutableMemberCache?[] _memberCaches = codeObject.MemberCacheCount == 0 ? Array.Empty<ExecutableMemberCache?>() : new ExecutableMemberCache?[codeObject.MemberCacheCount];
        private readonly ExecutableCallCache?[] _callCaches = codeObject.CallCacheCount == 0 ? Array.Empty<ExecutableCallCache?>() : new ExecutableCallCache?[codeObject.CallCacheCount];
        // Depth entries are recorded per block visit but only read when a
        // region matches, so region-free frames share the empty table.
        private readonly int?[] _blockEntryStackDepths = codeObject.ExceptionRegions.Count == 0 ? Array.Empty<int?>() : new int?[codeObject.Blocks.Count];

        // Stores through the same cell-mirroring and host-mirroring policy as
        // StoreLocal, so synthetic slots (chain, match, with) behave exactly
        // like compiler-issued stores.
        private static void StoreLocalValue(
            ExecutableCodeObject codeObject,
            object[] locals,
            ExecutableCell?[]? localCells,
            ExecutionContext context,
            int slot,
            object value,
            LythonSourceSpan span)
        {
            locals[slot] = value;
            if (localCells?[slot] is ExecutableCell localCell)
            {
                localCell.Value = value;
            }

            if (codeObject.RequiresLocalVariableMirroring || context.MirrorLocalStores)
            {
                context.Variables[codeObject.LocalNames[slot]] = value;
            }
        }

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
                    StoreLocalValue(codeObject, locals, localCells, context, instruction.LocalSlot, Pop(_stack, instruction.Span), instruction.Span);
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
                                _savedActiveExceptions is { Count: > 0 } saves ? saves.Pop().SavedException : null);
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

        // Ordinary returns deliver without throwing: a cleanup that handles
        // them in-frame routes through TryHandleAbrupt, otherwise the frame
        // records the value and exits normally (the Execute loop-end picks up
        // the flags). Frames without protected regions skip the routing scan
        // (and its per-return signal box) entirely. Propagated signals from
        // nested calls still arrive through the per-instruction catch below,
        // which stays as the routing backstop.
        private bool DeliverReturn(object value, LythonSourceSpan span)
        {
            if (codeObject.ExceptionRegions.Count != 0 &&
                TryHandleAbrupt(codeObject, context, _stack, _blockEntryStackDepths, _currentBlockIndex, new PendingReturn(value), span, ref _pendingAbrupt, ref _currentBlockIndex, out _))
            {
                return true;
            }

            AbandonFrame(span);
            _frameReturnValue = value;
            _hasFrameReturn = true;
            return true;
        }

        private bool DeliverJump(PendingJump jump, LythonSourceSpan span)
        {
            _pendingAbrupt = null;
            if (TryHandleAbrupt(codeObject, context, _stack, _blockEntryStackDepths, _currentBlockIndex, jump, span, ref _pendingAbrupt, ref _currentBlockIndex, out _)) return true;
            if (jump.DiscardIterator) _stack.RemoveTail(1);
            UnwindAbandonedHandlers(jump.TargetBlock);
            UnwindAbandonedHandlerVars(context, span, jump.TargetBlock);
            _currentBlockIndex = jump.TargetBlock;
            return true;
        }

        private bool ExecuteControlFlow(ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.AbruptJump:
                    return DeliverJump(new PendingJump(instruction.TargetBlockIndex, instruction.DiscardIterator), instruction.Span);

                case ExecutableOpCode.JumpIfFalse:
                    if (!IsTruthy(Pop(_stack, instruction.Span), context, instruction.Span))
                    {
                        _currentBlockIndex = instruction.TargetBlockIndex;
                        return true;
                    }
                    return false;

                case ExecutableOpCode.ChainLink:
                    return ExecuteChainLink(instruction);

                case ExecutableOpCode.Jump:
                    _currentBlockIndex = instruction.TargetBlockIndex;
                    return true;

                case ExecutableOpCode.ClearException:
                    if (instruction.ExceptionNameIndex >= 0)
                    {
                        _ = DeleteName(codeObject.Names[instruction.ExceptionNameIndex], context, instruction.Span);
                        if (context.ActiveHandlerVariables is { Count: > 0 } activeHandlers)
                        {
                            activeHandlers.Pop();
                        }
                    }
                    if (_savedActiveExceptions is { Count: > 0 } saves)
                    {
                        var saved = saves.Pop();
                        context.Services.SetCurrentException(saved.SavedException);
                        _pendingAbrupt = saved.SavedPendingAbrupt;
                    }
                    else context.Services.SetCurrentException(null);
                    return false;

                case ExecutableOpCode.EndFinally:
                    if (_pendingAbrupt is PendingJump pendingJump) return DeliverJump(pendingJump, instruction.Span);
                    if (_pendingAbrupt is PendingException)
                    {
                        // An exception-routed finally suite exits: restore the
                        // active exception saved when the suite was entered.
                        context.Services.SetCurrentException(
                            _savedActiveExceptions is { Count: > 0 } restored ? restored.Pop().SavedException : null);
                    }

                    if (_pendingAbrupt is not null)
                    {
                        PropagatePendingAbrupt(_pendingAbrupt);
                    }

                    _currentBlockIndex = instruction.TargetBlockIndex;
                    return true;

                case ExecutableOpCode.Return:
                    return DeliverReturn(Pop(_stack, instruction.Span), instruction.Span);

                case ExecutableOpCode.ReturnNone:
                    return DeliverReturn(PyNone.Instance, instruction.Span);

                default:
                    throw new InvalidOperationException($"Unknown executable opcode: {instruction.OpCode}");
            }
        }

        // Fused compare-and-branch for one chained-comparison link: pops the
        // fresh right operand, compares it against the retained left through
        // the shared Binary path, retains the right for the next link, and
        // jumps to the shared fail edge when the link is falsy. The transient
        // link value is observed like a Binary result would be; the delivered
        // chain value is observed at its own LoadConst.
        private bool ExecuteChainLink(ExecutableInstruction instruction)
        {
            var right = Pop(_stack, instruction.Span);
            var linkValue = EvaluateExecutableBinary(
                instruction.BinaryOperator,
                LoadLocal(codeObject, locals, instruction.ChainSlot, instruction.Span),
                right,
                instruction.Span,
                context);
            context.ObserveValue(linkValue, instruction.Span);
            StoreLocalValue(codeObject, locals, localCells, context, instruction.ChainSlot, right, instruction.Span);
            if (!IsTruthy(linkValue, context, instruction.Span))
            {
                _currentBlockIndex = instruction.FailureBlockIndex;
                return true;
            }

            return false;
        }

        internal bool HasFrameReturn => _hasFrameReturn;

        internal object FrameReturnValue => _frameReturnValue.RequireNotNull();

        public void Execute() => ExecuteCoreAsync(false).GetAwaiter().GetResult();
        public ValueTask ExecuteAsync() => ExecuteCoreAsync(true);

        private async ValueTask ExecuteCoreAsync(bool asynchronous)
        {
            // Every control-flow edge must arrive at a block with the same value
            // stack depth. The first arrival records that depth; edge handling
            // below validates later arrivals. Abrupt signals remain separate so
            // finally blocks can run before return/break/continue is rethrown.
            while (true)
            {
                var block = codeObject.Blocks[_currentBlockIndex];
                if (_blockEntryStackDepths.Length != 0)
                {
                    _blockEntryStackDepths[_currentBlockIndex] ??= _stack.Count;
                }
                var jumped = false;

                var instructions = block.Instructions;
                for (; _instructionIndex < instructions.Count; _instructionIndex++)
                {
                    var instruction = instructions[_instructionIndex];
                    context.Services.CheckExecutionBudget(instruction.Span);

                    try
                    {
                        if (_injectedException is not null && _delegation is null)
                        {
                            var injected = _injectedException;
                            _injectedException = null;
                            throw injected;
                        }
                        switch (instruction.OpCode)
                        {
                            case ExecutableOpCode.Yield:
                                YieldValue = Pop(_stack, instruction.Span);
                                HasYield = true;
                                _waitingForSend = true;
                                _instructionIndex++;
                                return;
                            case ExecutableOpCode.YieldFrom:
                                if (_delegation is null)
                                {
                                    var source = Pop(_stack, instruction.Span);
                                    var iterator = asynchronous ? await IterAsync([source], instruction.Span, context).ConfigureAwait(false) : Iter([source], instruction.Span, context);
                                    _delegation = new GeneratorDelegation(iterator, context, instruction.Span);
                                }
                                var delegated = asynchronous ? await _delegation.AdvanceAsync(_sentValue, _injectedException, true).ConfigureAwait(false)
                                    : _delegation.Advance(_sentValue, _injectedException);
                                _sentValue = PyNone.Instance;
                                _injectedException = null;
                                if (delegated.HasValue)
                                {
                                    YieldValue = delegated.Value;
                                    HasYield = true;
                                    return;
                                }
                                _stack.Push(_delegation.ReturnValue);
                                _delegation = null;
                                break;
                            case ExecutableOpCode.Import or
                                 ExecutableOpCode.DefineFunction or
                                 ExecutableOpCode.ExecuteFallbackStatement:
                                if (asynchronous) await ExecuteDefinitionOrFallbackAsync(instruction).ConfigureAwait(false);
                                else ExecuteDefinitionOrFallback(instruction);
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
                                if (asynchronous && instruction.OpCode == ExecutableOpCode.EvaluateFallbackExpression)
                                    PushObserved(await EvaluateLoweredExpressionAsync(codeObject.ExpressionFallbacks[instruction.ExpressionFallbackIndex].Expression, context).ConfigureAwait(false), instruction.Span);
                                else if (asynchronous && instruction.OpCode == ExecutableOpCode.LoadMember)
                                {
                                    var target = Pop(_stack, instruction.Span);
                                    var name = codeObject.Names[instruction.NameIndex];
                                    var member = await TryResolveRuntimeMemberAsync(target, name, context, instruction.Span).ConfigureAwait(false);
                                    if (!member.Found) throw PyMemberAccess.CreateMissingMemberError(target, name, instruction.Span, context);
                                    PushObserved(member.Value, instruction.Span);
                                }
                                else ExecuteStackTransfer(instruction);
                                break;

                            case ExecutableOpCode.MakeList or
                                 ExecutableOpCode.MakeTuple or
                                 ExecutableOpCode.MakeSet or
                                 ExecutableOpCode.MakeDict or
                                 ExecutableOpCode.ResolveContextManager or
                                 ExecutableOpCode.EnterContextManager or
                                 ExecutableOpCode.ExitContextManager or
                                 ExecutableOpCode.MatchCase:
                                jumped = asynchronous ? await ExecuteStructureAsync(instruction).ConfigureAwait(false) : ExecuteStructure(instruction);
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
                                jumped = asynchronous ? await ExecuteValueOperationAsync(instruction).ConfigureAwait(false) : ExecuteValueOperation(instruction);
                                break;

                            case ExecutableOpCode.AbruptJump or ExecutableOpCode.JumpIfFalse or
                                 ExecutableOpCode.ChainLink or
                                 ExecutableOpCode.Jump or
                                 ExecutableOpCode.ClearException or
                                 ExecutableOpCode.EndFinally or
                                 ExecutableOpCode.Return or
                                 ExecutableOpCode.ReturnNone:
                                jumped = asynchronous ? await ExecuteControlFlowAsync(instruction).ConfigureAwait(false) : ExecuteControlFlow(instruction);
                                break;

                            default:
                                throw new InvalidOperationException($"Unknown executable opcode: {instruction.OpCode}");
                        }
                    }
                    catch (ReturnSignal signal)
                    {
                        if (!TryHandleAbrupt(codeObject, context, _stack, _blockEntryStackDepths, _currentBlockIndex, new PendingReturn(signal.Value), instruction.Span, ref _pendingAbrupt, ref _currentBlockIndex, out _))
                        {
                            AbandonFrame(instruction.Span);
                            _frameReturnValue = signal.Value;
                            _hasFrameReturn = true;
                            return;
                        }

                        jumped = true;
                    }
                    catch (ControlSignal signal)
                    {
                        if (!TryHandleAbrupt(codeObject, context, _stack, _blockEntryStackDepths, _currentBlockIndex, new PendingControl(signal), instruction.Span, ref _pendingAbrupt, ref _currentBlockIndex, out _))
                        {
                            AbandonFrame(instruction.Span);
                            throw;
                        }

                        jumped = true;
                    }
                    catch (LythonRuntimeException ex)
                    {
                        var previousActive = context.Services.CurrentException;
                        var previousPending = _pendingAbrupt;
                        _delegation = null;
                        _injectedException = null;
                        if (!TryHandleAbrupt(codeObject, context, _stack, _blockEntryStackDepths, _currentBlockIndex, new PendingException(ex), instruction.Span, ref _pendingAbrupt, ref _currentBlockIndex, out var matchedRegion))
                        {
                            var failure = _pendingAbrupt is PendingException unhandled ? unhandled.Exception : ex;
                            AbandonFrame(instruction.Span);
                            throw failure;
                        }

                        var routedToHandler = _pendingAbrupt is null;
                        var routedToCleanup = !routedToHandler && _pendingAbrupt is PendingException;
                        var routedException = _pendingAbrupt is PendingException pending ? pending.Exception : ex;
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
                            var retainedPending = routedToHandler &&
                                _savedActiveExceptions is { Count: > 0 } enclosing &&
                                enclosing.Peek() is { SuiteStartBlockIndex: int start, SuiteEndBlockIndex: int end } &&
                                _currentBlockIndex >= start && _currentBlockIndex <= end
                                ? previousPending : null;
                            SavedActiveExceptions().Push(new ActiveExceptionSave(
                                context.Services.CurrentException,
                                matchedRegion?.SuiteStartBlockIndex,
                                matchedRegion?.SuiteEndBlockIndex,
                                retainedPending));
                            context.Services.SetCurrentException(
                                routedToHandler ? installed : CreatePythonExceptionInstance(routedException));
                        }

                        jumped = true;
                    }

                    if (jumped)
                    {
                        _instructionIndex = 0;
                        break;
                    }
                }

                if (_hasFrameReturn)
                {
                    return;
                }

                if (!jumped)
                {
                    return;
                }
            }
        }
    }
}
