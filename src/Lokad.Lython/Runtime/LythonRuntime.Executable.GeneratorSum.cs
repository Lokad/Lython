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
                var iterator = PyEnumerableIterator.Create(outer, expression.OuterSpan, this);
                ChargeClosureRetention(FunctionClosureContext, governor, span);
                SeedGeneratorOuter(ref work, iterator);
                return new BorrowedGeneratorSum(work, iterator, governor, promotionBytes);
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

        // The operand owns the initialized slots, iterator, and prepaid
        // promotion storage. Once promoted, .0 may escape with the frame's
        // other values, so the iterator and frame charges outlive the operand.
        internal sealed class BorrowedGeneratorSum(ExecutableWorkState work, PyEnumerableIterator outer,
            MemoryGovernor governor, long promotionBytes) : ExecutableTemporaryValue
        {
            internal const long StorageBytes = 256;
            private ExecutableWorkState _work = work;
            private MemoryGovernor? _governor = governor;
            private PyEnumerableIterator? _outer = outer;
            private long _subtotal;

            internal bool TryAdvanceOuter(ref ExecutableWorkState state, in ExecutableInstruction instruction,
                out bool hasNext, out ExecutableSlot value)
            {
                hasNext = false;
                value = default;
                if (_outer is not { } iterator || !ReferenceEquals(state.Stack.Values[instruction.InputStart].Reference, iterator))
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
                    var pause = ExecuteInstructionPrefix(ref _work, scope, null, true, out _, out _, out _, reduction: this);
                    if (pause == InstructionPause.Boundary) return null;
                    if (pause != InstructionPause.ContextRequired)
                        throw new InvalidOperationException("A borrowed generator cannot suspend.");

                    var frame = new ExecutionContext(scope.FunctionClosureContext, _work.Code.ScopeFacts)
                    { FunctionName = "<genexpr>", FirstPositionalParameterName = ".0" };
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
