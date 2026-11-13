using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private static bool CallResultConsumedByIteration(in ExecutableWorkState execution, ExecutableCallSite call)
            => call.ConsumedByIteration || execution.ReturnConsumedByIteration && call.ReturnedDirectly;

        // The same native selection serves ordinary and not-yet-materialized
        // invocations. A false result has performed no operation or admission.
        private static bool TryExecuteNativeCall(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, ExecutableCallSite call, int start, object target, out ExecutionContext? started)
        {
            started = null;
            // Closed helpers complete here just like native builtins. Borrow the
            // evaluated arguments without manufacturing a pending call/resume.
            // Other function calls still use the existing binding/frame runner.
            var function = target as PyExecutableFunction;
            object? functionReceiver = call.HasReceiver && execution.Stack[start] is not PyNone
                ? execution.Stack[start] : null;
            if (target is PyBoundMethod { Function: PyExecutableFunction boundFunction } bound)
            {
                function = boundFunction;
                functionReceiver = bound.Receiver;
            }
            if (function is not null && function.CanStartInline && invocation is not null)
            {
                var borrow = call.IsPositionalOnly && function.SupportsDirectPositional
                    && function.CanBorrowPositionalArguments(call.ArgumentCount, functionReceiver);
                if (!borrow && !function.CanBindNativeArguments) return false;
                var callEntered = false;
                try
                {
                    scope.EnterInterpreterFrame(call.CallSpan);
                    callEntered = true;
                    scope.CheckExecution(call.CallSpan);
                    var arguments = execution.Stack.Slice(start + call.ArgumentOffset, call.ArgumentCount).Span;
                    if (!function.TryStartNativeCall(call, arguments, functionReceiver, scope, call.CallSpan,
                        CallResultConsumedByIteration(in execution, call), borrow, out var value, out var frame)) return false;
                    if (frame is not null)
                    {
                        try
                        {
                            frame.EnterExecutableSlots();
                            invocation.AttachPreparedCall(frame, call, start, call.CallSpan, CallCompletion.Call, function.DefinitionContext.MemoryGovernor);
                        }
                        catch
                        {
                            AbortPreparedNativeCall(frame, function.DefinitionContext.MemoryGovernor);
                            throw;
                        }
                        started = frame;
                        callEntered = false; // The existing child completion owns it.
                        return true;
                    }
                    scope.LeaveInterpreterFrame();
                    callEntered = false;
                    PublishInstructionResult(ref execution, invocation, execution.Code.Instructions[execution.InstructionIndex], value);
                    return true;
                }
                catch (Exception error)
                {
                    execution.Stack.RetireTo(start);
                    if (TryTranslateCallableException(error, call.CallSpan, out var translated)) throw translated;
                    throw;
                }
                finally { if (callEntered) scope.LeaveInterpreterFrame(); }
            }

            // These existing native receiver operations need execution services,
            // not a function namespace. Exact strings with no arguments cannot
            // invoke a guest coercion protocol; dictionary views only retain
            // their native receiver. Other shapes still materialize their frame
            // before binding, callbacks or actual suspension.
            if (call.HasReceiver && call.ArgumentCount == 0 && call.IsPositionalOnly)
            {
                if (call.CasedSplit is not null
                    && TryExecuteCasedSplit(ref execution, invocation ?? scope, invocation, call, start, target)) return true;
                try
                {
                    object result;
                    var context = invocation ?? scope;
                    var receiver = execution.Stack[start];
                    if (receiver is PyString or PyDict && target is ReceiverMethod method && method.AcceptsPositional(0))
                        result = InvokeReceiverMethod(method, receiver, call.CallSpan, context);
                    else return false;
                    PublishInstructionResult(ref execution, invocation, execution.Code.Instructions[execution.InstructionIndex], result);
                    return true;
                }
                catch { execution.Stack.RetireTo(start); throw; }
            }

            // Closed text arguments need execution services, not an invocation
            // namespace. Snapshot evaluated inputs before admission can reenter;
            // dynamic coercions/keywords still promote through the existing path.
            if (call.HasReceiver && call.IsPositionalOnly && call.ArgumentCount is >= 1 and <= 3
                && execution.Stack[start] is PyString text
                && SelectNativeStringOperation(target, call.ArgumentCount) is { } operation)
            {
                var inputs = execution.Stack.Slice(start + call.ArgumentOffset, call.ArgumentCount).Span;
                var first = inputs[0];
                var second = inputs.Length > 1 ? inputs[1] : null;
                var third = inputs.Length > 2 ? inputs[2] : null;
                if (CanUseNativeStringCall(operation, first, second, third))
                {
                    try
                    {
                        var result = InvokeNativeStringOperation(operation, text, first, second, third,
                            call.CallSpan, invocation ?? scope);
                        PublishInstructionResult(ref execution, invocation, execution.Code.Instructions[execution.InstructionIndex], result);
                        return true;
                    }
                    catch { execution.Stack.RetireTo(start); throw; }
                }
            }

            var argument = call.ArgumentCount == 0 ? null : execution.Stack[start + call.ArgumentOffset];
            var dictionary = call.HasReceiver && call.IsPositionalOnly && call.ArgumentCount is 1 or 2 &&
                ReferenceEquals(target, ReceiverMethods.DictionaryGet)
                ? execution.Stack[start] as PyDict : null;
            var nativeGet = dictionary is not null && dictionary.CanReadKeySynchronously(argument!);
            var closed = false;
            var unary = call.IsPositionalOnly && call.ArgumentCount == 1
                ? SelectNativeUnaryOperation(target, argument, out closed) : null;
            if (invocation is null && !closed) unary = null;
            if (!nativeGet && unary is null) return false;
            try
            {
                if (nativeGet && call.AccumulatesDictionary && TryExecuteDictionaryAccumulation(
                    ref execution, scope, call, start, dictionary!, (PyString)argument!)) return true;
                var result = nativeGet
                    ? InvokeDictionaryGet(dictionary!, argument!, call.ArgumentCount == 2
                        ? execution.Stack[start + call.ArgumentOffset + 1] : PyNone.Instance, call.CallSpan, scope)
                    : InvokeSingleInput(argument!, unary!, call.CallSpan, scope);
                PublishInstructionResult(ref execution, invocation, execution.Code.Instructions[execution.InstructionIndex], result);
                return true;
            }
            catch { execution.Stack.RetireTo(start); throw; }
        }

        internal static void AbortPreparedNativeCall(ExecutionContext frame, MemoryGovernor storageOwner)
        {
            try
            {
                if (frame.TakePreparedNativeChild() is { } child)
                {
                    try { AbortPreparedNativeCall(child, child._boundStorageOwner!); }
                    finally { frame.LeaveInterpreterFrame(); }
                }
            }
            finally { AbortPreparedNativeFrame(frame, storageOwner); }
        }

        private static void AbortPreparedNativeFrame(ExecutionContext frame, MemoryGovernor storageOwner)
        {
            try
            {
                frame.LeaveExecutableSlots();
                try { frame.AbandonFrame(); }
                finally { frame._execution.Stack.ReleaseStorage(); }
            }
            finally
            {
                try { frame.LeaveFunctionCall(); }
                finally
                {
                    try { ReturnBoundArguments(frame._execution.Locals, storageOwner, frame._execution.Code.ValueStorageCapacity); }
                    finally { frame.DetachExecutableExecution(); }
                }
            }
        }

        // The opcode has already read its evaluated target and offered it to the
        // linear frame runner. Both APIs use the same remaining call selection.
        private ValueTask ExecuteOrdinaryCall(ExecutableCallSite call, int start, object target, bool asynchronous)
        {
            try
            {
                object result;
                if (call.HasReceiver && target is ReceiverMethod receiverMethod && call.IsPositionalOnly && receiverMethod.AcceptsPositional(call.ArgumentCount))
                {
                    var receiver = _execution.Stack[start];
                    var arguments = _execution.Stack.Slice(start + call.ArgumentOffset, call.ArgumentCount);
                    if (asynchronous) return CompleteCallValue(InvokeReceiverMethodAsync(receiverMethod, receiver, arguments, call.CallSpan, this), start);
                    result = InvokeReceiverMethod(receiverMethod, receiver, arguments, call.CallSpan, this);
                }
                else if (target is PyFunctionBase function)
                {
                    var arguments = _execution.Stack.Slice(start + call.ArgumentOffset, call.ArgumentCount);
                    var receiver = call.HasReceiver && _execution.Stack[start] is not PyNone ? _execution.Stack[start] : null;
                    if (asynchronous) return CompleteCallValue(InvokeCallableFromOperandsAsync(function, arguments,
                        call.CallSpan, this, receiver, call), start);
                    result = InvokeCallableFromOperands(function, arguments, call.CallSpan, this, receiver, call);
                }
                else if (target is PyBoundMethod { Function: PyFunctionBase method } bound)
                {
                    var arguments = _execution.Stack.Slice(start + call.ArgumentOffset, call.ArgumentCount);
                    if (asynchronous) return CompleteCallValue(InvokeCallableFromOperandsAsync(method, arguments,
                        call.CallSpan, this, bound.Receiver, call), start);
                    result = InvokeCallableFromOperands(method, arguments, call.CallSpan, this, bound.Receiver, call);
                }
                else
                {
                    var pending = InvokeExecutableGeneralTargetAsync(call, start, target, asynchronous);
                    if (asynchronous) return CompleteCallValue(pending, start);
                    result = pending.GetAwaiter().GetResult();
                }
                PublishInstructionResult(_execution.Code.Instructions[_execution.InstructionIndex], result);
                return default;
            }
            catch
            {
                _execution.Stack.RetireTo(start);
                throw;
            }
        }

        // Native results above travel as objects. Only potentially asynchronous
        // branches reach this completion helper; their operands stay borrowed.
        private ValueTask CompleteCallValue(ValueTask<object> pending, int start)
        {
            if (!pending.IsCompleted) return AwaitCallValueAsync(pending, start);
            PublishInstructionResult(_execution.Code.Instructions[_execution.InstructionIndex], pending.GetAwaiter().GetResult());
            return default;
        }

        private async ValueTask AwaitCallValueAsync(ValueTask<object> pending, int start)
        {
            try { PublishInstructionResult(_execution.Code.Instructions[_execution.InstructionIndex], await pending.ConfigureAwait(false)); }
            catch { _execution.Stack.RetireTo(start); throw; }
        }

        private ValueTask<object> InvokeExecutableGeneralTargetAsync(ExecutableCallSite call, int start, object target, bool asynchronous)
        {
            if (target is ReceiverMethod method)
                target = method.Bind(_execution.Stack[start]);
            // Constructors can consume the same packet across __new__/__init__
            // suspension. Other short calls bind a managed stack borrow before
            // returning any pending work; retain no per-thread packet lifecycle.
            if (call.ArgumentCount > 3 || _preparedCallValue is PyInstance)
                return InvokeOwnedExecutableTargetAsync(call, start, target, asynchronous);
            CallableInvocation.Arguments arguments = default;
            PrepareExecutableCallArguments(call, ref _execution.Stack, start, arguments[..call.ArgumentCount], this);
            return asynchronous
                ? InvokeBorrowedCallableTargetAsync(target, call, arguments[..call.ArgumentCount], this)
                : new(InvokeBorrowedCallableTarget(target, call, arguments[..call.ArgumentCount], this));
        }

        private async ValueTask<object> InvokeOwnedExecutableTargetAsync(ExecutableCallSite call, int start, object target, bool asynchronous)
        {
            using var storage = ReserveExecutableCallArguments(call, this);
            var arguments = new CallArgumentValue[call.ArgumentCount];
            try
            {
                PrepareExecutableCallArguments(call, ref _execution.Stack, start, arguments, this);
                if (_preparedCallValue is PyInstance instance)
                {
                    _preparedCallValue = null;
                    return await InitializePreparedConstructorAsync(instance, arguments, call.CallSpan, this, asynchronous).ConfigureAwait(false);
                }
                return asynchronous
                    ? await InvokeCallableTargetAsync(target, call.TargetSpan, call.CallSpan, this, arguments).ConfigureAwait(false)
                    : InvokeCallableTarget(target, call.TargetSpan, call.CallSpan, this, arguments);
            }
            finally { Array.Clear(arguments); }
        }
    }
}
