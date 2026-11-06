using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private ValueTask<bool> ExecuteMemberLoadAsync(ExecutableInstruction instruction)
        {
            var target = _stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
            if (TryReadMemberCache(instruction, target, out var cached))
            {
                if (instruction.OpCode is ExecutableOpCode.LoadCallReceiver or ExecutableOpCode.LoadCallMember)
                {
                    _stack[instruction.ResultSlot] = PyNone.Instance;
                    _stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, cached);
                }
                else PublishInstructionResult(instruction, cached);
                return new(false);
            }
            var resolved = TryResolveRuntimeMemberAsync(target, _codeObject.Names[instruction.NameIndex], this, instruction.Span);
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
                _codeObject.Names[instruction.NameIndex], instruction.Span, this);
            PublishMemberCache(instruction, target, member.Value);
            if (instruction.OpCode is ExecutableOpCode.LoadCallReceiver or ExecutableOpCode.LoadCallMember)
            {
                _stack[instruction.ResultSlot] = PyNone.Instance;
                _stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, member.Value);
            }
            else PublishInstructionResult(instruction, member.Value);
        }

        private ValueTask<bool> ExecuteForNextAsync(ExecutableInstruction instruction)
        {
            var cursor = (PyIteration.Cursor)_stack[instruction.InputStart];
            var next = cursor.TryMoveNextAsync();
            return next.IsCompleted
                ? CompleteForNextAsync(next.GetAwaiter().GetResult(), cursor, instruction)
                : AwaitForNextAsync(next, cursor, instruction);
        }

        private async ValueTask<bool> AwaitForNextAsync(ValueTask<PyIterationResult> pending,
            PyIteration.Cursor cursor, ExecutableInstruction instruction)
            => await CompleteForNextAsync(await pending.ConfigureAwait(false), cursor, instruction).ConfigureAwait(false);

        private ValueTask<bool> CompleteForNextAsync(PyIterationResult next, PyIteration.Cursor cursor, ExecutableInstruction instruction)
        {
            if (next.HasValue)
            {
                PublishInstructionResult(instruction, next.Value);
                return new(false);
            }
            _ = _stack.TakeValue(instruction.InputStart, instruction.InputStart);
            var disposal = cursor.DisposeAsync();
            if (!disposal.IsCompleted) return AwaitCursorDisposalAsync(disposal, instruction.TargetIndex);
            disposal.GetAwaiter().GetResult();
            _instructionIndex = instruction.TargetIndex;
            return new(true);
        }

        private async ValueTask<bool> AwaitCursorDisposalAsync(ValueTask pending, int targetIndex)
        {
            await pending.ConfigureAwait(false);
            _instructionIndex = targetIndex;
            return true;
        }

        private async ValueTask<bool> ExecuteConditionalJumpAsync(ExecutableInstruction instruction)
        {
            if (await IsTruthyAsync(_stack.TakeValue(instruction.InputStart, instruction.LiveExtent), this, instruction.Span).ConfigureAwait(false))
                return false;
            _instructionIndex = instruction.TargetIndex;
            return true;
        }

        private async ValueTask<bool> ExecuteDelegationAsync(ExecutableInstruction instruction)
        {
            if (_delegation is null)
            {
                var source = _stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
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
                YieldValue = delegated.Value;
                HasYield = true;
            }
            else
            {
                PublishInstructionResult(instruction, _delegation!.ReturnValue);
                _delegation = null;
            }
        }

        private async ValueTask ExecuteDefinitionOrFallbackAsync(ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.Import:
                    var import = _codeObject.Imports[instruction.ImportIndex];
                    await ExecuteImportAsync(import, this).ConfigureAwait(false);
                    // Import stores update their bound slots directly. Copying
                    // the whole namespace would erase unrelated unmirrored locals.
                    return;
                case ExecutableOpCode.DefineFunction:
                    await ExecuteExecutableFunctionDefinitionAsync(_codeObject, _codeObject.Functions[instruction.FunctionIndex],
                        _locals, _localCells, this, true).ConfigureAwait(false);
                    return;
                default:
                    await DispatchLoweredStatementAsync(_codeObject.StatementFallbacks[instruction.StatementFallbackIndex],
                        this, AsynchronousLoweredStatementExecution.Instance).ConfigureAwait(false);
                    break;
            }
            SyncExecutableLocalsFromContext(_codeObject, _locals, _localCells, this);
        }

        private async ValueTask<bool> ExecuteContextManagerAsync(ExecutableInstruction instruction)
        {
            if (instruction.OpCode == ExecutableOpCode.ResolveContextManager)
            {
                var target = _stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                PublishInstructionResult(instruction, await PyContextManagers.ResolveAsync(target, instruction.Span, this).ConfigureAwait(false));
                return false;
            }
            if (instruction.OpCode == ExecutableOpCode.EnterContextManager)
            {
                var enteredContext = await PyContextManagers.EnterAsync(RequireContextManager(_stack.TakeValue(instruction.InputStart, instruction.ResultSlot), instruction.Span), instruction.Span, this).ConfigureAwait(false);
                PublishInstructionResult(instruction, enteredContext);
                return false;
            }
            if (instruction.OpCode == ExecutableOpCode.ExitContextManager)
            {
                var manager = RequireContextManager(_stack.TakeValue(instruction.InputStart, instruction.ResultSlot), instruction.Span);
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
            var slot = instruction.InputStart;
            try
            {
                var pending = PyIteration.Cursor.CreateAsync(_stack[slot], instruction.Span, this);
                if (!pending.IsCompleted) return AwaitIteratorCreationAsync(pending, slot);
                _stack[slot] = pending.GetAwaiter().GetResult();
                return default;
            }
            catch { _stack.RetireTo(slot); throw; }
        }

        private async ValueTask AwaitIteratorCreationAsync(ValueTask<PyIteration.Cursor> pending, int slot)
        {
            try { _stack[slot] = await pending.ConfigureAwait(false); }
            catch { _stack.RetireTo(slot); throw; }
        }

        private async ValueTask ExecuteOperandValueOperationAsync(ExecutableInstruction instruction)
        {
            var first = instruction.InputStart;
            try
            {
                object result;
                switch (instruction.OpCode)
                {
                    case ExecutableOpCode.Subscript:
                        result = _preparedCallValue is not null
                            ? await InvokePreparedTypeSubscription(_stack[first + 1], instruction.Span, true).ConfigureAwait(false)
                            : await ReadLoweredSubscriptAsync(_stack[first], _stack[first + 1], instruction.Span, this).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Slice:
                        var (start, end, step) = ReadExecutableSliceBounds(in _stack, instruction.SliceParts, instruction.InputStart + 1);
                        result = await ReadSliceValueAsync(_stack[first], start, end, step, instruction.Span, this).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Binary:
                        result = await EvaluateBinaryOperatorAsync(instruction.BinaryOperator,
                            ReadOperand(first), ReadOperand(instruction.SecondInput), this, instruction.Span).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Augmented:
                        result = await EvaluateAugmentedAssignmentAsync(ReadOperand(first), ReadOperand(instruction.SecondInput),
                            instruction.AugmentedOperator, this, instruction.Span).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Unary:
                        result = await EvaluateUnaryOperatorAsync(MapExecutableUnary(instruction.UnaryOperator), ReadOperand(first), this, instruction.Span).ConfigureAwait(false);
                        break;
                    default: throw new InvalidOperationException("Unexpected value operation.");
                }
                PublishInstructionResult(instruction, result);
            }
            catch { _stack.RetireTo(instruction.ResultSlot); throw; }
        }

    }
}
