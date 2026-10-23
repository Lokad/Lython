namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ExecutableFrameInterpreter
    {
        // Synchronous and asynchronous dispatch must unwind the same frame state.
        private bool TryRouteReturn(ReturnSignal signal, LythonSourceSpan span)
        {
            if (TryHandleAbrupt(_codeObject, _context, ref _stack, _blockEntryStackDepths, _currentBlockIndex,
                new PendingReturn(signal.Value), span, ref _pendingAbrupt, ref _currentBlockIndex, out _))
                return true;

            AbandonFrame(span);
            _frameReturnValue = signal.Value;
            _hasFrameReturn = true;
            return false;
        }

        private bool TryRouteControl(ControlSignal signal, LythonSourceSpan span)
        {
            if (TryHandleAbrupt(_codeObject, _context, ref _stack, _blockEntryStackDepths, _currentBlockIndex,
                new PendingControl(signal), span, ref _pendingAbrupt, ref _currentBlockIndex, out _))
                return true;

            AbandonFrame(span);
            return false;
        }

        private bool TryRouteRuntimeException(LythonRuntimeException ex, LythonSourceSpan span)
        {
            var previousActive = _context.Services.CurrentException;
            if (previousActive is not null && !ReferenceEquals(ex.OriginalPythonException, previousActive))
                ex.PythonContext ??= previousActive;
            var previousPending = _pendingAbrupt;
            _delegation = null;
            _injectedException = null;
            // Most functions have no handlers. Avoid the large abrupt-routing
            // path while unwinding a stack-admission failure, and let the catch
            // rethrow the original CLR exception instead of throwing it anew.
            if (_codeObject.ExceptionRegions.Count == 0)
            {
                AbandonFrame(span);
                return false;
            }
            if (!TryHandleAbrupt(_codeObject, _context, ref _stack, _blockEntryStackDepths, _currentBlockIndex,
                new PendingException(ex), span, ref _pendingAbrupt, ref _currentBlockIndex, out var matchedRegion))
            {
                var failure = _pendingAbrupt is PendingException unhandled ? unhandled.Exception : ex;
                AbandonFrame(span);
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
                var installed = routedToHandler ? _context.Services.CurrentException : null;
                _context.Services.SetCurrentException(previousActive);
                UnwindAbandonedHandlers(_currentBlockIndex);
                var retainedPending = PendingCleanupContains(previousPending, _currentBlockIndex)
                    ? previousPending : null;
                SavedActiveExceptions().Push(new ActiveExceptionSave(
                    _context.Services.CurrentException,
                    matchedRegion?.SuiteStartBlockIndex,
                    matchedRegion?.SuiteEndBlockIndex,
                    retainedPending));
                _context.Services.SetCurrentException(
                    routedToHandler ? installed : CreatePythonExceptionInstance(routedException));
            }
            return true;
        }
    }
}
