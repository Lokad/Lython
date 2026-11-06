using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        // The opcode has already read its evaluated target and offered it to the
        // linear frame runner. Both APIs use the same remaining call selection.
        private ValueTask ExecuteOrdinaryCall(ExecutableCallSite call, int start, object target, bool asynchronous)
        {
            try
            {
                object result;
                if (call.HasReceiver && ReferenceEquals(target, DirectListAppend))
                {
                    result = InvokeListAppend((PyList)_stack[start], _stack[start + call.ArgumentOffset], call.CallSpan, this);
                }
                else if (call.HasReceiver && ReferenceEquals(target, DirectFileWrite))
                {
                    result = InvokeFileWrite(_stack[start], _stack[start + call.ArgumentOffset], call.CallSpan, this);
                }
                else if (call.HasReceiver && ReferenceEquals(target, DirectCsvWriterow))
                {
                    if (asynchronous) return CompleteCallValue(InvokeCsvWriterowAsync((CsvWriterObject)_stack[start],
                        _stack[start + call.ArgumentOffset], call.CallSpan, this), start);
                    result = InvokeCsvWriterow((CsvWriterObject)_stack[start], _stack[start + call.ArgumentOffset], call.CallSpan, this);
                }
                else if (call.HasReceiver && _stack[start] is PyString stringReceiver &&
                    target is Func<PyString, LythonSourceSpan, ExecutionContext, object> stringOperation)
                {
                    result = InvokeSingleInput(stringReceiver, stringOperation, call.CallSpan, this);
                }
                else if (call.HasReceiver && _stack[start] is PyDict dictionaryReceiver &&
                    target is Func<PyDict, LythonSourceSpan, ExecutionContext, object> dictionaryOperation)
                {
                    result = InvokeSingleInput(dictionaryReceiver, dictionaryOperation, call.CallSpan, this);
                }
                else if (call.HasReceiver && ReferenceEquals(target, DirectStringJoin))
                {
                    if (asynchronous) return CompleteCallValue(InvokeStringJoinAsync((PyString)_stack[start],
                        _stack[start + call.ArgumentOffset], call.CallSpan, this), start);
                    result = InvokeStringJoin((PyString)_stack[start], _stack[start + call.ArgumentOffset], call.CallSpan, this);
                }
                else if (call.HasReceiver && ReferenceEquals(target, DirectStringSplit))
                {
                    var separator = call.ArgumentCount >= 1 ? _stack[start + call.ArgumentOffset] : null;
                    var count = call.ArgumentCount == 2 ? _stack[start + call.ArgumentOffset + 1] : null;
                    if (asynchronous && count is PyInstance)
                        return CompleteCallValue(InvokeStringSplitAsync((PyString)_stack[start], separator, count, call.CallSpan, this), start);
                    result = InvokeStringSplit((PyString)_stack[start], separator, count, call.CallSpan, this);
                }
                else if (call.HasReceiver && (ReferenceEquals(target, DirectStringStartsWith) || ReferenceEquals(target, DirectStringEndsWith)))
                {
                    var arguments = _stack.Slice(start + call.ArgumentOffset, call.ArgumentCount);
                    // Only guest bounds need an awaited protocol invocation.
                    if (asynchronous && ((arguments.Length >= 2 && arguments.Span[1] is PyInstance) ||
                        (arguments.Length == 3 && arguments.Span[2] is PyInstance)))
                        return CompleteCallValue(InvokeStringAffixFromOperandsAsync((PyString)_stack[start], arguments,
                            call.CallSpan, this, isStart: ReferenceEquals(target, DirectStringStartsWith)), start);
                    result = InvokeStringAffixFromOperands((PyString)_stack[start], arguments, call.CallSpan, this,
                        isStart: ReferenceEquals(target, DirectStringStartsWith));
                }
                else if (call.HasReceiver && ReferenceEquals(target, DirectStringReplace))
                {
                    result = InvokeStringReplace((PyString)_stack[start], _stack[start + call.ArgumentOffset],
                        _stack[start + call.ArgumentOffset + 1], call.ArgumentCount == 3 ? _stack[start + call.ArgumentOffset + 2] : null,
                        call.CallSpan, this);
                }
                else if (call.HasReceiver && ReferenceEquals(target, DirectDictionaryGet))
                {
                    var receiver = (PyDict)_stack[start];
                    var key = _stack[start + call.ArgumentOffset];
                    var fallback = call.ArgumentCount == 2 ? _stack[start + call.ArgumentOffset + 1] : PyNone.Instance;
                    if (asynchronous && !receiver.CanReadKeySynchronously(key))
                        return CompleteCallValue(InvokeDictionaryGetAsync(receiver, key, fallback, call.CallSpan, this), start);
                    result = InvokeDictionaryGet(receiver, key, fallback, call.CallSpan, this);
                }
                else if (call.HasReceiver && ReferenceEquals(target, DirectDictionarySetDefault))
                {
                    result = InvokeDictionarySetDefault((PyDict)_stack[start], _stack[start + call.ArgumentOffset],
                        call.ArgumentCount == 2 ? _stack[start + call.ArgumentOffset + 1] : PyNone.Instance, call.CallSpan, this);
                }
                else if (call.IsPositionalOnly && call.ArgumentCount == 1 &&
                    SelectNativeUnaryOperation(target, _stack[start + call.ArgumentOffset], NativeReturnShape.Other, out _) is { } nativeOperation)
                {
                    result = InvokeSingleInput(_stack[start + call.ArgumentOffset], nativeOperation, call.CallSpan, this);
                }
                else if (call.IsPositionalOnly && target is PyType type)
                {
                    var arguments = _stack.Slice(start + call.ArgumentOffset, call.ArgumentCount);
                    if (asynchronous) return CompleteCallValue(InvokeCallableFromOperandsAsync(type, arguments,
                        call.CallSpan, this, callSite: call), start);
                    result = InvokeCallableFromOperands(type, arguments, call.CallSpan, this, callSite: call);
                }
                else if (target is PyFunctionBase function)
                {
                    var site = call.IsPositionalOnly && function is PyExecutableFunction { SupportsDirectPositional: true } ? null : call;
                    var arguments = _stack.Slice(start + call.ArgumentOffset, call.ArgumentCount);
                    var receiver = call.HasReceiver && _stack[start] is not PyNone ? _stack[start] : null;
                    if (asynchronous) return CompleteCallValue(InvokeCallableFromOperandsAsync(function, arguments,
                        call.CallSpan, this, receiver, site), start);
                    result = InvokeCallableFromOperands(function, arguments, call.CallSpan, this, receiver, site);
                }
                else if (target is PyBoundMethod { Function: PyFunctionBase method } bound)
                {
                    var site = call.IsPositionalOnly && method is PyFunction { SupportsPositionalBinding: true } ? null : call;
                    var arguments = _stack.Slice(start + call.ArgumentOffset, call.ArgumentCount);
                    if (asynchronous) return CompleteCallValue(InvokeCallableFromOperandsAsync(method, arguments,
                        call.CallSpan, this, bound.Receiver, site), start);
                    result = InvokeCallableFromOperands(method, arguments, call.CallSpan, this, bound.Receiver, site);
                }
                else
                {
                    if (asynchronous) return CompleteCallValue(InvokeExecutableGeneralTargetAsync(call, start, target), start);
                    using var storage = ReserveExecutableCallArguments(call, this);
                    var arguments = PrepareExecutableCallArguments(call, ref _stack, start, this);
                    try { result = InvokeCallableTarget(target, call.TargetSpan, call.CallSpan, this, arguments); }
                    finally { ReturnCallArguments(arguments); }
                }
                PublishInstructionResult(_codeObject.Instructions[_instructionIndex], result);
                return default;
            }
            catch
            {
                _stack.RetireTo(start);
                throw;
            }
        }

        // Native results above travel as objects. Only potentially asynchronous
        // branches reach this completion helper; their operands stay borrowed.
        private ValueTask CompleteCallValue(ValueTask<object> pending, int start)
        {
            if (!pending.IsCompleted) return AwaitCallValueAsync(pending, start);
            PublishInstructionResult(_codeObject.Instructions[_instructionIndex], pending.GetAwaiter().GetResult());
            return default;
        }

        private async ValueTask AwaitCallValueAsync(ValueTask<object> pending, int start)
        {
            try { PublishInstructionResult(_codeObject.Instructions[_instructionIndex], await pending.ConfigureAwait(false)); }
            catch { _stack.RetireTo(start); throw; }
        }

        private async ValueTask<object> InvokeExecutableGeneralTargetAsync(ExecutableCallSite call, int start, object target)
        {
            using var storage = ReserveExecutableCallArguments(call, this);
            var arguments = PrepareExecutableCallArguments(call, ref _stack, start, this);
            try { return await InvokeCallableTargetAsync(target, call.TargetSpan, call.CallSpan, this, arguments).ConfigureAwait(false); }
            finally { ReturnCallArguments(arguments); }
        }
    }
}
