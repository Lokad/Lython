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
                    var import = codeObject.Imports[instruction.ImportIndex];
                    await ExecuteImportAsync(new ImportStatementSyntax(import.ModuleName, import.BindingName,
                        import.BoundModuleName, import.ImportedMembers, import.Span), context).ConfigureAwait(false);
                    break;
                case ExecutableOpCode.DefineFunction:
                    await ExecuteLoweredFunctionDefinitionAsync(codeObject.Functions[instruction.FunctionIndex].Function, context).ConfigureAwait(false);
                    break;
                default:
                    await DispatchLoweredStatementAsync(codeObject.StatementFallbacks[instruction.StatementFallbackIndex].Statement,
                        context, AsynchronousLoweredStatementExecution.Instance).ConfigureAwait(false);
                    break;
            }
            SyncExecutableLocalsFromContext(codeObject, locals, localCells, context);
        }

        private async ValueTask<bool> ExecuteStructureAsync(ExecutableInstruction instruction)
        {
            if (instruction.OpCode == ExecutableOpCode.EnterContextManager)
            {
                PushObserved(await PyContextManagers.EnterAsync(PopContextManager(_stack, instruction.Span), instruction.Span, context).ConfigureAwait(false), instruction.Span);
                return false;
            }
            if (instruction.OpCode == ExecutableOpCode.ExitContextManager)
            {
                var manager = PopContextManager(_stack, instruction.Span);
                if (_pendingAbrupt is PendingException { Exception: var exception } pendingException &&
                    (pendingException.CleanupId ?? -1) == instruction.CleanupId)
                {
                    if (await PyContextManagers.ExitAsync(manager, ResolvePythonExceptionType(exception, context),
                        CreatePythonExceptionInstance(exception), PyNone.Instance, instruction.Span, context).ConfigureAwait(false))
                    {
                        RestoreSuppressedCompletion();
                    }
                }
                else _ = await PyContextManagers.ExitAsync(manager, PyNone.Instance, PyNone.Instance, PyNone.Instance,
                    instruction.Span, context).ConfigureAwait(false);
                return false;
            }
            return ExecuteStructure(instruction);
        }

        private async ValueTask<bool> ExecuteValueOperationAsync(ExecutableInstruction instruction)
        {
            switch (instruction.OpCode)
            {
                case ExecutableOpCode.GetIter:
                    _stack.Push(await PyIteration.Cursor.CreateAsync(Pop(_stack, instruction.Span), instruction.Span, context).ConfigureAwait(false));
                    break;
                case ExecutableOpCode.ForNext:
                    var iterator = (PyIteration.Cursor)Peek(_stack, instruction.Span);
                    var advanced = await iterator.TryMoveNextAsync().ConfigureAwait(false);
                    if (!advanced.HasValue)
                    {
                        _ = Pop(_stack, instruction.Span);
                        await iterator.DisposeAsync().ConfigureAwait(false);
                        _currentBlockIndex = instruction.TargetBlockIndex;
                        return true;
                    }
                    _stack.Push(advanced.Value);
                    break;
                case ExecutableOpCode.AssignLoopTarget:
                    var loop = codeObject.LoopTargets[instruction.LoopTargetIndex];
                    await AssignLoopTargetAsync(loop.Target, Pop(_stack, instruction.Span), loop.Span, context).ConfigureAwait(false);
                    break;
                case ExecutableOpCode.AssignUnpackingTargets:
                    var unpacking = codeObject.UnpackingTargets[instruction.UnpackingTargetIndex];
                    await AssignTargetAsync(unpacking.Lowered, Pop(_stack, instruction.Span), context).ConfigureAwait(false);
                    break;
                case ExecutableOpCode.Call:
                    var call = codeObject.CallSites[instruction.CallSiteIndex];
                    var start = _stack.Count - call.ArgumentCount - 1;
                    var callable = _stack[start];
                    var arguments = RentCallArguments(call.ArgumentCount);
                    for (var i = 0; i < arguments.Length; i++)
                        arguments[i] = call.Arguments[i].Kind == CallArgumentKind.Keyword
                            ? CallArgumentValue.Keyword(call.Arguments[i].KeywordName, _stack[start + i + 1])
                            : CallArgumentValue.Positional(_stack[start + i + 1]);
                    _stack.RemoveTail(call.ArgumentCount + 1);
                    try
                    {
                        PushObserved(await InvokeCallableTargetAsync(callable, call.TargetSpan, call.CallSpan, context,
                            () => new ValueTask<CallArgumentValue[]>(arguments)).ConfigureAwait(false), instruction.Span);
                    }
                    finally { ReturnCallArguments(arguments); }
                    break;
                case ExecutableOpCode.Subscript:
                    var index = Pop(_stack, instruction.Span);
                    var receiver = Pop(_stack, instruction.Span);
                    PushObserved(await ReadLoweredSubscriptAsync(receiver, index, instruction.Span, context).ConfigureAwait(false), instruction.Span);
                    break;
                case ExecutableOpCode.Slice:
                    var (sliceStart, sliceEnd, sliceStep) = PopExecutableSliceBounds(_stack, instruction.SliceParts, instruction.Span);
                    var target = Pop(_stack, instruction.Span);
                    PushObserved(await ReadSliceValueAsync(target, sliceStart, sliceEnd, sliceStep, instruction.Span, context).ConfigureAwait(false), instruction.Span);
                    break;
                case ExecutableOpCode.Binary:
                    var right = Pop(_stack, instruction.Span);
                    var left = Pop(_stack, instruction.Span);
                    PushObserved(await EvaluateBinaryOperatorAsync(MapExecutableBinary(instruction.BinaryOperator), left, right, context, instruction.Span).ConfigureAwait(false), instruction.Span);
                    break;
                case ExecutableOpCode.Augmented:
                    var augmentedRight = Pop(_stack, instruction.Span);
                    var augmentedLeft = Pop(_stack, instruction.Span);
                    PushObserved(await EvaluateAugmentedAssignmentAsync(augmentedLeft, augmentedRight,
                        MapExecutableAugmented(instruction.AugmentedOperator), context, instruction.Span).ConfigureAwait(false), instruction.Span);
                    break;
                case ExecutableOpCode.Unary:
                    PushObserved(await EvaluateUnaryOperatorAsync(MapExecutableUnary(instruction.UnaryOperator), Pop(_stack, instruction.Span), context, instruction.Span).ConfigureAwait(false), instruction.Span);
                    break;
                default: return ExecuteValueOperation(instruction);
            }
            return false;
        }

        private async ValueTask<bool> ExecuteControlFlowAsync(ExecutableInstruction instruction)
        {
            if (instruction.OpCode == ExecutableOpCode.JumpIfFalse)
            {
                if (await IsTruthyAsync(Pop(_stack, instruction.Span), context, instruction.Span).ConfigureAwait(false)) return false;
                _currentBlockIndex = instruction.TargetBlockIndex;
                return true;
            }
            if (instruction.OpCode == ExecutableOpCode.ChainLink)
            {
                var right = Pop(_stack, instruction.Span);
                var result = await EvaluateBinaryOperatorAsync(MapExecutableBinary(instruction.BinaryOperator),
                    LoadLocal(codeObject, locals, instruction.ChainSlot, instruction.Span), right, context, instruction.Span).ConfigureAwait(false);
                StoreLocalValue(codeObject, locals, localCells, context, instruction.ChainSlot, right, instruction.Span);
                if (await IsTruthyAsync(result, context, instruction.Span).ConfigureAwait(false)) return false;
                _currentBlockIndex = instruction.FailureBlockIndex;
                return true;
            }
            return ExecuteControlFlow(instruction);
        }
    }
}
