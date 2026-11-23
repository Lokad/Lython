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
        private ExecutableSlot _completedCallValue;
        private Exception? _completedCallError;
        private enum CallCompletion { Call, TypeGetter, TypeSubscription, Constructor, Unpacked }
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
                try { child = current.TakePreparedChild() ?? current.ExecuteUntilCall(); }
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
                try { child = current.TakePreparedChild() ?? await current.ExecuteUntilCallAsync().ConfigureAwait(false); }
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
                getter is PyExecutableFunction { SupportsPositionalBinding: true } function)
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
            if (function is not { SupportsPositionalBinding: true }) return false;
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

            var result = current.HasFrameReturn ? current.FrameReturnSlot : ExecutableSlot.FromObject(PyNone.Instance);
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
                ApplyCallResult(start, span, completion, default, error);
                return new(this);
            }
            finally { if (!prepared && callEntered) LeaveInterpreterFrame(); }
        }

        private async ValueTask<ExecutionContext?> AwaitPendingBinding(ValueTask<BoundFunctionArguments> binding,
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
                ApplyCallResult(start, span, completion, default, error);
                return this;
            }
            finally { if (!prepared && callEntered) LeaveInterpreterFrame(); }
        }

        // Binding locals own this descriptor through the synchronous transition.
        // Borrow it rather than copying its references onto the outgoing stack;
        // surviving values and ownership transfer into the work/frame below.
        private ExecutionContext BeginBoundCall(PyExecutableFunction function, in BoundFunctionArguments bound,
            ExecutableCallSite? site, int start, LythonSourceSpan span, CallCompletion completion,
            bool callEntered)
        {
            var consumedByIteration = completion == CallCompletion.Call && site is not null
                && CallResultConsumedByIteration(in _execution, site);
            ExecutableSlot completedValue = default;
            Exception? completedError = null;
            ExecutionContext? frame = null;
            var functionEntered = false;
            var transferred = false;
            var unpacked = false;
            try
            {
                CheckExecution(span);
                var originalExtent = _execution.Stack.LiveExtent;
                var returned = completion == CallCompletion.Call && site is not null
                    ? PrepareUnpackedCallReturn(ref _execution, site, start, out originalExtent) : default;
                var prepared = function.TryStartBoundInvocation(bound, span, consumedByIteration,
                    returned, out unpacked, out completedValue, out frame);
                if (!unpacked) _execution.Stack.RestoreNativeExtent(originalExtent);
                if (prepared)
                {
                    if (frame is null) goto Completed;
                    functionEntered = true;
                }
                else
                {
                    frame = function.EnterInvocationFrame(bound, span);
                    functionEntered = true;
                    InitializeExecutableFrame(function.CodeObject, frame, bound,
                        function.Captures, lookupOwner: function, returnConsumedByIteration: consumedByIteration);
                }
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
                completedError = error;
            }
            finally
            {
                if (!transferred)
                {
                    try
                    {
                        if (functionEntered) State.LeaveFunctionCall();
                        // The prefix can write locally without creating a frame.
                        // Clear the full workspace before caching it.
                        if (bound.Values is not null) ReturnBoundArguments(bound, function.CodeObject.ValueStorageCapacity);
                        frame?.DetachExecutableExecution();
                    }
                    finally { if (callEntered) LeaveInterpreterFrame(); }
                }
            }
        Completed:
            if (unpacked && completedError is null)
                FinishUnpackedCall(start, site!.UnpackedReturnCount);
            else ApplyCallResult(start, span, completion, completedValue, completedError);
            return this;
        }

        private static ExecutionContext PromoteBorrowedInvocation(PyExecutableFunction function,
            in BoundFunctionArguments bound, ref ExecutableWorkState work, LythonSourceSpan span, PyException? entryException,
            InstructionPause pause, ValueTask<ExecutionContext?> nextCall)
        {
            if (pause is not (InstructionPause.Call or InstructionPause.ContextRequired))
                throw new InvalidOperationException("A borrowed invocation cannot suspend.");
            // A borrowed call transfers only prepared work. Host suspension
            // happens later, after the existing frame driver takes over.
            var child = pause == InstructionPause.Call ? nextCall.Result : null;
            try
            {
                var frame = function.EnterInvocationFrame(bound, span, enterFunctionCall: false);
                if (child is not null)
                {
                    ref readonly var call = ref work.Code.Instructions[work.InstructionIndex];
                    frame.AdoptPreparedChild(child, work.Code.CallSites[call.CallSiteIndex], call.ResultSlot,
                        child._boundStorageOwner!);
                }

                frame.AdoptExecutableExecution(ref work, entryException);
                return frame;
            }
            catch
            {
                if (child is not null)
                {
                    try { AbortPreparedCall(child, child._boundStorageOwner!); }
                    finally { function.DefinitionContext.LeaveInterpreterFrame(); }
                }
                throw;
            }
        }

        private static bool TryExecuteDeferredChild(ref ExecutableWorkState caller, ExecutionContext scope,
            ExecutableCallSite site, int start, object target, out ExecutionContext? child)
        {
            child = null;
            // Only full positional loads use this entry. The binder cannot
            // suspend, and the leaf's borrowed prefix cannot recurse through
            // another explicit call. A later context takes over dynamic work.
            if (!site.IsPositionalOnly || site.HasReceiver || target is not PyExecutableFunction function ||
                function.CodeObject.CallSites.Count != 0 || !function.CanDeferInvocationContext ||
                !function.HasCompletePositionalSupply(site.ArgumentCount, null))
                return false;

            scope.EnterInterpreterFrame(site.CallSpan);
            BoundFunctionArguments bound = default;
            try
            {
                ExecutableSlot value;
                bool unpacked;
                int count;
                try
                {
                    var operands = caller.Stack.Slice(start + site.ArgumentOffset, site.ArgumentCount).Span;
                    bound = function.BindCallOperands(operands, site.CallSpan, scope, null, site);
                    var returned = PrepareUnpackedCallReturn(ref caller, site, start, out var originalExtent);
                    child = StartBorrowedInvocation(function, bound, site.CallSpan, CallResultConsumedByIteration(in caller, site),
                        returned, out value, out unpacked);
                    count = returned.Length;
                    if (!unpacked) caller.Stack.RestoreNativeExtent(originalExtent);
                    if (child is not null) child._boundStorageOwner = bound.StorageOwner;
                }
                finally
                {
                    if (child is null)
                    {
                        try
                        {
                            if (bound.Values is not null) ReturnBoundArguments(bound, function.CodeObject.ValueStorageCapacity);
                        }
                        finally { scope.LeaveInterpreterFrame(); }
                    }
                }

                if (child is not null) return true;
                if (unpacked) CompleteUnpackedCallReturn(ref caller, scope, start, count);
                else PublishInstructionResult(ref caller, null, caller.Code.Instructions[caller.InstructionIndex], value);
                return true;
            }
            catch (Exception error)
            {
                caller.Stack.RetireTo(start);
                if (TryTranslateCallableException(error, site.CallSpan, out var translated)) throw translated;
                throw;
            }
        }

        internal static ExecutionContext? StartBorrowedInvocation(PyExecutableFunction function,
            in BoundFunctionArguments bound, LythonSourceSpan span, bool consumedByIteration,
            Span<ExecutableSlot> destination, out ExecutableSlot value, out bool unpacked)
        {
            var scope = function.DefinitionContext;
            scope.EnterFunctionCall(span);
            var entryException = scope.State.CurrentException;
            ExecutableWorkState work = default;
            ExecutionContext? frame = null;
            try
            {
                work = CreateExecutableWorkState(function.CodeObject, scope, bound, function.Captures,
                    lookupOwner: function, returnConsumedByIteration: consumedByIteration, borrowedInvocation: true);
                var pause = ExecuteInstructionPrefix(ref work, scope, null, false, out _, out _, out var nextCall, destination);
                unpacked = pause == InstructionPause.UnpackedReturn;
                value = work.ReturnValue.IsUnassigned ? ExecutableSlot.FromObject(PyNone.Instance) : work.ReturnValue;
                if (unpacked || pause == InstructionPause.Boundary) return null;
                return frame = PromoteBorrowedInvocation(function, bound, ref work, span, entryException, pause, nextCall);
            }
            catch (Exception error)
            {
                if (TryTranslateCallableException(error, span, out var translated))
                {
                    PyFunctionBinding.AnnotateException(translated, scope, function.Name, span);
                    throw translated;
                }
                if (error is LythonRuntimeException runtimeError)
                    PyFunctionBinding.AnnotateException(runtimeError, scope, function.Name, span);
                throw;
            }
            finally
            {
                try
                {
                    if (work.Code is not null)
                    {
                        try { work.Stack.RetireTo(work.Code.FirstTemporarySlot); }
                        finally { work.Stack.ReleaseStorage(); }
                    }
                }
                finally
                {
                    if (frame is null) scope.LeaveFunctionCall();
                    scope.State.SetCurrentException(entryException);
                }
            }
        }

        private void FinishUnpackedCall(int start, int count)
        {
            try
            {
                CompleteUnpackedCallReturn(ref _execution, this, start, count);
                _execution.InstructionIndex++;
            }
            catch { _execution.Stack.RetireTo(start); throw; }
        }

        private bool TryStageUnpackedFrameReturn(in ExecutableInstruction tuple)
        {
            var caller = _caller;
            if (caller is null || caller._callCompletion != CallCompletion.Call ||
                caller._pendingCallSite is not { UnpackedReturnCount: > 0 } call ||
                _execution.Code.ExceptionRegions.Count != 0 ||
                !CanForwardFixedReturn(in _execution, tuple, call.UnpackedReturnCount))
                return false;

            var returned = PrepareUnpackedCallReturn(ref caller._execution, call, caller._pendingCallStart, out var originalExtent);
            if (returned.IsEmpty) return false;
            try
            {
                CopyFixedReturn(ref _execution, this, tuple, returned);
                caller._callCompletion = CallCompletion.Unpacked;
                return true;
            }
            catch
            {
                caller._execution.Stack.RestoreNativeExtent(originalExtent);
                throw;
            }
        }

        private static bool CanForwardFixedReturn(in ExecutableWorkState execution, in ExecutableInstruction tuple, int count)
            => IsUnpackedTupleReturn(execution.Code, execution.InstructionIndex, tuple, count)
                && execution.Stack.CanRetireWithoutCleanup(tuple.InputStart);

        private static void CopyFixedReturn(ref ExecutableWorkState execution, ExecutionContext scope,
            in ExecutableInstruction tuple, Span<ExecutableSlot> destination)
        {
            var items = execution.Stack.Values.AsSpan(tuple.InputStart, tuple.ItemCount);
            AdmitUnpackedTuple(items, scope, tuple.Span);
            execution.InstructionIndex++;
            scope.CheckExecution(execution.Code.Instructions[execution.InstructionIndex].Span);
            // Both arrays are live. Publication follows the callee's cleanup.
            items.CopyTo(destination);
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

        internal void AdoptPreparedChild(ExecutionContext child, ExecutableCallSite site,
            int start, MemoryGovernor storageOwner)
        {
            child.EnterExecutableSlots();
            AttachPreparedCall(child, site, start, site.CallSpan, CallCompletion.Call, storageOwner);
            _preparedCallValue = child;
        }

        private ExecutionContext? TakePreparedChild()
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

        private void CompletePendingCall(ExecutableSlot value, Exception? failure)
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
            var site = _pendingCallSite;
            var span = PendingCallSpan;
            _pendingCallSite = null;
            _protocolCallSpan = null;
            _callCompletion = CallCompletion.Call;
            _completedCallValue = default;
            _completedCallError = null;
            try
            {
                if (completion == CallCompletion.Unpacked && failure is null) FinishUnpackedCall(start, site!.UnpackedReturnCount);
                else ApplyCallResult(start, span, completion, value, failure);
            }
            catch (LythonRuntimeException error) { _injectedException = error; }
        }

        private void ApplyCallResult(int start, LythonSourceSpan span, CallCompletion completion,
            ExecutableSlot value, Exception? failure)
        {
            if (completion == CallCompletion.Constructor)
            {
                var instance = (PyInstance)_preparedCallValue!;
                _preparedCallValue = null;
                CheckExecution(span);
                if (failure is null)
                {
                    if (!ReferenceEquals(value.Reference, PyNone.Instance))
                        failure = new LythonRuntimeException("TypeError", $"__init__ for class '{instance.Type.Name}' must return None.", span);
                    else value.Object = instance;
                }
            }
            if (completion == CallCompletion.TypeGetter)
            {
                _execution.Stack.RetireTo(start + 2);
                CheckExecution(span);
                if (failure is LythonRuntimeException { ExceptionType: "AttributeError" })
                {
                    failure = null;
                    value.Object = PyNone.Instance;
                }
                if (failure is null)
                {
                    if (value.Reference is not PyNone)
                    {
                        _preparedCallValue = value.Object.RequireNotNull();
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
                PublishInstructionResult(_execution.Code.Instructions[_execution.InstructionIndex], value.IsUnassigned ? ExecutableSlot.FromObject(PyNone.Instance) : value);
                _execution.InstructionIndex++;
            }
        }
    }
}
