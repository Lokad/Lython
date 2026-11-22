using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private ValueTask<bool> ExecuteMemberLoadAsync(ExecutableInstruction instruction)
        {
            var target = ReadOperand(ref _execution, instruction.InputStart, this, 0);
            _execution.Stack.RetireTo(instruction.ResultSlot);
            if (TryReadMemberCache(instruction, target, out var cached))
            {
                if (instruction.OpCode == ExecutableOpCode.LoadCallMember)
                {
                    _execution.Stack[instruction.ResultSlot] = PyNone.Instance;
                    _execution.Stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, cached);
                }
                else PublishInstructionResult(instruction, cached);
                return new(false);
            }
            var resolved = TryResolveRuntimeMemberAsync(target, _execution.Code.Names[instruction.NameIndex], this, instruction.Span);
            if (!resolved.IsCompleted) return AwaitMemberLoadAsync(resolved, target, instruction);
            CompleteMemberLoad(resolved.GetAwaiter().GetResult(), target, instruction);
            return new(false);
        }

        private async ValueTask<bool> AwaitMemberLoadAsync(ValueTask<(bool Found, object Value)> pending,
            object target, ExecutableInstruction instruction)
        {
            CompleteMemberLoad(await pending.ConfigureAwait(false), target, instruction);
            return false;
        }

        private void CompleteMemberLoad((bool Found, object Value) member, object target, ExecutableInstruction instruction)
        {
            if (!member.Found) throw PyMemberAccess.CreateMissingMemberError(target,
                _execution.Code.Names[instruction.NameIndex], instruction.Span, this);
            PublishMemberCache(instruction, target, member.Value);
            if (instruction.OpCode == ExecutableOpCode.LoadCallMember)
            {
                _execution.Stack[instruction.ResultSlot] = PyNone.Instance;
                _execution.Stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, member.Value);
            }
            else PublishInstructionResult(instruction, member.Value);
        }

        private ValueTask<bool> ExecuteForNextAsync(ExecutableInstruction instruction)
        {
            var cursor = (PyIteration.Cursor)_execution.Stack[instruction.InputStart];
            var next = cursor.TryMoveNextAsync();
            return next.IsCompleted
                ? CompleteForNextAsync(next.GetAwaiter().GetResult(), cursor, instruction)
                : AwaitForNextAsync(next, cursor, instruction);
        }

        private ValueTask<bool> AdvanceCsvUnpack(CsvRecordSource csv, PyIteration.Cursor cursor,
            in ExecutableInstruction iteration, bool asynchronous)
        {
            ref readonly var unpack = ref _execution.Code.Instructions[_execution.InstructionIndex + 1];
            CheckExecution(iteration.Span);
            var destination = _execution.Stack.Slice(unpack.ResultSlot, unpack.UnpackLayout.TargetCount);
            // The frame's cleanup owns the slots even while the native read is pending.
            _execution.Stack.RestoreNativeExtent(unpack.LiveExtent);
            if (!asynchronous)
            {
                var count = csv.UnpackNext(destination);
                return new(count < 0 ? CompleteForNext(cursor, iteration) : CompleteFixedUnpack(count));
            }

            var row = csv.UnpackNextAsync(destination);
            return row.IsCompleted ? CompleteCsvUnpackAsync(row.GetAwaiter().GetResult(), cursor, iteration)
                : AwaitCsvUnpackAsync(row, cursor, iteration);
        }

        private async ValueTask<bool> AwaitCsvUnpackAsync(ValueTask<int> pending, PyIteration.Cursor cursor,
            ExecutableInstruction instruction)
            => await CompleteCsvUnpackAsync(await pending.ConfigureAwait(false), cursor, instruction).ConfigureAwait(false);

        private ValueTask<bool> CompleteCsvUnpackAsync(int count, PyIteration.Cursor cursor, ExecutableInstruction instruction)
            => count < 0 ? CompleteForNextAsync(PyIterationResult.End, cursor, instruction) : new(CompleteFixedUnpack(count));

        private async ValueTask<bool> AwaitForNextAsync(ValueTask<PyIterationResult> pending,
            PyIteration.Cursor cursor, ExecutableInstruction instruction)
            => await CompleteForNextAsync(await pending.ConfigureAwait(false), cursor, instruction).ConfigureAwait(false);

        private ValueTask<bool> CompleteForNextAsync(PyIterationResult next, PyIteration.Cursor cursor, ExecutableInstruction instruction)
        {
            if (next.HasValue)
            {
                PublishInstructionResult(instruction, next.TakeSlotValue());
                return new(false);
            }
            _ = _execution.Stack.TakeValue(instruction.InputStart, instruction.InputStart);
            var disposal = cursor.DisposeAsync();
            if (!disposal.IsCompleted) return AwaitCursorDisposalAsync(disposal, instruction.TargetIndex);
            disposal.GetAwaiter().GetResult();
            _execution.InstructionIndex = instruction.TargetIndex;
            return new(true);
        }

        private async ValueTask<bool> AwaitCursorDisposalAsync(ValueTask pending, int targetIndex)
        {
            await pending.ConfigureAwait(false);
            _execution.InstructionIndex = targetIndex;
            return true;
        }

        private async ValueTask<bool> ExecuteConditionalJumpAsync(ExecutableInstruction instruction)
        {
            if (await IsTruthyAsync(TakeOperand(ref _execution, instruction, this), this, instruction.Span).ConfigureAwait(false))
                return false;
            _execution.InstructionIndex = instruction.TargetIndex;
            return true;
        }

        private async ValueTask<bool> ExecuteDelegationAsync(ExecutableInstruction instruction)
        {
            if (_delegation is null)
            {
                var source = _execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                _delegation = new GeneratorDelegation(await IterAsync([source], instruction.Span, this).ConfigureAwait(false),
                    this, instruction.Span);
            }
            CompleteDelegatedStep(await _delegation.AdvanceAsync(_sentValue, _injectedException, true).ConfigureAwait(false), instruction);
            return false;
        }

        private void CompleteDelegatedStep(PyIterationResult delegated, ExecutableInstruction instruction)
        {
            _sentValue = PyNone.Instance;
            _injectedException = null;
            if (delegated.HasValue)
            {
                YieldValue = ExecutableSlot.FromObject(delegated.Value);
                HasYield = true;
            }
            else
            {
                PublishInstructionResult(instruction, _delegation!.ReturnValue);
                _delegation = null;
            }
        }

        private async ValueTask<bool> ExecuteContextManagerAsync(ExecutableInstruction instruction)
        {
            if (instruction.OpCode == ExecutableOpCode.ResolveContextManager)
            {
                var target = _execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                PublishInstructionResult(instruction, await PyContextManagers.ResolveAsync(target, instruction.Span, this).ConfigureAwait(false));
                return false;
            }
            if (instruction.OpCode == ExecutableOpCode.EnterContextManager)
            {
                var enteredContext = await PyContextManagers.EnterAsync(RequireContextManager(_execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot), instruction.Span), instruction.Span, this).ConfigureAwait(false);
                PublishInstructionResult(instruction, enteredContext);
                return false;
            }
            if (instruction.OpCode == ExecutableOpCode.ExitContextManager)
            {
                var manager = RequireContextManager(_execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot), instruction.Span);
                if (_pendingAbrupt is PendingException { Exception: var exception } pendingException &&
                    (pendingException.CleanupId ?? -1) == instruction.CleanupId)
                {
                    if (await PyContextManagers.ExitAsync(manager, ResolvePythonExceptionType(exception, this),
                        CreatePythonExceptionInstance(exception), PyNone.Instance, instruction.Span, this).ConfigureAwait(false))
                    {
                        RestoreSuppressedCompletion();
                    }
                }
                else _ = await PyContextManagers.ExitAsync(manager, PyNone.Instance, PyNone.Instance, PyNone.Instance,
                    instruction.Span, this).ConfigureAwait(false);
                return false;
            }
            throw new InvalidOperationException("Unexpected asynchronous context-manager instruction.");
        }

        private ValueTask ExecuteGetIteratorAsync(ExecutableInstruction instruction)
        {
            var slot = instruction.ResultSlot;
            try
            {
                var source = ReadOperand(ref _execution, instruction.InputStart, this, 0);
                var pending = PyIteration.Cursor.CreateAsync(source, instruction.Span, this);
                if (!pending.IsCompleted) return AwaitIteratorCreationAsync(pending, slot, instruction.LiveExtent);
                _execution.Stack.PublishValue(slot, instruction.LiveExtent, pending.GetAwaiter().GetResult());
                return default;
            }
            catch { _execution.Stack.RetireTo(slot); throw; }
        }

        private async ValueTask AwaitIteratorCreationAsync(ValueTask<PyIteration.Cursor> pending, int slot, int liveExtent)
        {
            try { _execution.Stack.PublishValue(slot, liveExtent, await pending.ConfigureAwait(false)); }
            catch { _execution.Stack.RetireTo(slot); throw; }
        }

        private async ValueTask ExecuteSliceValueOperationAsync(ExecutableInstruction instruction)
        {
            try
            {
                var (start, end, step) = ReadExecutableSliceBounds(in _execution.Stack, instruction.SliceParts, instruction.InputStart + 1);
                var result = await ReadSliceValueAsync(_execution.Stack[instruction.InputStart], start, end, step, instruction.Span, this).ConfigureAwait(false);
                PublishInstructionResult(instruction, result);
            }
            catch { _execution.Stack.RetireTo(instruction.ResultSlot); throw; }
        }

    }
}
