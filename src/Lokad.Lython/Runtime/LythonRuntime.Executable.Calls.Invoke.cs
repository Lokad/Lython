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
                    var unpackedReturn = PrepareUnpackedCallReturn(ref execution, call, start, out var originalExtent);
                    function.StartNativeCall(call, arguments, functionReceiver, scope, call.CallSpan,
                        CallResultConsumedByIteration(in execution, call), borrow, unpackedReturn,
                        out var unpacked, out var value, out var frame);
                    if (!unpacked) execution.Stack.RestoreNativeExtent(originalExtent);
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
                    if (unpacked)
                    {
                        CompleteUnpackedCallReturn(ref execution, scope, start, unpackedReturn.Length);
                    }
                    else PublishInstructionResult(ref execution, invocation, execution.Code.Instructions[execution.InstructionIndex], value);
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

            if (call.HasReceiver && call.IsPositionalOnly)
            {
                if (call.ArgumentCount == 0 && call.CasedSplit is not null
                    && TryExecuteCasedSplit(ref execution, invocation ?? scope, invocation, call, start, target)) return true;
                if (target is not ReceiverMethod method) return false;
                try
                {
                    if (TryConsumePrivateStringResult(method, execution.Code, execution.Stack.Values,
                        ref execution.InstructionIndex, call, start, invocation ?? scope, out var consumed))
                    {
                        PublishInstructionResult(ref execution, invocation, execution.Code.Instructions[execution.InstructionIndex], consumed);
                        return true;
                    }
                    var receiver = execution.Stack[start];
                    var arguments = execution.Stack.Slice(start + call.ArgumentOffset, call.ArgumentCount).Span;
                    if (call.UnpackedReturnCount != 0 && ReferenceEquals(method, ReceiverMethods.StringSplit)
                        && receiver is PyString text && arguments.Length <= 2
                        && TryExecuteSplitUnpack(ref execution, invocation, scope, call, start, text, arguments))
                        return true;
                    if (call.AccumulatesDictionary && arguments.Length is 1 or 2
                        && ReferenceEquals(method, ReceiverMethods.DictionaryGet) && receiver is PyDict dictionary
                        && dictionary.CanReadKeySynchronously(arguments[0].Object!)
                        && TryExecuteDictionaryAccumulation(ref execution, scope, call, start, dictionary,
                            (PyString)arguments[0].Object!)) return true;
                    if (!TryInvokeClosedReceiver(method, receiver, arguments, call.CallSpan, invocation ?? scope,
                        out var result)) return false;
                    PublishInstructionResult(ref execution, invocation, execution.Code.Instructions[execution.InstructionIndex], result);
                    return true;
                }
                catch { execution.Stack.RetireTo(start); throw; }
            }

            var argument = call.ArgumentCount == 0 ? null : execution.Stack[start + call.ArgumentOffset];
            var closed = false;
            var unary = call.IsPositionalOnly && call.ArgumentCount == 1
                ? SelectNativeUnaryOperation(target, argument, out closed) : null;
            if (unary is null || invocation is null && !closed) return false;
            try
            {
                var result = InvokeSingleInput(argument!, unary, call.CallSpan, scope);
                PublishInstructionResult(ref execution, invocation, execution.Code.Instructions[execution.InstructionIndex], result);
                return true;
            }
            catch { execution.Stack.RetireTo(start); throw; }
        }

        private static bool TryExecuteSplitUnpack(ref ExecutableWorkState execution, ExecutionContext? invocation,
            ExecutionContext scope, ExecutableCallSite call, int start, PyString text, ReadOnlySpan<ExecutableSlot> arguments)
        {
            var separator = arguments.Length == 0 ? null : arguments[0].Object;
            var count = arguments.Length < 2 ? null : arguments[1].Object;
            if (!CanUseNativeStringCall(NativeStringOperation.Split, separator, count, null)) return false;
            var unpacked = PrepareUnpackedCallReturn(ref execution, call, start, out var originalExtent);
            if (unpacked.IsEmpty) return false;

            var context = invocation ?? scope;
            PyList? list;
            context.EnterInterpreterFrame(call.CallSpan);
            try { list = StringMembers.SplitForUnpack(text, separator as PyString, count, unpacked, call.CallSpan, context); }
            catch (Exception error) when (TryTranslateCallableException(error, call.CallSpan, out var translated))
            { throw translated; }
            finally { context.LeaveInterpreterFrame(); }

            if (list is null)
                CompleteUnpackedCallReturn(ref execution, scope, start, unpacked.Length);
            else
            {
                execution.Stack.RestoreNativeExtent(originalExtent);
                PublishInstructionResult(ref execution, invocation, execution.Code.Instructions[execution.InstructionIndex], list);
            }
            return true;
        }

        private static Span<ExecutableSlot> PrepareUnpackedCallReturn(ref ExecutableWorkState execution,
            ExecutableCallSite call, int start, out int originalExtent)
        {
            originalExtent = execution.Stack.LiveExtent;
            // Eligibility depends on these actual input values, not ownership
            // metadata for unrelated users of the same physical slots.
            if (call.UnpackedReturnCount == 0 || call.ArgumentCount > NativeValueCapacity
                || !execution.Stack.CanRetireWithoutCleanup(start)) return default;
            var returned = execution.Stack.Values.AsSpan(start, call.UnpackedReturnCount);
            // Track every reference before the callee writes, including results
            // wider than the inputs. Nothing reaches guest targets before cleanup.
            execution.Stack.RestoreNativeExtent(Math.Max(originalExtent, start + returned.Length));
            return returned;
        }

        private static void CompleteUnpackedCallReturn(ref ExecutableWorkState execution,
            ExecutionContext scope, int start, int count)
        {
            ref readonly var unpack = ref execution.Code.Instructions[execution.InstructionIndex + 1];
            scope.CheckExecution(unpack.Span);
            execution.Stack.RetireTo(start + count);
            execution.Stack.Values.AsSpan(start, count).Reverse();
            execution.InstructionIndex++;
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
                    if (asynchronous) return CompleteCallValue(InvokeReceiverMethodAsync(receiverMethod, receiver, new ReceiverArguments(_execution.Stack.Values, start + call.ArgumentOffset, call.ArgumentCount), call.CallSpan, this), start);
                    result = InvokeReceiverMethod(receiverMethod, receiver, new ReceiverArguments(_execution.Stack.Values, start + call.ArgumentOffset, call.ArgumentCount), call.CallSpan, this);
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
                ? InvokeBorrowedCallableTargetAsync(target, call.TargetSpan, call.CallSpan, arguments[..call.ArgumentCount], this)
                : new(InvokeBorrowedCallableTarget(target, call.TargetSpan, call.CallSpan, arguments[..call.ArgumentCount], this));
        }

        private async ValueTask<object> InvokeOwnedExecutableTargetAsync(ExecutableCallSite call, int start, object target, bool asynchronous)
        {
            using var storage = ReserveCallArgumentStorage(call.ArgumentCount, this, call.CallSpan);
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
