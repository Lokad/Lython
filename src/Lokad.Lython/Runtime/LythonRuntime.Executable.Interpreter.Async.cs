using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ExecutableFrameInterpreter
    {
        private ValueTask<bool> ExecuteMemberLoadAsync(ExecutableInstruction instruction)
        {
            var target = _stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
            if (TryReadMemberCache(instruction, target, out var cached))
            {
                if (instruction.OpCode == ExecutableOpCode.LoadCallReceiver)
                {
                    _stack[instruction.ResultSlot] = PyNone.Instance;
                    _stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, cached);
                }
                else _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, cached);
                return new(false);
            }
            var resolved = TryResolveRuntimeMemberAsync(target, _codeObject.Names[instruction.NameIndex], _context, instruction.Span);
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
                _codeObject.Names[instruction.NameIndex], instruction.Span, _context);
            PublishMemberCache(instruction, target, member.Value);
            if (instruction.OpCode == ExecutableOpCode.LoadCallReceiver)
            {
                _stack[instruction.ResultSlot] = PyNone.Instance;
                _stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent, member.Value);
            }
            else _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, member.Value);
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
                _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, next.Value);
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
            if (await IsTruthyAsync(_stack.TakeValue(instruction.InputStart, instruction.LiveExtent), _context, instruction.Span).ConfigureAwait(false))
                return false;
            _instructionIndex = instruction.TargetIndex;
            return true;
        }

        private async ValueTask<bool> ExecuteDelegationAsync(ExecutableInstruction instruction)
        {
            if (_delegation is null)
            {
                var source = _stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                _delegation = new GeneratorDelegation(await IterAsync([source], instruction.Span, _context).ConfigureAwait(false),
                    _context, instruction.Span);
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
                _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, _delegation!.ReturnValue);
                _delegation = null;
            }
        }

        private async ValueTask ExecuteDefinitionOrFallbackAsync(ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.Import:
                    var import = _codeObject.Imports[instruction.ImportIndex];
                    await ExecuteImportAsync(import, _context).ConfigureAwait(false);
                    // Import stores update their bound slots directly. Copying
                    // the whole namespace would erase unrelated unmirrored locals.
                    return;
                case ExecutableOpCode.DefineFunction:
                    await ExecuteExecutableFunctionDefinitionAsync(_codeObject, _codeObject.Functions[instruction.FunctionIndex],
                        _locals, _localCells, _context, true).ConfigureAwait(false);
                    return;
                default:
                    await DispatchLoweredStatementAsync(_codeObject.StatementFallbacks[instruction.StatementFallbackIndex],
                        _context, AsynchronousLoweredStatementExecution.Instance).ConfigureAwait(false);
                    break;
            }
            SyncExecutableLocalsFromContext(_codeObject, _locals, _localCells, _context);
        }

        private async ValueTask<bool> ExecuteContextManagerAsync(ExecutableInstruction instruction)
        {
            if (instruction.OpCode == ExecutableOpCode.ResolveContextManager)
            {
                var target = _stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, await PyContextManagers.ResolveAsync(target, instruction.Span, _context).ConfigureAwait(false));
                return false;
            }
            if (instruction.OpCode == ExecutableOpCode.EnterContextManager)
            {
                var enteredContext = await PyContextManagers.EnterAsync(RequireContextManager(_stack.TakeValue(instruction.InputStart, instruction.ResultSlot), instruction.Span), instruction.Span, _context).ConfigureAwait(false);
                _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, enteredContext);
                return false;
            }
            if (instruction.OpCode == ExecutableOpCode.ExitContextManager)
            {
                var manager = RequireContextManager(_stack.TakeValue(instruction.InputStart, instruction.ResultSlot), instruction.Span);
                if (_pendingAbrupt is PendingException { Exception: var exception } pendingException &&
                    (pendingException.CleanupId ?? -1) == instruction.CleanupId)
                {
                    if (await PyContextManagers.ExitAsync(manager, ResolvePythonExceptionType(exception, _context),
                        CreatePythonExceptionInstance(exception), PyNone.Instance, instruction.Span, _context).ConfigureAwait(false))
                    {
                        RestoreSuppressedCompletion();
                    }
                }
                else _ = await PyContextManagers.ExitAsync(manager, PyNone.Instance, PyNone.Instance, PyNone.Instance,
                    instruction.Span, _context).ConfigureAwait(false);
                return false;
            }
            throw new InvalidOperationException("Unexpected asynchronous context-manager instruction.");
        }

        private ValueTask ExecuteGetIteratorAsync(ExecutableInstruction instruction)
        {
            var slot = instruction.InputStart;
            try
            {
                var pending = PyIteration.Cursor.CreateAsync(_stack[slot], instruction.Span, _context);
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
                        result = _preparedTypeSubscription is not null
                            ? await InvokePreparedTypeSubscription(_stack[first + 1], instruction.Span, true).ConfigureAwait(false)
                            : await ReadLoweredSubscriptAsync(_stack[first], _stack[first + 1], instruction.Span, _context).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Slice:
                        var (start, end, step) = ReadExecutableSliceBounds(in _stack, instruction.SliceParts, instruction.InputStart + 1);
                        result = await ReadSliceValueAsync(_stack[first], start, end, step, instruction.Span, _context).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Binary:
                        result = await EvaluateBinaryOperatorAsync(instruction.BinaryOperator,
                            _stack[first], _stack[first + 1], _context, instruction.Span).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Augmented:
                        result = await EvaluateAugmentedAssignmentAsync(_stack[first], _stack[first + 1],
                            MapExecutableAugmented(instruction.AugmentedOperator), _context, instruction.Span).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Unary:
                        result = await EvaluateUnaryOperatorAsync(MapExecutableUnary(instruction.UnaryOperator), _stack[first], _context, instruction.Span).ConfigureAwait(false);
                        break;
                    default: throw new InvalidOperationException("Unexpected value operation.");
                }
                _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, result);
            }
            catch { _stack.RetireTo(first); throw; }
        }

    }
}
