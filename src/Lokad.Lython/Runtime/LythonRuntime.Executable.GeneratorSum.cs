using System.Numerics;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private BorrowedGeneratorSum? TryPrepareBorrowedGeneratorSum(ExecutableCreateGeneratorExpression expression,
            object outer, LythonSourceSpan span, PyFunctionBase? lookupOwner)
        {
            var code = expression.CodeObject;
            // This prefix can advance only an exact builtin outer iterator.
            // General acquisition still runs at expression construction.
            if (outer is not (PyList or PyTuple or PyRange) || IsClassBody ||
                code.RequiresLocalVariableMirroring ||
                code.ScopeFacts.RequiresClassCell || code.ScopeFacts.NonlocalNames.Count != 0 ||
                code.CapturedLocalSlots.Count != 0 || code.ExceptionRegions.Count != 0)
                return null;

            var promotionBytes = checked(PyGenerator.EstimateExpressionShell(code) + PyIteratorBase.IteratorValueBytes);
            var totalBytes = checked(BorrowedGeneratorSum.StorageBytes + promotionBytes +
                ExecutableSlotsBytes(code.ValueStorageCapacity) + ExecutableFrameExtraBytes(code, 0));
            var governor = MemoryGovernor;
            governor.Reserve(totalBytes, span);
            governor.Commit(totalBytes);
            ExecutableWorkState work = default;
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
                return new BorrowedGeneratorSum(work, acquired, governor, promotionBytes);
            }
            catch
            {
                if (work.Code is null) governor.Release(totalBytes);
                else
                {
                    try { ReleaseBorrowedGeneratorWork(ref work); }
                    finally { governor.Release(BorrowedGeneratorSum.StorageBytes + promotionBytes); }
                }
                throw;
            }
        }

        private static void ReleaseBorrowedGeneratorWork(ref ExecutableWorkState work)
        {
            var code = work.Code;
            try { work.Stack.UnwindTo(code.FirstTemporarySlot); }
            finally
            {
                work.Stack.ReleaseStorage(code.LookupCacheCount);
                work = default;
            }
        }

        // The operand owns the initialized slots and prepaid promotion storage.
        // An exact sequence keeps its position in the loop slot until .0 can
        // be observed. After promotion the frame and iterator charges survive.
        internal sealed class BorrowedGeneratorSum(ExecutableWorkState work, object outer,
            MemoryGovernor governor, long promotionBytes) : ExecutableTemporaryValue
        {
            internal const long StorageBytes = 256;
            private ExecutableWorkState _work = work;
            private MemoryGovernor? _governor = governor;
            private object? _outer = outer;
            private long _subtotal;

            internal bool TryAdvanceOuter(ref ExecutableWorkState state, in ExecutableInstruction instruction,
                out bool hasNext, out ExecutableSlot value)
            {
                hasNext = false;
                value = default;
                if (_outer is not PyEnumerableIterator iterator || !ReferenceEquals(state.Stack.Values[instruction.InputStart].Reference, iterator))
                    return false;
                state.Stack.State.CheckInterpreterStack(instruction.Span);
                var next = iterator.TryMoveNext();
                hasNext = next.HasValue;
                if (hasNext) value = next.TakeSlotValue();
                return true;
            }

            internal bool TryAddBorrowedInteger(in ExecutableSlot value)
                => (value.TryGetInteger(out var integer) || SumState.TryInt64(value.Object.RequireNotNull(), out integer))
                    && TryAccumulateSumInteger(ref _subtotal, integer);

            internal ValueTask<object> Reduce(ExecutionContext scope, LythonSourceSpan span, bool asynchronous)
            {
                var generatorSpan = scope.PrivateGeneratorSpan;
                var frame = RunPrefix(scope, generatorSpan);
                return frame is null
                    ? new(new BigInteger(_subtotal))
                    : ReducePromoted(frame, generatorSpan, span, scope, asynchronous);
            }

            private async ValueTask<object> ReducePromoted(ExecutionContext frame, LythonSourceSpan generatorSpan,
                LythonSourceSpan span, ExecutionContext scope, bool asynchronous)
            {
                try
                {
                    var sum = new SumState(new BigInteger(_subtotal));
                    return asynchronous
                        ? await ReduceSumAsync(sum, frame, null, generatorSpan, span, scope).ConfigureAwait(false)
                        : ReduceSum(sum, frame, null, generatorSpan, span, scope);
                }
                finally { PyGenerator.CompleteFrame(frame); }
            }

            private ExecutionContext? RunPrefix(ExecutionContext scope, LythonSourceSpan span)
            {
                var entryException = scope.State.CurrentException;
                var enteredFunction = false;
                var enteredInterpreter = false;
                try
                {
                    scope.EnterFunctionCall(span);
                    enteredFunction = true;
                    scope.EnterInterpreterFrame(span);
                    enteredInterpreter = true;
                    // Inline advancement stays inside the entered frame. Keep
                    // the iterator's post-entry admission before the first pull.
                    if (_outer is PyList or PyTuple)
                        scope.State.CheckInterpreterStack(_work.Code.Instructions[_work.Code.EntryIndex].Span);
                    var pause = ExecuteInstructionPrefix(ref _work, scope, null, true, out _, out _, out _, reduction: this);
                    if (pause == InstructionPause.Boundary) return null;
                    if (pause != InstructionPause.ContextRequired)
                        throw new InvalidOperationException("A borrowed generator cannot suspend.");

                    var frame = new ExecutionContext(scope.FunctionClosureContext, _work.Code.ScopeFacts)
                    { FunctionName = "<genexpr>", FirstPositionalParameterName = ".0" };
                    if (_outer is PyList or PyTuple) MaterializeSequenceOuter(_outer, scope);
                    frame.AdoptExecutableExecution(ref _work, entryException);
                    _outer = null;
                    return frame;
                }
                catch (LythonRuntimeException exception)
                {
                    PyFunctionBinding.AnnotateException(exception, scope, "<genexpr>", span);
                    if (exception.Identity == PythonExceptionIdentity.Builtin("StopIteration"))
                        throw GeneratorRaisedStopIteration(exception, scope, span);
                    throw;
                }
                finally
                {
                    if (enteredInterpreter) scope.LeaveInterpreterFrame();
                    if (enteredFunction) scope.LeaveFunctionCall();
                    scope.State.SetCurrentException(entryException);
                }
            }

            private void MaterializeSequenceOuter(object sequence, ExecutionContext scope)
            {
                var code = _work.Code;
                var slot = code.FirstTemporarySlot;
                var exhausted = _work.Stack.LiveExtent <= slot;
                var position = exhausted ? 0 : _work.Stack.Values[slot].NativeSequencePosition;
                var iterator = PyEnumerableIterator.CreateSequenceAt(sequence, position, exhausted,
                    code.Instructions[code.EntryIndex].Span, scope);
                WriteLocalValue(_work.Locals, code.LocalNameToSlot[".0"], iterator);
                if (!exhausted) _work.Stack[slot] = iterator;
            }

            public override void Dispose()
            {
                if (_governor is not { } owner) return;
                _governor = null;
                _outer = null;
                var release = StorageBytes;
                try
                {
                    if (_work.Code is null) return;
                    release += promotionBytes;
                    ReleaseBorrowedGeneratorWork(ref _work);
                }
                finally { owner.Release(release); }
            }
        }
    }
}
