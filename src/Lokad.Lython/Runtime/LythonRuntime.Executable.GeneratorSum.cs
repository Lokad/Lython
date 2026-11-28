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

            var shellBytes = PyGenerator.EstimateExpressionShell(code);
            var totalBytes = checked(BorrowedGeneratorSum.StorageBytes + shellBytes +
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
                var iterator = (PyEnumerableIterator)PyIteration.AcquireGeneratorOuter(outer, expression.OuterSpan, this);
                ChargeClosureRetention(FunctionClosureContext, governor, span);
                SeedGeneratorOuter(ref work, iterator);
                return new BorrowedGeneratorSum(work, iterator, governor, shellBytes);
            }
            catch
            {
                if (work.Code is null) governor.Release(totalBytes);
                else
                {
                    try { ReleaseBorrowedGeneratorWork(ref work); }
                    finally { governor.Release(BorrowedGeneratorSum.StorageBytes + shellBytes); }
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

        private static bool TryAdvanceReductionOuter(ref ExecutableWorkState work, ExecutionContext scope,
            in ExecutableInstruction instruction, PyEnumerableIterator outer, out bool hasNext, out ExecutableSlot value)
        {
            if (!ReferenceEquals(work.Stack.Values[instruction.InputStart].Reference, outer))
            {
                hasNext = false;
                value = default;
                return false;
            }
            scope.State.CheckInterpreterStack(instruction.Span);
            var next = outer.TryMoveNext();
            hasNext = next.HasValue;
            value = hasNext ? next.TakeSlotValue() : default;
            return true;
        }

        // The call operand owns initialized slots before and during the call.
        // Promotion transfers the slots and their prepaid shell charge.
        internal sealed class BorrowedGeneratorSum(ExecutableWorkState work, PyEnumerableIterator outer,
            MemoryGovernor governor, long shellBytes) : ExecutableTemporaryValue
        {
            internal const long StorageBytes = 192;
            private ExecutableWorkState _work = work;
            private MemoryGovernor? _governor = governor;

            internal async ValueTask<object> Reduce(ExecutionContext scope, LythonSourceSpan span, bool asynchronous)
            {
                var generatorSpan = scope.PrivateGeneratorSpan;
                scope.MemoryGovernor.Reserve(64, span);
                ExecutionContext? frame = null;
                try
                {
                    var sum = new SumState(BigInteger.Zero);
                    frame = RunPrefix(scope, sum, generatorSpan);
                    if (frame is null) return sum.Result;
                    return asynchronous
                        ? await ReduceSumAsync(sum, frame, null, generatorSpan, span, scope).ConfigureAwait(false)
                        : ReduceSum(sum, frame, null, generatorSpan, span, scope);
                }
                finally
                {
                    try { PyGenerator.CompleteFrame(frame); }
                    finally { scope.MemoryGovernor.ReleaseReserved(64); }
                }
            }

            private ExecutionContext? RunPrefix(ExecutionContext scope, SumState sum, LythonSourceSpan span)
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
                    var pause = ExecuteInstructionPrefix(ref _work, scope, null, true, out _, out _, out _,
                        reduction: sum, reductionOuter: outer);
                    if (pause == InstructionPause.Boundary) return null;
                    if (pause != InstructionPause.ContextRequired)
                        throw new InvalidOperationException("A borrowed generator cannot suspend.");

                    var frame = new ExecutionContext(scope.FunctionClosureContext, _work.Code.ScopeFacts)
                    { FunctionName = "<genexpr>", FirstPositionalParameterName = ".0" };
                    frame.AdoptExecutableExecution(ref _work, entryException);
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
                var release = StorageBytes;
                try
                {
                    if (_work.Code is null) return;
                    release += shellBytes;
                    ReleaseBorrowedGeneratorWork(ref _work);
                }
                finally { owner.Release(release); }
            }
        }
    }
}
