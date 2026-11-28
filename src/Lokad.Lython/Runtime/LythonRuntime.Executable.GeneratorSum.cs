using System.Numerics;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private ValueTask ExecutePreparedGenerator(ExecutableCreateGeneratorExpression expression,
            in ExecutableInstruction instruction, bool asynchronous, ref LythonSourceSpan operationSpan)
        {
            var targetSlot = instruction.PrivateGeneratorCallTarget;
            var privateFrame = targetSlot != int.MaxValue &&
                _execution.Stack.Values[targetSlot].Reference is { } target && IsSumBuiltin(target);
            var lookupOwner = privateFrame && expression.SharedLookupStart >= 0 &&
                _execution.Stack.LookupOwner is { } definition && ReferenceEquals(definition.CodeObject, _execution.Code)
                ? definition : null;
            var outer = _execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
            var code = expression.CodeObject;
            if (!privateFrame || outer is not (PyList or PyTuple or PyRange) || IsClassBody ||
                code.RequiresLocalVariableMirroring ||
                code.ScopeFacts.RequiresClassCell || code.ScopeFacts.NonlocalNames.Count != 0 ||
                code.CapturedLocalSlots.Count != 0 || code.ExceptionRegions.Count != 0)
                return CreatePreparedGeneratorAsync(expression, instruction, outer, lookupOwner, privateFrame, asynchronous);

            ref readonly var call = ref _execution.Code.Instructions[_execution.InstructionIndex + 1];
            var promotionBytes = checked(PyGenerator.EstimateExpressionShell(code) + PyIteratorBase.IteratorValueBytes);
            ExecutableWorkState work = default;
            var acquired = PreparePrivateGenerator(ref work, expression, outer, lookupOwner, instruction.Span, promotionBytes);
            var reduction = new PrivateGeneratorReduction(acquired);
            _execution.InstructionIndex++;
            operationSpan = call.Span;
            return ExecutePreparedGeneratorSum(ref work, ref reduction, instruction.Span, operationSpan,
                call.ResultSlot, promotionBytes, asynchronous);
        }

        private object PreparePrivateGenerator(ref ExecutableWorkState work, ExecutableCreateGeneratorExpression expression,
            object outer, PyFunctionBase? lookupOwner, LythonSourceSpan span, long promotionBytes)
        {
            // The layout proves the adjacent sole consumer. Fund and acquire
            // the generator at its expression before entering the sum call.
            var code = expression.CodeObject;
            var totalBytes = checked(promotionBytes +
                ExecutableSlotsBytes(code.ValueStorageCapacity) + ExecutableFrameExtraBytes(code, 0));
            var governor = MemoryGovernor;
            governor.Reserve(totalBytes, span);
            governor.Commit(totalBytes);
            try
            {
                var captures = CaptureExecutableClosures(code, this, span);
                InitializeExecutableWorkState(ref work, code, this, default, captures, lookupOwner,
                    borrowedInvocation: true, sharedLookupStart: lookupOwner is null ? -1 : expression.SharedLookupStart,
                    storagePrepaid: true);
                var acquired = outer is PyList or PyTuple ? outer : PyEnumerableIterator.Create(outer, expression.OuterSpan, this);
                ChargeClosureRetention(FunctionClosureContext, governor, span);
                if (acquired is PyEnumerableIterator iterator) SeedGeneratorOuter(ref work, iterator);
                else
                {
                    var slot = code.FirstTemporarySlot;
                    System.Diagnostics.Debug.Assert(code.Instructions[code.EntryIndex].Span == expression.OuterSpan);
                    work.Stack.PublishSlot(slot, slot + 1, ExecutableSlot.PrepareNativeLoop(acquired));
                    SkipGeneratorIteratorEntry(ref work, slot);
                }

                return acquired;
            }
            catch
            {
                if (work.Code is null) governor.Release(totalBytes);
                else ReleaseBorrowedGeneratorWork(ref work, promotionBytes);
                throw;
            }
        }

        private async ValueTask CreatePreparedGeneratorAsync(ExecutableCreateGeneratorExpression expression,
            ExecutableInstruction instruction, object outer, PyFunctionBase? lookupOwner, bool privateFrame, bool asynchronous)
        {
            var generator = await PyGenerator.CreateExpressionAsync(expression.CodeObject, this,
                outer, expression.OuterSpan, instruction.Span, asynchronous, privateFrame, lookupOwner,
                lookupOwner is null ? -1 : expression.SharedLookupStart).ConfigureAwait(false);
            try { PublishInstructionResult(instruction, generator); }
            catch
            {
                if (generator is ExecutionContext frame) PyGenerator.CompleteFrame(frame);
                throw;
            }
        }

        private ValueTask ExecutePreparedGeneratorSum(ref ExecutableWorkState work, ref PrivateGeneratorReduction reduction,
            LythonSourceSpan generatorSpan, LythonSourceSpan callSpan, int start, long promotionBytes, bool asynchronous)
        {
            var entered = false;
            try
            {
                ValueTask<object> pending;
                try
                {
                    EnterInterpreterFrame(callSpan);
                    entered = true;
                    var frame = RunPrivateGeneratorPrefix(ref work, ref reduction, generatorSpan);
                    var subtotal = new BigInteger(reduction.Subtotal);
                    pending = frame is null ? new(subtotal) : ReducePromoted(frame, subtotal, generatorSpan, callSpan, asynchronous);
                    if (!pending.IsCompleted)
                    {
                        pending = AwaitBorrowedCall(pending, callSpan, this);
                        entered = false;
                    }
                    else pending = new(RuntimeValue(pending.GetAwaiter().GetResult()));
                }
                catch (Exception error) when (TryTranslateCallableException(error, callSpan, out var translated)) { throw translated; }
                finally
                {
                    if (entered) LeaveInterpreterFrame();
                    reduction = default;
                    if (work.Code is not null) ReleaseBorrowedGeneratorWork(ref work, promotionBytes);
                }
                return CompleteCallValue(pending, start);
            }
            catch
            {
                _execution.Stack.RetireTo(start);
                throw;
            }
        }

        private async ValueTask<object> ReducePromoted(ExecutionContext frame, BigInteger subtotal,
            LythonSourceSpan generatorSpan, LythonSourceSpan callSpan, bool asynchronous)
        {
            try
            {
                var sum = new SumState(subtotal);
                return asynchronous
                    ? await ReduceSumAsync(sum, frame, null, generatorSpan, callSpan, this).ConfigureAwait(false)
                    : ReduceSum(sum, frame, null, generatorSpan, callSpan, this);
            }
            finally { PyGenerator.CompleteFrame(frame); }
        }

        private ExecutionContext? RunPrivateGeneratorPrefix(ref ExecutableWorkState work,
            ref PrivateGeneratorReduction reduction, LythonSourceSpan span)
        {
            var entryException = State.CurrentException;
            var enteredFunction = false;
            var enteredInterpreter = false;
            try
            {
                EnterFunctionCall(span);
                enteredFunction = true;
                EnterInterpreterFrame(span);
                enteredInterpreter = true;
                // Keep the iterator's post-entry admission before the first pull.
                if (reduction.Outer is PyList or PyTuple)
                    State.CheckInterpreterStack(work.Code.Instructions[work.Code.EntryIndex].Span);
                var pause = ExecuteInstructionPrefix(ref work, this, null, true, out _, out _, out _, reduction: new(ref reduction));
                if (pause == InstructionPause.Boundary) return null;
                if (pause != InstructionPause.ContextRequired)
                    throw new InvalidOperationException("A borrowed generator cannot suspend.");

                var frame = new ExecutionContext(FunctionClosureContext, work.Code.ScopeFacts)
                { FunctionName = "<genexpr>", FirstPositionalParameterName = ".0" };
                if (reduction.Outer is PyList or PyTuple) MaterializeSequenceOuter(ref work, reduction.Outer);
                frame.AdoptExecutableExecution(ref work, entryException);
                return frame;
            }
            catch (LythonRuntimeException exception)
            {
                PyFunctionBinding.AnnotateException(exception, this, "<genexpr>", span);
                if (exception.Identity == PythonExceptionIdentity.Builtin("StopIteration"))
                    throw GeneratorRaisedStopIteration(exception, this, span);
                throw;
            }
            finally
            {
                if (enteredInterpreter) LeaveInterpreterFrame();
                if (enteredFunction) LeaveFunctionCall();
                State.SetCurrentException(entryException);
            }
        }

        private void MaterializeSequenceOuter(ref ExecutableWorkState work, object sequence)
        {
            var code = work.Code;
            var slot = code.FirstTemporarySlot;
            var exhausted = work.Stack.LiveExtent <= slot;
            var position = exhausted ? 0 : work.Stack.Values[slot].NativeSequencePosition;
            var iterator = PyEnumerableIterator.CreateSequenceAt(sequence, position, exhausted,
                code.Instructions[code.EntryIndex].Span, this);
            WriteLocalValue(work.Locals, code.LocalNameToSlot[".0"], iterator);
            if (!exhausted) work.Stack[slot] = iterator;
        }

        private static bool TryReduceRecordComparison(ref ExecutableWorkState work, ExecutionContext scope,
            in ExecutableInstruction read, object fieldValue, ref PrivateGeneratorReduction reduction)
        {
            var index = work.InstructionIndex + 1;
            var code = work.Code;
            var instructions = code.Instructions;
            if (read.HasResultConsumer || index + 1 >= instructions.Length) return false;
            ref readonly var compare = ref instructions[index];
            ref readonly var yielded = ref instructions[index + 1];
            var slot = read.ResultSlot;
            if (compare.OpCode != ExecutableOpCode.Binary || compare.HasResultConsumer ||
                compare.InputStart != slot || compare.ResultSlot != slot ||
                (uint)~compare.SecondInput >= (uint)code.Constants.Length ||
                !IsScalarComparison(compare.BinaryOperator) ||
                yielded.OpCode != ExecutableOpCode.Yield || !yielded.DiscardsYieldResult ||
                yielded.InputStart != slot || yielded.LiveExtent != slot ||
                !work.Stack.CanRetireWithoutCleanup(slot))
                return false;

            // The literal and fetched field are the original operands.
            // Declining a user comparison leaves the field for ordinary
            // publication, so promotion resumes without another record read.
            scope.State.CheckExecution(compare.Span);
            if (!TryEvaluateScalarComparison(compare.BinaryOperator, fieldValue, RuntimeValue(code.Constants[~compare.SecondInput]),
                scope, compare.Span, out var truth))
                return false;

            scope.State.CheckExecution(yielded.Span);
            if (!TryAccumulateSumInteger(ref reduction.Subtotal, truth ? 1 : 0))
            {
                // Widen at the existing yield with the completed Python bool.
                work.InstructionIndex = index;
                PublishInstructionResult(ref work, null, compare, RuntimeValue(truth));
                return true;
            }
            work.Stack.RetireTo(yielded.LiveExtent);
            work.InstructionIndex = index + 1;
            return true;
        }

        private void ReleaseBorrowedGeneratorWork(ref ExecutableWorkState work, long promotionBytes)
        {
            var code = work.Code;
            try { work.Stack.UnwindTo(code.FirstTemporarySlot); }
            finally
            {
                try { work.Stack.ReleaseStorage(code.LookupCacheCount); }
                finally
                {
                    work = default;
                    MemoryGovernor.Release(promotionBytes);
                }
            }
        }

        // The synchronous dispatcher mutates this stack local. Only a promoted
        // frame and the subtotal cross a real suspension.
        private struct PrivateGeneratorReduction(object outer)
        {
            internal readonly object Outer = outer;
            internal long Subtotal;

            internal bool TryAdvanceOuter(ref ExecutableWorkState state, in ExecutableInstruction instruction,
                out bool hasNext, out ExecutableSlot value)
            {
                hasNext = false;
                value = default;
                if (Outer is not PyEnumerableIterator iterator || !ReferenceEquals(state.Stack.Values[instruction.InputStart].Reference, iterator))
                    return false;
                state.Stack.State.CheckInterpreterStack(instruction.Span);
                var next = iterator.TryMoveNext();
                hasNext = next.HasValue;
                if (hasNext) value = next.TakeSlotValue();
                return true;
            }

            internal bool TryAddBorrowedInteger(in ExecutableSlot value)
                => (value.TryGetInteger(out var integer) || SumState.TryInt64(value.Object.RequireNotNull(), out integer))
                    && TryAccumulateSumInteger(ref Subtotal, integer);
        }
    }
}
