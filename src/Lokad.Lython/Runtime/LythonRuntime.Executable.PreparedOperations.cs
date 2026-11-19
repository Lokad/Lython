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
        private static bool TryExecuteFormattedInstruction(ref ExecutableWorkState execution,
            ExecutionContext? invocation, in ExecutableInstruction instruction, ExecutionContext context)
        {
            ExecutableFormatField? field = null;
            object? value = null;
            string? specifier = null;
            if (instruction.OpCode is ExecutableOpCode.ConvertFormattedValue or ExecutableOpCode.FormatField)
            {
                var source = instruction.OpCode == ExecutableOpCode.ConvertFormattedValue
                    ? instruction.InputStart : instruction.ResultSlot;
                if (!IsImmediateFormattedValue(execution.Stack[source])) return false;
                if (instruction.OpCode == ExecutableOpCode.FormatField)
                {
                    field = (ExecutableFormatField)execution.Code.Constants[instruction.ConstantIndex]!;
                    specifier = GetExecutableFormatSpecifier(field, field.DynamicSpecifier
                        ? execution.Stack.TakeValue(instruction.ResultSlot + 1, instruction.ResultSlot + 1) : null);
                }
                value = execution.Stack.TakeValue(source, instruction.ResultSlot);
            }
            else if (instruction.OpCode == ExecutableOpCode.AppendFormattedText)
                value = execution.Code.Constants[instruction.ConstantIndex];
            var output = instruction.OpCode is ExecutableOpCode.AppendFormattedText or ExecutableOpCode.FinishFormattedString
                || field?.Append == true ? (GovernedByteBuilder)execution.Stack[instruction.InputStart] : null;
            var result = ExecuteBuiltinFormattedInstruction(instruction, output, value, specifier, field, context);
            if (instruction.OpCode == ExecutableOpCode.FinishFormattedString)
                execution.Stack.RetireTo(instruction.ResultSlot);
            if (result is not null)
            {
                try { PublishInstructionResult(ref execution, invocation, instruction, result); }
                catch { if (result is ExecutableTemporaryValue owned) owned.Dispose(); throw; }
            }
            return true;
        }

        // Only actual protocol values need the function context/async driver.
        // Already-evaluated native values and dynamic specs remain in the prefix.
        private async ValueTask ExecuteFormattedValueAsync(ExecutableInstruction instruction, bool asynchronous)
        {
            var span = instruction.Span;
            if (instruction.OpCode == ExecutableOpCode.ConvertFormattedValue)
            {
                var converted = await FormatSuspendedFieldAsync(
                    _execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot),
                    instruction.FormattedConversion, null, this, span, asynchronous).ConfigureAwait(false);
                PublishInstructionResult(instruction, converted);
                return;
            }
            var field = (ExecutableFormatField)_execution.Code.Constants[instruction.ConstantIndex]!;
            var specifier = field.DynamicSpecifier
                ? ((PyString)_execution.Stack.TakeValue(instruction.ResultSlot + 1, instruction.ResultSlot + 1)).AsString()
                : field.FormatSpecifier;
            var value = _execution.Stack.TakeValue(instruction.ResultSlot, instruction.ResultSlot);
            if (field.Append)
            {
                var output = (GovernedByteBuilder)_execution.Stack[instruction.InputStart];
                if (value is PyInstance instance)
                    output.Append(await FormatInstanceFieldAsync(instance, null, specifier, this, span, asynchronous).ConfigureAwait(false));
                else AppendInterpolatedStringPart(output, value, null, specifier, this, span, field.PreparedFormat);
            }
            else PublishInstructionResult(instruction,
                await FormatSuspendedFieldAsync(value, null, specifier, this, span, asynchronous, field.PreparedFormat).ConfigureAwait(false));
        }

        private static bool TryExecutePreparedOperation(ref ExecutableWorkState execution,
            ExecutionContext scope, ExecutionContext? invocation, in ExecutableInstruction instruction,
            bool asynchronous, out ValueTask pending)
        {
            pending = default;
            var context = invocation ?? scope;
            var operation = (ExecutableOperation)execution.Code.Constants[instruction.ConstantIndex]!;
            var span = instruction.Span;
            switch (operation)
            {
                case ExecutableStartComprehension start when invocation is not null:
                {
                    var comprehensionOuter = execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                    var comprehension = invocation.EnterComprehension(start.ScopeFacts);
                    var onStack = false;
                    try
                    {
                        object result = start.Kind switch
                        {
                            ExecutableDisplayKind.Set => new PySet(context.MemoryGovernor, span),
                            ExecutableDisplayKind.Dictionary => new PyDict(context.MemoryGovernor, span),
                            _ => new PyList([], context.MemoryGovernor, span),
                        };
                        execution.Stack.PublishValue(instruction.ResultSlot, instruction.ResultSlot + 1, comprehension);
                        onStack = true;
                        execution.Stack.PublishValue(instruction.ResultSlot + 1, instruction.ResultSlot + 2, result);
                        execution.Stack.PublishValue(instruction.ResultSlot + 2, instruction.LiveExtent, comprehensionOuter);
                    }
                    catch { if (!onStack) comprehension.Dispose(); throw; }
                    break;
                }
                case ExecutableFinishComprehension when invocation is not null:
                    var completed = execution.Stack.TakeValue(instruction.InputStart + 1, instruction.InputStart + 1);
                    ((ExecutableTemporaryValue)execution.Stack.TakeValue(instruction.InputStart, instruction.InputStart)).Dispose();
                    PublishInstructionResult(ref execution, invocation, instruction, completed);
                    break;
                case ExecutableStartDisplay start when invocation is not null:
                    object collection = start.Kind switch
                    {
                        ExecutableDisplayKind.Set => new PySet(context.MemoryGovernor, span),
                        ExecutableDisplayKind.Dictionary => new PyDict(context.MemoryGovernor, span),
                        _ => new PyList([], context.MemoryGovernor, span),
                    };
                    PublishInstructionResult(ref execution, invocation, instruction, collection);
                    break;
                case ExecutableFinishTuple when invocation is not null:
                    var expanded = (PyList)execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                    var tuple = expanded.Count == 0 ? PyTuple.Empty : new PyTuple(expanded, context.MemoryGovernor, span);
                    PublishInstructionResult(ref execution, invocation, instruction, tuple);
                    break;
                case ExecutableStartCall call when invocation is not null:
                    var targetSlot = instruction.InputStart;
                    try { execution.Stack[targetSlot] = new CallExpansion.CallArgumentAccumulator(call.ArgumentCount, context, execution.Stack[targetSlot], deferSingleStar: call.DeferSingleStar); }
                    catch { execution.Stack.RetireTo(targetSlot); throw; }
                    break;
                // Display and call operations with a suspension path.
                case ExecutableAppendDisplay or ExecutableStoreDictionaryItem or ExecutableAppendCall
                    or ExecutableFinishCall or ExecutableCallWithKeywordMapping
                    or ExecutableCreateGeneratorExpression when invocation is not null:
                    pending = invocation.ExecutePreparedSuspendingOperationAsync(operation, instruction, asynchronous);
                    break;
                case ExecutableAnnotationsEnabled:
                    PublishInstructionResult(ref execution, invocation, instruction, !context.PostponedAnnotations);
                    break;
                case ExecutablePostponedAnnotation postponed when invocation is not null:
                    PublishInstructionResult(ref execution, invocation, instruction, CreatePostponedAnnotationValue(postponed.Annotation, context));
                    break;
                case ExecutableStoreModuleAnnotation annotation when invocation is not null:
                    var value = execution.Stack.TakeValue(instruction.InputStart, instruction.LiveExtent);
                    pending = StoreAnnotationEntryAsync(ResolveName("__annotations__", span, context),
                        annotation.Name, value, context, span, asynchronous);
                    break;
                case ExecutableStartFunctionDefaults start:
                    var owner = new PendingFunctionDefaults(start.Binding.ParameterLayout, context, span,
                        ref execution, start.Binding.KeywordRecordCache);
                    try { PublishInstructionResult(ref execution, invocation, instruction, owner); }
                    catch { owner.Dispose(); throw; }
                    break;
                case ExecutableCreateFunction function:
                    int[]? functionCaptures = null;
                    if (invocation is null)
                    {
                        var binding = function.Binding;
                        if (binding.TypeParameters is not null ||
                            binding.DecoratorCount != 0 || !function.AnnotationsOnStack && binding.Annotations.Length != 0 ||
                            HasTypeParameterScope(scope) || !CanDefineWithoutInvocation(binding.CodeObject,
                                in execution, scope, span, out functionCaptures)) return false;
                    }
                    pending = CreateExecutableFunction(ref execution, context, invocation, function, instruction,
                        asynchronous, functionCaptures);
                    break;
                case ExecutableCreateLambda lambda:
                    {
                        int[]? lambdaCaptures = null;
                        if (invocation is null && (lambda.Expression.NamespaceClosure || !CanDefineWithoutInvocation(
                            lambda.CodeObject, in execution, scope, span, out lambdaCaptures)))
                            return false;
                        var layout = lambda.Expression.ParameterLayout;
                        using var defaults = layout.Defaults.Length == 0 ? null
                            : new PendingFunctionDefaults(layout, context, span, ref execution, lambda.KeywordRecordCache);
                        var first = instruction.InputStart;
                        PyFunctionBase function;
                        try
                        {
                            defaults?.Complete(execution.Stack.Values.AsSpan(first, layout.Defaults.Length));
                            function = invocation is null
                                ? CreateFunctionValue(layout, lambda.CodeObject, GetGlobalContext(context),
                                    CaptureExecutableCells(lambda.CodeObject, context, span, ref execution, lambdaCaptures),
                                    defaults?.Values ?? default, lambda.Expression.IsGenerator, context, span, PythonCallableKind.Lambda)
                                : CreateLambdaFunction(lambda.Expression, defaults?.Values ?? default, context);
                        }
                        finally { execution.Stack.RetireTo(instruction.ResultSlot); }
                        defaults?.Publish();
                        PublishInstructionResult(ref execution, invocation, instruction, function);
                    }
                    break;
                case ExecutableStartClassHeader header when invocation is not null:
                    PublishInstructionResult(ref execution, invocation, instruction, new CallExpansion.CallArgumentAccumulator(header.Count, context, ClassHeaderExpansionTarget));
                    break;
                case ExecutableCreateClass create when invocation is not null:
                    pending = invocation.ExecutePreparedClassDefinitionAsync(create, instruction.InputStart, span, asynchronous);
                    break;
                case ExecutableOperation when invocation is null: return false;
                default: throw new InvalidOperationException("Unknown prepared operation.");
            }
            return true;
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
                        await foreach (var item in PyIteration.Cursor.ForAsyncEnumeration(value, span, this).ConfigureAwait(false))
                            Add(item);
                    }
                    else foreach (var item in PyIteration.Cursor.Create(value, span, this))
                             Add(item);
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
                            await foreach (var key in PyIteration.Cursor.ForAsyncEnumeration(keys, span, this).ConfigureAwait(false))
                                AddListDisplayValue(snapshot, key, span, this);
                        }
                        else foreach (var key in PyIteration.Cursor.Create(keys, span, this))
                                 AddListDisplayValue(snapshot, key, span, this);
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
                {
                    object result;
                    using (var accumulator = (CallExpansion.CallArgumentAccumulator)_execution.Stack.TakeValue(
                        instruction.InputStart, instruction.ResultSlot))
                        result = await accumulator.InvokeAsync(call.TargetSpan, span, asynchronous).ConfigureAwait(false);
                    PublishInstructionResult(instruction, result);
                    break;
                }
                case ExecutableCallWithKeywordMapping call:
                {
                    var first = instruction.InputStart;
                    try
                    {
                        var target = _execution.Stack[first];
                        var positional = _execution.Stack.Slice(first + 1, call.PositionalCount);
                        var mapping = _execution.Stack[first + call.PositionalCount + 1];
                        var result = await InvokeWithKeywordMapping(target, positional, mapping, call,
                            span, asynchronous).ConfigureAwait(false);
                        PublishInstructionResult(instruction, result);
                    }
                    catch { _execution.Stack.RetireTo(first); throw; }
                    break;
                }
                case ExecutableCreateGeneratorExpression comprehension:
                    var outer = _execution.Stack.TakeValue(instruction.InputStart, instruction.ResultSlot);
                    var generatorExpression = await PyGenerator.CreateExpressionAsync(comprehension.CodeObject, this,
                        outer, comprehension.OuterSpan, span, asynchronous).ConfigureAwait(false);
                    PublishInstructionResult(instruction, generatorExpression);
                    break;
                default: throw new InvalidOperationException("Unknown prepared suspension operation.");
            }
        }

        private ValueTask<object> InvokeWithKeywordMapping(object target, Memory<ExecutableSlot> positional, object mapping,
            ExecutableCallWithKeywordMapping call, LythonSourceSpan span, bool asynchronous)
        {
            var method = target as PyBoundMethod;
            if (mapping is PyDict dictionary && (method?.Function ?? target) is PyFunctionBase function &&
                function.CanTakeExpandedKeywords(positional.Length, method?.Receiver) &&
                function.PrepareExpandedKeywords(dictionary, this, call.MappingSpan, span, call.FreshMapping) is { } keywords)
                return InvokeWithPreparedKeywords(function, positional.Span, method?.Receiver, keywords, span, this, asynchronous);

            // Operand storage belongs to the caller and remains live through
            // mapping callbacks. Only the general route needs an argument packet.
            return ExpandKeywordMappingAndInvokeAsync(target, positional, mapping, call, span, asynchronous);
        }

        private async ValueTask<object> ExpandKeywordMappingAndInvokeAsync(object target, Memory<ExecutableSlot> positional,
            object mapping, ExecutableCallWithKeywordMapping call, LythonSourceSpan span, bool asynchronous)
        {
            using var accumulator = new CallExpansion.CallArgumentAccumulator(checked(positional.Length + 1), this, target);
            for (var i = 0; i < positional.Length; i++)
            {
                var value = positional.Span[i].Object.RequireNotNull();
                accumulator.Add(CallArgumentValue.Positional(value), span);
            }
            await accumulator.AppendAsync(CallArgumentForm.StarredDictionary, mapping, call.MappingSpan, asynchronous).ConfigureAwait(false);
            return await accumulator.InvokeAsync(call.TargetSpan, span, asynchronous).ConfigureAwait(false);
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
