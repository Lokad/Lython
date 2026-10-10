using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    /// <summary>
    /// Owns the mutable stack, caches, and control-flow state for one executable
    /// code-object invocation. Keeping this state together makes opcode handlers
    /// independently reviewable without extending the runtime entry-point method.
    /// </summary>
    private sealed partial class ExecutableFrameInterpreter : ExecutableFrameState
    {
        private readonly ExecutionContext _context;

        public ExecutableFrameInterpreter(
            ExecutableCodeObject codeObject,
            ExecutionContext context,
            object[] locals,
            ExecutableCell?[]? localCells,
            IReadOnlyList<ExecutableCell>? closureCells)
            : base(codeObject, locals, localCells, closureCells)
        {
            _context = context;
            _stack = new(Math.Max(8, codeObject.LocalNames.Count));
            _currentBlockIndex = codeObject.EntryBlockIndex;
            _entryActiveException = context.Services.CurrentException;
            _memberCaches = codeObject.MemberCacheCount == 0 ? Array.Empty<ExecutableMemberCache?>() : new ExecutableMemberCache?[codeObject.MemberCacheCount];
            _callCaches = codeObject.CallCacheCount == 0 ? Array.Empty<ExecutableCallCache?>() : new ExecutableCallCache?[codeObject.CallCacheCount];
            _blockEntryStackDepths = codeObject.ExceptionRegions.Count == 0 ? Array.Empty<int?>() : new int?[codeObject.Blocks.Count];
        }

        // The mutable stack lives in this interpreter, including across generator
        // suspension. Helpers must borrow it by reference rather than copy it.
        private ExecutableValueStack _stack;
        private int _currentBlockIndex;
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

        private PyException? _entryActiveException;

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
            _stack.RemoveTail(_stack.Count);
            _savedActiveExceptions?.Clear();
            AbandonActiveHandlerVars(_context, span);
            _context.Services.SetCurrentException(_entryActiveException);
        }

        // Pops saves for suites this propagation abandoned: a handler stays
        // live exactly while execution resumes inside its suite. Entries
        // without a suite range are kept, erring toward a stale value rather
        // than dropping a live save.
        private bool PendingCleanupContains(PendingAbruptSignal? pending, int block)
        {
            if (pending?.CleanupId is not int id) return false;
            for (var i = 0; i < _codeObject.ExceptionRegions.Count; i++)
            {
                var region = _codeObject.ExceptionRegions[i];
                if (region.CleanupId == id && region.SuiteStartBlockIndex is int start &&
                    region.SuiteEndBlockIndex is int end && block >= start && block <= end) return true;
            }
            return false;
        }

        private void RestoreSuppressedCompletion()
        {
            var saved = _savedActiveExceptions is { Count: > 0 } saves ? saves.Pop() : null;
            _pendingAbrupt = saved?.SavedPendingAbrupt;
            _context.Services.SetCurrentException(saved?.SavedException);
        }

        private void UnwindAbandonedHandlers(int targetBlockIndex)
        {
            while (_savedActiveExceptions is { Count: > 0 } saves &&
                saves.Peek() is { SuiteStartBlockIndex: int start, SuiteEndBlockIndex: int end } &&
                (targetBlockIndex < start || targetBlockIndex > end))
            {
                _context.Services.SetCurrentException(saves.Pop().SavedException);
            }
        }

        // Empty cache tables are shared: no instruction can name an index the
        // compiler never emitted (any such access already throws today).
        private readonly ExecutableMemberCache?[] _memberCaches;
        private readonly ExecutableCallCache?[] _callCaches;
        // Depth entries are recorded per block visit but only read when a
        // region matches, so region-free frames share the empty table.
        private readonly int?[] _blockEntryStackDepths;

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
            _context.ObserveValue(value, span);
            _stack.Push(value);
        }

        private void ExecuteDefinitionOrFallback(in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.Import:
                    ExecuteExecutableImport(_codeObject, _codeObject.Imports[instruction.ImportIndex], _locals, _localCells, _context);
                    break;

                case ExecutableOpCode.DefineFunction:
                    ExecuteExecutableFunctionDefinition(_codeObject, _codeObject.Functions[instruction.FunctionIndex], _locals, _localCells, _context);
                    break;

                case ExecutableOpCode.ExecuteFallbackStatement:
                    DispatchLoweredStatementAsync(
                        _codeObject.StatementFallbacks[instruction.StatementFallbackIndex].Statement,
                        _context,
                        SynchronousLoweredStatementExecution.Instance).GetAwaiter().GetResult();
                    SyncExecutableLocalsFromContext(_codeObject, _locals, _localCells, _context);
                    break;
            }
        }

        private void ExecuteStackTransfer(in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.LoadConst:
                    var constant = RuntimeValue(_codeObject.Constants[instruction.ConstantIndex]);
                    _context.ObserveValue(constant, instruction.Span);
                    _stack.Push(constant);
                    break;

                case ExecutableOpCode.LoadLocal:
                    _stack.Push(LoadLocal(_codeObject, _locals, instruction.LocalSlot, instruction.Span));
                    break;

                case ExecutableOpCode.LoadClosure:
                    _stack.Push(LoadClosure(_codeObject, _context, instruction.ClosureSlot, instruction.Span));
                    break;

                case ExecutableOpCode.LoadGlobal:
                    _stack.Push(ResolveExecutableGlobal(_codeObject.Names[instruction.NameIndex], instruction.Span, _context));
                    break;

                case ExecutableOpCode.LoadName:
                    _stack.Push(ResolveExecutableName(_codeObject.Names[instruction.NameIndex], instruction.Span, _context));
                    break;

                case ExecutableOpCode.EvaluateFallbackExpression:
                    var fallback = EvaluateLoweredExpression(_codeObject.ExpressionFallbacks[instruction.ExpressionFallbackIndex].Expression, _context);
                    _context.ObserveValue(fallback, instruction.Span);
                    _stack.Push(fallback);
                    break;

                case ExecutableOpCode.LoadMember:
                    var target = Pop(ref _stack, instruction.Span);
                    var memberName = _codeObject.Names[instruction.NameIndex];
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
                        if (!TryResolveRuntimeMember(target, memberName, _context, instruction.Span, out var resolvedMember))
                        {
                            throw PyMemberAccess.CreateMissingMemberError(target, memberName, instruction.Span, _context);
                        }

                        memberValue = resolvedMember;
                        _memberCaches[instruction.MemberCacheIndex] = CanCacheRuntimeMemberValue(target, memberValue)
                            ? new ExecutableMemberCache(target, memberValue)
                            : null;
                    }

                    _context.ObserveValue(memberValue, instruction.Span);
                    _stack.Push(memberValue);
                    break;

                case ExecutableOpCode.StoreLocal:
                    StoreLocalValue(_codeObject, _locals, _localCells, _context, instruction.LocalSlot, Pop(ref _stack, instruction.Span), instruction.Span);
                    break;

                case ExecutableOpCode.StoreClosure:
                    StoreExecutableClosure(
                        _codeObject,
                        _context,
                        instruction.ClosureSlot,
                        Pop(ref _stack, instruction.Span),
                        instruction.Span);
                    break;

                case ExecutableOpCode.StoreGlobal:
                    StoreName(
                        _codeObject.Names[instruction.NameIndex],
                        Pop(ref _stack, instruction.Span),
                        _context,
                        instruction.Span);
                    break;

                case ExecutableOpCode.StoreName:
                    AssignExecutableBoundName(
                        _codeObject,
                        _locals,
                        _localCells,
                        _codeObject.Names[instruction.NameIndex],
                        Pop(ref _stack, instruction.Span),
                        _context,
                        instruction.Span);
                    break;

                case ExecutableOpCode.Dup:
                    _stack.Push(Peek(ref _stack, instruction.Span));
                    break;

                case ExecutableOpCode.PopTop:
                    _ = Pop(ref _stack, instruction.Span);
                    break;
            }
        }

        private bool ExecuteStructure(in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.MakeList:
                    PushObserved(CreateListFromStack(ref _stack, instruction.ItemCount, instruction.Span, _context), instruction.Span);
                    break;

                case ExecutableOpCode.MakeTuple:
                    PushObserved(CreateTupleFromStack(ref _stack, instruction.ItemCount, instruction.Span, _context), instruction.Span);
                    break;

                case ExecutableOpCode.MakeSet:
                    PushObserved(ExecuteExecutableMakeSet(ref _stack, instruction.ItemCount, instruction.Span, _context), instruction.Span);
                    break;

                case ExecutableOpCode.MakeDict:
                    PushObserved(ExecuteExecutableMakeDict(ref _stack, instruction.PairCount, instruction.Span, _context), instruction.Span);
                    break;

                case ExecutableOpCode.ResolveContextManager:
                    var managerValue = Pop(ref _stack, instruction.Span);
                    _stack.Push(PyContextManagers.Resolve(managerValue, instruction.Span, _context));
                    break;

                case ExecutableOpCode.EnterContextManager:
                    var enteringManager = PopContextManager(ref _stack, instruction.Span);
                    PushObserved(enteringManager.Enter(), instruction.Span);
                    break;

                case ExecutableOpCode.ExitContextManager:
                    var exitingManager = PopContextManager(ref _stack, instruction.Span);
                    // Only exceptions are suppressible. Return/break/continue
                    // are normal exits to __exit__ and remain pending afterward.
                    if (_pendingAbrupt is PendingException { Exception: var exception } pendingException &&
                        (pendingException.CleanupId ?? -1) == instruction.CleanupId)
                    {
                        if (exitingManager.Exit(
                                ResolvePythonExceptionType(exception, _context),
                                CreatePythonExceptionInstance(exception),
                                PyNone.Instance))
                        {
                            RestoreSuppressedCompletion();
                        }
                    }
                    else
                    {
                        _ = exitingManager.Exit(PyNone.Instance, PyNone.Instance, PyNone.Instance);
                    }
                    break;

                case ExecutableOpCode.MatchCase:
                    var subject = Pop(ref _stack, instruction.Span);
                    if (!TryExecuteExecutableMatchCase(
                            _codeObject.MatchCases[instruction.MatchCaseIndex],
                            subject,
                            _context,
                            _locals,
                            _localCells))
                    {
                        _currentBlockIndex = instruction.FailureBlockIndex;
                        return true;
                    }
                    break;
            }

            return false;
        }

        private bool ExecuteForNext(in ExecutableInstruction instruction)
        {
            var iterator = PeekIterator(ref _stack, instruction.Span);
            if (!iterator.MoveNext())
            {
                _ = Pop(ref _stack, instruction.Span);
                _currentBlockIndex = instruction.TargetBlockIndex;
                return true;
            }

            _stack.Push(iterator.Current);
            return false;
        }

        private bool ExecuteValueOperation(in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.GetIter:
                    var iterable = Pop(ref _stack, instruction.Span);
                    _stack.Push(ToSequence(iterable, instruction.Span, _context).GetEnumerator());
                    break;

                case ExecutableOpCode.ForNext:
                    return ExecuteForNext(instruction);

                case ExecutableOpCode.AssignLoopTarget:
                    var loopBinding = _codeObject.LoopTargets[instruction.LoopTargetIndex];
                    AssignLoopTarget(loopBinding.Target, Pop(ref _stack, instruction.Span), loopBinding.Span, _context);
                    break;

                case ExecutableOpCode.AssignUnpackingTargets:
                    var unpackingBinding = _codeObject.UnpackingTargets[instruction.UnpackingTargetIndex];
                    AssignTargets(unpackingBinding.Targets, Pop(ref _stack, instruction.Span), unpackingBinding.Span, _context);
                    break;

                case ExecutableOpCode.Call:
                    var callResult = ExecuteExecutableCall(
                        _codeObject.CallSites[instruction.CallSiteIndex],
                        ref _stack,
                        _context,
                        ref _callCaches[instruction.CallCacheIndex]);
                    PushObserved(callResult, instruction.Span);
                    break;

                case ExecutableOpCode.Subscript:
                    var index = Pop(ref _stack, instruction.Span);
                    var target = Pop(ref _stack, instruction.Span);
                    PushObserved(ReadSubscriptValue(target, index, instruction.Span, _context), instruction.Span);
                    break;

                case ExecutableOpCode.Slice:
                    PushObserved(ExecuteExecutableSlice(ref _stack, instruction.SliceParts, instruction.Span, _context), instruction.Span);
                    break;

                case ExecutableOpCode.MakeSlice:
                    var (start, stop, step) = PopExecutableSliceBounds(ref _stack, instruction.SliceParts, instruction.Span);
                    PushObserved(CreateSliceValue(start, stop, step, instruction.Span, _context), instruction.Span);
                    break;

                case ExecutableOpCode.Binary:
                    var binaryRight = Pop(ref _stack, instruction.Span);
                    var binaryLeft = Pop(ref _stack, instruction.Span);
                    PushObserved(EvaluateExecutableBinary(
                        instruction.BinaryOperator,
                        binaryLeft,
                        binaryRight,
                        instruction.Span,
                        _context), instruction.Span);
                    break;

                case ExecutableOpCode.Augmented:
                    var augmentedRight = Pop(ref _stack, instruction.Span);
                    var augmentedLeft = Pop(ref _stack, instruction.Span);
                    PushObserved(EvaluateExecutableAugmented(
                        instruction.AugmentedOperator,
                        augmentedLeft,
                        augmentedRight,
                        _context,
                        instruction.Span), instruction.Span);
                    break;

                case ExecutableOpCode.Unary:
                    PushObserved(EvaluateExecutableUnary(
                        instruction.UnaryOperator,
                        Pop(ref _stack, instruction.Span),
                        _context,
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
            if (_codeObject.ExceptionRegions.Count != 0 &&
                TryHandleAbrupt(_codeObject, _context, ref _stack, _blockEntryStackDepths, _currentBlockIndex, new PendingReturn(value), span, ref _pendingAbrupt, ref _currentBlockIndex, out _))
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
            var previous = ReferenceEquals(_pendingAbrupt, jump) ? jump.SavedPendingAbrupt : _pendingAbrupt;
            jump.SavedPendingAbrupt = PendingCleanupContains(previous, jump.TargetBlock) ? previous : null;
            _pendingAbrupt = jump.SavedPendingAbrupt;
            if (TryHandleAbrupt(_codeObject, _context, ref _stack, _blockEntryStackDepths, _currentBlockIndex, jump, span, ref _pendingAbrupt, ref _currentBlockIndex, out _)) return true;
            if (jump.DiscardIterator) _stack.RemoveTail(1);
            UnwindAbandonedHandlers(jump.TargetBlock);
            UnwindAbandonedHandlerVars(_context, span, jump.TargetBlock);
            _currentBlockIndex = jump.TargetBlock;
            return true;
        }

        private bool ExecuteControlFlow(in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.AbruptJump:
                    return DeliverJump(new PendingJump(instruction.TargetBlockIndex, instruction.DiscardIterator), instruction.Span);

                case ExecutableOpCode.JumpIfFalse:
                    if (!IsTruthy(Pop(ref _stack, instruction.Span), _context, instruction.Span))
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
                        _ = DeleteName(_codeObject.Names[instruction.ExceptionNameIndex], _context, instruction.Span);
                        if (_context.ActiveHandlerVariables is { Count: > 0 } activeHandlers)
                        {
                            activeHandlers.Pop();
                        }
                    }
                    if (_savedActiveExceptions is { Count: > 0 } saves)
                    {
                        var saved = saves.Pop();
                        _context.Services.SetCurrentException(saved.SavedException);
                        _pendingAbrupt = saved.SavedPendingAbrupt;
                    }
                    else _context.Services.SetCurrentException(null);
                    return false;

                case ExecutableOpCode.MatchException:
                    var active = _context.Services.CurrentException
                        ?? throw new InvalidOperationException("Exception selection requires an active exception.");
                    _stack.Push(MatchesCaughtExceptionValue(Pop(ref _stack, instruction.Span), active.Identity, _context, instruction.Span));
                    return false;

                case ExecutableOpCode.BindException:
                    var bound = _context.Services.CurrentException
                        ?? throw new InvalidOperationException("Exception binding requires an active exception.");
                    ChargeBoundException(_context.MemoryGovernor, instruction.Span);
                    var name = _codeObject.Names[instruction.NameIndex];
                    AssignExecutableBoundName(_codeObject, _locals, _localCells, name, bound, _context, instruction.Span);
                    var suite = SavedActiveExceptions().Peek();
                    _context.ActiveHandlerVariables ??= new();
                    _context.ActiveHandlerVariables.Push((name, suite.SuiteStartBlockIndex ?? 0, suite.SuiteEndBlockIndex ?? int.MaxValue));
                    return false;

                case ExecutableOpCode.ReraiseException:
                    ThrowReraisedException(instruction.Span, _context);
                    return false;

                case ExecutableOpCode.EndFinally:
                    if (_pendingAbrupt is not null && (_pendingAbrupt.CleanupId ?? -1) != instruction.CleanupId)
                    {
                        // Normal nested cleanup leaves the outer completion pending.
                        _currentBlockIndex = instruction.TargetBlockIndex;
                        return true;
                    }
                    if (_pendingAbrupt is PendingJump pendingJump) return DeliverJump(pendingJump, instruction.Span);
                    if (_pendingAbrupt is PendingException)
                    {
                        // An exception-routed finally suite exits: restore the
                        // active exception saved when the suite was entered.
                        _context.Services.SetCurrentException(
                            _savedActiveExceptions is { Count: > 0 } restored ? restored.Pop().SavedException : null);
                    }

                    if (_pendingAbrupt is not null)
                    {
                        PropagatePendingAbrupt(_pendingAbrupt);
                    }

                    _currentBlockIndex = instruction.TargetBlockIndex;
                    return true;

                case ExecutableOpCode.Return:
                    return DeliverReturn(Pop(ref _stack, instruction.Span), instruction.Span);

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
            var right = Pop(ref _stack, instruction.Span);
            var linkValue = EvaluateExecutableBinary(
                instruction.BinaryOperator,
                LoadLocal(_codeObject, _locals, instruction.ChainSlot, instruction.Span),
                right,
                instruction.Span,
                _context);
            _context.ObserveValue(linkValue, instruction.Span);
            StoreLocalValue(_codeObject, _locals, _localCells, _context, instruction.ChainSlot, right, instruction.Span);
            if (!IsTruthy(linkValue, _context, instruction.Span))
            {
                _currentBlockIndex = instruction.FailureBlockIndex;
                return true;
            }

            return false;
        }

        internal bool HasFrameReturn => _hasFrameReturn;

        internal object FrameReturnValue => _frameReturnValue.RequireNotNull();

        public ValueTask ExecuteAsync() => ExecuteCoreAsync(true);

        private async ValueTask ExecuteCoreAsync(bool asynchronous)
        {
            // Every control-flow edge must arrive at a block with the same value
            // stack depth. The first arrival records that depth; edge handling
            // below validates later arrivals. Abrupt signals remain separate so
            // finally blocks can run before return/break/continue is rethrown.
            while (true)
            {
                var block = _codeObject.Blocks[_currentBlockIndex];
                if (_blockEntryStackDepths.Length != 0)
                {
                    _blockEntryStackDepths[_currentBlockIndex] ??= _stack.Count;
                }
                var jumped = false;

                var instructions = block.InstructionArray;
                for (; _instructionIndex < instructions.Length; _instructionIndex++)
                {
                    var instruction = instructions[_instructionIndex];
                    _context.Services.CheckExecution(instruction.Span);

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
                            case ExecutableOpCode.ApplyOperation:
                                await ExecutePreparedOperationAsync((ExecutableOperation)_codeObject.Constants[instruction.ConstantIndex]!, instruction.Span, asynchronous).ConfigureAwait(false);
                                break;
                            case ExecutableOpCode.Yield:
                                YieldValue = Pop(ref _stack, instruction.Span);
                                HasYield = true;
                                _waitingForSend = true;
                                _instructionIndex++;
                                return;
                            case ExecutableOpCode.YieldFrom:
                                if (_delegation is null)
                                {
                                    var source = Pop(ref _stack, instruction.Span);
                                    var iterator = asynchronous ? await IterAsync([source], instruction.Span, _context).ConfigureAwait(false) : Iter([source], instruction.Span, _context);
                                    _delegation = new GeneratorDelegation(iterator, _context, instruction.Span);
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
                                    PushObserved(await EvaluateLoweredExpressionAsync(_codeObject.ExpressionFallbacks[instruction.ExpressionFallbackIndex].Expression, _context).ConfigureAwait(false), instruction.Span);
                                else if (asynchronous && instruction.OpCode == ExecutableOpCode.LoadMember)
                                {
                                    var target = Pop(ref _stack, instruction.Span);
                                    var name = _codeObject.Names[instruction.NameIndex];
                                    var member = await TryResolveRuntimeMemberAsync(target, name, _context, instruction.Span).ConfigureAwait(false);
                                    if (!member.Found) throw PyMemberAccess.CreateMissingMemberError(target, name, instruction.Span, _context);
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
                                 ExecutableOpCode.MakeSlice or
                                 ExecutableOpCode.Binary or
                                 ExecutableOpCode.Augmented or
                                 ExecutableOpCode.Unary:
                                jumped = asynchronous ? await ExecuteValueOperationAsync(instruction).ConfigureAwait(false) : ExecuteValueOperation(instruction);
                                break;

                            case ExecutableOpCode.AbruptJump or ExecutableOpCode.JumpIfFalse or
                                 ExecutableOpCode.ChainLink or
                                 ExecutableOpCode.Jump or
                                 ExecutableOpCode.MatchException or
                                 ExecutableOpCode.BindException or
                                 ExecutableOpCode.ReraiseException or
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
                        if (!TryRouteReturn(signal, instruction.Span)) return;
                        jumped = true;
                    }
                    catch (ControlSignal signal)
                    {
                        if (!TryRouteControl(signal, instruction.Span)) throw;
                        jumped = true;
                    }
                    catch (LythonRuntimeException ex)
                    {
                        RouteRuntimeException(ex, instruction.Span);
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
