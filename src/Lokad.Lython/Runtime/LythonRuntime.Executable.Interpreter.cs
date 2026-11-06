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
    internal sealed partial class ExecutionContext : ExecutionFrame
    {
        private bool TryLoadDirectCallReceiver(ExecutableInstruction instruction)
        {
            if (instruction.OpCode is not (ExecutableOpCode.LoadCallMember or ExecutableOpCode.LoadCallReceiver)) return false;
            var name = _execution.Code.Names[instruction.NameIndex];
            if (_execution.Stack[instruction.InputStart] is PyInstance instance &&
                PyAttributeLookup.TryResolveInstanceFunctionForCall(instance, name, out var function))
            {
                _execution.Stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, function);
                return true;
            }
            if (instruction.OpCode != ExecutableOpCode.LoadCallReceiver) return false;
            var operation = _execution.Stack[instruction.InputStart] switch
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
            _execution.Stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, operation);
            return true;
        }

        internal void InitializeExecutableExecution(
            ExecutableCodeObject codeObject, object[] locals, ExecutableCell?[]? localCells,
            IReadOnlyList<ExecutableCell>? closureCells, ExecutableValueStorage stack)
        {
            InitializeExecutableBindings(codeObject, locals, localCells, closureCells);
            _execution.InstructionIndex = codeObject.EntryIndex;
            _entryActiveException = State.CurrentException;
            _execution.Stack = stack;
        }

        internal void EnsureExecutableStateAvailable()
        {
            if (_execution.Code is not null)
                throw new InvalidOperationException("An invocation context cannot execute overlapping code objects.");
        }

        // Lexical contexts may escape through closures. Retire all invocation
        // borrows after their users/cleanup finish; no refunded workspace or
        // handler/caller chain may survive in that retained lexical context.
        internal void DetachExecutableExecution()
        {
            ClearExecutableBindings();
            _execution.Stack = default;
            _pendingAbrupt = null;
            _execution.InstructionIndex = 0;
            _waitingForSend = false;
            _sentValue = PyNone.Instance;
            _injectedException = null;
            _delegation = null;
            HasYield = false;
            YieldValue = PyNone.Instance;
            _frameReturnValue = null;
            _hasFrameReturn = false;
            _savedActiveExceptions = null;
            _entryActiveException = null;
            _caller = null;
            _boundStorageOwner = null;
            _boundStorageBytes = 0;
            _pendingCallSite = null;
            _pendingFunction = null;
            _pendingReceiver = null;
            _pendingCallStart = 0;
            _completedCallValue = null;
            _completedCallError = null;
            _callCompletion = CallCompletion.Call;
            _protocolCallSpan = null;
            _preparedCallValue = null;
        }

        // The mutable stack lives in this invocation, including across generator
        // suspension. Helpers must borrow it by reference rather than copy it.
        private PendingAbruptSignal? _pendingAbrupt;
        private bool _waitingForSend;
        private object _sentValue = PyNone.Instance;
        private LythonRuntimeException? _injectedException;
        private GeneratorDelegation? _delegation;
        internal bool HasYield { get; private set; }
        internal object YieldValue { get; private set; } = PyNone.Instance;
        internal LythonSourceSpan? CurrentInstructionSpan
            => _execution.InstructionIndex < _execution.Code.Instructions.Length
                ? _execution.Code.Instructions[_execution.InstructionIndex].Span : null;

        internal void SeedGeneratorOuter(object iterator, PyIteration.Cursor cursor)
        {
            _execution.Locals[_execution.Code.LocalNameToSlot[".0"]] = iterator;
            _execution.Stack.PublishValue(_execution.Code.FirstTemporarySlot, _execution.Code.FirstTemporarySlot + 1, cursor);
        }

        internal async ValueTask ReleaseGeneratorTemporariesAsync(bool asynchronous)
        {
            _execution.Stack.DeferAsyncDisposal = asynchronous;
            try
            {
                _execution.Stack.RetireTo(_execution.Code.FirstTemporarySlot);
                try { if (_execution.Stack.HasPendingDisposals) await DrainPendingDisposalsAsync().ConfigureAwait(false); }
                finally { _execution.Stack.ReleaseStorage(); }
            }
            finally { _execution.Stack.DeferAsyncDisposal = false; }
        }

        internal void Resume(object sent, LythonRuntimeException? injected)
        {
            // A retained closure can write a cell while this generator is
            // suspended. Refresh only captured slots, including direct-call inputs.
            for (var index = 0; index < _execution.Code.CapturedLocalSlots.Count; index++)
            {
                var slot = _execution.Code.CapturedLocalSlots[index];
                _execution.Locals[slot] = _execution.LocalCells![slot]!.Value;
            }
            HasYield = false;
            _sentValue = sent;
            _injectedException = injected;
            if (_waitingForSend)
            {
                _waitingForSend = false;
                if (injected is null)
                {
                    var yielded = _execution.Code.Instructions[_execution.InstructionIndex - 1];
                    _execution.Stack.PublishValue(yielded.ResultSlot, yielded.LiveExtent, sent);
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
        private void AbandonFrame()
        {
            try { _execution.Stack.RetireTo(_execution.Code.FirstTemporarySlot); }
            finally
            {
                _savedActiveExceptions?.Clear();
                try
                {
                    if (ActiveHandlerVariables is { Count: > 0 })
                        AbandonActiveHandlerVars(this,
                            CurrentInstructionSpan ?? _execution.Code.Instructions[^1].Span);
                }
                finally { State.SetCurrentException(_entryActiveException); }
            }
        }

        // Pops saves for suites this propagation abandoned: a handler stays
        // live exactly while execution resumes inside its suite. Entries
        // without a suite range are kept, erring toward a stale value rather
        // than dropping a live save.
        private bool PendingCleanupContains(PendingAbruptSignal? pending, int block)
        {
            if (pending?.CleanupId is not int id) return false;
            for (var i = 0; i < _execution.Code.ExceptionRegions.Count; i++)
            {
                var region = _execution.Code.ExceptionRegions[i];
                if (region.CleanupId == id && region.SuiteStartIndex is int start &&
                    region.SuiteEndIndex is int end && block >= start && block <= end) return true;
            }
            return false;
        }

        private void RestoreSuppressedCompletion()
        {
            var saved = _savedActiveExceptions is { Count: > 0 } saves ? saves.Pop() : null;
            _pendingAbrupt = saved?.SavedPendingAbrupt;
            State.SetCurrentException(saved?.SavedException);
        }

        private void UnwindAbandonedHandlers(int targetBlockIndex)
        {
            while (_savedActiveExceptions is { Count: > 0 } saves &&
                saves.Peek() is { SuiteStartIndex: int start, SuiteEndIndex: int end } &&
                (targetBlockIndex < start || targetBlockIndex > end))
            {
                State.SetCurrentException(saves.Pop().SavedException);
            }
        }

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
            => StoreLocalValue(_execution.Code, _execution.Locals, _execution.LocalCells, this, instruction.LocalSlot,
                TakeOperand(instruction), instruction.Span);

        private object ReadOperand(int source)
            => source < 0 ? RuntimeValue(_execution.Code.Constants[~source]) : _execution.Stack[source];

        private object TakeOperand(in ExecutableInstruction instruction)
        {
            if (instruction.InputStart >= _execution.Code.FirstTemporarySlot)
                return _execution.Stack.TakeValue(instruction.InputStart, instruction.LiveExtent);
            var value = ReadOperand(instruction.InputStart);
            _execution.Stack.RetireTo(instruction.LiveExtent);
            return value;
        }

        private void ExecuteDefinitionOrFallback(in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.Import:
                    ExecuteExecutableImport(_execution.Code, _execution.Code.Imports[instruction.ImportIndex], _execution.Locals, _execution.LocalCells, this);
                    break;

                case ExecutableOpCode.DefineFunction:
                    ExecuteExecutableFunctionDefinitionAsync(_execution.Code, _execution.Code.Functions[instruction.FunctionIndex],
                        _execution.Locals, _execution.LocalCells, this, false).GetAwaiter().GetResult();
                    break;

                case ExecutableOpCode.ExecuteFallbackStatement:
                    DispatchLoweredStatementAsync(
                        _execution.Code.StatementFallbacks[instruction.StatementFallbackIndex],
                        this,
                        SynchronousLoweredStatementExecution.Instance).GetAwaiter().GetResult();
                    SyncExecutableLocalsFromContext(_execution.Code, _execution.Locals, _execution.LocalCells, this);
                    break;
            }
        }

        private bool TryReadMemberCache(ExecutableInstruction instruction, object target, out object value)
        {
            var cached = _execution.Stack.GetMemberCaches(_execution.Code.MemberCacheCount, instruction.Span)[instruction.MemberCacheIndex];
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
            => _execution.Stack.GetMemberCaches(_execution.Code.MemberCacheCount, instruction.Span)[instruction.MemberCacheIndex] = CanCacheRuntimeMemberValue(target, value,
                instruction.OpCode is ExecutableOpCode.LoadCallMember or ExecutableOpCode.LoadCallReceiver)
                ? new ExecutableMemberCache(target, value) : null;

        private void ExecuteStackTransfer(in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.LoadConst:
                    var constant = RuntimeValue(_execution.Code.Constants[instruction.ConstantIndex]);
                    PublishInstructionResult(instruction, constant);
                    break;

                case ExecutableOpCode.LoadClosure:
                    PublishInstructionResult(instruction, LoadClosure(_execution.Code, this, instruction.ClosureSlot, instruction.Span));
                    break;

                case ExecutableOpCode.LoadGlobal:
                    PublishInstructionResult(instruction, ResolveExecutableGlobal(_execution.Code.Names[instruction.NameIndex], instruction.Span, this));
                    break;

                case ExecutableOpCode.LoadName:
                    PublishInstructionResult(instruction, ResolveExecutableName(_execution.Code.Names[instruction.NameIndex], instruction.Span, this));
                    break;


                case ExecutableOpCode.LoadCallReceiver:
                case ExecutableOpCode.LoadCallMember:
                    if (TryLoadDirectCallReceiver(instruction)) break;
                    goto case ExecutableOpCode.LoadMember;
                case ExecutableOpCode.LoadMember:
                    var target = _execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                    var memberName = _execution.Code.Names[instruction.NameIndex];
                    if (!TryReadMemberCache(instruction, target, out var memberValue))
                    {
                        if (!TryResolveRuntimeMember(target, memberName, this, instruction.Span, out var resolvedMember))
                        {
                            throw PyMemberAccess.CreateMissingMemberError(target, memberName, instruction.Span, this);
                        }

                        memberValue = resolvedMember;
                        PublishMemberCache(instruction, target, memberValue);
                    }
                    if (instruction.OpCode is ExecutableOpCode.LoadCallReceiver or ExecutableOpCode.LoadCallMember)
                    {
                        _execution.Stack[instruction.ResultSlot] = PyNone.Instance;
                        _execution.Stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, memberValue);
                    }
                    else PublishInstructionResult(instruction, memberValue);
                    break;

                case ExecutableOpCode.PreloadImplicitSuperArguments:
                    PreloadImplicitSuperArguments(this, instruction.Span);
                    break;

                case ExecutableOpCode.StoreClosure:
                    StoreExecutableClosure(
                        _execution.Code,
                        this,
                        instruction.ClosureSlot,
                        _execution.Stack.TakeValue(instruction.InputStart, instruction.LiveExtent),
                        instruction.Span);
                    break;

                case ExecutableOpCode.StoreGlobal:
                    StoreName(
                        _execution.Code.Names[instruction.NameIndex],
                        _execution.Stack.TakeValue(instruction.InputStart, instruction.LiveExtent),
                        this,
                        instruction.Span);
                    break;

                case ExecutableOpCode.StoreName:
                    AssignExecutableBoundName(
                        _execution.Code,
                        _execution.Locals,
                        _execution.LocalCells,
                        _execution.Code.Names[instruction.NameIndex],
                        _execution.Stack.TakeValue(instruction.InputStart, instruction.LiveExtent),
                        this,
                        instruction.Span);
                    break;

                case ExecutableOpCode.Dup:
                    _execution.Stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, _execution.Stack[instruction.InputStart]);
                    break;

                case ExecutableOpCode.PopTop:
                    _ = _execution.Stack[instruction.InputStart];
                    _execution.Stack.RetireTo(instruction.LiveExtent);
                    break;
            }
        }

        private bool ExecuteStructure(in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.MakeList:
                    PublishInstructionResult(instruction,
                        CreateListFromStack(ref _execution.Stack, instruction.InputStart, instruction.ItemCount, instruction.Span, this));
                    break;

                case ExecutableOpCode.MakeTuple:
                    PublishInstructionResult(instruction,
                        CreateTupleFromStack(ref _execution.Stack, instruction.InputStart, instruction.ItemCount, instruction.Span, this));
                    break;

                case ExecutableOpCode.MakeSet:
                    PublishInstructionResult(instruction,
                        ExecuteExecutableMakeSet(ref _execution.Stack, instruction.InputStart, instruction.ItemCount, instruction.Span, this));
                    break;

                case ExecutableOpCode.MakeDict:
                    PublishInstructionResult(instruction,
                        ExecuteExecutableMakeDict(ref _execution.Stack, instruction.InputStart, instruction.PairCount, instruction.Span, this));
                    break;

                case ExecutableOpCode.ResolveContextManager:
                    var managerValue = _execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                    PublishInstructionResult(instruction, PyContextManagers.Resolve(managerValue, instruction.Span, this));
                    break;

                case ExecutableOpCode.EnterContextManager:
                    var enteringManager = RequireContextManager(_execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot), instruction.Span);
                    PublishInstructionResult(instruction, enteringManager.Enter());
                    break;

                case ExecutableOpCode.ExitContextManager:
                    var exitingManager = RequireContextManager(_execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot), instruction.Span);
                    // Only exceptions are suppressible. Return/break/continue
                    // are normal exits to __exit__ and remain pending afterward.
                    if (_pendingAbrupt is PendingException { Exception: var exception } pendingException &&
                        (pendingException.CleanupId ?? -1) == instruction.CleanupId)
                    {
                        if (exitingManager.Exit(
                                ResolvePythonExceptionType(exception, this),
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
                    var subject = _execution.Stack.TakeValue(instruction.InputStart, instruction.LiveExtent);
                    if (!TryExecuteExecutableMatchCase(
                            _execution.Code.MatchCases[instruction.MatchCaseIndex],
                            subject,
                            this,
                            _execution.Locals,
                            _execution.LocalCells,
                            _execution.Stack.Values.AsSpan(_execution.Code.PatternStorageStart)))
                    {
                        _execution.InstructionIndex = instruction.FailureIndex;
                        return true;
                    }
                    break;
            }

            return false;
        }

        private bool ExecuteForNext(in ExecutableInstruction instruction)
        {
            var iterator = (PyIteration.Cursor)_execution.Stack[instruction.InputStart];
            if (!iterator.TryMoveNext(out var value))
            {
                _ = _execution.Stack.TakeValue(instruction.InputStart, instruction.InputStart);
                iterator.Dispose();
                _execution.InstructionIndex = instruction.TargetIndex;
                return true;
            }

            PublishInstructionResult(instruction, value);
            return false;
        }

        private void ExecuteValueOperation(in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.GetIter:
                    var iterableSlot = instruction.InputStart;
                    try { _execution.Stack[iterableSlot] = PyIteration.Cursor.Create(_execution.Stack[iterableSlot], instruction.Span, this); }
                    catch { _execution.Stack.RetireTo(iterableSlot); throw; }
                    break;

                case ExecutableOpCode.Slice:
                    ExecuteOperandValueOperation(instruction);
                    break;

                case ExecutableOpCode.MakeSlice:
                    var (start, stop, step) = ReadExecutableSliceBounds(in _execution.Stack, instruction.SliceParts, instruction.InputStart);
                    _execution.Stack.RetireTo(instruction.ResultSlot);
                    PublishInstructionResult(instruction, CreateSliceValue(start, stop, step, instruction.Span, this));
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
                    ExecutableOpCode.Subscript => _preparedCallValue is not null
                        ? InvokePreparedTypeSubscription(_execution.Stack[first + 1], instruction.Span, false).GetAwaiter().GetResult()
                        : ReadSubscriptValue(_execution.Stack[first], _execution.Stack[first + 1], instruction.Span, this),
                    ExecutableOpCode.Slice => ExecuteExecutableSlice(in _execution.Stack, instruction.InputStart, instruction.SliceParts, instruction.Span, this),
                    ExecutableOpCode.Binary => EvaluateBinaryOperator(instruction.BinaryOperator,
                        ReadOperand(first), ReadOperand(instruction.SecondInput), this, instruction.Span),
                    ExecutableOpCode.Augmented => EvaluateAugmentedAssignment(ReadOperand(first), ReadOperand(instruction.SecondInput),
                        instruction.AugmentedOperator, this, instruction.Span),
                    ExecutableOpCode.Unary => EvaluateExecutableUnary(instruction.UnaryOperator, ReadOperand(first), this, instruction.Span),
                    _ => throw new InvalidOperationException("Unexpected value operation."),
                };
                PublishInstructionResult(instruction, result);
            }
            catch { _execution.Stack.RetireTo(instruction.ResultSlot); throw; }
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
            if (_execution.Code.ExceptionRegions.Count != 0 &&
                TryHandleAbrupt(_execution.Code, this, ref _execution.Stack, _execution.InstructionIndex, new PendingReturn(value), span, ref _pendingAbrupt, ref _execution.InstructionIndex, out _))
            {
                return true;
            }

            _frameReturnValue = value;
            _hasFrameReturn = true;
            return true;
        }

        private bool DeliverJump(PendingJump jump, LythonSourceSpan span)
        {
            var previous = ReferenceEquals(_pendingAbrupt, jump) ? jump.SavedPendingAbrupt : _pendingAbrupt;
            jump.SavedPendingAbrupt = PendingCleanupContains(previous, jump.TargetIndex) ? previous : null;
            _pendingAbrupt = jump.SavedPendingAbrupt;
            if (TryHandleAbrupt(_execution.Code, this, ref _execution.Stack, _execution.InstructionIndex, jump, span, ref _pendingAbrupt, ref _execution.InstructionIndex, out _)) return true;
            if (jump.DiscardIterator) _execution.Stack.RetireTo(_execution.Stack.LiveExtent - 1);
            UnwindAbandonedHandlers(jump.TargetIndex);
            UnwindAbandonedHandlerVars(this, span, jump.TargetIndex);
            _execution.InstructionIndex = jump.TargetIndex;
            return true;
        }

        private bool ExecuteControlFlow(in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.AbruptJump:
                    return DeliverJump(new PendingJump(instruction.TargetIndex, instruction.DiscardIterator), instruction.Span);

                case ExecutableOpCode.JumpIfFalse:
                    if (!IsTruthy(_execution.Stack.TakeValue(instruction.InputStart, instruction.LiveExtent), this, instruction.Span))
                    {
                        _execution.InstructionIndex = instruction.TargetIndex;
                        return true;
                    }
                    return false;

                case ExecutableOpCode.Jump:
                    _execution.InstructionIndex = instruction.TargetIndex;
                    return true;

                case ExecutableOpCode.ClearException:
                    if (instruction.ExceptionNameIndex >= 0)
                    {
                        _ = DeleteName(_execution.Code.Names[instruction.ExceptionNameIndex], this, instruction.Span);
                        if (ActiveHandlerVariables is { Count: > 0 } activeHandlers)
                        {
                            activeHandlers.Pop();
                        }
                    }
                    if (_savedActiveExceptions is { Count: > 0 } saves)
                    {
                        var saved = saves.Pop();
                        State.SetCurrentException(saved.SavedException);
                        _pendingAbrupt = saved.SavedPendingAbrupt;
                    }
                    else State.SetCurrentException(null);
                    return false;

                case ExecutableOpCode.MatchException:
                    var active = State.CurrentException
                        ?? throw new InvalidOperationException("Exception selection requires an active exception.");
                    PublishInstructionResult(instruction,
                        MatchesCaughtExceptionValue(_execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot), active.Identity, this, instruction.Span));
                    return false;

                case ExecutableOpCode.BindException:
                    var bound = State.CurrentException
                        ?? throw new InvalidOperationException("Exception binding requires an active exception.");
                    bound.Adopt(this, instruction.Span);
                    var name = _execution.Code.Names[instruction.NameIndex];
                    AssignExecutableBoundName(_execution.Code, _execution.Locals, _execution.LocalCells, name, bound, this, instruction.Span);
                    var suite = SavedActiveExceptions().Peek();
                    ActiveHandlerVariables ??= new();
                    ActiveHandlerVariables.Push((name, suite.SuiteStartIndex ?? 0, suite.SuiteEndIndex ?? int.MaxValue));
                    return false;

                case ExecutableOpCode.ReraiseException:
                    ThrowReraisedException(instruction.Span, this);
                    return false;

                case ExecutableOpCode.EndFinally:
                    if (_pendingAbrupt is not null && (_pendingAbrupt.CleanupId ?? -1) != instruction.CleanupId)
                    {
                        // Normal nested cleanup leaves the outer completion pending.
                        _execution.InstructionIndex = instruction.TargetIndex;
                        return true;
                    }
                    if (_pendingAbrupt is PendingJump pendingJump) return DeliverJump(pendingJump, instruction.Span);
                    if (_pendingAbrupt is PendingException)
                    {
                        // An exception-routed finally suite exits: restore the
                        // active exception saved when the suite was entered.
                        State.SetCurrentException(
                            _savedActiveExceptions is { Count: > 0 } restored ? restored.Pop().SavedException : null);
                    }

                    if (_pendingAbrupt is not null)
                    {
                        PropagatePendingAbrupt(_pendingAbrupt);
                    }

                    _execution.InstructionIndex = instruction.TargetIndex;
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
            while (_execution.Stack.TakePendingDisposal() is { } cursor)
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
            _execution.Stack.DeferAsyncDisposal = true;
            try
            {
                ResumePendingCall();
                if (_execution.Stack.HasPendingDisposals) await DrainPendingDisposalsAsync().ConfigureAwait(false);
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
                    if (_execution.Stack.HasPendingDisposals) await DrainPendingDisposalsAsync().ConfigureAwait(false);
                    if (_hasFrameReturn || HasYield) return;
                    if (!jumped) _execution.InstructionIndex++;
                }
            }
            finally
            {
                try
                {
                    if (!HasYield && !HasPendingCall)
                    {
                        _preparedCallValue = null;
                        AbandonFrame();
                    }
                    try { if (_execution.Stack.HasPendingDisposals) await DrainPendingDisposalsAsync().ConfigureAwait(false); }
                    finally { if (!HasYield && !HasPendingCall) _execution.Stack.ReleaseStorage(); }
                }
                finally { _execution.Stack.DeferAsyncDisposal = false; }
            }
        }
    }
}
