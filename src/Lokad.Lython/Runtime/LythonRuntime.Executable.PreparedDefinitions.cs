using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        private async ValueTask ExecutePreparedDefinitionOperationAsync(ExecutableOperation operation, ExecutableInstruction instruction, bool asynchronous)
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
                    var value = _stack.TakeValue(instruction.InputStart, instruction.LiveExtent);
                    await StoreAnnotationEntryAsync(ResolveName("__annotations__", span, this),
                        annotation.Name, value, this, span, asynchronous).ConfigureAwait(false);
                    break;
                case ExecutableCreateFunction function:
                    using (var storage = MemoryGovernor.ReserveTemporary(320L + 160L * function.Inputs.Count
                        + 128L * function.Binding.Parameters.Count, span))
                    {
                        var captured = CaptureDefinitionInputs(function.Inputs, instruction.InputStart);
                        var template = function.Binding;
                        var definition = template with
                        {
                            Decorators = template.Decorators.Select(Capture).ToArray(),
                            Parameters = template.Parameters.Select(p => p with
                            {
                                DefaultValue = p.DefaultValue is null ? null : Capture(p.DefaultValue),
                                Annotation = p.Annotation is null ? null : Capture(p.Annotation),
                            }).ToArray(),
                            ReturnAnnotation = template.ReturnAnnotation is null ? null : Capture(template.ReturnAnnotation),
                        };
                        AdmitPreparedDefinitionStorage(64L + 160L * definition.Parameters.Count, span);
                        AdmitPreparedDefinitionStorage(128L + 64L * definition.Decorators.Count, span);
                        await ExecuteExecutableFunctionDefinitionAsync(_codeObject,
                            definition,
                            _locals, _localCells, this, asynchronous).ConfigureAwait(false);
                        LoweredExpression Capture(LoweredExpression input) => captured.TryGetValue(input, out var value) ? value : input;
                    }
                    break;
                case ExecutableCreateLambda lambda:
                    {
                        var parameters = lambda.Expression.Parameters;
                        var count = parameters.Count(parameter => parameter.DefaultValue is not null);
                        using var storage = ReserveDefaultArgumentsWorkingStorage(count, MemoryGovernor, span);
                        var first = instruction.InputStart;
                        PyFunctionBase function;
                        try
                        {
                            IReadOnlyDictionary<string, object> defaults = System.Collections.Frozen.FrozenDictionary<string, object>.Empty;
                            if (count != 0)
                            {
                                var values = new Dictionary<string, object>(count, StringComparer.Ordinal);
                                var slot = first;
                                foreach (var parameter in parameters)
                                {
                                    if (parameter.DefaultValue is not null)
                                        values.Add(parameter.Name, RuntimeValue(_stack[slot++]));
                                }
                                defaults = values;
                            }
                            function = CreateLambdaFunction(lambda.Expression, parameters, defaults, this, lambda.CodeObject);
                        }
                        finally { _stack.RetireTo(instruction.ResultSlot); }
                        PublishInstructionResult(instruction, function);
                    }
                    break;
                case ExecutableStartClassHeader header:
                    PublishInstructionResult(instruction, new CallExpansion.CallArgumentAccumulator(header.Count, this, ClassHeaderExpansionTarget, retained: true, deferSingleStar: false));
                    break;
                case ExecutableCreateClass create:
                    using (var accumulator = create.ExpandedHeader ? (CallExpansion.CallArgumentAccumulator)_stack.TakeValue(instruction.InputStart + create.Definition.Decorators.Count, instruction.InputStart + create.Definition.Decorators.Count) : null)
                    using (var storage = MemoryGovernor.ReserveTemporary(256L + 128L * create.Definition.Decorators.Count, span))
                    {
                        var definition = create.Definition;
                        var decorators = new LoweredExpression[definition.Decorators.Count];
                        for (var i = decorators.Length - 1; i >= 0; i--)
                            decorators[i] = new LoweredCapturedExpression(definition.Decorators[i].Syntax, _stack.TakeValue(instruction.InputStart + i, instruction.InputStart + i));
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
                                    new LoweredCapturedExpression(create.Definition.HeaderArguments[0].Expression.Syntax, argument.Value));
                            }
                            definition = definition with { HeaderArguments = headers };
                        }
                        if (asynchronous) await ExecuteLoweredClassDefinitionAsync(definition, this).ConfigureAwait(false);
                        else ExecuteLoweredClassDefinition(definition, this);
                        SyncExecutableLocalsFromContext(_codeObject, _locals, _localCells, this);
                    }
                    break;
                default: throw new InvalidOperationException("Unknown prepared definition operation.");
            }
        }

        private void AdmitPreparedDefinitionStorage(long bytes, LythonSourceSpan span)
        {
            MemoryGovernor.Reserve(bytes, span);
            MemoryGovernor.Commit(bytes);
        }

        private Dictionary<LoweredExpression, LoweredExpression> CaptureDefinitionInputs(IReadOnlyList<LoweredExpression> inputs, int first)
        {
            var result = new Dictionary<LoweredExpression, LoweredExpression>(inputs.Count);
            for (var i = inputs.Count - 1; i >= 0; i--)
                result[inputs[i]] = new LoweredCapturedExpression(inputs[i].Syntax, _stack.TakeValue(first + i, first + i));
            return result;
        }
    }
}
