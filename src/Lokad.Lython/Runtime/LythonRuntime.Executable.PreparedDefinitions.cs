using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private ValueTask ExecutePreparedDefinitionOperation(ExecutableOperation operation, ExecutableInstruction instruction, bool asynchronous)
        {
            var span = instruction.Span;
            switch (operation)
            {
                case ExecutableAnnotationsEnabled:
                    PublishInstructionResult(instruction, !PostponedAnnotations);
                    break;
                case ExecutablePostponedAnnotation postponed:
                    PublishInstructionResult(instruction, CreatePostponedAnnotationValue(postponed.Annotation, this));
                    break;
                case ExecutableStoreModuleAnnotation annotation:
                    var value = _execution.Stack.TakeValue(instruction.InputStart, instruction.LiveExtent);
                    return StoreAnnotationEntryAsync(ResolveName("__annotations__", span, this),
                        annotation.Name, value, this, span, asynchronous);
                case ExecutableStartFunctionDefaults start:
                    var owner = new PendingFunctionDefaults(start.Binding.ParameterLayout, this, span,
                        start.Binding.KeywordRecordCache);
                    try { PublishInstructionResult(instruction, owner); }
                    catch { owner.Dispose(); throw; }
                    break;
                case ExecutableCreateFunction function:
                    return CreateExecutableFunction(function, instruction.InputStart, asynchronous);
                case ExecutableCreateLambda lambda:
                    {
                        var layout = lambda.Expression.ParameterLayout;
                        using var defaults = layout.Defaults.Length == 0 ? null
                            : new PendingFunctionDefaults(layout, this, span, lambda.KeywordRecordCache);
                        var first = instruction.InputStart;
                        PyFunctionBase function;
                        try
                        {
                            if (defaults is not null)
                            {
                                for (var i = 0; i < layout.Defaults.Length; i++)
                                    defaults.Add(layout.Defaults[i], RuntimeValue(_execution.Stack[first + i]), span);
                                defaults.Complete();
                            }
                            function = CreateLambdaFunction(lambda.Expression, defaults?.Values ?? default, this);
                        }
                        finally { _execution.Stack.RetireTo(instruction.ResultSlot); }
                        defaults?.Publish();
                        PublishInstructionResult(instruction, function);
                    }
                    break;
                case ExecutableStartClassHeader header:
                    PublishInstructionResult(instruction, new CallExpansion.CallArgumentAccumulator(header.Count, this, ClassHeaderExpansionTarget));
                    break;
                case ExecutableCreateClass create:
                    return ExecutePreparedClassDefinitionAsync(create, instruction.InputStart, span, asynchronous);
                default: throw new InvalidOperationException("Unknown prepared definition operation.");
            }
            return ValueTask.CompletedTask;
        }

        private async ValueTask ExecutePreparedClassDefinitionAsync(ExecutableCreateClass create, int first, LythonSourceSpan span, bool asynchronous)
        {
            var definition = create.Definition;
            var decoratorCount = definition.Decorators.Count;
            using var accumulator = create.ExpandedHeader
                ? (CallExpansion.CallArgumentAccumulator)_execution.Stack.TakeValue(first + decoratorCount, first + decoratorCount)
                : null;
            using var storage = MemoryGovernor.ReserveTemporary(256L + 128L * decoratorCount, span);
            var decorators = new LoweredExpression[decoratorCount];
            for (var i = decorators.Length - 1; i >= 0; i--)
                decorators[i] = new LoweredCapturedExpression(definition.Decorators[i].Syntax, _execution.Stack.TakeValue(first + i, first + i));
            definition = definition with { Decorators = decorators };
            if (accumulator is not null)
            {
                var arguments = accumulator.ToArray();
                storage.Grow(128L * arguments.Length, span);
                var headers = new LoweredCallArgument[arguments.Length];
                for (var i = 0; i < arguments.Length; i++)
                {
                    var argument = arguments[i];
                    if (argument.IsKeyword) storage.Grow(2L * argument.KeywordName.Length, span);
                    headers[i] = new LoweredCallArgument(argument.IsKeyword ? CallArgumentForm.Keyword(argument.KeywordName) : CallArgumentForm.Positional,
                        new LoweredCapturedExpression(definition.HeaderArguments[0].Expression.Syntax, argument.Value));
                }
                definition = definition with { HeaderArguments = headers };
            }
            if (asynchronous) await ExecuteLoweredClassDefinitionAsync(definition, this).ConfigureAwait(false);
            else ExecuteLoweredClassDefinition(definition, this);
            SyncExecutableLocalsFromContext(_execution.Code, _execution.Locals, this);
        }

        private ValueTask CreateExecutableFunction(ExecutableCreateFunction operation, int first, bool asynchronous)
        {
            var binding = operation.Binding;
            var span = binding.Span;
            EnterInterpreterFrame(span);
            var transferred = false;
            try
            {
                var layout = binding.ParameterLayout;
                var endDecorators = first + binding.DecoratorCount;
                var next = endDecorators;
                PendingFunctionDefaults? defaults = null;
                if (layout.Defaults.Length != 0)
                {
                    defaults = (PendingFunctionDefaults)_execution.Stack[next++];
                    foreach (var slot in layout.Defaults)
                        defaults.Add(slot, RuntimeValue(_execution.Stack[next++]), span);
                    defaults.Complete();
                }

                var cells = CaptureExecutableClosures(binding.CodeObject, this, span);
                var typeScope = CreateTypeScope(binding.TypeParameters, this, span);
                var closure = typeScope.Closure.FunctionClosureContext;
                var values = defaults?.Values ?? default;
                using var storage = MemoryGovernor.ReserveTemporary(FunctionValueBytes, span);
                PyFunctionBase function = binding.IsGenerator
                    ? new PyGeneratorFunction(binding.DeclaredName, layout, binding.CodeObject, closure, values, binding.CodeObject.ScopeFacts, cells)
                    : new PyExecutableFunction(binding.DeclaredName, layout, binding.CodeObject, closure, cells, values, binding.CodeObject.ScopeFacts);
                storage.Commit(FunctionValueBytes);
                ChargeClosureRetention(closure, MemoryGovernor, span);
                PyFunctionBase.CaptureFunctionDocstring(function, binding.Docstring, this, span);
                var metadata = ValueTask.CompletedTask;
                if (binding.Annotations.Length != 0 || binding.TypeParameters is not null || HasTypeParameterScope(this))
                {
                    var evaluated = operation.AnnotationsOnStack ? _execution.Stack.Slice(next, binding.Annotations.Length) : default;
                    metadata = AttachTypeMetadataAsync(function, binding.Annotations, binding.TypeParameters is not null,
                        span, typeScope, this, asynchronous, evaluated, binding.AnnotationRecordCache);
                }

                if (binding.DecoratorCount != 0 || !metadata.IsCompletedSuccessfully)
                {
                    var completion = FinishExecutableFunctionAsync(binding, function, defaults, metadata, first, asynchronous);
                    transferred = true;
                    return completion;
                }

                metadata.GetAwaiter().GetResult();
                defaults?.Publish();
                AssignExecutableBoundName(_execution.Code, _execution.Locals, binding.Name, function, this, span);
                return ValueTask.CompletedTask;
            }
            finally
            {
                if (!transferred) RetireExecutableDefinition(first);
            }
        }

        private async ValueTask FinishExecutableFunctionAsync(ExecutableFunctionBinding binding, PyFunctionBase function,
            PendingFunctionDefaults? defaults, ValueTask metadata, int first, bool asynchronous)
        {
            var span = binding.Span;
            try
            {
                await metadata.ConfigureAwait(false);
                // Decorator callbacks can publish the original definition even
                // when they replace it or raise. Transfer the default owner first.
                defaults?.Publish();
                object decorated = function;
                for (var i = first + binding.DecoratorCount - 1; i >= first; i--)
                {
                    if (_execution.Stack[i] is not ICallable decorator)
                        throw new LythonRuntimeException("TypeError", "Decorator expression must evaluate to a callable.", span);
                    decorated = asynchronous ? await CallableInvocation.InvokeUnaryAsync(decorator, decorated, span, this).ConfigureAwait(false)
                        : CallableInvocation.InvokeUnary(decorator, decorated, span, this);
                }
                AssignExecutableBoundName(_execution.Code, _execution.Locals, binding.Name, decorated, this, span);
            }
            finally
            {
                RetireExecutableDefinition(first);
            }
        }

        internal PyDict CreateDefinitionRecord<T>(int count, int cacheIndex, T names,
            Func<T, int, PyString> nameAt, LythonSourceSpan span)
        {
            if (cacheIndex < 0) return new PyDict(count, MemoryGovernor, span);
            ref var cache = ref _execution.Stack.GetLookupCaches(_execution.Code.LookupCacheCount, span)[cacheIndex];
            var keys = PyDict.RecordKeys.PrepareDefinition(ref cache, count, names, nameAt, this, span);
            return keys.CreateRecord(this, span, count);
        }

        private void RetireExecutableDefinition(int first)
        {
            try { _execution.Stack.RetireTo(first); }
            finally { LeaveInterpreterFrame(); }
        }
    }
}
