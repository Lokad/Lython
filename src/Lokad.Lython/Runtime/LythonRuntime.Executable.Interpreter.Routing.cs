namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ExecutableFrameInterpreter
    {
        // Synchronous and asynchronous dispatch must unwind the same frame state.
        private bool TryRouteReturn(ReturnSignal signal, LythonSourceSpan span)
        {
            if (TryHandleAbrupt(codeObject, context, ref _stack, _blockEntryStackDepths, _currentBlockIndex,
                new PendingReturn(signal.Value), span, ref _pendingAbrupt, ref _currentBlockIndex, out _))
                return true;

            AbandonFrame(span);
            _frameReturnValue = signal.Value;
            _hasFrameReturn = true;
            return false;
        }

        private bool TryRouteControl(ControlSignal signal, LythonSourceSpan span)
        {
            if (TryHandleAbrupt(codeObject, context, ref _stack, _blockEntryStackDepths, _currentBlockIndex,
                new PendingControl(signal), span, ref _pendingAbrupt, ref _currentBlockIndex, out _))
                return true;

            AbandonFrame(span);
            return false;
        }

        private void RouteRuntimeException(LythonRuntimeException ex, LythonSourceSpan span)
        {
            var previousActive = context.Services.CurrentException;
            if (previousActive is not null && !ReferenceEquals(ex.OriginalPythonException, previousActive))
                ex.PythonContext ??= previousActive;
            var previousPending = _pendingAbrupt;
            _delegation = null;
            _injectedException = null;
            if (!TryHandleAbrupt(codeObject, context, ref _stack, _blockEntryStackDepths, _currentBlockIndex,
                new PendingException(ex), span, ref _pendingAbrupt, ref _currentBlockIndex, out var matchedRegion))
            {
                var failure = _pendingAbrupt is PendingException unhandled ? unhandled.Exception : ex;
                AbandonFrame(span);
                throw failure;
            }

            var routedToHandler = _pendingAbrupt is null;
            var routedToCleanup = !routedToHandler && _pendingAbrupt is PendingException;
            var routedException = _pendingAbrupt is PendingException pending ? pending.Exception : ex;
            if (routedToHandler || routedToCleanup)
            {
                // Hold the installed handler aside while abandoned suites unwind;
                // restore the displaced nesting before installing the routed error.
                var installed = routedToHandler ? context.Services.CurrentException : null;
                context.Services.SetCurrentException(previousActive);
                UnwindAbandonedHandlers(_currentBlockIndex);
                var retainedPending = PendingCleanupContains(previousPending, _currentBlockIndex)
                    ? previousPending : null;
                SavedActiveExceptions().Push(new ActiveExceptionSave(
                    context.Services.CurrentException,
                    matchedRegion?.SuiteStartBlockIndex,
                    matchedRegion?.SuiteEndBlockIndex,
                    retainedPending));
                context.Services.SetCurrentException(
                    routedToHandler ? installed : CreatePythonExceptionInstance(routedException));
            }
        }
    }
}
