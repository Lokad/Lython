using System.Runtime.ExceptionServices;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        // Only the active child links to its caller. Caller operands remain owned
        // until the child and its cleanup finish; locals reuse admitted bound storage.
        private ExecutionContext? _caller;
        private MemoryGovernor? _boundStorageOwner;
        private long _boundStorageBytes;
        private ExecutableCallSite? _pendingCallSite;
        private PyExecutableFunction? _pendingFunction;
        private object? _pendingReceiver;
        private int _pendingCallStart;
        private object? _completedCallValue;
        private Exception? _completedCallError;
        private enum CallCompletion { Call, TypeGetter, TypeSubscription, Constructor }
        private CallCompletion _callCompletion;
        private LythonSourceSpan? _protocolCallSpan;
        private object? _preparedCallValue;
        private bool HasPendingCall => _pendingCallSite is not null || _protocolCallSpan is not null;
        private LythonSourceSpan PendingCallSpan => _pendingCallSite?.CallSpan ?? _protocolCallSpan!;

        public void Execute()
        {
            ExecutionContext? current = this;
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
            ExecutionContext? current = this;
            while (current is not null)
            {
                Exception? failure = null;
                try { await current.ExecuteUntilCallAsync().ConfigureAwait(false); }
                catch (Exception error) { failure = error; }
                current = AdvanceCallFrames(current, failure);
            }
        }

        private bool TrySuspendForExecutableCall(ExecutableCallSite site, int start, object target)
        {
            if (!site.IsPositionalOnly) return false;
            if (target is PyType type) return TrySuspendForExecutableConstructor(type, site, start);
            var function = target as PyExecutableFunction;
            object? receiver = site.HasReceiver && _execution.Stack[start] is not PyNone ? _execution.Stack[start] : null;
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

        private bool TrySuspendForExecutableConstructor(PyType type, ExecutableCallSite site, int start)
        {
            if (!type.CanPrepareExecutableConstructor) return false;
            EnterInterpreterFrame(site.CallSpan);
            var transferred = false;
            try
            {
                var instance = new PyInstance(type, MemoryGovernor, site.CallSpan);
                CheckExecution(site.CallSpan);
                _preparedCallValue = instance;
                // Admission may change live metadata. Continue the generic init
                // stage on this same instance when it no longer fits the runner.
                if (!type.TryGetExecutableInitializer(out var initialize)) return false;
                _pendingCallSite = site;
                _pendingFunction = initialize;
                _pendingReceiver = instance;
                _pendingCallStart = start;
                _callCompletion = CallCompletion.Constructor;
                transferred = true;
                return true;
            }
            finally { if (!transferred) LeaveInterpreterFrame(); }
        }

        // Type subscription has at most two calls: a raw descriptor getter,
        // then its callable result. Both use the existing admitted frame runner.
        private bool TrySuspendForTypeSubscription(ExecutableInstruction instruction)
        {
            var start = instruction.InputStart;
            if (_preparedCallValue is { } prepared)
            {
                if (!TryPrepareSubscriptionCallable(prepared, instruction.Span, start)) return false;
                _preparedCallValue = null;
                return true;
            }
            if (_execution.Stack[start] is not PyType type ||
                !type.TryLookupInMro("__class_getitem__", 0, out var raw, out _)) return false;
            if (raw is PyInstance descriptor &&
                descriptor.Type.TryLookupInMro("__get__", 0, out var getter, out _) &&
                getter is PyExecutableFunction { SupportsDirectPositional: true } function)
            {
                // Match raw ternary invocation: prefix self through the binder,
                // retaining the original receiver and key in their current slots.
                _execution.Stack.PublishValue(start + 2, start + 3, PyNone.Instance);
                _execution.Stack.PublishValue(start + 3, start + 4, type);
                _pendingFunction = function;
                _pendingReceiver = descriptor;
                _pendingCallStart = start;
                _protocolCallSpan = instruction.Span;
                _callCompletion = CallCompletion.TypeGetter;
                return true;
            }
            if (raw is not (PyClassMethod or PyStaticMethod or PyExecutableFunction)) return false;
            var member = PyAttributeLookup.BindForType(type, raw, this, instruction.Span);
            if (TryPrepareSubscriptionCallable(member, instruction.Span, start)) return true;
            _preparedCallValue = member;
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
            var member = _preparedCallValue!;
            _preparedCallValue = null;
            return InvokeTypeSubscriptionMemberAsync(member, index, this, span, asynchronous);
        }

        private static ExecutionContext? AdvanceCallFrames(ExecutionContext current, Exception? failure)
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

        private ExecutionContext BeginPendingCall()
        {
            var function = _pendingFunction!;
            var span = PendingCallSpan;
            var ordinaryCall = _pendingCallSite is not null;
            var argumentStart = ordinaryCall ? _pendingCallStart + _pendingCallSite!.ArgumentOffset
                : _pendingCallStart + (_callCompletion == CallCompletion.TypeGetter ? 2 : 1);
            var argumentCount = ordinaryCall ? _pendingCallSite!.ArgumentCount
                : _callCompletion == CallCompletion.TypeGetter ? 2 : 1;
            BoundCallArguments bound = default;
            ExecutionContext? frame = null;
            ExecutableWorkState work = default;
            var ownsWork = false;
            var functionEntered = false;
            var callEntered = _callCompletion == CallCompletion.Constructor;
            var transferred = false;
            try
            {
                // Ordinary calls own one admission through child completion.
                // Raw protocols retain their function/managed-stack checks
                // without gaining an ordinary call-opcode admission.
                if (ordinaryCall && !callEntered)
                {
                    EnterInterpreterFrame(span);
                    callEntered = true;
                }
                CheckExecution(span);
                var operands = _execution.Stack.Slice(argumentStart, argumentCount).Span;
                var nativeAttempted = ordinaryCall && function.CanBorrowPositionalArguments(argumentCount, _pendingReceiver);
                if (nativeAttempted && function.TryExecuteWithoutFrame(operands, span, out var borrowedValue))
                {
                    CompletePendingCall(borrowedValue, null);
                    return this;
                }
                bound = function.BindPositionalOperands(
                    operands, span, this, _pendingReceiver);
                var arguments = bound.Values.AsSpan(0, bound.Assigned.Length);
                if (!nativeAttempted && function.TryExecuteWithoutFrame(arguments, span, out var nativeValue))
                {
                    CompletePendingCall(nativeValue, null);
                    return this;
                }
                if (function.CanDeferInvocationContext)
                {
                    var scope = function.DefinitionContext;
                    scope.EnterFunctionCall(span);
                    functionEntered = true;
                    var entryException = State.CurrentException;
                    try
                    {
                        work = CreateExecutableWorkState(function.CodeObject, scope, bound, function.ClosureCells, argumentsAreLocals: true);
                        ownsWork = true;
                        var pause = ExecuteInstructionPrefix(ref work, scope, null, false, out _, out _);
                        if (pause == InstructionPause.Boundary)
                        {
                            CompletePendingCall(work.HasReturn ? work.ReturnValue : PyNone.Instance, null);
                            return this;
                        }
                        if (pause != InstructionPause.ContextRequired)
                            throw new InvalidOperationException("A borrowed invocation cannot suspend.");
                        frame = function.EnterInvocationFrame(bound, span, enterFunctionCall: false);
                        frame.AdoptExecutableExecution(ref work, entryException);
                        ownsWork = false;
                    }
                    finally { State.SetCurrentException(entryException); }
                }
                else
                {
                    frame = function.EnterInvocationFrame(bound, span);
                    functionEntered = true;
                    if (TryExecuteNativeReturn(function.CodeObject, frame, arguments, span, out var value))
                    {
                        CompletePendingCall(value, null);
                        return this;
                    }
                    InitializeExecutableFrame(function.CodeObject, frame, bound,
                        function.ClosureCells, argumentsAreLocals: true);
                }
                frame.EnterExecutableSlots();
                frame._caller = this;
                frame._boundStorageOwner = bound.StorageOwner;
                frame._boundStorageBytes = bound.StorageBytes;
                transferred = true;
                return frame;
            }
            catch (Exception error)
            {
                if (TryTranslateCallableException(error, span, out var translated)) error = translated;
                if (frame is not null && error is LythonRuntimeException runtimeError)
                    PyFunctionBinding.AnnotateException(runtimeError, frame, function.Name, span);
                else if (functionEntered && error is LythonRuntimeException borrowedError)
                    PyFunctionBinding.AnnotateException(borrowedError, function.DefinitionContext, function.Name, span);
                CompletePendingCall(null, error);
                return this;
            }
            finally
            {
                if (!transferred)
                {
                    try
                    {
                        try { if (ownsWork) work.Stack.ReleaseStorage(); }
                        finally
                        {
                            if (functionEntered) State.LeaveFunctionCall();
                            if (bound.Values is not null) ReturnBoundArguments(bound);
                            frame?.DetachExecutableExecution();
                        }
                    }
                    finally { if (callEntered) LeaveInterpreterFrame(); }
                }
            }
        }

        private void CloseInvocation(Exception? failure)
        {
            var caller = _caller!;
            var span = caller.PendingCallSpan;
            LeaveExecutableSlots();
            try
            {
                if (failure is LythonRuntimeException runtimeError)
                    PyFunctionBinding.AnnotateException(runtimeError, this, FunctionName!, span);
            }
            finally
            {
                try
                {
                    LeaveFunctionCall();
                    ReturnBoundArguments(_execution.Locals, _boundStorageOwner, _boundStorageBytes);
                }
                finally
                {
                    DetachExecutableExecution();
                    if (caller._pendingCallSite is not null) caller.LeaveInterpreterFrame();
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
            if (completion == CallCompletion.Constructor)
            {
                var instance = (PyInstance)_preparedCallValue!;
                _preparedCallValue = null;
                CheckExecution(span);
                if (failure is null)
                {
                    if (!ReferenceEquals(value, PyNone.Instance))
                        failure = new LythonRuntimeException("TypeError", $"__init__ for class '{instance.Type.Name}' must return None.", span);
                    else value = instance;
                }
            }
            if (completion == CallCompletion.TypeGetter)
            {
                _execution.Stack.RetireTo(start + 2);
                CheckExecution(span);
                if (failure is LythonRuntimeException { ExceptionType: "AttributeError" })
                {
                    failure = null;
                    value = PyNone.Instance;
                }
                if (failure is null)
                {
                    if (value is not PyNone)
                    {
                        _preparedCallValue = value;
                        return; // Resume this subscript without rebinding.
                    }
                    failure = new LythonRuntimeException("TypeError",
                        $"type '{((PyType)_execution.Stack[start]).Name}' is not subscriptable", span);
                }
            }
            if (completion == CallCompletion.TypeSubscription) CheckExecution(span);
            if (failure is not null)
            {
                _execution.Stack.RetireTo(start);
                if (failure is LythonRuntimeException runtimeError) _injectedException = runtimeError;
                else ExceptionDispatchInfo.Throw(failure);
            }
            else
            {
                PublishInstructionResult(_execution.Code.Instructions[_execution.InstructionIndex], RuntimeValue(value));
                _execution.InstructionIndex++;
            }
        }
    }
}
