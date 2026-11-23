using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private enum InstructionPause { Continue, Boundary, UnpackedReturn, Call, Operation, Disposal, ContextRequired }

        // Both APIs execute completed instructions here, outside an async state
        // machine. An incomplete operation leaves its instruction/span current.
        // The async driver owns the pending value and all disposal until resume.
        private static InstructionPause ExecuteInstructionPrefix(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, bool asynchronous, out ValueTask<bool> pending, out LythonSourceSpan? pendingSpan,
            out ValueTask<ExecutionContext?> nextCall, Span<ExecutableSlot> unpackedReturn = default)
        {
            pending = default;
            pendingSpan = default;
            nextCall = default;
            var instructions = execution.Code.Instructions;
            while (execution.InstructionIndex < instructions.Length)
            {
                var jumped = false;
                LythonRuntimeException returnedFailure;
                ref readonly var instruction = ref instructions[execution.InstructionIndex];
                scope.State.CheckExecution(instruction.Span);
                try
                {
                    switch (instruction.OpCode)
                    {
                        case ExecutableOpCode.Complete:
                            return InstructionPause.Boundary;
                        case ExecutableOpCode.LoadLocal:
                            PublishInstructionResult(ref execution, invocation, instruction,
                                LoadLocal(execution.Code, execution.Locals, instruction.LocalSlot, instruction.Span, scope));
                            break;
                        case ExecutableOpCode.CheckLocal:
                            if (execution.Locals[instruction.LocalSlot].IsUnassigned)
                                throw RuntimeErrors.UnboundLocalVariable(execution.Code.LocalNames[instruction.LocalSlot], instruction.Span);
                            break;
                        case ExecutableOpCode.LoadClosure:
                            if (!execution.Stack.HasPendingDisposals && TryExecuteClosureArithmetic(execution.Code,
                                execution.Captures, execution.InstructionIndex, execution.Stack.LiveExtent, scope))
                            { execution.InstructionIndex += 2; break; }
                            if (!TryLoadClosure(in execution, invocation, instruction.ClosureSlot, instruction.Span, out var captured))
                                return InstructionPause.ContextRequired;
                            PublishInstructionResult(ref execution, invocation, instruction, captured);
                            break;
                        case ExecutableOpCode.StoreLocal:
                            StoreExecutableLocal(ref execution, scope, invocation, instruction);
                            break;
                        case ExecutableOpCode.StoreClosure:
                            if (!TryStoreClosure(ref execution, invocation, instruction)) return InstructionPause.ContextRequired;
                            break;
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
                        case ExecutableOpCode.StartFormattedString or ExecutableOpCode.AppendFormattedText
                            or ExecutableOpCode.FinishFormattedString or ExecutableOpCode.ConvertFormattedValue or ExecutableOpCode.FormatField:
                            if (TryExecuteFormattedInstruction(ref execution, invocation, instruction, invocation ?? scope)) break;
                            if (invocation is null) return InstructionPause.ContextRequired;
                            if (!asynchronous) invocation.ExecuteFormattedValueAsync(instruction, false).GetAwaiter().GetResult();
                            else
                            {
                                pending = WithoutJump(invocation.ExecuteFormattedValueAsync(instruction, true));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.StartFunctionDefaults:
                            System.Diagnostics.Debug.Assert(execution.Stack.LiveExtent == instruction.ResultSlot);
                            StartExecutableDefaults((FunctionParameterLayout)execution.Code.Constants[instruction.ConstantIndex]!,
                                execution.Stack.Values.AsSpan(instruction.ResultSlot, instruction.LiveExtent - instruction.ResultSlot),
                                instruction.StartsKeywordDefaultsOnly, invocation ?? scope, instruction.Span);
                            execution.Stack.RestoreNativeExtent(instruction.LiveExtent);
                            break;
                        case ExecutableOpCode.StoreFunctionDefault:
                            var defaultValue = execution.Stack.TakeSlotValue(instruction.ResultSlot, instruction.LiveExtent);
                            StoreDefaultResult(execution.Stack.Values, instruction, defaultValue, (invocation ?? scope).State);
                            break;
                        case ExecutableOpCode.ApplyOperation:
                            if (!TryExecutePreparedOperation(ref execution, scope, invocation, instruction, asynchronous, out var operationTask))
                                return InstructionPause.ContextRequired;
                            if (!asynchronous)
                                operationTask.GetAwaiter().GetResult();
                            else
                            {
                                pending = WithoutJump(operationTask);
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.Yield:
                        {
                            if (invocation is null) return InstructionPause.ContextRequired;
                            var value = execution.Stack.TakeSlotValue(instruction.InputStart, instruction.ResultSlot);
                            // A private funded consumer can take an ordinary
                            // discarded yield without suspending this frame or
                            // publishing the unused send(None) result. Retain
                            // real resume boundaries for mirrored namespaces,
                            // delegated yields and outstanding cleanup.
                            var following = execution.InstructionIndex + 1;
                            var buffered = invocation._yieldDestination;
                            var discardsSend = !invocation.MirrorsExecutableLocals && !invocation.MirrorLocalStores
                                && following < instructions.Length
                                && instructions[following].OpCode == ExecutableOpCode.PopTop;
                            if (discardsSend && buffered is not null
                                && invocation.TryAppendBufferedYield(buffered, value))
                            {
                                scope.CheckExecution(instructions[following].Span);
                                scope.State.SetCurrentException(invocation.HasActiveHandler
                                    ? scope.State.CurrentException : invocation._entryActiveException);
                                execution.Stack.RetireTo(instructions[following].LiveExtent);
                                execution.InstructionIndex += 2;
                                continue;
                            }
                            invocation!.YieldValue = value;
                            invocation!.HasYield = true;
                            invocation!._waitingForSend = true;
                            execution.InstructionIndex++;
                            // The same prefix owns other completed yields too.
                            // A declined direct sum must reach ordinary addition
                            // without probing or changing its state twice.
                            if (!discardsSend && buffered is not null && invocation.TryBufferYield(buffered)) continue;
                            return InstructionPause.Boundary;
                        }
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
                            if (invocation!.HasYield)
                            {
                                if (invocation._yieldDestination is { } delegatedDestination
                                    && invocation.TryBufferYield(delegatedDestination)) continue;
                                return InstructionPause.Boundary;
                            }
                            break;
                        case ExecutableOpCode.Import:
                            if (invocation is null) return InstructionPause.ContextRequired;
                            var import = execution.Code.Imports[instruction.ImportIndex];
                            if (!asynchronous) ExecuteExecutableImport(execution.Code, import, execution.Locals, invocation);
                            else
                            {
                                pending = WithoutJump(ExecuteImportAsync(import, invocation));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.LoadMember or ExecutableOpCode.LoadCallMember:
                            if (TryLoadDirectCallReceiver(ref execution, scope, invocation, instruction)) break;
                            if (invocation is null) return InstructionPause.ContextRequired;
                            if (!asynchronous) ExecuteSynchronousMemberLoad(ref execution, scope, invocation, instruction);
                            else
                            {
                                pending = invocation.ExecuteMemberLoadAsync(instruction);
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.LoadConst:
                            PublishInstructionResult(ref execution, invocation, instruction,
                                RuntimeValue(execution.Code.Constants[instruction.ConstantIndex]));
                            break;
                        case ExecutableOpCode.LoadGlobal:
                            PublishInstructionResult(ref execution, invocation, instruction,
                                ResolveExecutableGlobal(execution.Code, instruction.NameIndex, instruction.Span, scope));
                            break;
                        case ExecutableOpCode.LoadName:
                            if (invocation is null) return InstructionPause.ContextRequired;
                            PublishInstructionResult(ref execution, invocation, instruction,
                                ResolveExecutableName(execution.Code.Names[instruction.NameIndex], instruction.Span, invocation));
                            break;
                        case ExecutableOpCode.PreloadImplicitSuperArguments:
                            if (invocation is null) return InstructionPause.ContextRequired;
                            PreloadImplicitSuperArguments(invocation, instruction.Span);
                            break;
                        case ExecutableOpCode.StoreGlobal:
                            if (invocation is null) return InstructionPause.ContextRequired;
                            StoreName(execution.Code.Names[instruction.NameIndex],
                                execution.Stack.TakeValue(instruction.InputStart, instruction.LiveExtent), invocation, instruction.Span);
                            break;
                        case ExecutableOpCode.StoreName:
                            if (invocation is null) return InstructionPause.ContextRequired;
                            AssignExecutableBoundName(execution.Code, execution.Locals, execution.Code.Names[instruction.NameIndex],
                                execution.Stack.TakeValue(instruction.InputStart, instruction.LiveExtent), invocation, instruction.Span);
                            break;
                        case ExecutableOpCode.Dup:
                            execution.Stack.PublishValue(instruction.ResultSlot + 1, instruction.LiveExtent,
                                execution.Stack[instruction.InputStart]);
                            break;
                        case ExecutableOpCode.PopTop:
                            _ = execution.Stack[instruction.InputStart];
                            execution.Stack.RetireTo(instruction.LiveExtent);
                            break;
                        case ExecutableOpCode.MakeList:
                            PublishInstructionResult(ref execution, invocation, instruction,
                                CreateListFromStack(ref execution.Stack, instruction.InputStart, instruction.ItemCount, instruction.Span, scope));
                            break;
                        case ExecutableOpCode.ReverseAssignmentValues:
                            ReverseAssignmentValues(execution.Stack.Values.AsSpan(instruction.InputStart, instruction.AssignmentValueCount),
                                scope.State, instruction.Span);
                            break;
                        case ExecutableOpCode.MakeTuple:
                            if (invocation is not null && invocation.TryStageUnpackedFrameReturn(instruction))
                                return InstructionPause.Boundary;
                            if (!unpackedReturn.IsEmpty &&
                                CanForwardFixedReturn(in execution, instruction, unpackedReturn.Length))
                            {
                                CopyFixedReturn(ref execution, scope, instruction, unpackedReturn);
                                return InstructionPause.UnpackedReturn;
                            }
                            PublishInstructionResult(ref execution, invocation, instruction,
                                CreateTupleFromStack(ref execution.Stack, instruction.InputStart, instruction.ItemCount, instruction.Span, scope));
                            break;
                        case ExecutableOpCode.MakeSet or ExecutableOpCode.MakeDict or ExecutableOpCode.MakeRecord or ExecutableOpCode.MatchCase:
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
                        case ExecutableOpCode.CompareJumpIfFalse:
                            var comparisonPause = ExecuteComparisonInstruction(ref execution, scope, invocation, instruction, asynchronous,
                                out pending, out pendingSpan);
                            if (comparisonPause != InstructionPause.Continue) return comparisonPause;
                            jumped = ConsumeOperation(ref pending);
                            break;
                        case ExecutableOpCode.Binary or ExecutableOpCode.Augmented or ExecutableOpCode.Unary:
                            if (TryExecuteBuiltinArithmetic(ref execution, scope, invocation, instruction)) break;
                            goto case ExecutableOpCode.StoreSubscript;
                        case ExecutableOpCode.Subscript:
                            if (TryExecuteSequenceIndex(ref execution, scope, invocation, instruction)) break;
                            goto case ExecutableOpCode.StoreSubscript;
                        case ExecutableOpCode.StoreSubscript:
                        case ExecutableOpCode.StoreSubscriptLast:
                            var pause = ExecuteInputInstruction(ref execution, scope, invocation, instruction, asynchronous,
                                out pending, out pendingSpan, out nextCall);
                            if (pause != InstructionPause.Continue) return pause;
                            break;
                        case ExecutableOpCode.ForNextDictionaryAccumulation:
                            if (TryExecuteDictionaryLoop(ref execution, scope, invocation, instruction))
                            {
                                jumped = true;
                                break;
                            }
                            goto case ExecutableOpCode.ForNext;
                        case ExecutableOpCode.ForNextUnpack:
                            if (invocation is null || execution.Stack.LiveExtent != instruction.ResultSlot ||
                                execution.Stack.Values[instruction.InputStart].Reference is not
                                    PyIteration.Cursor { NativeCsvSource: { CanUnpackDirectly: true } csv } cursor)
                                goto case ExecutableOpCode.ForNext;
                            pending = invocation.AdvanceCsvUnpack(csv, cursor, instruction, asynchronous);
                            if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                            jumped = ConsumeOperation(ref pending);
                            break;
                        case ExecutableOpCode.ForNext:
                            if (execution.Stack.TryAdvanceNativeIteration(instruction.InputStart, out var hasNativeNext, out var nativeNext))
                            {
                                if (hasNativeNext) PublishInstructionResult(ref execution, invocation, instruction, nativeNext);
                                else
                                {
                                    execution.Stack.RetireTo(instruction.InputStart);
                                    execution.InstructionIndex = instruction.TargetIndex;
                                    jumped = true;
                                }
                            }
                            else if (invocation is null) return InstructionPause.ContextRequired;
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
                            var callStart = instruction.ResultSlot;
                            if (instruction.InputStart < 0 && execution.Stack.LiveExtent == callStart)
                            {
                                var callee = ReadOperand(ref execution, instruction.InputStart, scope, 0);
                                var source = instruction.SecondInput;
                                if (source >= 0 && execution.Stack.Values[source].TryGetShortUtf8(out var packed)
                                    && TryInvokeShortUtf8Float(packed, callee, call.CallSpan, scope, out var converted))
                                {
                                    PublishInstructionResult(ref execution, invocation, instruction, converted);
                                    break;
                                }
                                var argument = ReadOperand(ref execution, source, scope, 1);
                                if (callee is BuiltinCallable builtin &&
                                    TryInvokeUnaryBuiltin(builtin, argument, call.CallSpan, scope,
                                        allowProtocol: invocation is not null, out var value))
                                {
                                    if (value.Reference is LythonRuntimeException error)
                                    {
                                        returnedFailure = error;
                                        goto RouteReturnedFailure;
                                    }
                                    PublishInstructionResult(ref execution, invocation, instruction, value);
                                    break;
                                }
                                // A promoted frame resumes this same call with the
                                // evaluated pair. It never repeats its name reads.
                                execution.Stack.PublishValue(callStart, callStart + 1, callee);
                                execution.Stack.PublishValue(callStart + 1, callStart + 2, argument);
                            }
                            var target = execution.Stack[callStart + call.TargetOffset];
                            if (TryExecuteNativeCall(ref execution, scope, invocation, call, callStart, target, out var continuation))
                            {
                                if (continuation is null) break;
                                if (continuation is LythonRuntimeException failure)
                                {
                                    returnedFailure = failure;
                                    goto RouteReturnedFailure;
                                }
                                nextCall = new((ExecutionContext)continuation);
                                pendingSpan = call.CallSpan;
                                return InstructionPause.Call;
                            }
                            if (invocation is null)
                            {
                                if (!TryExecuteDeferredChild(ref execution, scope, call, callStart, target, out var child))
                                    return InstructionPause.ContextRequired;
                                if (child is null) break;
                                nextCall = new(child);
                                pendingSpan = call.CallSpan;
                                return InstructionPause.Call;
                            }
                            if (invocation!.TryBeginExecutableCall(call, callStart, target, asynchronous, out nextCall))
                            {
                                if (!nextCall.IsCompletedSuccessfully || !ReferenceEquals(nextCall.Result, invocation))
                                {
                                    pendingSpan = call.CallSpan;
                                    return InstructionPause.Call;
                                }
                                nextCall = default;
                                // Completed calls already published after cleanup.
                                jumped = true;
                                break;
                            }
                            if (!asynchronous) invocation!.ExecuteOrdinaryCall(call, callStart, target, false).GetAwaiter().GetResult();
                            else
                            {
                                pending = WithoutJump(invocation!.ExecuteOrdinaryCall(call, callStart, target, true));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.GetIter:
                            if (execution.Stack.TryPrepareNativeIteration(
                                ReadOperand(ref execution, instruction.InputStart, scope, 0),
                                instruction.ResultSlot, instruction.LiveExtent, instruction.Span)) break;
                            if (invocation is null) return InstructionPause.ContextRequired;
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
                                pending = WithoutJump(invocation!.ExecuteSliceValueOperationAsync(instruction));
                                if (!pending.IsCompleted) return PauseOperation(instruction.Span, out pendingSpan);
                                _ = ConsumeOperation(ref pending);
                            }
                            break;
                        case ExecutableOpCode.MakeSlice:
                            if (invocation is null) return InstructionPause.ContextRequired;
                            invocation!.ExecuteValueOperation(instruction);
                            break;
                        case ExecutableOpCode.JumpIfFalse:
                            var condition = ReadOperand(ref execution, instruction.InputStart, scope, 0);
                            if (invocation is null && !CanUseNativeTruth(condition))
                                return InstructionPause.ContextRequired;
                            if (!asynchronous || condition is not PyInstance)
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
                            if (!execution.ReturnValue.IsUnassigned)
                            {
                                // Keep the returning instruction/span current and
                                // finish queued native disposal before leaving.
                                if (asynchronous && execution.Stack.HasPendingDisposals)
                                {
                                    pending = new(jumped);
                                    return InstructionPause.Disposal;
                                }
                                return InstructionPause.Boundary;
                            }
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
                catch (LythonRuntimeException error)
                {
                    if (!RouteInstructionFailure(error, scope, invocation, instruction.Span)) throw;
                    jumped = true;
                }
            FinishInstruction:
                if (asynchronous && execution.Stack.HasPendingDisposals)
                {
                    pending = new(jumped);
                    return InstructionPause.Disposal;
                }
                if (!jumped) execution.InstructionIndex++;
                continue;

            // Route outside the CLR catch so an unhandled returned failure is
            // propagated once with the current instruction still selected.
            RouteReturnedFailure:
                if (!RouteInstructionFailure(returnedFailure, scope, invocation, instruction.Span)) throw returnedFailure;
                jumped = true;
                goto FinishInstruction;
            }
            return InstructionPause.Boundary;
        }

        private static bool RouteInstructionFailure(LythonRuntimeException error, ExecutionContext scope,
            ExecutionContext? invocation, LythonSourceSpan span)
        {
            if (invocation is not null) return invocation.TryRouteRuntimeException(error, span);
            var previous = scope.State.CurrentException;
            if (previous is not null && !ReferenceEquals(error.OriginalPythonException, previous)) error.PythonContext ??= previous;
            return false;
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
