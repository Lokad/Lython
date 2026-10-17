using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

// Only interpreter-owned storage is disposed during operand-stack unwinding.
internal interface IExecutableTemporaryValue : IDisposable;

internal sealed partial class LythonRuntime
{
    private sealed partial class ExecutableFrameInterpreter
    {
        private async ValueTask ExecutePreparedOperationAsync(ExecutableOperation operation, LythonSourceSpan span, bool asynchronous)
        {
            switch (operation)
            {
                case ExecutableStartComprehension start:
                {
                    var comprehensionOuter = Pop(ref _stack, span);
                    var scope = _context.EnterComprehension(start.ScopeFacts);
                    var onStack = false;
                    try
                    {
                        object result = start.Kind switch
                        {
                            ExecutableDisplayKind.Set => new PySet(_context.MemoryGovernor, span),
                            ExecutableDisplayKind.Dictionary => new PyDict(_context.MemoryGovernor, span),
                            _ => new PyList([], _context.MemoryGovernor, span),
                        };
                        _context.ObserveValue(result, span);
                        _stack.Push(scope);
                        onStack = true;
                        _stack.Push(result);
                        _stack.Push(comprehensionOuter);
                    }
                    catch { if (!onStack) scope.Dispose(); throw; }
                    break;
                }
                case ExecutableFinishComprehension:
                    var completed = Pop(ref _stack, span);
                    ((IExecutableTemporaryValue)Pop(ref _stack, span)).Dispose();
                    _stack.Push(completed);
                    break;
                case ExecutableStartDisplay start:
                    object collection = start.Kind switch
                    {
                        ExecutableDisplayKind.Set => new PySet(_context.MemoryGovernor, span),
                        ExecutableDisplayKind.Dictionary => new PyDict(_context.MemoryGovernor, span),
                        _ => new PyList([], _context.MemoryGovernor, span),
                    };
                    _context.ObserveValue(collection, span);
                    _stack.Push(collection);
                    break;
                case ExecutableAppendDisplay append:
                    var value = Pop(ref _stack, span);
                    var destination = _stack[_stack.Count - 1 - append.IteratorDepth];
                    if (!append.Unpacking) Add(value);
                    else if (asynchronous)
                    {
                        await foreach (var item in ToSequenceAsync(value, span, _context).ConfigureAwait(false)) Add(item);
                    }
                    else foreach (var item in ToSequence(value, span, _context)) Add(item);
                    break;

                    void Add(object item)
                    {
                        _context.CheckExecution(span);
                        if (destination is PyList list) AddListDisplayValue(list, item, span, _context);
                        else if (destination is PySet set)
                        {
                            using var ambient = PyStructuralGuard.PushAmbient(_context, span);
                            set.Add(ValidateSetItem(item, span));
                            _context.ObserveCollectionCount(set.Count, span);
                        }
                        _context.ObserveValue(destination, span);
                    }
                case ExecutableStoreDictionaryItem item:
                    var itemValue = Pop(ref _stack, span);
                    if (!item.Unpacking)
                    {
                        var key = Pop(ref _stack, span);
                        Put(key, itemValue);
                    }
                    else if (itemValue is PyInstance mapping)
                    {
                        var keysMember = asynchronous
                            ? await TryResolveRuntimeMemberAsync(mapping, "keys", _context, span).ConfigureAwait(false)
                            : TryResolveRuntimeMember(mapping, "keys", _context, span, out var member)
                                ? (Found: true, Value: member) : (Found: false, Value: (object)PyNone.Instance);
                        if (!keysMember.Found) throw RuntimeErrors.Type("'" + RuntimeErrors.OperandTypeName(mapping) + "' object is not a mapping", span);
                        var keys = asynchronous
                            ? await InvokeCallableTargetAsync(keysMember.Value, span, span, _context, () => new ValueTask<CallArgumentValue[]>([])).ConfigureAwait(false)
                            : InvokeCallableTarget(keysMember.Value, span, span, _context, () => []);
                        var snapshot = new PyList([], _context.MemoryGovernor, span);
                        if (asynchronous)
                        {
                            await foreach (var key in ToSequenceAsync(keys, span, _context).ConfigureAwait(false))
                                AddListDisplayValue(snapshot, key, span, _context);
                        }
                        else foreach (var key in ToSequence(keys, span, _context)) AddListDisplayValue(snapshot, key, span, _context);
                        foreach (var key in snapshot)
                            Put(key, asynchronous ? await GetUserItemAsync(mapping, key, _context, span).ConfigureAwait(false)
                                : GetUserItem(mapping, key, _context, span));
                    }
                    else foreach (var pair in EnumerateMappingItems(itemValue, _context, span)) Put(pair.Key, pair.Value);
                    break;

                    void Put(object key, object itemValue)
                    {
                        _context.CheckExecution(span);
                        using var ambient = PyStructuralGuard.PushAmbient(_context, span);
                        var dictionary = (PyDict)_stack[_stack.Count - 1 - item.IteratorDepth];
                        dictionary.SetItem(ValidateDictionaryKey(key, span), itemValue);
                        _context.ObserveCollectionCount(dictionary.Count, span);
                        _context.ObserveValue(dictionary, span);
                    }
                case ExecutableFinishTuple:
                    var expanded = (PyList)Pop(ref _stack, span);
                    var tuple = expanded.Count == 0 ? PyTuple.Empty : new PyTuple(expanded, _context.MemoryGovernor, span);
                    PushObserved(tuple, span);
                    break;
                case ExecutableStartCall call:
                    var targetSlot = _stack.Count - 1;
                    try { _stack[targetSlot] = new CallExpansion.CallArgumentAccumulator(call.ArgumentCount, _context, _stack[targetSlot], retained: true, deferSingleStar: call.DeferSingleStar); }
                    catch { _stack.RemoveTail(_stack.Count - targetSlot); throw; }
                    break;
                case ExecutableAppendCall argument:
                    var argumentSlot = _stack.Count - 1;
                    try { await ((CallExpansion.CallArgumentAccumulator)_stack[argumentSlot - 1]).AppendAsync(argument.Form, _stack[argumentSlot], span, asynchronous).ConfigureAwait(false); }
                    finally { _stack.RemoveTail(_stack.Count - argumentSlot); }
                    break;
                case ExecutableFinishCall call:
                    var accumulatorSlot = _stack.Count - 1;
                    try
                    {
                        var accumulator = (CallExpansion.CallArgumentAccumulator)_stack[accumulatorSlot];
                        var result = await accumulator.InvokeAsync(call.TargetSpan, span, asynchronous).ConfigureAwait(false);
                        CompleteExecutableValueTransfer(ref _stack, accumulatorSlot, result, _context, span);
                    }
                    catch { _stack.RemoveTail(_stack.Count - accumulatorSlot); throw; }
                    break;
                case ExecutableConvertFormattedValue conversion:
                    PushObserved(await FormatSuspendedFieldAsync(Pop(ref _stack, span), conversion.Conversion,
                        null, _context, span, asynchronous).ConfigureAwait(false), span);
                    break;
                case ExecutableFormatField field:
                    var specifier = field.DynamicSpecifier ? ((PyString)Pop(ref _stack, span)).AsString() : field.FormatSpecifier;
                    var fieldValue = Pop(ref _stack, span);
                    PushObserved(await FormatSuspendedFieldAsync(fieldValue, null, specifier, _context, span, asynchronous).ConfigureAwait(false), span);
                    break;
                case ExecutableJoinFormattedParts join:
                    var builder = new GovernedByteBuilder(_context.MemoryGovernor, span);
                    try
                    {
                        var first = _stack.Count - join.Count;
                        for (var i = first; i < _stack.Count; i++) builder.Append((PyString)_stack[i]);
                        _stack.RemoveTail(join.Count);
                        var text = builder.ToPyStringAndRelease();
                        PushObserved(text, span);
                    }
                    finally { builder.Release(); }
                    break;
                case ExecutableCreateGeneratorExpression comprehension:
                    var outer = Pop(ref _stack, span);
                    PushObserved(await PyGenerator.CreateExpressionAsync(comprehension.CodeObject, _context,
                        outer, comprehension.OuterSpan, span, asynchronous).ConfigureAwait(false), span);
                    break;
                default: await ExecutePreparedDefinitionOperationAsync(operation, span, asynchronous).ConfigureAwait(false); break;
            }
        }
    }

