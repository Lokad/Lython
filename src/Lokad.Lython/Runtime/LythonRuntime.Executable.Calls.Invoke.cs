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
            var argument = call.ArgumentCount == 0 ? null : execution.Stack[start + call.ArgumentOffset];
            var dictionary = call.HasReceiver && ReferenceEquals(target, DirectDictionaryGet)
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
                if (call.HasReceiver && ReferenceEquals(target, DirectListAppend))
                {
                    result = InvokeListAppend((PyList)_execution.Stack[start], _execution.Stack[start + call.ArgumentOffset], call.CallSpan, this);
                }
                else if (call.HasReceiver && ReferenceEquals(target, DirectFileWrite))
                {
                    result = InvokeFileWrite(_execution.Stack[start], _execution.Stack[start + call.ArgumentOffset], call.CallSpan, this);
                }
                else if (call.HasReceiver && ReferenceEquals(target, DirectCsvWriterow))
                {
                    if (asynchronous) return CompleteCallValue(InvokeCsvWriterowAsync((CsvWriterObject)_execution.Stack[start],
                        _execution.Stack[start + call.ArgumentOffset], call.CallSpan, this), start);
                    result = InvokeCsvWriterow((CsvWriterObject)_execution.Stack[start], _execution.Stack[start + call.ArgumentOffset], call.CallSpan, this);
                }
                else if (call.HasReceiver && _execution.Stack[start] is PyString stringReceiver &&
                    target is Func<PyString, LythonSourceSpan, ExecutionContext, object> stringOperation)
                {
                    result = InvokeSingleInput(stringReceiver, stringOperation, call.CallSpan, this);
                }
                else if (call.HasReceiver && _execution.Stack[start] is PyDict dictionaryReceiver &&
                    target is Func<PyDict, LythonSourceSpan, ExecutionContext, object> dictionaryOperation)
                {
                    result = InvokeSingleInput(dictionaryReceiver, dictionaryOperation, call.CallSpan, this);
                }
                else if (call.HasReceiver && ReferenceEquals(target, DirectStringJoin))
                {
                    if (asynchronous) return CompleteCallValue(InvokeStringJoinAsync((PyString)_execution.Stack[start],
                        _execution.Stack[start + call.ArgumentOffset], call.CallSpan, this), start);
                    result = InvokeStringJoin((PyString)_execution.Stack[start], _execution.Stack[start + call.ArgumentOffset], call.CallSpan, this);
                }
                else if (call.HasReceiver && ReferenceEquals(target, DirectStringSplit))
                {
                    var separator = call.ArgumentCount >= 1 ? _execution.Stack[start + call.ArgumentOffset] : null;
                    var count = call.ArgumentCount == 2 ? _execution.Stack[start + call.ArgumentOffset + 1] : null;
                    if (asynchronous && count is PyInstance)
                        return CompleteCallValue(InvokeStringSplitAsync((PyString)_execution.Stack[start], separator, count, call.CallSpan, this), start);
                    result = InvokeStringSplit((PyString)_execution.Stack[start], separator, count, call.CallSpan, this);
                }
                else if (call.HasReceiver && (ReferenceEquals(target, DirectStringStartsWith) || ReferenceEquals(target, DirectStringEndsWith)))
                {
                    var arguments = _execution.Stack.Slice(start + call.ArgumentOffset, call.ArgumentCount);
                    // Only guest bounds need an awaited protocol invocation.
                    if (asynchronous && ((arguments.Length >= 2 && arguments.Span[1] is PyInstance) ||
                        (arguments.Length == 3 && arguments.Span[2] is PyInstance)))
                        return CompleteCallValue(InvokeStringAffixFromOperandsAsync((PyString)_execution.Stack[start], arguments,
                            call.CallSpan, this, isStart: ReferenceEquals(target, DirectStringStartsWith)), start);
                    result = InvokeStringAffixFromOperands((PyString)_execution.Stack[start], arguments, call.CallSpan, this,
                        isStart: ReferenceEquals(target, DirectStringStartsWith));
                }
                else if (call.HasReceiver && ReferenceEquals(target, DirectStringReplace))
                {
                    result = InvokeStringReplace((PyString)_execution.Stack[start], _execution.Stack[start + call.ArgumentOffset],
                        _execution.Stack[start + call.ArgumentOffset + 1], call.ArgumentCount == 3 ? _execution.Stack[start + call.ArgumentOffset + 2] : null,
                        call.CallSpan, this);
                }
                else if (call.HasReceiver && ReferenceEquals(target, DirectDictionaryGet))
                {
                    var receiver = (PyDict)_execution.Stack[start];
                    var key = _execution.Stack[start + call.ArgumentOffset];
                    var fallback = call.ArgumentCount == 2 ? _execution.Stack[start + call.ArgumentOffset + 1] : PyNone.Instance;
                    if (asynchronous && !receiver.CanReadKeySynchronously(key))
                        return CompleteCallValue(InvokeDictionaryGetAsync(receiver, key, fallback, call.CallSpan, this), start);
                    result = InvokeDictionaryGet(receiver, key, fallback, call.CallSpan, this);
                }
                else if (call.HasReceiver && ReferenceEquals(target, DirectDictionarySetDefault))
                {
                    result = InvokeDictionarySetDefault((PyDict)_execution.Stack[start], _execution.Stack[start + call.ArgumentOffset],
                        call.ArgumentCount == 2 ? _execution.Stack[start + call.ArgumentOffset + 1] : PyNone.Instance, call.CallSpan, this);
                }
                else if (target is PyFunctionBase function)
                {
                    var site = call.IsPositionalOnly && function is PyExecutableFunction { SupportsDirectPositional: true } ? null : call;
                    var arguments = _execution.Stack.Slice(start + call.ArgumentOffset, call.ArgumentCount);
                    var receiver = call.HasReceiver && _execution.Stack[start] is not PyNone ? _execution.Stack[start] : null;
                    if (asynchronous) return CompleteCallValue(InvokeCallableFromOperandsAsync(function, arguments,
                        call.CallSpan, this, receiver, site), start);
                    result = InvokeCallableFromOperands(function, arguments, call.CallSpan, this, receiver, site);
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
