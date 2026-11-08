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
        private static bool TryLoadDirectCallReceiver(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, in ExecutableInstruction instruction)
        {
            if (instruction.OpCode != ExecutableOpCode.LoadCallMember) return false;
            var name = execution.Code.Names[instruction.NameIndex];
            var receiver = ReadOperand(ref execution, instruction.InputStart, scope, 0);
            if (invocation is not null && receiver is PyInstance instance &&
                PyAttributeLookup.TryResolveInstanceFunctionForCall(instance, name, out var function))
            {
                if (instruction.InputStart != instruction.ResultSlot)
                    execution.Stack.PublishValue(instruction.ResultSlot, instruction.ResultSlot + 1, receiver);
                execution.Stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, function);
                return true;
            }
            object? operation = receiver switch
            {
                PyList when name == "append" => ReceiverMethods.ListAppend,
                PyDict => DictMembers.GetMethod(name),
                ExecutionContext.TextFileHandle when name == "write" => ReceiverMethods.TextWrite,
                ExecutionContext.BinaryFileHandle when name == "write" => ReceiverMethods.BinaryWrite,
                CsvWriterObject when name == "writerow" => ReceiverMethods.CsvWriterow,
                PyString => StringMembers.GetMethod(name),
                ReMatchObject => ReMatchMembers.GetMethod(name),
                _ => null,
            };
            if (operation is null) return false;
            // Keep the evaluated receiver, then append the immutable operation.
            // Arguments are evaluated afterwards and can rebind the source name.
            if (instruction.InputStart != instruction.ResultSlot)
                execution.Stack.PublishValue(instruction.ResultSlot, instruction.ResultSlot + 1, receiver);
            execution.Stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, operation);
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

        internal void AdoptExecutableExecution(ref ExecutableWorkState execution, PyException? entryException)
        {
            _execution = execution;
            _entryActiveException = entryException;
            execution = default;
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
            _execution.ReturnValue = null;
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
            ExecutionContext? context,
            int slot,
            object value,
            LythonSourceSpan span)
        {
            var name = codeObject.LocalNames[slot];
            var guestName = !name.StartsWith('<');
            if (context?.GlobalNamespace is not null && guestName)
            {
                StoreGlobalNamespaceName(context, name, value);
                return;
            }
            locals[slot] = value;
            if (localCells?[slot] is ExecutableCell localCell)
            {
                localCell.Value = value;
            }

            if (guestName && (codeObject.RequiresLocalVariableMirroring || context?.MirrorLocalStores == true))
            {
                context!.Variables[name] = value;
            }
        }

        // Pop's returned reference belongs to this completed transfer, rather
        // than the long-running dispatch loop. Keep the CLR temporary here so
        // deleting a slot can make its payload collectible before frame exit.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void StoreExecutableLocal(ref ExecutableWorkState execution, ExecutionContext scope, ExecutionContext? invocation, in ExecutableInstruction instruction)
            => StoreLocalValue(execution.Code, execution.Locals, execution.LocalCells, invocation, instruction.LocalSlot,
                TakeOperand(ref execution, instruction, scope), instruction.Span);

        private static object ReadOperand(ref ExecutableWorkState execution, int source, ExecutionContext scope, int input)
        {
            if (source < 0) return RuntimeValue(execution.Code.Constants[~source]);
            var value = execution.Stack[source];
            if (source < execution.Code.FirstTemporarySlot && ReferenceEquals(value, UninitializedLocal))
                return LoadLocal(execution.Code, execution.Locals, source,
                    execution.Code.OperandReadSpan(execution.InstructionIndex, input), scope);
            return value;
        }

        private static object TakeOperand(ref ExecutableWorkState execution, in ExecutableInstruction instruction, ExecutionContext scope)
        {
            if (instruction.InputStart >= execution.Code.FirstTemporarySlot)
                return execution.Stack.TakeValue(instruction.InputStart, instruction.LiveExtent);
            var value = ReadOperand(ref execution, instruction.InputStart, scope, 0);
            execution.Stack.RetireTo(instruction.LiveExtent);
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
                instruction.OpCode == ExecutableOpCode.LoadCallMember)
                ? new ExecutableMemberCache(target, value) : null;

        private static void ExecuteStackTransfer(ref ExecutableWorkState execution, ExecutionContext scope, ExecutionContext? invocation, in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.LoadConst:
                    var constant = RuntimeValue(execution.Code.Constants[instruction.ConstantIndex]);
                    PublishInstructionResult(ref execution, invocation, instruction, constant);
                    break;

                case ExecutableOpCode.LoadClosure:
                    PublishInstructionResult(ref execution, invocation, instruction, LoadClosure(execution.Code, invocation!, instruction.ClosureSlot, instruction.Span));
                    break;

                case ExecutableOpCode.LoadGlobal:
                    PublishInstructionResult(ref execution, invocation, instruction, ResolveExecutableGlobal(execution.Code.Names[instruction.NameIndex], instruction.Span, scope));
                    break;

                case ExecutableOpCode.LoadName:
                    PublishInstructionResult(ref execution, invocation, instruction, ResolveExecutableName(execution.Code.Names[instruction.NameIndex], instruction.Span, invocation!));
                    break;


                case ExecutableOpCode.LoadCallMember:
                    if (TryLoadDirectCallReceiver(ref execution, scope, invocation, instruction)) break;
                    goto case ExecutableOpCode.LoadMember;
                case ExecutableOpCode.LoadMember:
                    var target = ReadOperand(ref execution, instruction.InputStart, scope, 0);
                    execution.Stack.RetireTo(instruction.ResultSlot);
                    var memberName = execution.Code.Names[instruction.NameIndex];
                    if (!invocation!.TryReadMemberCache(instruction, target, out var memberValue))
                    {
                        if (!TryResolveRuntimeMember(target, memberName, invocation!, instruction.Span, out var resolvedMember))
                        {
                            throw PyMemberAccess.CreateMissingMemberError(target, memberName, instruction.Span, invocation!);
                        }

                        memberValue = resolvedMember;
                        invocation!.PublishMemberCache(instruction, target, memberValue);
                    }
                    if (instruction.OpCode == ExecutableOpCode.LoadCallMember)
                    {
                        execution.Stack[instruction.ResultSlot] = PyNone.Instance;
                        execution.Stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, memberValue);
                    }
                    else PublishInstructionResult(ref execution, invocation, instruction, memberValue);
                    break;

                case ExecutableOpCode.PreloadImplicitSuperArguments:
                    PreloadImplicitSuperArguments(invocation!, instruction.Span);
                    break;

                case ExecutableOpCode.StoreClosure:
                    StoreExecutableClosure(
                        execution.Code,
                        invocation!,
                        instruction.ClosureSlot,
                        execution.Stack.TakeValue(instruction.InputStart, instruction.LiveExtent),
                        instruction.Span);
                    break;

                case ExecutableOpCode.StoreGlobal:
                    StoreName(
                        execution.Code.Names[instruction.NameIndex],
                        execution.Stack.TakeValue(instruction.InputStart, instruction.LiveExtent),
                        invocation!,
                        instruction.Span);
                    break;

                case ExecutableOpCode.StoreName:
                    AssignExecutableBoundName(
                        execution.Code,
                        execution.Locals,
                        execution.LocalCells,
                        execution.Code.Names[instruction.NameIndex],
                        execution.Stack.TakeValue(instruction.InputStart, instruction.LiveExtent),
                        invocation!,
                        instruction.Span);
                    break;

                case ExecutableOpCode.Dup:
                    execution.Stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, execution.Stack[instruction.InputStart]);
                    break;

                case ExecutableOpCode.PopTop:
                    _ = execution.Stack[instruction.InputStart];
                    execution.Stack.RetireTo(instruction.LiveExtent);
                    break;
            }
        }

        private bool ExecuteStructure(in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
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
                    var iterableSlot = instruction.ResultSlot;
                    try
                    {
                        var source = ReadOperand(ref _execution, instruction.InputStart, this, 0);
                        _execution.Stack.PublishValue(iterableSlot, instruction.LiveExtent, PyIteration.Cursor.Create(source, instruction.Span, this));
                    }
                    catch { _execution.Stack.RetireTo(iterableSlot); throw; }
                    break;

                case ExecutableOpCode.Slice:
                    ExecuteSliceValueOperation(ref _execution, this, this, instruction);
                    break;

                case ExecutableOpCode.MakeSlice:
                    var (start, stop, step) = ReadExecutableSliceBounds(in _execution.Stack, instruction.SliceParts, instruction.InputStart);
                    _execution.Stack.RetireTo(instruction.ResultSlot);
                    PublishInstructionResult(instruction, CreateSliceValue(start, stop, step, instruction.Span, this));
                    break;

            }

        }

        private static void ExecuteSliceValueOperation(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, in ExecutableInstruction instruction)
        {
            try
            {
                var result = ExecuteExecutableSlice(in execution.Stack, instruction.InputStart, instruction.SliceParts, instruction.Span, scope);
                PublishInstructionResult(ref execution, invocation, instruction, result);
            }
            catch { execution.Stack.RetireTo(instruction.ResultSlot); throw; }
        }

        // Ordinary returns deliver without throwing: a cleanup that handles
        // them in-frame routes through TryHandleAbrupt, otherwise the frame
        // records the value and exits normally at that control-flow instruction.
        // Frames without protected regions skip the routing scan
        // (and its per-return signal box) entirely. Propagated signals from
        // nested calls still arrive through the per-instruction catch below,
        // which stays as the routing backstop.
        private static bool DeliverReturn(ref ExecutableWorkState execution, ExecutionContext? invocation, object value, LythonSourceSpan span)
        {
            if (execution.Code.ExceptionRegions.Count != 0 &&
                TryHandleAbrupt(execution.Code, invocation!, ref execution.Stack, execution.InstructionIndex, new PendingReturn(value), span, ref invocation!._pendingAbrupt, ref execution.InstructionIndex, out _))
            {
                return true;
            }

            execution.ReturnValue = value;
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

        private static bool ExecuteControlFlow(ref ExecutableWorkState execution, ExecutionContext scope, ExecutionContext? invocation, in ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.AbruptJump:
                    return invocation!.DeliverJump(new PendingJump(instruction.TargetIndex, instruction.DiscardIterator), instruction.Span);

                case ExecutableOpCode.JumpIfFalse:
                    if (!IsTruthy(TakeOperand(ref execution, instruction, scope), scope, instruction.Span))
                    {
                        execution.InstructionIndex = instruction.TargetIndex;
                        return true;
                    }
                    return false;

                case ExecutableOpCode.Jump:
                    execution.InstructionIndex = instruction.TargetIndex;
                    return true;

                case ExecutableOpCode.ClearException:
                    if (instruction.ExceptionNameIndex >= 0)
                    {
                        _ = DeleteName(execution.Code.Names[instruction.ExceptionNameIndex], invocation!, instruction.Span);
                        if (invocation!.ActiveHandlerVariables is { Count: > 0 } activeHandlers)
                        {
                            activeHandlers.Pop();
                        }
                    }
                    if (invocation!._savedActiveExceptions is { Count: > 0 } saves)
                    {
                        var saved = saves.Pop();
                        scope.State.SetCurrentException(saved.SavedException);
                        invocation!._pendingAbrupt = saved.SavedPendingAbrupt;
                    }
                    else scope.State.SetCurrentException(null);
                    return false;

                case ExecutableOpCode.MatchException:
                    var active = scope.State.CurrentException
                        ?? throw new InvalidOperationException("Exception selection requires an active exception.");
                    PublishInstructionResult(ref execution, invocation, instruction,
                        MatchesCaughtExceptionValue(execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot), active.Identity, invocation!, instruction.Span));
                    return false;

                case ExecutableOpCode.BindException:
                    var bound = scope.State.CurrentException
                        ?? throw new InvalidOperationException("Exception binding requires an active exception.");
                    bound.Adopt(invocation!, instruction.Span);
                    var name = execution.Code.Names[instruction.NameIndex];
                    AssignExecutableBoundName(execution.Code, execution.Locals, execution.LocalCells, name, bound, invocation!, instruction.Span);
                    var suite = invocation!.SavedActiveExceptions().Peek();
                    invocation!.ActiveHandlerVariables ??= new();
                    invocation!.ActiveHandlerVariables.Push((name, suite.SuiteStartIndex ?? 0, suite.SuiteEndIndex ?? int.MaxValue));
                    return false;

                case ExecutableOpCode.ReraiseException:
                    ThrowReraisedException(instruction.Span, invocation!);
                    return false;

                case ExecutableOpCode.EndFinally:
                    if (invocation!._pendingAbrupt is not null && (invocation!._pendingAbrupt.CleanupId ?? -1) != instruction.CleanupId)
                    {
                        // Normal nested cleanup leaves the outer completion pending.
                        execution.InstructionIndex = instruction.TargetIndex;
                        return true;
                    }
                    if (invocation!._pendingAbrupt is PendingJump pendingJump) return invocation!.DeliverJump(pendingJump, instruction.Span);
                    if (invocation!._pendingAbrupt is PendingException)
                    {
                        // An exception-routed finally suite exits: restore the
                        // active exception saved when the suite was entered.
                        scope.State.SetCurrentException(
                            invocation!._savedActiveExceptions is { Count: > 0 } restored ? restored.Pop().SavedException : null);
                    }

                    if (invocation!._pendingAbrupt is not null)
                    {
                        PropagatePendingAbrupt(invocation!._pendingAbrupt);
                    }

                    execution.InstructionIndex = instruction.TargetIndex;
                    return true;

                case ExecutableOpCode.Return:
                    return DeliverReturn(ref execution, invocation, TakeOperand(ref execution, instruction, scope), instruction.Span);

                case ExecutableOpCode.ReturnNone:
                    return DeliverReturn(ref execution, invocation, PyNone.Instance, instruction.Span);

                default:
                    throw new InvalidOperationException($"Unknown executable opcode: {instruction.OpCode}");
            }
        }

        internal bool HasFrameReturn => _execution.ReturnValue is not null;

        internal object FrameReturnValue => _execution.ReturnValue.RequireNotNull();

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
                RoutePendingFrameException();
                while (true)
                {
                    var pause = ExecuteInstructionPrefix(ref _execution, this, this, true, out var pending, out var span);
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
                    if (_execution.ReturnValue is not null || HasYield) return;
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
