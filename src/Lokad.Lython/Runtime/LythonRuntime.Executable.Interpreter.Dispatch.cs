using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private enum InstructionPause { Boundary, Operation, Disposal, ContextRequired }

        // Both APIs execute completed instructions here, outside an async state
        // machine. An incomplete operation leaves its instruction/span current.
        // The async driver owns the pending value and all disposal until resume.
        private static InstructionPause ExecuteInstructionPrefix(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, bool asynchronous, out ValueTask<bool> pending, out LythonSourceSpan? pendingSpan)
        {
            pending = default;
            pendingSpan = default;
            var instructions = execution.Code.Instructions;
            while (execution.InstructionIndex < instructions.Length)
            {
                var jumped = false;
                ref readonly var instruction = ref instructions[execution.InstructionIndex];
                scope.State.CheckExecution(instruction.Span);
                try
                {
                    if (invocation is not null && invocation._injectedException is not null && invocation._delegation is null)
                    {
                        var injected = invocation!._injectedException;
                        invocation!._injectedException = null;
                        throw injected;
                    }
                    switch (instruction.OpCode)
                    {
                        case ExecutableOpCode.Complete:
                            return InstructionPause.Boundary;
                        case ExecutableOpCode.LoadLocal:
                            PublishInstructionResult(ref execution, scope, invocation, instruction,
                                LoadLocal(execution.Code, execution.Locals, instruction.LocalSlot, instruction.Span, scope));
                            break;
                        case ExecutableOpCode.StoreLocal:
                            StoreExecutableLocal(ref execution, invocation, instruction);
                            break;
                        case ExecutableOpCode.StoreSubscript or ExecutableOpCode.StoreSubscriptLast:
                            if (invocation is null)
                            {
                                if (!TryExecuteSubscriptStoreWithoutContext(ref execution, instruction, scope)) return InstructionPause.ContextRequired;
                                break;
                            }
                            goto case ExecutableOpCode.StoreMember;
                        case ExecutableOpCode.StoreMember:
                        case ExecutableOpCode.StoreMemberLast
                            or ExecutableOpCode.StoreSlice or ExecutableOpCode.StoreSliceLast
                            or ExecutableOpCode.ReadRetainedSubscript or ExecutableOpCode.ReadRetainedSlice
                            or ExecutableOpCode.UnpackValues or ExecutableOpCode.FailAssertion or ExecutableOpCode.Raise
                            or ExecutableOpCode.DeleteName or ExecutableOpCode.DeleteMember
                            or ExecutableOpCode.DeleteSubscript or ExecutableOpCode.DeleteSlice:
                            if (invocation is null) return InstructionPause.ContextRequired;
                            if (!asynchronous) invocation!.ExecuteTargetInstruction(instruction, false).GetAwaiter().GetResult();
                            else
                            {
                                pending = WithoutJump(invocation!.ExecuteTargetInstruction(instruction, true));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.ApplyOperation:
                            if (invocation is null) return InstructionPause.ContextRequired;
                            if (!asynchronous)
                                invocation!.ExecutePreparedOperationAsync(instruction, false).GetAwaiter().GetResult();
                            else
                            {
                                pending = WithoutJump(invocation!.ExecutePreparedOperationAsync(instruction, true));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.Yield:
                            if (invocation is null) return InstructionPause.ContextRequired;
                            invocation!.YieldValue = execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                            invocation!.HasYield = true;
                            invocation!._waitingForSend = true;
                            execution.InstructionIndex++;
                            return InstructionPause.Boundary;
                        case ExecutableOpCode.YieldFrom:
                            if (invocation is null) return InstructionPause.ContextRequired;
                            if (asynchronous)
                            {
                                pending = invocation!.ExecuteDelegationAsync(instruction);
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            else
                            {
                                if (invocation!._delegation is null)
                                {
                                    var source = execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                                    invocation!._delegation = new GeneratorDelegation(Iter([source], instruction.Span, invocation!), invocation!, instruction.Span);
                                }
                                invocation!.CompleteDelegatedStep(invocation!._delegation.Advance(invocation!._sentValue, invocation!._injectedException), instruction);
                            }
                            if (invocation!.HasYield) return InstructionPause.Boundary;
                            break;
                        case ExecutableOpCode.Import or ExecutableOpCode.DefineFunction or ExecutableOpCode.ExecuteFallbackStatement:
                            if (invocation is null) return InstructionPause.ContextRequired;
                            if (!asynchronous) invocation!.ExecuteDefinitionOrFallback(instruction);
                            else
                            {
                                pending = WithoutJump(invocation!.ExecuteDefinitionOrFallbackAsync(instruction));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.LoadMember or ExecutableOpCode.LoadCallMember or ExecutableOpCode.LoadCallReceiver:
                            if (invocation is null)
                            {
                                if (!TryLoadDirectCallReceiver(ref execution, null, instruction)) return InstructionPause.ContextRequired;
                                break;
                            }
                            if (!asynchronous) ExecuteStackTransfer(ref execution, scope, invocation, instruction);
                            else if (!TryLoadDirectCallReceiver(ref execution, invocation, instruction))
                            {
                                pending = invocation!.ExecuteMemberLoadAsync(instruction);
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.LoadConst or ExecutableOpCode.LoadClosure or ExecutableOpCode.LoadGlobal or
                             ExecutableOpCode.LoadName or ExecutableOpCode.PreloadImplicitSuperArguments or
                             ExecutableOpCode.StoreClosure or ExecutableOpCode.StoreGlobal or ExecutableOpCode.StoreName or
                             ExecutableOpCode.Dup or ExecutableOpCode.PopTop:
                            if (invocation is null && instruction.OpCode is not (ExecutableOpCode.LoadConst or ExecutableOpCode.LoadGlobal or ExecutableOpCode.Dup or ExecutableOpCode.PopTop))
                                return InstructionPause.ContextRequired;
                            ExecuteStackTransfer(ref execution, scope, invocation, instruction);
                            break;
                        case ExecutableOpCode.MakeList or ExecutableOpCode.MakeTuple or ExecutableOpCode.MakeSet or
                             ExecutableOpCode.MakeDict or ExecutableOpCode.MatchCase:
                            if (invocation is null) return InstructionPause.ContextRequired;
                            jumped = invocation!.ExecuteStructure(instruction);
                            break;
                        case ExecutableOpCode.ResolveContextManager or ExecutableOpCode.EnterContextManager or ExecutableOpCode.ExitContextManager:
                            if (invocation is null) return InstructionPause.ContextRequired;
                            if (!asynchronous) jumped = invocation!.ExecuteStructure(instruction);
                            else
                            {
                                pending = invocation!.ExecuteContextManagerAsync(instruction);
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                jumped = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.Binary or ExecutableOpCode.Augmented:
                            var immediateBinary = (!asynchronous && invocation is not null) ||
                                (ReadOperand(ref execution, instruction.InputStart) is BigInteger or double or bool &&
                                 ReadOperand(ref execution, instruction.SecondInput) is BigInteger or double or bool) ||
                                (ReadOperand(ref execution, instruction.InputStart) is PyString && ReadOperand(ref execution, instruction.SecondInput) is PyString);
                            if (!immediateBinary && invocation is null) return InstructionPause.ContextRequired;
                            if (immediateBinary)
                                ExecuteOperandValueOperation(ref execution, scope, invocation, instruction);
                            else
                            {
                                pending = WithoutJump(invocation!.ExecuteOperandValueOperationAsync(instruction));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.Unary:
                            var immediateUnary = (!asynchronous && invocation is not null) ||
                                ReadOperand(ref execution, instruction.InputStart) is BigInteger or double or bool;
                            if (!immediateUnary && invocation is null) return InstructionPause.ContextRequired;
                            if (immediateUnary)
                                ExecuteOperandValueOperation(ref execution, scope, invocation, instruction);
                            else
                            {
                                pending = WithoutJump(invocation!.ExecuteOperandValueOperationAsync(instruction));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.Subscript:
                            if (invocation is not null && invocation.TrySuspendForTypeSubscription(instruction)) return InstructionPause.Boundary;
                            // Evaluate the key before inspecting protocol-key state.
                            var immediateSubscript = (!asynchronous && invocation is not null) || (execution.Stack[instruction.InputStart + 1] is BigInteger &&
                                 execution.Stack[instruction.InputStart] is PyList or PyTuple or PyBytes or PyString or PyRange) ||
                                (execution.Stack[instruction.InputStart] is PyDict dictionary && dictionary.CanReadKeySynchronously(execution.Stack[instruction.InputStart + 1]));
                            if (!immediateSubscript && invocation is null) return InstructionPause.ContextRequired;
                            if (immediateSubscript)
                                ExecuteOperandValueOperation(ref execution, scope, invocation, instruction);
                            else
                            {
                                pending = WithoutJump(invocation!.ExecuteOperandValueOperationAsync(instruction));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.ForNext:
                            if (invocation is null) return InstructionPause.ContextRequired;
                            if (execution.Stack.TryAdvanceNativeIteration(instruction.InputStart, out var nativeNext))
                            {
                                if (nativeNext.HasValue) PublishInstructionResult(ref execution, scope, invocation, instruction, nativeNext.Value);
                                else
                                {
                                    _ = execution.Stack.TakeValue(instruction.InputStart, instruction.InputStart);
                                    execution.InstructionIndex = instruction.TargetIndex;
                                    jumped = true;
                                }
                            }
                            else if (!asynchronous) jumped = invocation!.ExecuteForNext(instruction);
                            else
                            {
                                pending = invocation!.ExecuteForNextAsync(instruction);
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                jumped = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.Call:
                            var call = execution.Code.CallSites[instruction.CallSiteIndex];
                            var callStart = instruction.InputStart;
                            var target = execution.Stack[callStart + call.TargetOffset];
                            if (TryExecuteNativeCall(ref execution, scope, invocation, call, callStart, target)) break;
                            if (invocation is null) return InstructionPause.ContextRequired;
                            if (invocation!.TrySuspendForExecutableCall(call, callStart, target)) return InstructionPause.Boundary;
                            if (!asynchronous) invocation!.ExecuteOrdinaryCall(call, callStart, target, false).GetAwaiter().GetResult();
                            else
                            {
                                pending = WithoutJump(invocation!.ExecuteOrdinaryCall(call, callStart, target, true));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.GetIter:
                            if (invocation is null) return InstructionPause.ContextRequired;
                            if (execution.Stack.TryPrepareNativeIteration(instruction.InputStart, instruction.Span)) break;
                            if (!asynchronous) invocation!.ExecuteValueOperation(instruction);
                            else
                            {
                                pending = WithoutJump(invocation!.ExecuteGetIteratorAsync(instruction));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.Slice:
                            if (invocation is null) return InstructionPause.ContextRequired;
                            if (!asynchronous) invocation!.ExecuteValueOperation(instruction);
                            else
                            {
                                pending = WithoutJump(invocation!.ExecuteOperandValueOperationAsync(instruction));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.MakeSlice:
                            if (invocation is null) return InstructionPause.ContextRequired;
                            invocation!.ExecuteValueOperation(instruction);
                            break;
                        case ExecutableOpCode.JumpIfFalse:
                            if (invocation is null && execution.Stack[instruction.InputStart] is not (PyNone or bool or BigInteger or double or PyString or PyBytes or PyList or PyTuple or PyDict or PySet or PyRange))
                                return InstructionPause.ContextRequired;
                            if (!asynchronous || execution.Stack[instruction.InputStart] is not PyInstance)
                                jumped = ExecuteControlFlow(ref execution, scope, invocation, instruction);
                            else
                            {
                                pending = invocation!.ExecuteConditionalJumpAsync(instruction);
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                jumped = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.AbruptJump or ExecutableOpCode.Jump or ExecutableOpCode.MatchException or
                             ExecutableOpCode.BindException or ExecutableOpCode.ReraiseException or ExecutableOpCode.ClearException or
                             ExecutableOpCode.EndFinally or ExecutableOpCode.Return or ExecutableOpCode.ReturnNone:
                            if (invocation is null && instruction.OpCode is not (ExecutableOpCode.Jump or ExecutableOpCode.Return or ExecutableOpCode.ReturnNone))
                                return InstructionPause.ContextRequired;
                            jumped = ExecuteControlFlow(ref execution, scope, invocation, instruction);
                            break;
                        default:
                            throw new InvalidOperationException($"Unknown executable opcode: {instruction.OpCode}");
                    }
                }
                catch (ReturnSignal signal)
                {
                    if (invocation is null) throw;
                    if (!invocation.TryRouteReturn(signal, instruction.Span)) return InstructionPause.Boundary;
                    jumped = true;
                }
                catch (ControlSignal signal)
                {
                    if (invocation is null || !invocation.TryRouteControl(signal, instruction.Span)) throw;
                    jumped = true;
                }
                catch (LythonRuntimeException error)
                {
                    if (invocation is null)
                    {
                        var previous = scope.State.CurrentException;
                        if (previous is not null && !ReferenceEquals(error.OriginalPythonException, previous)) error.PythonContext ??= previous;
                        throw;
                    }
                    if (!invocation.TryRouteRuntimeException(error, instruction.Span)) throw;
                    jumped = true;
                }

                if (asynchronous && execution.Stack.HasPendingDisposals)
                {
                    pending = new(jumped);
                    return InstructionPause.Disposal;
                }
                if (execution.HasReturn) return InstructionPause.Boundary;
                if (!jumped) execution.InstructionIndex++;
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
