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
        private ExecutableCallSite? _pendingCallSite;
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
                ExecutionContext? child = null;
                try { child = current.TakePreparedNativeChild() ?? current.ExecuteUntilCall(); }
                catch (Exception error) { failure = error; }
                current = child ?? CompleteCallFrame(current, failure);
            }
        }

        public async ValueTask ExecuteAsync()
        {
            ExecutionContext? current = this;
            while (current is not null)
            {
                Exception? failure = null;
                ExecutionContext? child = null;
                try { child = current.TakePreparedNativeChild() ?? await current.ExecuteUntilCallAsync().ConfigureAwait(false); }
                catch (Exception error) { failure = error; }
                current = child ?? CompleteCallFrame(current, failure);
            }
        }

        private bool TryBeginExecutableCall(ExecutableCallSite site, int start, object target,
            bool asynchronous, out ValueTask<ExecutionContext?> next)
        {
            next = default;
            if (target is PyType type) return TryBeginExecutableConstructor(type, site, start, asynchronous, out next);
            var function = target as PyExecutableFunction;
            object? receiver = site.HasReceiver && _execution.Stack[start] is not PyNone ? _execution.Stack[start] : null;
            if (target is PyBoundMethod { Function: PyExecutableFunction method } bound)
            {
                function = method;
                receiver = bound.Receiver;
            }
            if (function is null) return false;
            next = BeginFunctionCall(function, receiver, site, start, site.CallSpan,
                CallCompletion.Call, asynchronous);
            return true;
        }

        private bool TryBeginExecutableConstructor(PyType type, ExecutableCallSite site, int start,
            bool asynchronous, out ValueTask<ExecutionContext?> next)
        {
            next = default;
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
                transferred = true; // The call entry owns admission even on failure.
                next = BeginFunctionCall(initialize, instance, site, start, site.CallSpan,
                    CallCompletion.Constructor, asynchronous, callEntered: true);
                return true;
            }
            finally { if (!transferred) LeaveInterpreterFrame(); }
        }

        // Type subscription has at most two calls: a raw descriptor getter,
        // then its callable result. Both use the existing admitted frame runner.
        private bool TryBeginTypeSubscription(in ExecutableInstruction instruction, object receiver, object key,
            bool asynchronous, out ValueTask<ExecutionContext?> next)
        {
            next = default;
            var start = instruction.ResultSlot;
            if (_preparedCallValue is { } prepared)
            {
                return TryBeginSubscriptionCallable(prepared, instruction.Span, start, asynchronous, out next);
            }
            if (receiver is not PyType type ||
                !type.TryLookupInMro("__class_getitem__", 0, out var raw, out _)) return false;
            if (raw is PyInstance descriptor &&
                descriptor.Type.TryLookupInMro("__get__", 0, out var getter, out _) &&
                getter is PyExecutableFunction { SupportsDirectPositional: true } function)
            {
                _execution.Stack.PublishValue(start, start + 1, receiver);
                _execution.Stack.PublishValue(start + 1, start + 2, key);
                // Match raw ternary invocation: prefix self through the binder,
                // retaining the original receiver and key in their current slots.
                _execution.Stack.PublishValue(start + 2, start + 3, PyNone.Instance);
                _execution.Stack.PublishValue(start + 3, start + 4, type);
                next = BeginFunctionCall(function, descriptor, null, start, instruction.Span,
                    CallCompletion.TypeGetter, asynchronous);
                return true;
            }
            if (raw is not (PyClassMethod or PyStaticMethod or PyExecutableFunction)) return false;
            _execution.Stack.PublishValue(start, start + 1, receiver);
            _execution.Stack.PublishValue(start + 1, start + 2, key);
            var member = PyAttributeLookup.BindForType(type, raw, this, instruction.Span);
            if (TryBeginSubscriptionCallable(member, instruction.Span, start, asynchronous, out next)) return true;
            _preparedCallValue = member;
            return false;
        }

        private bool TryBeginSubscriptionCallable(object callable, LythonSourceSpan span, int start,
            bool asynchronous, out ValueTask<ExecutionContext?> next)
        {
            next = default;
            var function = callable as PyExecutableFunction;
            object? receiver = null;
            if (callable is PyBoundMethod { Function: PyExecutableFunction method } bound)
            {
                function = method;
                receiver = bound.Receiver;
            }
            if (function is not { SupportsDirectPositional: true }) return false;
            _preparedCallValue = null;
            next = BeginFunctionCall(function, receiver, null, start, span,
                CallCompletion.TypeSubscription, asynchronous);
            return true;
        }

        private ValueTask<object> InvokePreparedTypeSubscription(object index, LythonSourceSpan span, bool asynchronous)
        {
            var member = _preparedCallValue!;
            _preparedCallValue = null;
            return InvokeTypeSubscriptionMemberAsync(member, index, this, span, asynchronous);
        }

        private static ExecutionContext? CompleteCallFrame(ExecutionContext current, Exception? failure)
        {
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

        // Binding is prepared at the call instruction, rather than staged on
        // the caller and replayed by the frame driver. A completed body publishes
        // only after its binding/work/admission cleanup has finished.
        private ValueTask<ExecutionContext?> BeginFunctionCall(PyExecutableFunction function, object? receiver,
            ExecutableCallSite? site, int start, LythonSourceSpan span, CallCompletion completion,
            bool asynchronous, bool callEntered = false)
        {
            var ordinaryCall = site is not null;
            var argumentStart = ordinaryCall ? start + site!.ArgumentOffset
                : start + (completion == CallCompletion.TypeGetter ? 2 : 1);
            var argumentCount = ordinaryCall ? site!.ArgumentCount
                : completion == CallCompletion.TypeGetter ? 2 : 1;
            var prepared = false;
            try
            {
                if (ordinaryCall && !callEntered)
                {
                    EnterInterpreterFrame(span);
                    callEntered = true;
                }
                CheckExecution(span);
                var operands = _execution.Stack.Slice(argumentStart, argumentCount).Span;
                if (!asynchronous)
                {
                    var bound = function.BindCallOperands(operands, span, this, receiver, site);
                    prepared = true;
                    return new(BeginBoundCall(function, bound, site, start, span, completion, callEntered));
                }
                var binding = function.BindCallOperandsAsync(operands, span, this, receiver, site);
                if (!binding.IsCompleted)
                {
                    prepared = true;
                    return AwaitPendingBinding(binding, function, site, start, span, completion, callEntered);
                }
                var supplied = binding.GetAwaiter().GetResult();
                prepared = true;
                return new(BeginBoundCall(function, supplied, site, start, span, completion, callEntered));
            }
            catch (Exception error)
            {
                if (prepared) throw; // Bound execution already owns postprocessing.
                if (TryTranslateCallableException(error, span, out var translated)) error = translated;
                ApplyCallResult(start, span, completion, null, error);
                return new(this);
            }
            finally { if (!prepared && callEntered) LeaveInterpreterFrame(); }
        }

        private async ValueTask<ExecutionContext?> AwaitPendingBinding(ValueTask<BoundCallArguments> binding,
            PyExecutableFunction function, ExecutableCallSite? site, int start, LythonSourceSpan span,
            CallCompletion completion, bool callEntered)
        {
            var prepared = false;
            try
            {
                var bound = await binding.ConfigureAwait(false);
                prepared = true;
                return BeginBoundCall(function, bound, site, start, span, completion, callEntered);
            }
            catch (Exception error)
            {
                if (prepared) throw; // Bound execution already owns postprocessing.
                if (TryTranslateCallableException(error, span, out var translated)) error = translated;
                ApplyCallResult(start, span, completion, null, error);
                return this;
            }
            finally { if (!prepared && callEntered) LeaveInterpreterFrame(); }
        }

        // Binding locals own this descriptor through the synchronous transition.
        // Borrow it rather than copying its references onto the outgoing stack;
        // surviving values and ownership transfer into the work/frame below.
        private ExecutionContext BeginBoundCall(PyExecutableFunction function, in BoundCallArguments bound,
            ExecutableCallSite? site, int start, LythonSourceSpan span, CallCompletion completion,
            bool callEntered)
        {
            var consumedByIteration = completion == CallCompletion.Call && site is not null
                && CallResultConsumedByIteration(in _execution, site);
            object? completedValue = null;
            Exception? completedError = null;
            ExecutionContext? frame = null;
            ExecutableWorkState work = default;
            var ownsWork = false;
            var functionEntered = false;
            var transferred = false;
            var unpacked = false;
            try
            {
                CheckExecution(span);
                var arguments = bound.Values.AsSpan(0, bound.Assigned.Length);
                var originalExtent = _execution.Stack.LiveExtent;
                var returned = completion == CallCompletion.Call && site is not null
                    ? PrepareUnpackedCallReturn(ref _execution, site, start, out originalExtent) : default;
                var native = function.TryStartBoundInvocation(bound, span, consumedByIteration,
                    returned, out unpacked, out var nativeValue, out frame);
                if (!unpacked) _execution.Stack.RestoreNativeExtent(originalExtent);
                if (native)
                {
                    if (frame is null) { completedValue = nativeValue; goto Completed; }
                    functionEntered = true;
                    goto PreparedFrame;
                }
                if (function.CanDeferInvocationContext)
                {
                    var scope = function.DefinitionContext;
                    scope.EnterFunctionCall(span);
                    functionEntered = true;
                    var entryException = State.CurrentException;
                    try
                    {
                        work = CreateExecutableWorkState(function.CodeObject, scope, bound, function.ClosureCells,
                            argumentsAreLocals: true, lookupOwner: function, returnConsumedByIteration: consumedByIteration);
                        ownsWork = true;
                        var pause = ExecuteInstructionPrefix(ref work, scope, null, false, out _, out _, out _);
                        if (pause == InstructionPause.Boundary)
                        {
                            completedValue = work.ReturnValue ?? PyNone.Instance;
                            goto Completed;
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
                    InitializeExecutableFrame(function.CodeObject, frame, bound,
                        function.ClosureCells, argumentsAreLocals: true, lookupOwner: function, returnConsumedByIteration: consumedByIteration);
                }
            PreparedFrame:
                frame.EnterExecutableSlots();
                // Only a real child establishes a suspended caller. No reverse
                // link or preparation packet is needed for completed prefixes.
                AttachPreparedCall(frame, site, start, span, completion, bound.StorageOwner);
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
                completedError = error;
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
                            // A deferred prefix can write locals without ever
                            // creating a frame. Clear the compiler workspace,
                            // not only its parameter prefix, before caching it.
                            if (bound.Values is not null) ReturnBoundArguments(bound, function.CodeObject.ValueStorageCapacity);
                            frame?.DetachExecutableExecution();
                        }
                    }
                    finally { if (callEntered) LeaveInterpreterFrame(); }
                }
            }
        Completed:
            if (unpacked && completedError is null)
            {
                try
                {
                    CompleteUnpackedCallReturn(ref _execution, this, start, site!.UnpackedReturnCount);
                    _execution.InstructionIndex++;
                }
                catch { _execution.Stack.RetireTo(start); throw; }
            }
            else ApplyCallResult(start, span, completion, completedValue, completedError);
            return this;
        }

        private void AttachPreparedCall(ExecutionContext frame, ExecutableCallSite? site,
            int start, LythonSourceSpan span, CallCompletion completion, MemoryGovernor? storageOwner)
        {
            _pendingCallSite = site;
            _protocolCallSpan = site is null ? span : null;
            _pendingCallStart = start;
            _callCompletion = completion;
            frame._caller = this;
            frame._boundStorageOwner = storageOwner;
        }

        internal void AdoptPreparedNativeChild(ExecutionContext child, ExecutableCallSite site,
            int start, MemoryGovernor storageOwner)
        {
            child.EnterExecutableSlots();
            AttachPreparedCall(child, site, start, site.CallSpan, CallCompletion.Call, storageOwner);
            _preparedCallValue = child;
        }

        private ExecutionContext? TakePreparedNativeChild()
        {
            if (_preparedCallValue is not ExecutionContext child) return null;
            _preparedCallValue = null;
            return child;
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
                    ReturnBoundArguments(_execution.Locals, _boundStorageOwner, _execution.Code.ValueStorageCapacity);
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
        }

        private void ResumePendingCall()
        {
            if (!HasPendingCall) return;
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
            try { ApplyCallResult(start, span, completion, value, failure); }
            catch (LythonRuntimeException error) { _injectedException = error; }
        }

        private void ApplyCallResult(int start, LythonSourceSpan span, CallCompletion completion,
            object? value, Exception? failure)
        {
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
                ExceptionDispatchInfo.Throw(failure);
            }
            else
            {
                PublishInstructionResult(_execution.Code.Instructions[_execution.InstructionIndex], RuntimeValue(value));
                _execution.InstructionIndex++;
            }
        }
    }
}
