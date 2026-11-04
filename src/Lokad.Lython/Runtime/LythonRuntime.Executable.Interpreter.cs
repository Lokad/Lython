using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

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

        private bool TryLoadDirectCallReceiver(ExecutableInstruction instruction)
        {
            if (instruction.OpCode != ExecutableOpCode.LoadCallReceiver) return false;
            var name = _codeObject.Names[instruction.NameIndex];
            var operation = _stack[instruction.InputStart] switch
            {
                PyList when name == "append" => DirectListAppend,
                ExecutionContext.TextFileHandle or ExecutionContext.BinaryFileHandle when name == "write" => DirectFileWrite,
                CsvWriterObject when name == "writerow" => DirectCsvWriterow,
                PyDict when name == "get" => DirectDictionaryGet,
                PyDict when name == "setdefault" => DirectDictionarySetDefault,
                PyDict => DictMembers.GetNoArgumentsOperation(name),
                Text.PyString when name == "startswith" => DirectStringStartsWith,
                Text.PyString when name == "endswith" => DirectStringEndsWith,
                Text.PyString when name == "replace" => DirectStringReplace,
                Text.PyString when name == "split" => DirectStringSplit,
                Text.PyString when name == "join" => DirectStringJoin,
                Text.PyString => StringMembers.GetNoArgumentsOperation(name),
                _ => null
            };
            if (operation is null) return false;
            // Keep the evaluated receiver, then append the immutable operation.
            // Arguments are evaluated afterwards and can rebind the source name.
            _stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, operation);
            return true;
        }

        public ExecutableFrameInterpreter(
            ExecutableCodeObject codeObject,
            ExecutionContext context,
            object[] locals,
            ExecutableCell?[]? localCells,
            IReadOnlyList<ExecutableCell>? closureCells,
            ExecutableValueStorage stack)
            : base(codeObject, locals, localCells, closureCells)
        {
            _context = context;
            _instructionIndex = codeObject.EntryIndex;
            _entryActiveException = context.State.CurrentException;
            _memberCaches = codeObject.MemberCacheCount == 0 ? Array.Empty<ExecutableMemberCache?>() : new ExecutableMemberCache?[codeObject.MemberCacheCount];
            // The factory retains ownership until every construction allocation
            // succeeds. No throwing work follows this transfer.
            _stack = stack;
        }

        // The mutable stack lives in this interpreter, including across generator
        // suspension. Helpers must borrow it by reference rather than copy it.
        private ExecutableValueStorage _stack;
        private PendingAbruptSignal? _pendingAbrupt;
        private int _instructionIndex;
        private bool _waitingForSend;
        private object _sentValue = PyNone.Instance;
        private LythonRuntimeException? _injectedException;
        private GeneratorDelegation? _delegation;
        internal bool HasYield { get; private set; }
        internal object YieldValue { get; private set; } = PyNone.Instance;
        internal LythonSourceSpan? CurrentInstructionSpan
            => _instructionIndex < _codeObject.Instructions.Length
                ? _codeObject.Instructions[_instructionIndex].Span : null;

        internal void SeedGeneratorOuter(object iterator, PyIteration.Cursor cursor)
        {
            _locals[_codeObject.LocalNameToSlot[".0"]] = iterator;
            _stack.PublishValue(_codeObject.FirstTemporarySlot, _codeObject.FirstTemporarySlot + 1, cursor);
        }

        internal async ValueTask ReleaseGeneratorTemporariesAsync(bool asynchronous)
        {
            _stack.DeferAsyncDisposal = asynchronous;
            try
            {
                _stack.RetireTo(_codeObject.FirstTemporarySlot);
                try { if (_stack.HasPendingDisposals) await DrainPendingDisposalsAsync().ConfigureAwait(false); }
                finally { _stack.ReleaseStorage(); }
            }
            finally { _stack.DeferAsyncDisposal = false; }
        }

        internal void Resume(object sent, LythonRuntimeException? injected)
        {
            // A retained closure can write a cell while this generator is
            // suspended. Refresh only captured slots, including direct-call inputs.
            for (var index = 0; index < _codeObject.CapturedLocalSlots.Count; index++)
            {
                var slot = _codeObject.CapturedLocalSlots[index];
                _locals[slot] = _localCells![slot]!.Value;
            }
            HasYield = false;
            _sentValue = sent;
            _injectedException = injected;
            if (_waitingForSend)
            {
                _waitingForSend = false;
                if (injected is null)
                {
                    var yielded = _codeObject.Instructions[_instructionIndex - 1];
                    _stack.PublishValue(yielded.ResultSlot, yielded.LiveExtent, sent);
                }
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
            public int? SuiteStartIndex { get; } = suiteStartBlockIndex;
            public int? SuiteEndIndex { get; } = suiteEndBlockIndex;
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
            _stack.RetireTo(_codeObject.FirstTemporarySlot);
            _savedActiveExceptions?.Clear();
            AbandonActiveHandlerVars(_context, span);
            _context.State.SetCurrentException(_entryActiveException);
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
                if (region.CleanupId == id && region.SuiteStartIndex is int start &&
                    region.SuiteEndIndex is int end && block >= start && block <= end) return true;
            }
            return false;
        }

        private void RestoreSuppressedCompletion()
        {
            var saved = _savedActiveExceptions is { Count: > 0 } saves ? saves.Pop() : null;
            _pendingAbrupt = saved?.SavedPendingAbrupt;
            _context.State.SetCurrentException(saved?.SavedException);
        }

        private void UnwindAbandonedHandlers(int targetBlockIndex)
        {
            while (_savedActiveExceptions is { Count: > 0 } saves &&
                saves.Peek() is { SuiteStartIndex: int start, SuiteEndIndex: int end } &&
                (targetBlockIndex < start || targetBlockIndex > end))
            {
                _context.State.SetCurrentException(saves.Pop().SavedException);
            }
        }

        // Empty cache tables are shared: no instruction can name an index the
        // compiler never emitted (any such access already throws today).
        private readonly ExecutableMemberCache?[] _memberCaches;

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
            var name = codeObject.LocalNames[slot];
            var guestName = !name.StartsWith('<');
            if (context.GlobalNamespace is not null && guestName)
            {
                StoreGlobalNamespaceName(context, name, value);
                return;
            }
            locals[slot] = value;
            if (localCells?[slot] is ExecutableCell localCell)
            {
                localCell.Value = value;
            }

            if (guestName && (codeObject.RequiresLocalVariableMirroring || context.MirrorLocalStores))
            {
                context.Variables[name] = value;
            }
        }

        // Pop's returned reference belongs to this completed transfer, rather
        // than the long-running dispatch loop. Keep the CLR temporary here so
        // deleting a slot can make its payload collectible before frame exit.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void StoreExecutableLocal(in ExecutableInstruction instruction)
            => StoreLocalValue(_codeObject, _locals, _localCells, _context, instruction.LocalSlot,
                TakeOperand(instruction), instruction.Span);

        private object ReadOperand(int source)
            => source < 0 ? RuntimeValue(_codeObject.Constants[~source]) : _stack[source];

        private object TakeOperand(in ExecutableInstruction instruction)
        {
            if (instruction.InputStart >= _codeObject.FirstTemporarySlot)
                return _stack.TakeValue(instruction.InputStart, instruction.LiveExtent);
            var value = ReadOperand(instruction.InputStart);
            _stack.RetireTo(instruction.LiveExtent);
            return value;
        }

        private void ExecuteDefinitionOrFallback(in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.Import:
                    ExecuteExecutableImport(_codeObject, _codeObject.Imports[instruction.ImportIndex], _locals, _localCells, _context);
                    break;

                case ExecutableOpCode.DefineFunction:
                    ExecuteExecutableFunctionDefinitionAsync(_codeObject, _codeObject.Functions[instruction.FunctionIndex],
                        _locals, _localCells, _context, false).GetAwaiter().GetResult();
                    break;

                case ExecutableOpCode.ExecuteFallbackStatement:
                    DispatchLoweredStatementAsync(
                        _codeObject.StatementFallbacks[instruction.StatementFallbackIndex],
                        _context,
                        SynchronousLoweredStatementExecution.Instance).GetAwaiter().GetResult();
                    SyncExecutableLocalsFromContext(_codeObject, _locals, _localCells, _context);
                    break;
            }
        }

        private bool TryReadMemberCache(ExecutableInstruction instruction, object target, out object value)
        {
            var cached = _memberCaches[instruction.MemberCacheIndex];
            // Equal objects may have different mutable members; identity is required.
            if (cached is not null && ReferenceEquals(cached.Target, target))
            {
                value = cached.Value;
                return true;
            }
            value = PyNone.Instance;
            return false;
        }

        private void PublishMemberCache(ExecutableInstruction instruction, object target, object value)
            => _memberCaches[instruction.MemberCacheIndex] = CanCacheRuntimeMemberValue(target, value,
                instruction.OpCode is ExecutableOpCode.LoadCallMember or ExecutableOpCode.LoadCallReceiver)
                ? new ExecutableMemberCache(target, value) : null;

        private void ExecuteStackTransfer(in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.LoadConst:
                    var constant = RuntimeValue(_codeObject.Constants[instruction.ConstantIndex]);
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, constant);
                    break;

                case ExecutableOpCode.LoadClosure:
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, LoadClosure(_codeObject, _context, instruction.ClosureSlot, instruction.Span));
                    break;

                case ExecutableOpCode.LoadGlobal:
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, ResolveExecutableGlobal(_codeObject.Names[instruction.NameIndex], instruction.Span, _context));
                    break;

                case ExecutableOpCode.LoadName:
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, ResolveExecutableName(_codeObject.Names[instruction.NameIndex], instruction.Span, _context));
                    break;


                case ExecutableOpCode.LoadCallReceiver:
                    if (TryLoadDirectCallReceiver(instruction)) break;
                    goto case ExecutableOpCode.LoadCallMember;
                case ExecutableOpCode.LoadMember:
                case ExecutableOpCode.LoadCallMember:
                    var target = _stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                    var memberName = _codeObject.Names[instruction.NameIndex];
                    if (!TryReadMemberCache(instruction, target, out var memberValue))
                    {
                        if (!TryResolveRuntimeMember(target, memberName, _context, instruction.Span, out var resolvedMember))
                        {
                            throw PyMemberAccess.CreateMissingMemberError(target, memberName, instruction.Span, _context);
                        }

                        memberValue = resolvedMember;
                        PublishMemberCache(instruction, target, memberValue);
                    }
                    if (instruction.OpCode == ExecutableOpCode.LoadCallReceiver)
                    {
                        _stack[instruction.ResultSlot] = PyNone.Instance;
                        _stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, memberValue);
                    }
                    else _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, memberValue);
                    break;

                case ExecutableOpCode.PreloadImplicitSuperArguments:
                    PreloadImplicitSuperArguments(_context, instruction.Span);
                    break;

                case ExecutableOpCode.StoreClosure:
                    StoreExecutableClosure(
                        _codeObject,
                        _context,
                        instruction.ClosureSlot,
                        _stack.TakeValue(instruction.InputStart, instruction.LiveExtent),
                        instruction.Span);
                    break;

                case ExecutableOpCode.StoreGlobal:
                    StoreName(
                        _codeObject.Names[instruction.NameIndex],
                        _stack.TakeValue(instruction.InputStart, instruction.LiveExtent),
                        _context,
                        instruction.Span);
                    break;

                case ExecutableOpCode.StoreName:
                    AssignExecutableBoundName(
                        _codeObject,
                        _locals,
                        _localCells,
                        _codeObject.Names[instruction.NameIndex],
                        _stack.TakeValue(instruction.InputStart, instruction.LiveExtent),
                        _context,
                        instruction.Span);
                    break;

                case ExecutableOpCode.Dup:
                    _stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, _stack[instruction.InputStart]);
                    break;

                case ExecutableOpCode.PopTop:
                    _ = _stack[instruction.InputStart];
                    _stack.RetireTo(instruction.LiveExtent);
                    break;
            }
        }

        private bool ExecuteStructure(in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.MakeList:
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent,
                        CreateListFromStack(ref _stack, instruction.InputStart, instruction.ItemCount, instruction.Span, _context));
                    break;

                case ExecutableOpCode.MakeTuple:
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent,
                        CreateTupleFromStack(ref _stack, instruction.InputStart, instruction.ItemCount, instruction.Span, _context));
                    break;

                case ExecutableOpCode.MakeSet:
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent,
                        ExecuteExecutableMakeSet(ref _stack, instruction.InputStart, instruction.ItemCount, instruction.Span, _context));
                    break;

                case ExecutableOpCode.MakeDict:
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent,
                        ExecuteExecutableMakeDict(ref _stack, instruction.InputStart, instruction.PairCount, instruction.Span, _context));
                    break;

                case ExecutableOpCode.ResolveContextManager:
                    var managerValue = _stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, PyContextManagers.Resolve(managerValue, instruction.Span, _context));
                    break;

                case ExecutableOpCode.EnterContextManager:
                    var enteringManager = RequireContextManager(_stack.TakeValue(instruction.InputStart, instruction.ResultSlot), instruction.Span);
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, enteringManager.Enter());
                    break;

                case ExecutableOpCode.ExitContextManager:
                    var exitingManager = RequireContextManager(_stack.TakeValue(instruction.InputStart, instruction.ResultSlot), instruction.Span);
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
                    var subject = _stack.TakeValue(instruction.InputStart, instruction.LiveExtent);
                    if (!TryExecuteExecutableMatchCase(
                            _codeObject.MatchCases[instruction.MatchCaseIndex],
                            subject,
                            _context,
                            _locals,
                            _localCells))
                    {
                        _instructionIndex = instruction.FailureIndex;
                        return true;
                    }
                    break;
            }

            return false;
        }

        private bool ExecuteForNext(in ExecutableInstruction instruction)
        {
            var iterator = (PyIteration.Cursor)_stack[instruction.InputStart];
            if (!iterator.TryMoveNext(out var value))
            {
                _ = _stack.TakeValue(instruction.InputStart, instruction.InputStart);
                iterator.Dispose();
                _instructionIndex = instruction.TargetIndex;
                return true;
            }

            _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, value);
            return false;
        }

        private void ExecuteValueOperation(in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.GetIter:
                    var iterableSlot = instruction.InputStart;
                    try { _stack[iterableSlot] = PyIteration.Cursor.Create(_stack[iterableSlot], instruction.Span, _context); }
                    catch { _stack.RetireTo(iterableSlot); throw; }
                    break;

                case ExecutableOpCode.Slice:
                    ExecuteOperandValueOperation(instruction);
                    break;

                case ExecutableOpCode.MakeSlice:
                    var (start, stop, step) = ReadExecutableSliceBounds(in _stack, instruction.SliceParts, instruction.InputStart);
                    _stack.RetireTo(instruction.ResultSlot);
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, CreateSliceValue(start, stop, step, instruction.Span, _context));
                    break;

            }

        }

        private void ExecuteOperandValueOperation(in ExecutableInstruction instruction)
        {
            var first = instruction.InputStart;
            try
            {
                // Borrow the actual operands through callback reentry. Completion
                // reuses their first slot before result admission; no extra extent.
                var result = instruction.OpCode switch
                {
                    ExecutableOpCode.Subscript => _preparedTypeSubscription is not null
                        ? InvokePreparedTypeSubscription(_stack[first + 1], instruction.Span, false).GetAwaiter().GetResult()
                        : ReadSubscriptValue(_stack[first], _stack[first + 1], instruction.Span, _context),
                    ExecutableOpCode.Slice => ExecuteExecutableSlice(in _stack, instruction.InputStart, instruction.SliceParts, instruction.Span, _context),
                    ExecutableOpCode.Binary => EvaluateBinaryOperator(instruction.BinaryOperator,
                        ReadOperand(first), ReadOperand(instruction.SecondInput), _context, instruction.Span),
                    ExecutableOpCode.Augmented => EvaluateExecutableAugmented(instruction.AugmentedOperator,
                        ReadOperand(first), ReadOperand(instruction.SecondInput), _context, instruction.Span),
                    ExecutableOpCode.Unary => EvaluateExecutableUnary(instruction.UnaryOperator, ReadOperand(first), _context, instruction.Span),
                    _ => throw new InvalidOperationException("Unexpected value operation."),
                };
                _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, result);
            }
            catch { _stack.RetireTo(instruction.ResultSlot); throw; }
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
                TryHandleAbrupt(_codeObject, _context, ref _stack, _instructionIndex, new PendingReturn(value), span, ref _pendingAbrupt, ref _instructionIndex, out _))
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
            jump.SavedPendingAbrupt = PendingCleanupContains(previous, jump.TargetIndex) ? previous : null;
            _pendingAbrupt = jump.SavedPendingAbrupt;
            if (TryHandleAbrupt(_codeObject, _context, ref _stack, _instructionIndex, jump, span, ref _pendingAbrupt, ref _instructionIndex, out _)) return true;
            if (jump.DiscardIterator) _stack.RetireTo(_stack.LiveExtent - 1);
            UnwindAbandonedHandlers(jump.TargetIndex);
            UnwindAbandonedHandlerVars(_context, span, jump.TargetIndex);
            _instructionIndex = jump.TargetIndex;
            return true;
        }

        private bool ExecuteControlFlow(in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.AbruptJump:
                    return DeliverJump(new PendingJump(instruction.TargetIndex, instruction.DiscardIterator), instruction.Span);

                case ExecutableOpCode.JumpIfFalse:
                    if (!IsTruthy(_stack.TakeValue(instruction.InputStart, instruction.LiveExtent), _context, instruction.Span))
                    {
                        _instructionIndex = instruction.TargetIndex;
                        return true;
                    }
                    return false;

                case ExecutableOpCode.Jump:
                    _instructionIndex = instruction.TargetIndex;
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
                        _context.State.SetCurrentException(saved.SavedException);
                        _pendingAbrupt = saved.SavedPendingAbrupt;
                    }
                    else _context.State.SetCurrentException(null);
                    return false;

                case ExecutableOpCode.MatchException:
                    var active = _context.State.CurrentException
                        ?? throw new InvalidOperationException("Exception selection requires an active exception.");
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent,
                        MatchesCaughtExceptionValue(_stack.TakeValue(instruction.InputStart, instruction.ResultSlot), active.Identity, _context, instruction.Span));
                    return false;

                case ExecutableOpCode.BindException:
                    var bound = _context.State.CurrentException
                        ?? throw new InvalidOperationException("Exception binding requires an active exception.");
                    bound.Adopt(_context, instruction.Span);
                    var name = _codeObject.Names[instruction.NameIndex];
                    AssignExecutableBoundName(_codeObject, _locals, _localCells, name, bound, _context, instruction.Span);
                    var suite = SavedActiveExceptions().Peek();
                    _context.ActiveHandlerVariables ??= new();
                    _context.ActiveHandlerVariables.Push((name, suite.SuiteStartIndex ?? 0, suite.SuiteEndIndex ?? int.MaxValue));
                    return false;

                case ExecutableOpCode.ReraiseException:
                    ThrowReraisedException(instruction.Span, _context);
                    return false;

                case ExecutableOpCode.EndFinally:
                    if (_pendingAbrupt is not null && (_pendingAbrupt.CleanupId ?? -1) != instruction.CleanupId)
                    {
                        // Normal nested cleanup leaves the outer completion pending.
                        _instructionIndex = instruction.TargetIndex;
                        return true;
                    }
                    if (_pendingAbrupt is PendingJump pendingJump) return DeliverJump(pendingJump, instruction.Span);
                    if (_pendingAbrupt is PendingException)
                    {
                        // An exception-routed finally suite exits: restore the
                        // active exception saved when the suite was entered.
                        _context.State.SetCurrentException(
                            _savedActiveExceptions is { Count: > 0 } restored ? restored.Pop().SavedException : null);
                    }

                    if (_pendingAbrupt is not null)
                    {
                        PropagatePendingAbrupt(_pendingAbrupt);
                    }

                    _instructionIndex = instruction.TargetIndex;
                    return true;

                case ExecutableOpCode.Return:
                    return DeliverReturn(TakeOperand(instruction), instruction.Span);

                case ExecutableOpCode.ReturnNone:
                    return DeliverReturn(PyNone.Instance, instruction.Span);

                default:
                    throw new InvalidOperationException($"Unknown executable opcode: {instruction.OpCode}");
            }
        }

        internal bool HasFrameReturn => _hasFrameReturn;

        internal object FrameReturnValue => _frameReturnValue.RequireNotNull();

        private async ValueTask DrainPendingDisposalsAsync()
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
            while (_stack.TakePendingDisposal() is { } cursor)
            {
                try { await cursor.DisposeAsync().ConfigureAwait(false); }
                catch (Exception exception)
                {
                    // A failed inner disposal must not strand an outer native
                    // enumerator. Preserve the first cleanup failure afterwards.
                    failure ??= System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
                }
            }
            failure?.Throw();
        }

        private async ValueTask ExecuteUntilCallAsync()
        {
            _stack.DeferAsyncDisposal = true;
            try
            {
                ResumePendingCall();
                if (_stack.HasPendingDisposals) await DrainPendingDisposalsAsync().ConfigureAwait(false);
                while (true)
                {
                    var pause = ExecuteInstructionPrefix(true, out var pending, out var span);
                    if (pause == InstructionPause.Boundary) return;
                    var jumped = false;
                    if (pause == InstructionPause.Operation)
                    {
                        var operationSpan = span.RequireNotNull();
                        try { jumped = await pending.ConfigureAwait(false); }
                        catch (ReturnSignal signal)
                        {
                            if (!TryRouteReturn(signal, operationSpan)) return;
                            jumped = true;
                        }
                        catch (ControlSignal signal)
                        {
                            if (!TryRouteControl(signal, operationSpan)) throw;
                            jumped = true;
                        }
                        catch (LythonRuntimeException error)
                        {
                            if (!TryRouteRuntimeException(error, operationSpan)) throw;
                            jumped = true;
                        }
                        finally { pending = default; }
                    }
                    else jumped = ConsumeOperation(ref pending);

                    // A relinquished cursor must finish before another guest
                    // instruction or handler observes its cleanup effects.
                    if (_stack.HasPendingDisposals) await DrainPendingDisposalsAsync().ConfigureAwait(false);
                    if (_hasFrameReturn || HasYield) return;
                    if (!jumped) _instructionIndex++;
                }
            }
            finally
            {
                try
                {
                    if (!HasYield && !HasPendingCall)
                    {
                        _preparedTypeSubscription = null;
                        _stack.RetireTo(_codeObject.FirstTemporarySlot);
                    }
                    try { if (_stack.HasPendingDisposals) await DrainPendingDisposalsAsync().ConfigureAwait(false); }
                    finally { if (!HasYield && !HasPendingCall) _stack.ReleaseStorage(); }
                }
                finally { _stack.DeferAsyncDisposal = false; }
            }
        }
    }
}
