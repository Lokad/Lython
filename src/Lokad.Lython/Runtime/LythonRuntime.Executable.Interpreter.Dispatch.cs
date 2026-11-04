using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ExecutableFrameInterpreter
    {
        private enum InstructionPause { Boundary, Operation, Disposal }

        // Both APIs execute completed instructions here, outside an async state
        // machine. An incomplete operation leaves its instruction/span current.
        // The async driver owns the pending value and all disposal until resume.
        private InstructionPause ExecuteInstructionPrefix(bool asynchronous, out ValueTask<bool> pending, out LythonSourceSpan? pendingSpan)
        {
            pending = default;
            pendingSpan = default;
            var instructions = _codeObject.Instructions;
            while (_instructionIndex < instructions.Length)
            {
                var jumped = false;
                ref readonly var instruction = ref instructions[_instructionIndex];
                _context.State.CheckExecution(instruction.Span);
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
                        case ExecutableOpCode.Complete:
                            return InstructionPause.Boundary;
                        case ExecutableOpCode.LoadLocal:
                            _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent,
                                LoadLocal(_codeObject, _locals, instruction.LocalSlot, instruction.Span, _context));
                            break;
                        case ExecutableOpCode.StoreLocal:
                            StoreExecutableLocal(instruction);
                            break;
                        case ExecutableOpCode.ApplyOperation:
                            if (!asynchronous)
                                ExecutePreparedOperationAsync(instruction, false).GetAwaiter().GetResult();
                            else
                            {
                                pending = WithoutJump(ExecutePreparedOperationAsync(instruction, true));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.Yield:
                            YieldValue = _stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                            HasYield = true;
                            _waitingForSend = true;
                            _instructionIndex++;
                            return InstructionPause.Boundary;
                        case ExecutableOpCode.YieldFrom:
                            if (asynchronous)
                            {
                                pending = ExecuteDelegationAsync(instruction);
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            else
                            {
                                if (_delegation is null)
                                {
                                    var source = _stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                                    _delegation = new GeneratorDelegation(Iter([source], instruction.Span, _context), _context, instruction.Span);
                                }
                                CompleteDelegatedStep(_delegation.Advance(_sentValue, _injectedException), instruction);
                            }
                            if (HasYield) return InstructionPause.Boundary;
                            break;
                        case ExecutableOpCode.Import or ExecutableOpCode.DefineFunction or ExecutableOpCode.ExecuteFallbackStatement:
                            if (!asynchronous) ExecuteDefinitionOrFallback(instruction);
                            else
                            {
                                pending = WithoutJump(ExecuteDefinitionOrFallbackAsync(instruction));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.LoadMember or ExecutableOpCode.LoadCallMember or ExecutableOpCode.LoadCallReceiver:
                            if (!asynchronous) ExecuteStackTransfer(instruction);
                            else if (!TryLoadDirectCallReceiver(instruction))
                            {
                                pending = ExecuteMemberLoadAsync(instruction);
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.LoadConst or ExecutableOpCode.LoadClosure or ExecutableOpCode.LoadGlobal or
                             ExecutableOpCode.LoadName or ExecutableOpCode.PreloadImplicitSuperArguments or
                             ExecutableOpCode.StoreClosure or ExecutableOpCode.StoreGlobal or ExecutableOpCode.StoreName or
                             ExecutableOpCode.Dup or ExecutableOpCode.PopTop:
                            ExecuteStackTransfer(instruction);
                            break;
                        case ExecutableOpCode.MakeList or ExecutableOpCode.MakeTuple or ExecutableOpCode.MakeSet or
                             ExecutableOpCode.MakeDict or ExecutableOpCode.MatchCase:
                            jumped = ExecuteStructure(instruction);
                            break;
                        case ExecutableOpCode.ResolveContextManager or ExecutableOpCode.EnterContextManager or ExecutableOpCode.ExitContextManager:
                            if (!asynchronous) jumped = ExecuteStructure(instruction);
                            else
                            {
                                pending = ExecuteContextManagerAsync(instruction);
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                jumped = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.Binary or ExecutableOpCode.Augmented:
                            if (!asynchronous ||
                                (ReadOperand(instruction.InputStart) is BigInteger or double or bool &&
                                 ReadOperand(instruction.SecondInput) is BigInteger or double or bool) ||
                                (ReadOperand(instruction.InputStart) is PyString && ReadOperand(instruction.SecondInput) is PyString))
                                ExecuteOperandValueOperation(instruction);
                            else
                            {
                                pending = WithoutJump(ExecuteOperandValueOperationAsync(instruction));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.Unary:
                            if (!asynchronous || ReadOperand(instruction.InputStart) is BigInteger or double or bool)
                                ExecuteOperandValueOperation(instruction);
                            else
                            {
                                pending = WithoutJump(ExecuteOperandValueOperationAsync(instruction));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.Subscript:
                            if (TrySuspendForTypeSubscription(instruction)) return InstructionPause.Boundary;
                            // Evaluate the key before inspecting protocol-key state.
                            if (!asynchronous || (_stack[instruction.InputStart + 1] is BigInteger &&
                                 _stack[instruction.InputStart] is PyList or PyTuple or PyBytes or PyString or PyRange) ||
                                (_stack[instruction.InputStart + 1] is PyString && _stack[instruction.InputStart] is PyDict { HasProtocolKeys: false }))
                                ExecuteOperandValueOperation(instruction);
                            else
                            {
                                pending = WithoutJump(ExecuteOperandValueOperationAsync(instruction));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.ForNext:
                            if (_stack.TryAdvanceNativeIteration(instruction.InputStart, out var nativeNext))
                            {
                                if (nativeNext.HasValue) _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, nativeNext.Value);
                                else
                                {
                                    _ = _stack.TakeValue(instruction.InputStart, instruction.InputStart);
                                    _instructionIndex = instruction.TargetIndex;
                                    jumped = true;
                                }
                            }
                            else if (!asynchronous) jumped = ExecuteForNext(instruction);
                            else
                            {
                                pending = ExecuteForNextAsync(instruction);
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                jumped = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.Call:
                            var call = _codeObject.CallSites[instruction.CallSiteIndex];
                            var callStart = instruction.InputStart;
                            var target = _stack[callStart + call.TargetOffset];
                            if (TrySuspendForExecutableCall(call, callStart, target)) return InstructionPause.Boundary;
                            if (!asynchronous) ExecuteOrdinaryCall(call, callStart, target, false).GetAwaiter().GetResult();
                            else
                            {
                                pending = WithoutJump(ExecuteOrdinaryCall(call, callStart, target, true));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.GetIter:
                            if (_stack.TryPrepareNativeIteration(instruction.InputStart, instruction.Span)) break;
                            if (!asynchronous) ExecuteValueOperation(instruction);
                            else
                            {
                                pending = WithoutJump(ExecuteGetIteratorAsync(instruction));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.Slice:
                            if (!asynchronous) ExecuteValueOperation(instruction);
                            else
                            {
                                pending = WithoutJump(ExecuteOperandValueOperationAsync(instruction));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.MakeSlice:
                            ExecuteValueOperation(instruction);
                            break;
                        case ExecutableOpCode.JumpIfFalse:
                            if (!asynchronous || _stack[instruction.InputStart] is not PyInstance)
                                jumped = ExecuteControlFlow(instruction);
                            else
                            {
                                pending = ExecuteConditionalJumpAsync(instruction);
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                jumped = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.AbruptJump or ExecutableOpCode.Jump or ExecutableOpCode.MatchException or
                             ExecutableOpCode.BindException or ExecutableOpCode.ReraiseException or ExecutableOpCode.ClearException or
                             ExecutableOpCode.EndFinally or ExecutableOpCode.Return or ExecutableOpCode.ReturnNone:
                            jumped = ExecuteControlFlow(instruction);
                            break;
                        default:
                            throw new InvalidOperationException($"Unknown executable opcode: {instruction.OpCode}");
                    }
                }
                catch (ReturnSignal signal)
                {
                    if (!TryRouteReturn(signal, instruction.Span)) return InstructionPause.Boundary;
                    jumped = true;
                }
                catch (ControlSignal signal)
                {
                    if (!TryRouteControl(signal, instruction.Span)) throw;
                    jumped = true;
                }
                catch (LythonRuntimeException error)
                {
                    if (!TryRouteRuntimeException(error, instruction.Span)) throw;
                    jumped = true;
                }

                if (asynchronous && _stack.HasPendingDisposals)
                {
                    pending = new(jumped);
                    return InstructionPause.Disposal;
                }
                if (_hasFrameReturn) return InstructionPause.Boundary;
                if (!jumped) _instructionIndex++;
            }
            return InstructionPause.Boundary;
        }

        private static InstructionPause PauseOperation(LythonSourceSpan span, out LythonSourceSpan? pendingSpan)
        {
            // The helper can finish and update the jump target before its
            // awaiter resumes. Keep the originating span independent of that.
            pendingSpan = span;
            return InstructionPause.Operation;
        }

        private static bool ConsumeOperation(ref ValueTask<bool> pending)
        {
            var operation = pending;
            pending = default;
            return operation.GetAwaiter().GetResult();
        }

        private static ValueTask<bool> WithoutJump(ValueTask operation)
        {
            if (operation.IsCompleted)
            {
                operation.GetAwaiter().GetResult();
                return new(false);
            }
            return AwaitOperation(operation);

            static async ValueTask<bool> AwaitOperation(ValueTask pending)
            {
                await pending.ConfigureAwait(false);
                return false;
            }
        }
    }
}
