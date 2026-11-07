using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

// Only interpreter-owned storage is disposed during operand-stack unwinding.
internal abstract class ExecutableTemporaryValue : IDisposable
{
    public abstract void Dispose();
}

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        // Formatting shares the ordinary operand slots and cleanup. A dynamic
        // field asks the existing driver for its function context at that exact
        // instruction, without repeating earlier fields or their allocations.
        private static bool TryExecuteImmediateFormatting(ref ExecutableWorkState execution, ExecutionContext scope,
            ExecutionContext? invocation, in ExecutableInstruction instruction)
        {
            var operation = (ExecutableOperation)execution.Code.Constants[instruction.ConstantIndex]!;
            var context = invocation ?? scope;
            var span = instruction.Span;
            switch (operation)
            {
                case ExecutableStartFormattedString formatted:
                    var output = new GovernedByteBuilder(context.MemoryGovernor, span, formatted.InitialCapacity,
                        context.State.CancellationToken);
                    try { PublishInstructionResult(ref execution, invocation, instruction, output); }
                    catch { output.Release(); throw; }
                    return true;
                case ExecutableAppendFormattedText text:
                    ((GovernedByteBuilder)execution.Stack[instruction.InputStart]).Append(text.Text);
                    return true;
                case ExecutableFinishFormattedString:
                    var formattedResult = ((GovernedByteBuilder)execution.Stack[instruction.InputStart]).ToPyStringAndRelease();
                    execution.Stack.RetireTo(instruction.ResultSlot);
                    PublishInstructionResult(ref execution, invocation, instruction, formattedResult);
                    return true;
                case ExecutableConvertFormattedValue conversion when IsImmediateFormattedValue(execution.Stack[instruction.InputStart]):
                    var converted = FormatInterpolatedStringPart(execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot),
                        conversion.Conversion, null, context, span);
                    PublishInstructionResult(ref execution, invocation, instruction, converted);
                    return true;
                case ExecutableFormatField { DynamicSpecifier: false } field when IsImmediateFormattedValue(execution.Stack[instruction.ResultSlot]):
                    var value = execution.Stack.TakeValue(instruction.ResultSlot, instruction.ResultSlot);
                    if (field.Append)
                        AppendInterpolatedStringPart((GovernedByteBuilder)execution.Stack[instruction.InputStart], value,
                            null, field.FormatSpecifier, context, span, field.PreparedFormat);
                    else PublishInstructionResult(ref execution, invocation, instruction,
                        FormatInterpolatedStringPart(value, null, field.FormatSpecifier, context, span, field.PreparedFormat));
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsImmediateFormattedValue(object value)
            => value is PyNone or BigInteger or double or bool or PyString or PyBytes;

        private ValueTask ExecutePreparedOperationAsync(ExecutableInstruction instruction, bool asynchronous)
        {
            var operation = (ExecutableOperation)_execution.Code.Constants[instruction.ConstantIndex]!;
            var span = instruction.Span;
            switch (operation)
            {
                case ExecutableStartComprehension start:
                {
                    var comprehensionOuter = _execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                    var scope = EnterComprehension(start.ScopeFacts);
                    var onStack = false;
                    try
                    {
                        object result = start.Kind switch
                        {
                            ExecutableDisplayKind.Set => new PySet(MemoryGovernor, span),
                            ExecutableDisplayKind.Dictionary => new PyDict(MemoryGovernor, span),
                            _ => new PyList([], MemoryGovernor, span),
                        };
                        _execution.Stack.PublishValue(instruction.ResultSlot, instruction.ResultSlot + 1, scope);
                        onStack = true;
                        _execution.Stack.PublishValue(instruction.ResultSlot + 1, instruction.ResultSlot + 2, result);
                        _execution.Stack.PublishValue(instruction.ResultSlot + 2, instruction.LiveExtent, comprehensionOuter);
                    }
                    catch { if (!onStack) scope.Dispose(); throw; }
                    break;
                }
                case ExecutableFinishComprehension:
                    var completed = _execution.Stack.TakeValue(instruction.InputStart + 1, instruction.InputStart + 1);
                    ((ExecutableTemporaryValue)_execution.Stack.TakeValue(instruction.InputStart, instruction.InputStart)).Dispose();
                    PublishInstructionResult(instruction, completed);
                    break;
                case ExecutableStartDisplay start:
                    object collection = start.Kind switch
                    {
                        ExecutableDisplayKind.Set => new PySet(MemoryGovernor, span),
                        ExecutableDisplayKind.Dictionary => new PyDict(MemoryGovernor, span),
                        _ => new PyList([], MemoryGovernor, span),
                    };
                    PublishInstructionResult(instruction, collection);
                    break;
                case ExecutableFinishTuple:
                    var expanded = (PyList)_execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                    var tuple = expanded.Count == 0 ? PyTuple.Empty : new PyTuple(expanded, MemoryGovernor, span);
                    PublishInstructionResult(instruction, tuple);
                    break;
                case ExecutableStartCall call:
                    var targetSlot = instruction.InputStart;
                    try { _execution.Stack[targetSlot] = new CallExpansion.CallArgumentAccumulator(call.ArgumentCount, this, _execution.Stack[targetSlot], retained: true, deferSingleStar: call.DeferSingleStar); }
                    catch { _execution.Stack.RetireTo(targetSlot); throw; }
                    break;
                // Display/call and definition operations retain their own
                // suspension paths. Targets use ordinary opcodes instead.
                case ExecutableAppendDisplay or ExecutableStoreDictionaryItem or ExecutableAppendCall
                    or ExecutableFinishCall or ExecutableConvertFormattedValue or ExecutableFormatField
                    or ExecutableCreateGeneratorExpression:
                    return ExecutePreparedSuspendingOperationAsync(operation, instruction, asynchronous);
                case ExecutableAnnotationsEnabled or ExecutablePostponedAnnotation or ExecutableStoreModuleAnnotation
                    or ExecutableCreateFunction or ExecutableCreateLambda or ExecutableStartClassHeader or ExecutableCreateClass:
                    return ExecutePreparedDefinitionOperationAsync(operation, instruction, asynchronous);
                default: throw new InvalidOperationException("Unknown prepared operation.");
            }
            return ValueTask.CompletedTask;
        }

        private async ValueTask ExecutePreparedSuspendingOperationAsync(ExecutableOperation operation, ExecutableInstruction instruction, bool asynchronous)
        {
            var span = instruction.Span;
            switch (operation)
            {
                case ExecutableAppendDisplay append:
                    var value = _execution.Stack.TakeValue(instruction.ResultSlot, instruction.LiveExtent);
                    var destination = _execution.Stack[instruction.InputStart];
                    if (!append.Unpacking) Add(value);
                    else if (asynchronous)
                    {
                        await foreach (var item in ToSequenceAsync(value, span, this).ConfigureAwait(false)) Add(item);
                    }
                    else foreach (var item in ToSequence(value, span, this)) Add(item);
                    break;

                    void Add(object item)
                    {
                        CheckExecution(span);
                        if (destination is PyList list) AddListDisplayValue(list, item, span, this);
                        else if (destination is PySet set)
                        {
                            using var ambient = PyStructuralGuard.PushAmbient(this, span);
                            set.Add(ValidateSetItem(item, span));
                            CheckExecution(span);
                        }
                    }
                case ExecutableStoreDictionaryItem item:
                    var itemValue = _execution.Stack.TakeValue(instruction.ResultSlot + (item.Unpacking ? 0 : 1), instruction.ResultSlot + (item.Unpacking ? 0 : 1));
                    if (!item.Unpacking)
                    {
                        var key = _execution.Stack.TakeValue(instruction.ResultSlot, instruction.LiveExtent);
                        Put(key, itemValue);
                    }
                    else if (itemValue is PyInstance mapping)
                    {
                        var keysMember = asynchronous
                            ? await TryResolveRuntimeMemberAsync(mapping, "keys", this, span).ConfigureAwait(false)
                            : TryResolveRuntimeMember(mapping, "keys", this, span, out var member)
                                ? (Found: true, Value: member) : (Found: false, Value: (object)PyNone.Instance);
                        if (!keysMember.Found) throw RuntimeErrors.Type("'" + RuntimeErrors.OperandTypeName(mapping) + "' object is not a mapping", span);
                        var keys = asynchronous
                            ? await InvokeCallableTargetAsync(keysMember.Value, span, span, this, () => new ValueTask<CallArgumentValue[]>([])).ConfigureAwait(false)
                            : InvokeCallableTarget(keysMember.Value, span, span, this, () => []);
                        var snapshot = new PyList([], MemoryGovernor, span);
                        if (asynchronous)
                        {
                            await foreach (var key in ToSequenceAsync(keys, span, this).ConfigureAwait(false))
                                AddListDisplayValue(snapshot, key, span, this);
                        }
                        else foreach (var key in ToSequence(keys, span, this)) AddListDisplayValue(snapshot, key, span, this);
                        foreach (var key in snapshot)
                            Put(key, asynchronous ? await GetUserItemAsync(mapping, key, this, span).ConfigureAwait(false)
                                : GetUserItem(mapping, key, this, span));
                    }
                    else foreach (var pair in EnumerateMappingItems(itemValue, this, span)) Put(pair.Key, pair.Value);
                    break;

                    void Put(object key, object itemValue)
                    {
                        CheckExecution(span);
                        using var ambient = PyStructuralGuard.PushAmbient(this, span);
                        var dictionary = (PyDict)_execution.Stack[instruction.InputStart];
                        dictionary.SetItem(ValidateDictionaryKey(key, span), itemValue);
                        CheckExecution(span);
                    }
                case ExecutableAppendCall argument:
                    var argumentSlot = instruction.ResultSlot;
                    try { await ((CallExpansion.CallArgumentAccumulator)_execution.Stack[instruction.InputStart]).AppendAsync(argument.Form, _execution.Stack[argumentSlot], span, asynchronous).ConfigureAwait(false); }
                    finally { _execution.Stack.RetireTo(argumentSlot); }
                    break;
                case ExecutableFinishCall call:
                    var accumulatorSlot = instruction.InputStart;
                    try
                    {
                        var accumulator = (CallExpansion.CallArgumentAccumulator)_execution.Stack[accumulatorSlot];
                        var result = await accumulator.InvokeAsync(call.TargetSpan, span, asynchronous).ConfigureAwait(false);
                        PublishInstructionResult(instruction, result);
                    }
                    catch { _execution.Stack.RetireTo(accumulatorSlot); throw; }
                    break;
                case ExecutableConvertFormattedValue conversion:
                    var convertedField = await FormatSuspendedFieldAsync(_execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot), conversion.Conversion,
                        null, this, span, asynchronous).ConfigureAwait(false);
                    PublishInstructionResult(instruction, convertedField);
                    break;
                case ExecutableFormatField field:
                    var specifier = field.DynamicSpecifier ? ((PyString)_execution.Stack.TakeValue(instruction.ResultSlot + 1, instruction.ResultSlot + 1)).AsString() : field.FormatSpecifier;
                    var fieldValue = _execution.Stack.TakeValue(instruction.ResultSlot, instruction.ResultSlot);
                    if (field.Append)
                    {
                        var formattedDestination = (GovernedByteBuilder)_execution.Stack[instruction.InputStart];
                        if (fieldValue is PyInstance instance)
                            formattedDestination.Append(await FormatInstanceFieldAsync(instance, null, specifier, this, span, asynchronous).ConfigureAwait(false));
                        else AppendInterpolatedStringPart(formattedDestination, fieldValue, null, specifier, this, span, field.PreparedFormat);
                    }
                    else PublishInstructionResult(instruction, await FormatSuspendedFieldAsync(fieldValue, null, specifier, this, span, asynchronous, field.PreparedFormat).ConfigureAwait(false));
                    break;
                case ExecutableCreateGeneratorExpression comprehension:
                    var outer = _execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                    var generatorExpression = await PyGenerator.CreateExpressionAsync(comprehension.CodeObject, this,
                        outer, comprehension.OuterSpan, span, asynchronous).ConfigureAwait(false);
                    PublishInstructionResult(instruction, generatorExpression);
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
