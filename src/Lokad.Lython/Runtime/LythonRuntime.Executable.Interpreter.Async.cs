using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ExecutableFrameInterpreter
    {
        private ValueTask<bool> ExecuteMemberLoadAsync(ExecutableInstruction instruction)
        {
            var target = Pop(ref _stack, instruction.Span);
            if (TryReadMemberCache(instruction, target, out var cached))
            {
                if (instruction.OpCode == ExecutableOpCode.LoadCallReceiver) _stack.Push(PyNone.Instance);
                _stack.Push(cached);
                return new(false);
            }
            var resolved = TryResolveRuntimeMemberAsync(target, _codeObject.Names[instruction.NameIndex], _context, instruction.Span);
            if (!resolved.IsCompletedSuccessfully) return AwaitMemberLoadAsync(resolved, target, instruction);
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
            if (instruction.OpCode == ExecutableOpCode.LoadCallReceiver) _stack.Push(PyNone.Instance);
            _stack.Push(member.Value);
        }

        private ValueTask<bool> ExecuteForNextAsync(ExecutableInstruction instruction)
        {
            var cursor = (PyIteration.Cursor)Peek(ref _stack, instruction.Span);
            var next = cursor.TryMoveNextAsync();
            return next.IsCompletedSuccessfully
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
                _stack.Push(next.Value);
                return new(false);
            }
            _ = Pop(ref _stack, instruction.Span);
            var disposal = cursor.DisposeAsync();
            if (!disposal.IsCompletedSuccessfully) return AwaitCursorDisposalAsync(disposal, instruction.TargetIndex);
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
            if (await IsTruthyAsync(Pop(ref _stack, instruction.Span), _context, instruction.Span).ConfigureAwait(false))
                return false;
            _instructionIndex = instruction.TargetIndex;
            return true;
        }

        private async ValueTask<bool> ExecuteDelegationAsync(ExecutableInstruction instruction)
        {
            if (_delegation is null)
            {
                var source = Pop(ref _stack, instruction.Span);
                _delegation = new GeneratorDelegation(await IterAsync([source], instruction.Span, _context).ConfigureAwait(false),
                    _context, instruction.Span);
            }
            CompleteDelegatedStep(await _delegation.AdvanceAsync(_sentValue, _injectedException, true).ConfigureAwait(false));
            return false;
        }

        private void CompleteDelegatedStep(PyIterationResult delegated)
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
                _stack.Push(_delegation!.ReturnValue);
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
                var target = _stack.Pop();
                _stack.Push(await PyContextManagers.ResolveAsync(target, instruction.Span, _context).ConfigureAwait(false));
                return false;
            }
            if (instruction.OpCode == ExecutableOpCode.EnterContextManager)
            {
                var enteredContext = await PyContextManagers.EnterAsync(PopContextManager(ref _stack, instruction.Span), instruction.Span, _context).ConfigureAwait(false);
                _stack.Push(enteredContext);
                return false;
            }
            if (instruction.OpCode == ExecutableOpCode.ExitContextManager)
            {
                var manager = PopContextManager(ref _stack, instruction.Span);
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

        private async ValueTask ExecuteValueOperationAsync(ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.GetIter:
                    var iterableSlot = _stack.Count - 1;
                    try { _stack[iterableSlot] = await PyIteration.Cursor.CreateAsync(_stack[iterableSlot], instruction.Span, _context).ConfigureAwait(false); }
                    catch { _stack.RemoveTail(_stack.Count - iterableSlot); throw; }
                    break;
                case ExecutableOpCode.Call:
                    var call = _codeObject.CallSites[instruction.CallSiteIndex];
                    var start = ReadExecutableCallTarget(call, ref _stack, out var callable);
                    try
                    {
                        object result;
                        if (call.HasReceiver && ReferenceEquals(callable, DirectListAppend))
                        {
                            result = InvokeListAppend((PyList)_stack[start], _stack[start + call.ArgumentOffset], call.CallSpan, _context);
                        }
                        else if (call.HasReceiver && ReferenceEquals(callable, DirectFileWrite))
                        {
                            result = InvokeFileWrite(_stack[start],
                                _stack[start + call.ArgumentOffset], call.CallSpan, _context);
                        }
                        else if (call.HasReceiver && ReferenceEquals(callable, DirectCsvWriterow))
                        {
                            result = await InvokeCsvWriterowAsync((CsvWriterObject)_stack[start],
                                _stack[start + call.ArgumentOffset], call.CallSpan, _context).ConfigureAwait(false);
                        }
                        else if (call.HasReceiver && _stack[start] is Text.PyString stringReceiver &&
                            callable is Func<Text.PyString, LythonSourceSpan, ExecutionContext, object> stringOperation)
                        {
                            result = InvokeSingleInput(stringReceiver, stringOperation, call.CallSpan, _context);
                        }
                        else if (call.HasReceiver && _stack[start] is PyDict dictionaryReceiver &&
                            callable is Func<PyDict, LythonSourceSpan, ExecutionContext, object> dictionaryOperation)
                        {
                            result = InvokeSingleInput(dictionaryReceiver, dictionaryOperation, call.CallSpan, _context);
                        }
                        else if (call.HasReceiver && ReferenceEquals(callable, DirectStringJoin))
                        {
                            result = await InvokeStringJoinAsync((Text.PyString)_stack[start], _stack[start + call.ArgumentOffset], call.CallSpan, _context).ConfigureAwait(false);
                        }
                        else if (call.HasReceiver && ReferenceEquals(callable, DirectStringSplit))
                        {
                            var splitSeparator = call.ArgumentCount >= 1 ? _stack[start + call.ArgumentOffset] : null;
                            var splitCount = call.ArgumentCount == 2 ? _stack[start + call.ArgumentOffset + 1] : null;
                            result = splitCount is PyInstance
                                ? await InvokeStringSplitAsync((Text.PyString)_stack[start], splitSeparator, splitCount, call.CallSpan, _context).ConfigureAwait(false)
                                : InvokeStringSplit((Text.PyString)_stack[start], splitSeparator, splitCount, call.CallSpan, _context);
                        }
                        else if (call.HasReceiver && (ReferenceEquals(callable, DirectStringStartsWith) || ReferenceEquals(callable, DirectStringEndsWith)))
                        {
                            var arguments = _stack.Slice(start + call.ArgumentOffset, call.ArgumentCount);
                            // Native bounds stay synchronous; only guest index
                            // protocols need the awaited call wrapper.
                            result = (arguments.Length >= 2 && arguments.Span[1] is PyInstance) ||
                                (arguments.Length == 3 && arguments.Span[2] is PyInstance)
                                ? await InvokeStringAffixFromOperandsAsync((Text.PyString)_stack[start], arguments, call.CallSpan, _context,
                                    isStart: ReferenceEquals(callable, DirectStringStartsWith)).ConfigureAwait(false)
                                : InvokeStringAffixFromOperands((Text.PyString)_stack[start], arguments, call.CallSpan, _context,
                                    isStart: ReferenceEquals(callable, DirectStringStartsWith));
                        }
                        else if (call.HasReceiver && ReferenceEquals(callable, DirectStringReplace))
                        {
                            result = InvokeStringReplace((Text.PyString)_stack[start], _stack[start + call.ArgumentOffset],
                                _stack[start + call.ArgumentOffset + 1],
                                call.ArgumentCount == 3 ? _stack[start + call.ArgumentOffset + 2] : null,
                                call.CallSpan, _context);
                        }
                        else if (call.HasReceiver && ReferenceEquals(callable, DirectDictionaryGet))
                        {
                            var receiver = (PyDict)_stack[start];
                            var key = _stack[start + call.ArgumentOffset];
                            var fallback = call.ArgumentCount == 2 ? _stack[start + call.ArgumentOffset + 1] : PyNone.Instance;
                            // Native-key equality can reach asynchronous guest/host work.
                            result = receiver.HasProtocolKeys && !PyHashProtocols.NeedsProtocolKey(key)
                                ? await InvokeDictionaryGetAsync(receiver, key, fallback, call.CallSpan, _context).ConfigureAwait(false)
                                : InvokeDictionaryGet(receiver, key, fallback, call.CallSpan, _context);
                        }
                        else if (call.HasReceiver && ReferenceEquals(callable, DirectDictionarySetDefault))
                        {
                            result = InvokeDictionarySetDefault((PyDict)_stack[start], _stack[start + call.ArgumentOffset],
                                call.ArgumentCount == 2 ? _stack[start + call.ArgumentOffset + 1] : PyNone.Instance,
                                call.CallSpan, _context);
                        }
                        else if (call.IsPositionalOnly && call.ArgumentCount == 1 &&
                            SelectNativeUnaryOperation(callable, _stack[start + call.ArgumentOffset], NativeReturnShape.Other, out _) is { } nativeOperation)
                        {
                            result = InvokeSingleInput(_stack[start + call.ArgumentOffset], nativeOperation, call.CallSpan, _context);
                        }
                        else if (callable is PyFunctionBase function)
                        {
                            result = await InvokeFunctionFromOperandsAsync(function, _stack.Slice(start + call.ArgumentOffset, call.ArgumentCount),
                                call.CallSpan, _context, callSite: call.IsPositionalOnly && function is PyExecutableFunction { SupportsDirectPositional: true }
                                    ? null : call).ConfigureAwait(false);
                        }
                        else if (callable is PyBoundMethod { Function: PyFunctionBase method } bound)
                        {
                            result = await InvokeFunctionFromOperandsAsync(method, _stack.Slice(start + call.ArgumentOffset, call.ArgumentCount),
                                call.CallSpan, _context, bound.Receiver,
                                call.IsPositionalOnly && method is PyFunction { SupportsPositionalBinding: true } ? null : call).ConfigureAwait(false);
                        }
                        else
                        {
                            using var storage = ReserveExecutableCallArguments(call, _context);
                            var arguments = PrepareExecutableCallArguments(call, ref _stack, start, _context);
                            try
                            {
                                result = await InvokeCallableTargetAsync(callable, call.TargetSpan, call.CallSpan, _context,
                                    arguments).ConfigureAwait(false);
                            }
                            finally { ReturnCallArguments(arguments); }
                        }
                        CompleteExecutableValueTransfer(ref _stack, start, result);
                    }
                    catch
                    {
                        _stack.RemoveTail(_stack.Count - start);
                        throw;
                    }
                    break;
                default: throw new InvalidOperationException($"Unexpected asynchronous value-operation opcode: {instruction.OpCode}");
            }
        }

        private async ValueTask ExecuteOperandValueOperationAsync(ExecutableInstruction instruction)
        {
            var first = _stack.Count - ValueOperationInputCount(instruction);
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
                        var (start, end, step) = ReadExecutableSliceBounds(in _stack, instruction.SliceParts, out _);
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
                CompleteExecutableValueTransfer(ref _stack, first, result);
            }
            catch { _stack.RemoveTail(_stack.Count - first); throw; }
        }

    }
}
