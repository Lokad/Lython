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
        private enum CallCompletion { Call, TypeGetter, TypeSubscription }
        private CallCompletion _callCompletion;
        private LythonSourceSpan? _protocolCallSpan;
        private object? _preparedTypeSubscription;
        private bool HasPendingCall => _pendingCallSite is not null || _protocolCallSpan is not null;
        private LythonSourceSpan PendingCallSpan => _pendingCallSite?.CallSpan ?? _protocolCallSpan!;

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

        // Type subscription has at most two calls: a raw descriptor getter,
        // then its callable result. Both use the existing admitted frame runner.
        private bool TrySuspendForTypeSubscription(ExecutableInstruction instruction)
        {
            var start = _stack.Count - 2;
            if (_preparedTypeSubscription is { } prepared)
            {
                if (!TryPrepareSubscriptionCallable(prepared, instruction.Span, start)) return false;
                _preparedTypeSubscription = null;
                return true;
            }
            if (_stack[start] is not PyType type ||
                !type.TryLookupInMro("__class_getitem__", 0, out var raw, out _)) return false;
            if (raw is PyInstance descriptor &&
                descriptor.Type.TryLookupInMro("__get__", 0, out var getter, out _) &&
                getter is PyExecutableFunction { SupportsDirectPositional: true } function)
            {
                // Match raw ternary invocation: prefix self through the binder,
                // retaining the original receiver and key in their current slots.
                _stack.Push(PyNone.Instance);
                _stack.Push(type);
                _pendingFunction = function;
                _pendingReceiver = descriptor;
                _pendingCallStart = start;
                _protocolCallSpan = instruction.Span;
                _callCompletion = CallCompletion.TypeGetter;
                return true;
            }
            if (raw is not (PyClassMethod or PyStaticMethod or PyExecutableFunction)) return false;
            var member = PyAttributeLookup.BindForType(type, raw, _context, instruction.Span);
            if (TryPrepareSubscriptionCallable(member, instruction.Span, start)) return true;
            _preparedTypeSubscription = member;
            return false;
        }

        private bool TryPrepareSubscriptionCallable(object callable, LythonSourceSpan span, int start)
        {
            var function = callable as PyExecutableFunction;
            object? receiver = null;
            if (callable is PyBoundMethod { Function: PyExecutableFunction method } bound)
            {
                function = method;
                receiver = bound.Receiver;
            }
            if (function is not { SupportsDirectPositional: true }) return false;
            _pendingFunction = function;
            _pendingReceiver = receiver;
            _pendingCallStart = start;
            _protocolCallSpan = span;
            _callCompletion = CallCompletion.TypeSubscription;
            return true;
        }

        private ValueTask<object> InvokePreparedTypeSubscription(object index, LythonSourceSpan span, bool asynchronous)
        {
            var member = _preparedTypeSubscription!;
            _preparedTypeSubscription = null;
            return InvokeTypeSubscriptionMemberAsync(member, index, _context, span, asynchronous);
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
            var span = PendingCallSpan;
            var ordinaryCall = _callCompletion == CallCompletion.Call;
            var argumentStart = ordinaryCall ? _pendingCallStart + _pendingCallSite!.ArgumentOffset
                : _pendingCallStart + (_callCompletion == CallCompletion.TypeGetter ? 2 : 1);
            var argumentCount = ordinaryCall ? _pendingCallSite!.ArgumentCount
                : _callCompletion == CallCompletion.TypeGetter ? 2 : 1;
            BoundCallArguments bound = default;
            ExecutionContext? frame = null;
            var callEntered = false;
            var transferred = false;
            try
            {
                // Ordinary calls own one admission through child completion.
                // Raw protocols retain their function/managed-stack checks
                // without gaining an ordinary call-opcode admission.
                if (ordinaryCall)
                {
                    _context.EnterInterpreterFrame(span);
                    callEntered = true;
                }
                _context.CheckExecution(span);
                bound = function.BindPositionalOperands(
                    _stack.Slice(argumentStart, argumentCount).Span, span, _context, _pendingReceiver);
                frame = function.EnterInvocationFrame(bound, span);
                if (TryExecuteSimpleReturn(function.CodeObject, frame, bound, span, out var value))
                {
                    CompletePendingCall(value, null);
                    return this;
                }
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
                if (TryTranslateCallableException(error, span, out var translated)) error = translated;
                if (frame is not null && error is LythonRuntimeException runtimeError)
                    PyFunctionBinding.AnnotateException(runtimeError, frame, function.Name, span);
                CompletePendingCall(null, error);
                return this;
            }
            finally
            {
                if (!transferred)
                {
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
            var span = caller.PendingCallSpan;
            _context.LeaveExecutableSlots(null);
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
                finally
                {
                    if (caller._callCompletion == CallCompletion.Call) caller._context.LeaveInterpreterFrame();
                }
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
            if (!HasPendingCall) return;
            if (_pendingFunction is not null) throw new InvalidOperationException("Executable call has not completed.");
            var start = _pendingCallStart;
            var value = _completedCallValue;
            var failure = _completedCallError;
            var completion = _callCompletion;
            var span = PendingCallSpan;
            _pendingCallSite = null;
            _protocolCallSpan = null;
            _callCompletion = CallCompletion.Call;
            _completedCallValue = null;
            _completedCallError = null;
            if (completion == CallCompletion.TypeGetter)
            {
                _stack.RemoveTail(_stack.Count - start - 2);
                _context.CheckExecution(span);
                if (failure is LythonRuntimeException { ExceptionType: "AttributeError" })
                {
                    failure = null;
                    value = PyNone.Instance;
                }
                if (failure is null)
                {
                    if (value is not PyNone)
                    {
                        _preparedTypeSubscription = value;
                        return; // Resume this subscript without rebinding.
                    }
                    failure = new LythonRuntimeException("TypeError",
                        $"type '{((PyType)_stack[start]).Name}' is not subscriptable", span);
                }
            }
            if (completion == CallCompletion.TypeSubscription) _context.CheckExecution(span);
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
