using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

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
                if (instruction.OpCode != ExecutableOpCode.LoadMember)
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
            if (instruction.OpCode != ExecutableOpCode.LoadMember)
            {
                _execution.Stack[instruction.ResultSlot] = PyNone.Instance;
                _execution.Stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, member.Value);
            }
            else PublishInstructionResult(instruction, member.Value);
        }

        private ValueTask<bool> ExecuteForNextAsync(ExecutableInstruction instruction)
        {
            var iterator = _execution.Stack[instruction.InputStart];
            ValueTask<PyIterationResult> next;
            if (iterator is PyIteration.Cursor cursor) next = cursor.TryMoveNextAsync();
            else
            {
                State.CheckInterpreterStack(instruction.Span);
                if (iterator is TextFileHandle file)
                {
                    if (instruction.HasBufferedPrefixSearch && TrySkipFilePrefixMismatches(file, instruction)) return new(true);
                    var line = file.ReadLineAsync(-1);
                    return line.IsCompleted ? new(CompleteForNextLine(line.GetAwaiter().GetResult(), instruction))
                        : AwaitForNextLineAsync(line, instruction);
                }
                next = iterator is IPyAsyncIteratorValue asynchronous ? asynchronous.TryMoveNextAsync()
                    : new(((IPyIteratorValue)iterator).TryMoveNext());
            }
            return next.IsCompleted
                ? new(CompleteForNext(next.GetAwaiter().GetResult(), instruction))
                : AwaitForNextAsync(next, instruction);
        }

        private async ValueTask<bool> AwaitForNextLineAsync(ValueTask<PyString> pending, ExecutableInstruction instruction)
            => CompleteForNextLine(await pending.ConfigureAwait(false), instruction);

        private bool TryAdvanceFixedUnpack(object? iterator, in ExecutableInstruction iteration,
            bool asynchronous, out ValueTask<bool> pending)
        {
            ref readonly var unpack = ref _execution.Code.Instructions[_execution.InstructionIndex + 1];
            var source = iterator is PyIteration.Cursor cursor ? cursor.DirectUnpackSource : iterator;
            var csv = (source as CsvReaderObject)?.Records;
            var zip = source as PyZipIterator;
            if (csv is not { CanUnpackDirectly: true } && zip?.Arity != unpack.UnpackLayout.TargetCount)
            {
                pending = default;
                return false;
            }
            CheckExecution(iteration.Span);
            var destination = _execution.Stack.Slice(unpack.ResultSlot, unpack.UnpackLayout.TargetCount);
            // Cleanup owns any partially filled slots while a producer suspends.
            _execution.Stack.RestoreNativeExtent(unpack.LiveExtent);
            if (!asynchronous)
            {
                var count = zip is not null ? zip.UnpackNext(destination) : csv!.UnpackNext(destination);
                pending = new(CompleteIterationUnpack(count, iteration));
                return true;
            }

            var row = zip is not null ? zip.UnpackNextAsync(destination) : csv!.UnpackNextAsync(destination);
            pending = row.IsCompleted ? new(CompleteIterationUnpack(row.GetAwaiter().GetResult(), iteration))
                : AwaitIterationUnpackAsync(row, iteration);
            return true;
        }

        private async ValueTask<bool> AwaitIterationUnpackAsync(ValueTask<int> pending, ExecutableInstruction instruction)
            => CompleteIterationUnpack(await pending.ConfigureAwait(false), instruction);

        private bool CompleteIterationUnpack(int count, in ExecutableInstruction instruction)
        {
            if (count >= 0) return CompleteFixedUnpack(count, instruction.FixedUnpackLocalStart);
            _execution.Stack.RetireTo(instruction.ResultSlot);
            return ExitForNext(instruction);
        }

        private async ValueTask<bool> AwaitForNextAsync(ValueTask<PyIterationResult> pending, ExecutableInstruction instruction)
            => CompleteForNext(await pending.ConfigureAwait(false), instruction);

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
