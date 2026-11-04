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
        private ValueTask ExecutePreparedOperationAsync(ExecutableInstruction instruction, bool asynchronous)
        {
            var operation = (ExecutableOperation)_codeObject.Constants[instruction.ConstantIndex]!;
            var span = instruction.Span;
            switch (operation)
            {
                case ExecutableStartComprehension start:
                {
                    var comprehensionOuter = _stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
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
                        _stack.PublishValue(instruction.ResultSlot, instruction.ResultSlot + 1, scope);
                        onStack = true;
                        _stack.PublishValue(instruction.ResultSlot + 1, instruction.ResultSlot + 2, result);
                        _stack.PublishValue(instruction.ResultSlot + 2, instruction.LiveExtent, comprehensionOuter);
                    }
                    catch { if (!onStack) scope.Dispose(); throw; }
                    break;
                }
                case ExecutableFinishComprehension:
                    var completed = _stack.TakeValue(instruction.InputStart + 1, instruction.InputStart + 1);
                    ((IExecutableTemporaryValue)_stack.TakeValue(instruction.InputStart, instruction.InputStart)).Dispose();
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, completed);
                    break;
                case ExecutableStartDisplay start:
                    object collection = start.Kind switch
                    {
                        ExecutableDisplayKind.Set => new PySet(_context.MemoryGovernor, span),
                        ExecutableDisplayKind.Dictionary => new PyDict(_context.MemoryGovernor, span),
                        _ => new PyList([], _context.MemoryGovernor, span),
                    };
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, collection);
                    break;
                case ExecutableFinishTuple:
                    var expanded = (PyList)_stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                    var tuple = expanded.Count == 0 ? PyTuple.Empty : new PyTuple(expanded, _context.MemoryGovernor, span);
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, tuple);
                    break;
                case ExecutableStartCall call:
                    var targetSlot = instruction.InputStart;
                    try { _stack[targetSlot] = new CallExpansion.CallArgumentAccumulator(call.ArgumentCount, _context, _stack[targetSlot], retained: true, deferSingleStar: call.DeferSingleStar); }
                    catch { _stack.RetireTo(targetSlot); throw; }
                    break;
                case ExecutableStartFormattedString:
                    var output = new GovernedByteBuilder(_context.MemoryGovernor, span);
                    try { _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, output); }
                    catch { output.Release(); throw; }
                    break;
                case ExecutableAppendFormattedText text:
                    ((GovernedByteBuilder)_stack[instruction.InputStart]).Append(text.Text);
                    break;
                case ExecutableFinishFormattedString:
                    var outputSlot = instruction.InputStart;
                    var formattedResult = ((GovernedByteBuilder)_stack[outputSlot]).ToPyStringAndRelease();
                    _stack.RetireTo(instruction.ResultSlot);
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, formattedResult);
                    break;
                // Route directly to the handler that owns the operation. In
                // particular, a target store must not enter display/call and
                // definition state machines just to reach their default arms.
                case ExecutableAppendDisplay or ExecutableStoreDictionaryItem or ExecutableAppendCall
                    or ExecutableFinishCall or ExecutableConvertFormattedValue or ExecutableFormatField
                    or ExecutableCreateGeneratorExpression:
                    return ExecutePreparedSuspendingOperationAsync(operation, instruction, asynchronous);
                case ExecutableAnnotationsEnabled or ExecutablePostponedAnnotation or ExecutableStoreModuleAnnotation
                    or ExecutableCreateFunction or ExecutableCreateLambda or ExecutableStartClassHeader or ExecutableCreateClass:
                    return ExecutePreparedDefinitionOperationAsync(operation, instruction, asynchronous);
                default:
                    return ExecutePreparedTargetOperationAsync(operation, instruction, asynchronous);
            }
            return ValueTask.CompletedTask;
        }

        private async ValueTask ExecutePreparedSuspendingOperationAsync(ExecutableOperation operation, ExecutableInstruction instruction, bool asynchronous)
        {
            var span = instruction.Span;
            switch (operation)
            {
                case ExecutableAppendDisplay append:
                    var value = _stack.TakeValue(instruction.ResultSlot, instruction.LiveExtent);
                    var destination = _stack[instruction.InputStart];
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
                            _context.CheckExecution(span);
                        }
                    }
                case ExecutableStoreDictionaryItem item:
                    var itemValue = _stack.TakeValue(instruction.ResultSlot + (item.Unpacking ? 0 : 1), instruction.ResultSlot + (item.Unpacking ? 0 : 1));
                    if (!item.Unpacking)
                    {
                        var key = _stack.TakeValue(instruction.ResultSlot, instruction.LiveExtent);
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
                        var dictionary = (PyDict)_stack[instruction.InputStart];
                        dictionary.SetItem(ValidateDictionaryKey(key, span), itemValue);
                        _context.CheckExecution(span);
                    }
                case ExecutableAppendCall argument:
                    var argumentSlot = instruction.ResultSlot;
                    try { await ((CallExpansion.CallArgumentAccumulator)_stack[instruction.InputStart]).AppendAsync(argument.Form, _stack[argumentSlot], span, asynchronous).ConfigureAwait(false); }
                    finally { _stack.RetireTo(argumentSlot); }
                    break;
                case ExecutableFinishCall call:
                    var accumulatorSlot = instruction.InputStart;
                    try
                    {
                        var accumulator = (CallExpansion.CallArgumentAccumulator)_stack[accumulatorSlot];
                        var result = await accumulator.InvokeAsync(call.TargetSpan, span, asynchronous).ConfigureAwait(false);
                        _stack.PublishValue(accumulatorSlot, accumulatorSlot + 1, result);
                    }
                    catch { _stack.RetireTo(accumulatorSlot); throw; }
                    break;
                case ExecutableConvertFormattedValue conversion:
                    var convertedField = await FormatSuspendedFieldAsync(_stack.TakeValue(instruction.InputStart, instruction.ResultSlot), conversion.Conversion,
                        null, _context, span, asynchronous).ConfigureAwait(false);
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, convertedField);
                    break;
                case ExecutableFormatField field:
                    var specifier = field.DynamicSpecifier ? ((PyString)_stack.TakeValue(instruction.ResultSlot + 1, instruction.ResultSlot + 1)).AsString() : field.FormatSpecifier;
                    var fieldValue = _stack.TakeValue(instruction.ResultSlot, instruction.ResultSlot);
                    if (field.Append)
                    {
                        var formattedDestination = (GovernedByteBuilder)_stack[instruction.InputStart];
                        if (fieldValue is PyInstance instance)
                            formattedDestination.Append(await FormatInstanceFieldAsync(instance, null, specifier, _context, span, asynchronous).ConfigureAwait(false));
                        else AppendInterpolatedStringPart(formattedDestination, fieldValue, null, specifier, _context, span, field.PreparedFormat);
                    }
                    else _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, await FormatSuspendedFieldAsync(fieldValue, null, specifier, _context, span, asynchronous, field.PreparedFormat).ConfigureAwait(false));
                    break;
                case ExecutableCreateGeneratorExpression comprehension:
                    var outer = _stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                    var generatorExpression = await PyGenerator.CreateExpressionAsync(comprehension.CodeObject, _context,
                        outer, comprehension.OuterSpan, span, asynchronous).ConfigureAwait(false);
                    _stack.PublishValue(instruction.ResultSlot, instruction.LiveExtent, generatorExpression);
                    break;
                default: throw new InvalidOperationException("Unknown prepared suspension operation.");
            }
        }
    }

    private static ValueTask<PyString> FormatSuspendedFieldAsync(object value, char? conversion, string? specifier,
        ExecutionContext _context, LythonSourceSpan span, bool asynchronous, InterpolatedFormatSpecifier? prepared = null)
        => value is PyInstance instance
            ? FormatInstanceFieldAsync(instance, conversion, specifier, _context, span, asynchronous)
            : new(FormatInterpolatedStringPart(value, conversion, specifier, _context, span, prepared));

    private static async ValueTask<PyString> FormatInstanceFieldAsync(PyInstance instance, char? conversion, string? specifier,
        ExecutionContext _context, LythonSourceSpan span, bool asynchronous)
    {
        object value = instance;
        var hookName = conversion is 'r' or 'a' ? "__repr__" : conversion == 's' ? "__str__" : "__format__";
        (bool Found, object Value) hook;
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
        return FormatInterpolatedStringPart(value, conversion, specifier, _context, span);
    }
}