    private static async ValueTask<PyString> FormatSuspendedFieldAsync(object value, char? conversion, string? specifier,
        ExecutionContext _context, LythonSourceSpan span, bool asynchronous)
    {
        if (value is PyInstance instance)
        {
            var hookName = conversion is 'r' or 'a' ? "__repr__" : conversion == 's' ? "__str__" : "__format__";
            (bool Found, object Value) hook;
            if (conversion is 's' or 'r' or 'a')
            {
                try
                {
                    hook = instance.Type.TryLookupInMro(hookName, 0, out var raw, out _)
                        ? (true, asynchronous
                            ? await PyAttributeLookup.BindForInstanceAsync(instance, raw, _context, span).ConfigureAwait(false)
                            : PyAttributeLookup.BindForInstance(instance, raw, _context, span))
                        : (false, PyNone.Instance);
                }
                catch (LythonRuntimeException error) when ((conversion is 'r' or 'a') && error.Attribution is null)
                {
                    _context.CheckExecution(span);
                    var fallback = instance.RenderObjectRepresentation();
                    if (conversion == 'a') fallback = EscapeNonAsciiPyString(fallback, _context, span);
                    return FormatInterpolatedStringPart(fallback, null, specifier, _context, span);
                }
            }
            else
            {
                hook = asynchronous ? await TryResolveRuntimeMemberAsync(instance, hookName, _context, span).ConfigureAwait(false)
                    : TryResolveRuntimeMember(instance, hookName, _context, span, out var member)
                        ? (true, member) : (false, PyNone.Instance);
            }
            if (hook.Found)
            {
                var arguments = conversion is null ? new[] { CallArgumentValue.Positional(PyString.FromString(specifier ?? "", _context.MemoryGovernor, span)) } : [];
                var rendered = asynchronous ? await InvokeCallableTargetAsync(hook.Value, span, span, _context,
                    () => new ValueTask<CallArgumentValue[]>(arguments)).ConfigureAwait(false)
                    : InvokeCallableTarget(hook.Value, span, span, _context, () => arguments);
                if (rendered is not PyString text) throw RuntimeErrors.Type(hookName + " returned non-string", span);
                if (conversion is null) return text;
                value = conversion == 'a' ? EscapeNonAsciiPyString(text, _context, span) : text;
                conversion = null;
            }
        }
        return FormatInterpolatedStringPart(value, conversion, specifier, _context, span);
    }
}
