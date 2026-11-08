using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        // The same native selection serves ordinary and not-yet-materialized
        // invocations. A false result has performed no operation or admission.
        private static bool TryExecuteNativeCall(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, ExecutableCallSite call, int start, object target)
        {
            // Closed helpers complete here just like native builtins. Borrow the
            // evaluated arguments without manufacturing a pending call/resume.
            // Other function calls still use the existing binding/frame runner.
            if (call.IsPositionalOnly && target is PyExecutableFunction { SupportsDirectPositional: true } function
                && function.CodeObject.NativeBody is not null
                && (!call.HasReceiver || execution.Stack[start] is PyNone)
                && function.CanBorrowPositionalArguments(call.ArgumentCount, receiver: null))
            {
                try
                {
                    object value;
                    scope.EnterInterpreterFrame(call.CallSpan);
                    try
                    {
                        scope.CheckExecution(call.CallSpan);
                        if (!function.TryExecuteWithoutFrame(
                            execution.Stack.Slice(start + call.ArgumentOffset, call.ArgumentCount).Span,
                            call.CallSpan, out value)) return false;
                    }
                    catch (Exception error) when (TryTranslateCallableException(error, call.CallSpan, out var translated))
                    {
                        throw translated;
                    }
                    finally { scope.LeaveInterpreterFrame(); }
                    // As in the frame runner, release call admission before
                    // publishing the result or retiring any caller inputs.
                    PublishInstructionResult(ref execution, invocation, execution.Code.Instructions[execution.InstructionIndex], value);
                    return true;
                }
                catch { execution.Stack.RetireTo(start); throw; }
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

            var argument = call.ArgumentCount == 0 ? null : execution.Stack[start + call.ArgumentOffset];
            var dictionary = call.HasReceiver && call.IsPositionalOnly && call.ArgumentCount is 1 or 2 &&
                ReferenceEquals(target, ReceiverMethods.DictionaryGet)
                ? execution.Stack[start] as PyDict : null;
            var nativeGet = dictionary is not null && dictionary.CanReadKeySynchronously(argument!);
            var shape = NativeReturnShape.Other;
            var unary = call.IsPositionalOnly && call.ArgumentCount == 1
                ? SelectNativeUnaryOperation(target, argument, NativeReturnShape.Other, out shape) : null;
            if (invocation is null && shape == NativeReturnShape.Other) unary = null;
            if (!nativeGet && unary is null) return false;
            try
            {
                var result = nativeGet
                    ? InvokeDictionaryGet(dictionary!, argument!, call.ArgumentCount == 2
                        ? execution.Stack[start + call.ArgumentOffset + 1] : PyNone.Instance, call.CallSpan, scope)
                    : InvokeSingleInput(argument!, unary!, call.CallSpan, scope);
                PublishInstructionResult(ref execution, invocation, execution.Code.Instructions[execution.InstructionIndex], result);
                return true;
            }
            catch { execution.Stack.RetireTo(start); throw; }
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
                    var site = call.IsPositionalOnly && method is PyFunction { SupportsPositionalBinding: true } ? null : call;
                    var arguments = _execution.Stack.Slice(start + call.ArgumentOffset, call.ArgumentCount);
                    if (asynchronous) return CompleteCallValue(InvokeCallableFromOperandsAsync(method, arguments,
                        call.CallSpan, this, bound.Receiver, site), start);
                    result = InvokeCallableFromOperands(method, arguments, call.CallSpan, this, bound.Receiver, site);
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

        private async ValueTask<object> InvokeExecutableGeneralTargetAsync(ExecutableCallSite call, int start, object target, bool asynchronous)
        {
            if (target is ReceiverMethod method)
                target = method.Bind(_execution.Stack[start]);
            using var storage = ReserveExecutableCallArguments(call, this);
            var arguments = PrepareExecutableCallArguments(call, ref _execution.Stack, start, this);
            try
            {
                if (_preparedCallValue is PyInstance instance)
                {
                    _preparedCallValue = null;
                    return await InitializePreparedConstructorAsync(instance, arguments, call.CallSpan, this, asynchronous).ConfigureAwait(false);
                }
                return asynchronous
                    ? await InvokeCallableTargetAsync(target, call.TargetSpan, call.CallSpan, this, arguments).ConfigureAwait(false)
                    : InvokeCallableTarget(target, call.TargetSpan, call.CallSpan, this, arguments);
            }
            finally { ReturnCallArguments(arguments); }
        }
    }
}
