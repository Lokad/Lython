using System.Runtime.ExceptionServices;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ExecutableFrameInterpreter
    {
        // Only the active child links to its caller. Caller operands remain owned
        // until the child and its cleanup finish; locals reuse admitted bound storage.
        private ExecutableFrameInterpreter? _caller;
        private MemoryGovernor? _boundStorageOwner;
        private long _boundStorageBytes;
        private ExecutableCallSite? _pendingCallSite;
        private PyExecutableFunction? _pendingFunction;
        private object? _pendingReceiver;
        private int _pendingCallStart;
        private object? _completedCallValue;
        private Exception? _completedCallError;

        public void Execute()
        {
            ExecutableFrameInterpreter? current = this;
            while (current is not null)
            {
                Exception? failure = null;
                try { current.ExecuteUntilCall(); }
                catch (Exception error) { failure = error; }
                current = AdvanceCallFrames(current, failure);
            }
        }

        public async ValueTask ExecuteAsync()
        {
            ExecutableFrameInterpreter? current = this;
            while (current is not null)
            {
                Exception? failure = null;
                try { await current.ExecuteUntilCallAsync().ConfigureAwait(false); }
                catch (Exception error) { failure = error; }
                current = AdvanceCallFrames(current, failure);
            }
        }

        private bool TrySuspendForExecutableCall(ExecutableInstruction instruction)
        {
            var site = _codeObject.CallSites[instruction.CallSiteIndex];
            if (!site.IsPositionalOnly) return false;
            var start = ReadExecutableCallTarget(site, ref _stack, out var target);
            var function = target as PyExecutableFunction;
            object? receiver = null;
            if (target is PyBoundMethod { Function: PyExecutableFunction method } bound)
            {
                function = method;
                receiver = bound.Receiver;
            }
            if (function is not { SupportsDirectPositional: true }) return false;
            _pendingCallSite = site;
            _pendingFunction = function;
            _pendingReceiver = receiver;
            _pendingCallStart = start;
            return true;
        }

        private static ExecutableFrameInterpreter? AdvanceCallFrames(ExecutableFrameInterpreter current, Exception? failure)
        {
            if (failure is null && current._pendingFunction is not null)
            {
                try { return current.BeginPendingCall(); }
                catch (Exception error) { current.CompletePendingCall(null, error); return current; }
            }
            var caller = current._caller;
            if (caller is null)
            {
                // Propagate outside a catch, including native callback entries.
                if (failure is not null) ExceptionDispatchInfo.Throw(failure);
                return null;
            }

            var result = current.HasFrameReturn ? current.FrameReturnValue : PyNone.Instance;
            try { current.CloseInvocation(failure); }
            catch (Exception error) { failure = error; }
            current._caller = null;
            caller.CompletePendingCall(result, failure);
            return caller;
        }

        private ExecutableFrameInterpreter BeginPendingCall()
        {
            var function = _pendingFunction!;
            var site = _pendingCallSite!;
            BoundCallArguments bound = default;
            ExecutionContext? frame = null;
            var callEntered = false;
            var bodyEntered = false;
            var transferred = false;
            try
            {
                _context.EnterInterpreterFrame(site.CallSpan);
                callEntered = true;
                _context.CheckExecution(site.CallSpan);
                bound = function.BindPositionalOperands(
                    _stack.Slice(_pendingCallStart + site.ArgumentOffset, site.ArgumentCount).Span,
                    site.CallSpan, _context, _pendingReceiver);
                frame = function.EnterInvocationFrame(bound, site.CallSpan);
                if (TryExecuteSimpleReturn(function.CodeObject, frame, bound, site.CallSpan, out var value))
                {
                    CompletePendingCall(value, null);
                    return this;
                }
                frame.EnterInterpreterFrame(site.CallSpan);
                bodyEntered = true;
                var child = CreateExecutableInterpreter(function.CodeObject, frame, bound,
                    function.ClosureCells, argumentsAreLocals: true);
                frame.EnterExecutableSlots(child);
                child._caller = this;
                child._boundStorageOwner = bound.StorageOwner;
                child._boundStorageBytes = bound.StorageBytes;
                transferred = true;
                return child;
            }
            catch (Exception error)
            {
                if (TryTranslateCallableException(error, site.CallSpan, out var translated)) error = translated;
                if (frame is not null && error is LythonRuntimeException runtimeError)
                    PyFunctionBinding.AnnotateException(runtimeError, frame, function.Name, site.CallSpan);
                CompletePendingCall(null, error);
                return this;
            }
            finally
            {
                if (!transferred)
                {
                    if (bodyEntered)
                    {
                        frame!.LeaveExecutableSlots(null);
                        frame.LeaveInterpreterFrame();
                    }
                    try
                    {
                        frame?.LeaveFunctionCall();
                        if (bound.Values is not null) ReturnBoundArguments(bound);
                    }
                    finally { if (callEntered) _context.LeaveInterpreterFrame(); }
                }
            }
        }

        private void CloseInvocation(Exception? failure)
        {
            var caller = _caller!;
            var span = caller._pendingCallSite!.CallSpan;
            _context.LeaveExecutableSlots(null);
            _context.LeaveInterpreterFrame();
            try
            {
                if (failure is LythonRuntimeException runtimeError)
                    PyFunctionBinding.AnnotateException(runtimeError, _context, _context.FunctionName!, span);
            }
            finally
            {
                try
                {
                    _context.LeaveFunctionCall();
                    ReturnBoundArguments(_locals, _boundStorageOwner, _boundStorageBytes);
                }
                finally { caller._context.LeaveInterpreterFrame(); }
            }
        }

        private void CompletePendingCall(object? value, Exception? failure)
        {
            _completedCallValue = value;
            _completedCallError = failure;
            _pendingFunction = null;
            _pendingReceiver = null;
        }

        private void ResumePendingCall()
        {
            if (_pendingCallSite is null) return;
            if (_pendingFunction is not null) throw new InvalidOperationException("Executable call has not completed.");
            var start = _pendingCallStart;
            var value = _completedCallValue;
            var failure = _completedCallError;
            _pendingCallSite = null;
            _completedCallValue = null;
            _completedCallError = null;
            if (failure is not null)
            {
                _stack.RemoveTail(_stack.Count - start);
                if (failure is LythonRuntimeException runtimeError) _injectedException = runtimeError;
                else ExceptionDispatchInfo.Throw(failure);
            }
            else
            {
                CompleteExecutableValueTransfer(ref _stack, start, RuntimeValue(value));
                _instructionIndex++;
            }
        }
    }
}
