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
                    var destination = Peek(ref _stack, span);
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
                        _context.Services.State.CallTemporaries.TrackFreshMutable(snapshot, snapshot.CommittedStorageBytes, span);
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
                        var dictionary = (PyDict)Peek(ref _stack, span);
                        dictionary.SetItem(ValidateDictionaryKey(key, span), itemValue);
                        _context.ObserveCollectionCount(dictionary.Count, span);
                        _context.ObserveValue(dictionary, span);
                    }
                case ExecutableFinishTuple:
                    var expanded = (PyList)Pop(ref _stack, span);
                    var tuple = new PyTuple(expanded, _context.MemoryGovernor, span);
                    _context.Services.State.CallTemporaries.TrackFreshMutable(tuple, tuple.CommittedStorageBytes, span);
                    PushObserved(tuple, span);
                    break;
                case ExecutableStartCall call:
                    _stack.Push(new CallExpansion.CallArgumentAccumulator(call.ArgumentCount, _context, Pop(ref _stack, span), retained: true, deferSingleStar: call.DeferSingleStar));
                    break;
                case ExecutableAppendCall argument:
                    var argumentValue = Pop(ref _stack, span);
                    await ((CallExpansion.CallArgumentAccumulator)Peek(ref _stack, span)).AppendAsync(argument.Form, argumentValue, span, asynchronous).ConfigureAwait(false);
                    break;
                case ExecutableFinishCall call:
                    using (var accumulator = (CallExpansion.CallArgumentAccumulator)Pop(ref _stack, span))
                        PushObserved(await accumulator.InvokeAsync(call.TargetSpan, span, asynchronous).ConfigureAwait(false), span);
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
                        _context.Services.State.CallTemporaries.TrackFreshString(text, span);
                        PushObserved(text, span);
                    }
                    finally { builder.Release(); }
                    break;
                case ExecutableCreateComprehension comprehension:
                    var outer = Pop(ref _stack, span);
                    var expression = CaptureOutermostIterable(comprehension.Expression, outer, _context, span);
                    PushObserved(asynchronous ? await EvaluateLoweredExpressionAsync(expression, _context).ConfigureAwait(false)
                        : EvaluateLoweredExpression(expression, _context), span);
                    SyncExecutableLocalsFromContext(_codeObject, _locals, _localCells, _context);
                    break;
                default: await ExecutePreparedDefinitionOperationAsync(operation, span, asynchronous).ConfigureAwait(false); break;
            }
        }
    }

    private sealed class CapturedComprehensionClauses(
        IReadOnlyList<LoweredComprehensionClause> source, LoweredComprehensionClause first) : IReadOnlyList<LoweredComprehensionClause>
    {
        public int Count => source.Count;
        public LoweredComprehensionClause this[int index] => index == 0 ? first : source[index];
        public IEnumerator<LoweredComprehensionClause> GetEnumerator()
        {
            for (var i = 0; i < Count; i++) yield return this[i];
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static LoweredExpression CaptureOutermostIterable(LoweredExpression expression, object value, ExecutionContext _context, LythonSourceSpan span)
    {
        IReadOnlyList<LoweredComprehensionClause> Capture(IReadOnlyList<LoweredComprehensionClause> clauses)
        {
            // One small overlay retains the computed iterable; clause metadata
            // stays shared with the compiled script regardless of clause count.
            const long bytes = 256;
            _context.MemoryGovernor.Reserve(bytes, span);
            _context.MemoryGovernor.Commit(bytes);
            var overlay = new CapturedComprehensionClauses(clauses,
                clauses[0] with { Iterable = new LoweredCapturedExpression(clauses[0].Iterable.Syntax, value) });
            _context.Services.State.CallTemporaries.TrackFreshMutable(overlay, bytes, span);
            return overlay;
        }
        return expression switch
        {
            LoweredListComprehensionExpression c => c with { Clauses = Capture(c.Clauses) },
            LoweredSetComprehensionExpression c => c with { Clauses = Capture(c.Clauses) },
            LoweredDictComprehensionExpression c => c with { Clauses = Capture(c.Clauses) },
            LoweredGeneratorExpression c => c with { Clauses = Capture(c.Clauses) },
            _ => throw new InvalidOperationException("Expected comprehension."),
        };
    }

    private static async ValueTask<PyString> FormatSuspendedFieldAsync(object value, char? conversion, string? specifier,
        ExecutionContext _context, LythonSourceSpan span, bool asynchronous)
    {
        if (value is PyInstance instance)
        {
            var hookName = conversion is 'r' or 'a' ? "__repr__" : conversion == 's' ? "__str__" : "__format__";
            var hook = asynchronous ? await TryResolveRuntimeMemberAsync(instance, hookName, _context, span).ConfigureAwait(false)
                : TryResolveRuntimeMember(instance, hookName, _context, span, out var member)
                    ? (Found: true, Value: member) : (Found: false, Value: (object)PyNone.Instance);
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
