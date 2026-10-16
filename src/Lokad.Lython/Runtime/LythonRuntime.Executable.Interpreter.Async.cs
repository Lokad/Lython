using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ExecutableFrameInterpreter
    {
        private async ValueTask ExecuteDefinitionOrFallbackAsync(ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.Import:
                    var import = _codeObject.Imports[instruction.ImportIndex];
                    await ExecuteImportAsync(new ImportStatementSyntax(import.ModuleName, import.BindingName,
                        import.BoundModuleName, import.ImportedMembers, import.Span), _context).ConfigureAwait(false);
                    // Import stores update their bound slots directly. Copying
                    // the whole namespace would erase unrelated unmirrored locals.
                    return;
                case ExecutableOpCode.DefineFunction:
                    await ExecuteExecutableFunctionDefinitionAsync(_codeObject, _codeObject.Functions[instruction.FunctionIndex],
                        _locals, _localCells, _context, true).ConfigureAwait(false);
                    return;
                default:
                    await DispatchLoweredStatementAsync(_codeObject.StatementFallbacks[instruction.StatementFallbackIndex].Statement,
                        _context, AsynchronousLoweredStatementExecution.Instance).ConfigureAwait(false);
                    break;
            }
            SyncExecutableLocalsFromContext(_codeObject, _locals, _localCells, _context);
        }

        private async ValueTask<bool> ExecuteStructureAsync(ExecutableInstruction instruction)
        {
            if (instruction.OpCode == ExecutableOpCode.EnterContextManager)
            {
                PushObserved(await PyContextManagers.EnterAsync(PopContextManager(ref _stack, instruction.Span), instruction.Span, _context).ConfigureAwait(false), instruction.Span);
                return false;
            }
            if (instruction.OpCode == ExecutableOpCode.ExitContextManager)
            {
                var manager = PopContextManager(ref _stack, instruction.Span);
                if (_pendingAbrupt is PendingException { Exception: var exception } pendingException &&
                    (pendingException.CleanupId ?? -1) == instruction.CleanupId)
                {
                    if (await PyContextManagers.ExitAsync(manager, ResolvePythonExceptionType(exception, _context),
                        CreatePythonExceptionInstance(exception, _context), PyNone.Instance, instruction.Span, _context).ConfigureAwait(false))
                    {
                        RestoreSuppressedCompletion();
                    }
                }
                else _ = await PyContextManagers.ExitAsync(manager, PyNone.Instance, PyNone.Instance, PyNone.Instance,
                    instruction.Span, _context).ConfigureAwait(false);
                return false;
            }
            return ExecuteStructure(instruction);
        }

        private async ValueTask<bool> ExecuteValueOperationAsync(ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.GetIter:
                    var iterableSlot = _stack.Count - 1;
                    try { _stack[iterableSlot] = await PyIteration.Cursor.CreateAsync(_stack[iterableSlot], instruction.Span, _context).ConfigureAwait(false); }
                    catch { _stack.RemoveTail(_stack.Count - iterableSlot); throw; }
                    break;
                case ExecutableOpCode.ForNext:
                    var iterator = (PyIteration.Cursor)Peek(ref _stack, instruction.Span);
                    var advanced = await iterator.TryMoveNextAsync().ConfigureAwait(false);
                    if (!advanced.HasValue)
                    {
                        _ = Pop(ref _stack, instruction.Span);
                        await iterator.DisposeAsync().ConfigureAwait(false);
                        _currentBlockIndex = instruction.TargetBlockIndex;
                        return true;
                    }
                    _stack.Push(advanced.Value);
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
                        else if (call.HasReceiver && _stack[start] is Text.PyString stringReceiver &&
                            callable is Func<Text.PyString, LythonSourceSpan, ExecutionContext, object> stringOperation)
                        {
                            result = InvokeSingleInput(stringReceiver, stringOperation, call.CallSpan, _context);
                        }
                        else if (call.HasReceiver && ReferenceEquals(callable, DirectStringStartsWith))
                        {
                            result = InvokeStringStartsWith((Text.PyString)_stack[start], _stack[start + call.ArgumentOffset], call.CallSpan, _context);
                        }
                        else if (call.HasReceiver && ReferenceEquals(callable, DirectDictionaryGet))
                        {
                            var receiver = (PyDict)_stack[start];
                            var key = _stack[start + call.ArgumentOffset];
                            var fallback = call.ArgumentCount == 2 ? _stack[start + call.ArgumentOffset + 1] : PyNone.Instance;
                            // Only stored protocol keys can suspend a builtin-string lookup.
                            // Other keys keep the existing general dictionary protocol route.
                            result = key is Text.PyString && receiver.HasProtocolKeys
                                ? await InvokeDictionaryGetAsync(receiver, key, fallback, call.CallSpan, _context).ConfigureAwait(false)
                                : InvokeDictionaryGet(receiver, key, fallback, call.CallSpan, _context);
                        }
                        else if (call.IsPositionalOnly && call.ArgumentCount == 1 && ExecutionContext.IsLengthBuiltin(callable) &&
                            _stack[start + call.ArgumentOffset] is not PyInstance)
                        {
                            result = InvokeSingleInput(_stack[start + call.ArgumentOffset], LengthOperation, call.CallSpan, _context);
                        }
                        else if (call.IsPositionalOnly && callable is PyExecutableFunction { SupportsDirectPositional: true } function)
                        {
                            result = await InvokePositionalFunctionAsync(function, _stack.Slice(start + call.ArgumentOffset, call.ArgumentCount),
                                call.CallSpan, _context).ConfigureAwait(false);
                        }
                        else if (call.IsPositionalOnly && callable is PyBoundMethod { Function: PyFunction { SupportsPositionalBinding: true } method } bound)
                        {
                            result = await InvokePositionalFunctionAsync(method, _stack.Slice(start + call.ArgumentOffset, call.ArgumentCount),
                                call.CallSpan, _context, bound.Receiver).ConfigureAwait(false);
                        }
                        else
                        {
                            var arguments = PrepareExecutableCallArguments(call, ref _stack, start);
                            try
                            {
                                result = await InvokeCallableTargetAsync(callable, call.TargetSpan, call.CallSpan, _context,
                                    () => new ValueTask<CallArgumentValue[]>(arguments)).ConfigureAwait(false);
                            }
                            finally { ReturnCallArguments(arguments); }
                        }
                        CompleteExecutableValueTransfer(ref _stack, start, result, _context, instruction.Span);
                    }
                    catch
                    {
                        _stack.RemoveTail(_stack.Count - start);
                        throw;
                    }
                    break;
                case ExecutableOpCode.Subscript:
                case ExecutableOpCode.Slice:
                case ExecutableOpCode.Binary:
                case ExecutableOpCode.Augmented:
                case ExecutableOpCode.Unary:
                    await ExecuteObservedValueOperationAsync(instruction).ConfigureAwait(false);
                    break;
                default: return ExecuteValueOperation(instruction);
            }
            return false;
        }

        private async ValueTask ExecuteObservedValueOperationAsync(ExecutableInstruction instruction)
        {
            var first = _stack.Count - ValueOperationInputCount(instruction);
            try
            {
                object result;
                switch (instruction.OpCode)
                {
                    case ExecutableOpCode.Subscript:
                        result = await ReadLoweredSubscriptAsync(_stack[first], _stack[first + 1], instruction.Span, _context).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Slice:
                        var (start, end, step) = ReadExecutableSliceBounds(in _stack, instruction.SliceParts, out _);
                        result = await ReadSliceValueAsync(_stack[first], start, end, step, instruction.Span, _context).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Binary:
                        result = await EvaluateBinaryOperatorAsync(MapExecutableBinary(instruction.BinaryOperator),
                            _stack[first], _stack[first + 1], _context, instruction.Span).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Augmented:
                        result = await EvaluateAugmentedAssignmentAsync(_stack[first], _stack[first + 1],
                            MapExecutableAugmented(instruction.AugmentedOperator), _context, instruction.Span).ConfigureAwait(false);
                        break;
                    case ExecutableOpCode.Unary:
                        result = await EvaluateUnaryOperatorAsync(MapExecutableUnary(instruction.UnaryOperator), _stack[first], _context, instruction.Span).ConfigureAwait(false);
                        break;
                    default: throw new InvalidOperationException("Unexpected observed value operation.");
                }
                CompleteExecutableValueTransfer(ref _stack, first, result, _context, instruction.Span);
            }
            catch { _stack.RemoveTail(_stack.Count - first); throw; }
        }

        private async ValueTask<bool> ExecuteControlFlowAsync(ExecutableInstruction instruction)
        {
            if (instruction.OpCode == ExecutableOpCode.JumpIfFalse)
            {
                if (await IsTruthyAsync(Pop(ref _stack, instruction.Span), _context, instruction.Span).ConfigureAwait(false)) return false;
                _currentBlockIndex = instruction.TargetBlockIndex;
                return true;
            }
            if (instruction.OpCode == ExecutableOpCode.ChainLink)
            {
                var right = Pop(ref _stack, instruction.Span);
                var result = await EvaluateBinaryOperatorAsync(MapExecutableBinary(instruction.BinaryOperator),
                    LoadLocal(_codeObject, _locals, instruction.ChainSlot, instruction.Span, _context), right, _context, instruction.Span).ConfigureAwait(false);
                StoreLocalValue(_codeObject, _locals, _localCells, _context, instruction.ChainSlot, right, instruction.Span);
                if (await IsTruthyAsync(result, _context, instruction.Span).ConfigureAwait(false)) return false;
                _currentBlockIndex = instruction.FailureBlockIndex;
                return true;
            }
            throw new InvalidOperationException($"Unexpected asynchronous control-flow opcode: {instruction.OpCode}");
        }
    }
}
