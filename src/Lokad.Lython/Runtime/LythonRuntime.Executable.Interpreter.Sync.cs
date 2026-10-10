using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ExecutableFrameInterpreter
    {
        // Keep synchronous instruction dispatch out of an async state machine.
        // Operation handlers and abrupt routing retain the shared semantics.
        public void Execute()
        {
            // Every control-flow edge must arrive at a block with the same value
            // stack depth. The first arrival records that depth; edge handling
            // below validates later arrivals. Abrupt signals remain separate so
            // finally blocks can run before return/break/continue is rethrown.
            while (true)
            {
                var block = _codeObject.Blocks[_currentBlockIndex];
                if (_blockEntryStackDepths.Length != 0)
                {
                    _blockEntryStackDepths[_currentBlockIndex] ??= _stack.Count;
                }
                var jumped = false;

                var instructions = block.InstructionArray;
                for (; _instructionIndex < instructions.Length; _instructionIndex++)
                {
                    ref readonly var instruction = ref instructions[_instructionIndex];
                    _context.Services.CheckExecution(instruction.Span);

                    try
                    {
                        if (_injectedException is not null && _delegation is null)
                        {
                            var injected = _injectedException;
                            _injectedException = null;
                            throw injected;
                        }
                        switch (instruction.OpCode)
                        {
                            // Common local transfers keep the same operations and
                            // instruction boundary without a second opcode switch.
                            case ExecutableOpCode.LoadLocal:
                                _stack.Push(LoadLocal(_codeObject, _locals, instruction.LocalSlot, instruction.Span));
                                break;
                            case ExecutableOpCode.StoreLocal:
                                StoreLocalValue(_codeObject, _locals, _localCells, _context, instruction.LocalSlot, Pop(ref _stack, instruction.Span), instruction.Span);
                                break;
                            case ExecutableOpCode.ForNext:
                                jumped = ExecuteForNext(instruction);
                                break;
                            case ExecutableOpCode.ApplyOperation:
                                ExecutePreparedOperationAsync((ExecutableOperation)_codeObject.Constants[instruction.ConstantIndex]!, instruction.Span, false).GetAwaiter().GetResult();
                                break;
                            case ExecutableOpCode.Yield:
                                YieldValue = Pop(ref _stack, instruction.Span);
                                HasYield = true;
                                _waitingForSend = true;
                                _instructionIndex++;
                                return;
                            case ExecutableOpCode.YieldFrom:
                                if (_delegation is null)
                                {
                                    var source = Pop(ref _stack, instruction.Span);
                                    var iterator = Iter([source], instruction.Span, _context);
                                    _delegation = new GeneratorDelegation(iterator, _context, instruction.Span);
                                }
                                var delegated = _delegation.Advance(_sentValue, _injectedException);
                                _sentValue = PyNone.Instance;
                                _injectedException = null;
                                if (delegated.HasValue)
                                {
                                    YieldValue = delegated.Value;
                                    HasYield = true;
                                    return;
                                }
                                _stack.Push(_delegation.ReturnValue);
                                _delegation = null;
                                break;
                            case ExecutableOpCode.Import or
                                 ExecutableOpCode.DefineFunction or
                                 ExecutableOpCode.ExecuteFallbackStatement:
                                ExecuteDefinitionOrFallback(instruction);
                                break;

                            case ExecutableOpCode.LoadConst or
                                 ExecutableOpCode.LoadClosure or
                                 ExecutableOpCode.LoadGlobal or
                                 ExecutableOpCode.LoadName or
                                 ExecutableOpCode.EvaluateFallbackExpression or
                                 ExecutableOpCode.LoadMember or
                                 ExecutableOpCode.StoreClosure or
                                 ExecutableOpCode.StoreGlobal or
                                 ExecutableOpCode.StoreName or
                                 ExecutableOpCode.Dup or
                                 ExecutableOpCode.PopTop:
                                ExecuteStackTransfer(instruction);
                                break;

                            case ExecutableOpCode.MakeList or
                                 ExecutableOpCode.MakeTuple or
                                 ExecutableOpCode.MakeSet or
                                 ExecutableOpCode.MakeDict or
                                 ExecutableOpCode.ResolveContextManager or
                                 ExecutableOpCode.EnterContextManager or
                                 ExecutableOpCode.ExitContextManager or
                                 ExecutableOpCode.MatchCase:
                                jumped = ExecuteStructure(instruction);
                                break;

                            case ExecutableOpCode.GetIter or
                                 ExecutableOpCode.AssignLoopTarget or
                                 ExecutableOpCode.AssignUnpackingTargets or
                                 ExecutableOpCode.Call or
                                 ExecutableOpCode.Subscript or
                                 ExecutableOpCode.Slice or
                                 ExecutableOpCode.MakeSlice or
                                 ExecutableOpCode.Binary or
                                 ExecutableOpCode.Augmented or
                                 ExecutableOpCode.Unary:
                                jumped = ExecuteValueOperation(instruction);
                                break;

                            case ExecutableOpCode.AbruptJump or ExecutableOpCode.JumpIfFalse or
                                 ExecutableOpCode.ChainLink or
                                 ExecutableOpCode.Jump or
                                 ExecutableOpCode.MatchException or
                                 ExecutableOpCode.BindException or
                                 ExecutableOpCode.ReraiseException or
                                 ExecutableOpCode.ClearException or
                                 ExecutableOpCode.EndFinally or
                                 ExecutableOpCode.Return or
                                 ExecutableOpCode.ReturnNone:
                                jumped = ExecuteControlFlow(instruction);
                                break;

                            default:
                                throw new InvalidOperationException($"Unknown executable opcode: {instruction.OpCode}");
                        }
                    }
                    catch (ReturnSignal signal)
                    {
                        if (!TryRouteReturn(signal, instruction.Span)) return;
                        jumped = true;
                    }
                    catch (ControlSignal signal)
                    {
                        if (!TryRouteControl(signal, instruction.Span)) throw;
                        jumped = true;
                    }
                    catch (LythonRuntimeException ex)
                    {
                        RouteRuntimeException(ex, instruction.Span);
                        jumped = true;
                    }

                    if (jumped)
                    {
                        _instructionIndex = 0;
                        break;
                    }
                }

                if (_hasFrameReturn)
                {
                    return;
                }

                if (!jumped)
                {
                    return;
                }
            }
        }
    }
}
