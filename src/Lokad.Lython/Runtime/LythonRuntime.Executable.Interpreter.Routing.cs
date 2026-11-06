namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        // Resume/child completion are the only producers of pending exceptions.
        // Settle operand cleanup first, then route once before executing again.
        // Active delegation forwards its injected exception at YieldFrom instead.
        private void RoutePendingFrameException()
        {
            if (_injectedException is not { } injected || _delegation is not null
                || _execution.InstructionIndex >= _execution.Code.Instructions.Length) return;
            var span = _execution.Code.Instructions[_execution.InstructionIndex].Span;
            CheckExecution(span);
            _injectedException = null;
            if (!TryRouteRuntimeException(injected, span)) throw injected;
        }

        // Synchronous and asynchronous dispatch must unwind the same frame state.
        private bool TryRouteReturn(ReturnSignal signal, LythonSourceSpan span)
        {
            if (TryHandleAbrupt(_execution.Code, this, ref _execution.Stack, _execution.InstructionIndex,
                new PendingReturn(signal.Value), span, ref _pendingAbrupt, ref _execution.InstructionIndex, out _))
                return true;

            _execution.ReturnValue = signal.Value;
            return false;
        }

        private bool TryRouteControl(ControlSignal signal, LythonSourceSpan span)
        {
            if (TryHandleAbrupt(_execution.Code, this, ref _execution.Stack, _execution.InstructionIndex,
                new PendingControl(signal), span, ref _pendingAbrupt, ref _execution.InstructionIndex, out _))
                return true;

            return false;
        }

        private bool TryRouteRuntimeException(LythonRuntimeException ex, LythonSourceSpan span)
        {
            var previousActive = State.CurrentException;
            if (previousActive is not null && !ReferenceEquals(ex.OriginalPythonException, previousActive))
                ex.PythonContext ??= previousActive;
            var previousPending = _pendingAbrupt;
            _delegation = null;
            _injectedException = null;
            // Most functions have no handlers. Avoid the large abrupt-routing
            // path while unwinding a stack-admission failure, and let the catch
            // rethrow the original CLR exception instead of throwing it anew.
            if (_execution.Code.ExceptionRegions.Count == 0)
            {
                return false;
            }
            if (!TryHandleAbrupt(_execution.Code, this, ref _execution.Stack, _execution.InstructionIndex,
                new PendingException(ex), span, ref _pendingAbrupt, ref _execution.InstructionIndex, out var matchedRegion))
            {
                var failure = _pendingAbrupt is PendingException unhandled ? unhandled.Exception : ex;
                if (!ReferenceEquals(failure, ex)) throw failure;
                return false;
            }

            var routedToHandler = _pendingAbrupt is null;
            var routedToCleanup = !routedToHandler && _pendingAbrupt is PendingException;
            var routedException = _pendingAbrupt is PendingException pending ? pending.Exception : ex;
            if (routedToHandler || routedToCleanup)
            {
                // Hold the installed handler aside while abandoned suites unwind;
                // restore the displaced nesting before installing the routed error.
                var installed = routedToHandler ? State.CurrentException : null;
                State.SetCurrentException(previousActive);
                UnwindAbandonedHandlers(_execution.InstructionIndex);
                var retainedPending = PendingCleanupContains(previousPending, _execution.InstructionIndex)
                    ? previousPending : null;
                SavedActiveExceptions().Push(new ActiveExceptionSave(
                    State.CurrentException,
                    matchedRegion?.SuiteStartIndex,
                    matchedRegion?.SuiteEndIndex,
                    retainedPending));
                State.SetCurrentException(
                    routedToHandler ? installed : CreatePythonExceptionInstance(routedException));
            }
            return true;
        }
    }
}
