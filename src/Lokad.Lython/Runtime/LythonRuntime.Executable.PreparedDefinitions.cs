using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class ExecutableFrameInterpreter
    {
        private async ValueTask ExecutePreparedDefinitionOperationAsync(ExecutableOperation operation, LythonSourceSpan span, bool asynchronous)
        {
            switch (operation)
            {
                case ExecutableAnnotationsEnabled:
                    _stack.Push(!_context.PostponedAnnotations);
                    break;
                case ExecutablePostponedAnnotation postponed:
                    _stack.Push(CreatePostponedAnnotationValue(postponed.Annotation, _context));
                    break;
                case ExecutableStoreModuleAnnotation annotation:
                    var value = Pop(ref _stack, span);
                    await StoreAnnotationEntryAsync(ResolveName("__annotations__", span, _context),
                        annotation.Name, value, _context, span, asynchronous).ConfigureAwait(false);
                    break;
                case ExecutableCreateFunction function:
                    using (var storage = _context.MemoryGovernor.ReserveTemporary(256L + 160L * function.Inputs.Count + 128L * function.Definition.Parameters.Count, span))
                    {
                        var captured = CaptureDefinitionInputs(function.Inputs);
                        var definition = function.Definition with
                        {
                            Decorators = function.Definition.Decorators.Select(Capture).ToArray(),
                            Parameters = function.Definition.Parameters.Select(p => p with
                            {
                                DefaultValue = p.DefaultValue is null ? null : Capture(p.DefaultValue),
                                Annotation = p.Annotation is null ? null : Capture(p.Annotation),
                            }).ToArray(),
                            ReturnAnnotation = function.Definition.ReturnAnnotation is null ? null : Capture(function.Definition.ReturnAnnotation),
                        };
                        AdmitPreparedDefinitionStorage(64L + 160L * definition.Parameters.Count, span);
                        AdmitPreparedDefinitionStorage(128L + 64L * definition.Decorators.Count, span);
                        if (asynchronous) await ExecuteLoweredFunctionDefinitionAsync(definition, _context).ConfigureAwait(false);
                        else ExecuteLoweredFunctionDefinition(definition, _context);
                        SyncExecutableLocalsFromContext(_codeObject, _locals, _localCells, _context);
                        LoweredExpression Capture(LoweredExpression input) => captured.TryGetValue(input, out var value) ? value : input;
                    }
                    break;
                case ExecutableCreateLambda lambda:
                    {
                        var parameters = lambda.Expression.Parameters;
                        var count = parameters.Count(parameter => parameter.DefaultValue is not null);
                        using var storage = ReserveDefaultArgumentsWorkingStorage(count, _context.MemoryGovernor, span);
                        var first = _stack.Count - count;
                        PyFunctionBase function;
                        try
                        {
                            var defaults = new Dictionary<string, object>(count, StringComparer.Ordinal);
                            var slot = first;
                            foreach (var parameter in parameters)
                            {
                                if (parameter.DefaultValue is not null)
                                    defaults.Add(parameter.Name, RuntimeValue(_stack[slot++]));
                            }
                            function = CreateLambdaFunction(lambda.Expression, parameters, defaults, _context, lambda.CodeObject);
                        }
                        finally { _stack.RemoveTail(count); }
                        _stack.Push(function);
                    }
                    break;
                case ExecutableStartClassHeader header:
                    _stack.Push(new CallExpansion.CallArgumentAccumulator(header.Count, _context, ClassHeaderExpansionTarget, retained: true, deferSingleStar: false));
                    break;
                case ExecutableCreateClass create:
                    using (var accumulator = create.ExpandedHeader ? (CallExpansion.CallArgumentAccumulator)Pop(ref _stack, span) : null)
                    using (var storage = _context.MemoryGovernor.ReserveTemporary(256L + 128L * create.Definition.Decorators.Count, span))
                    {
                        var definition = create.Definition;
                        var decorators = new LoweredExpression[definition.Decorators.Count];
                        for (var i = decorators.Length - 1; i >= 0; i--)
                            decorators[i] = new LoweredCapturedExpression(definition.Decorators[i].Syntax, Pop(ref _stack, span));
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
                        if (asynchronous) await ExecuteLoweredClassDefinitionAsync(definition, _context).ConfigureAwait(false);
                        else ExecuteLoweredClassDefinition(definition, _context);
                        SyncExecutableLocalsFromContext(_codeObject, _locals, _localCells, _context);
                    }
                    break;
                default: await ExecutePreparedTargetOperationAsync(operation, span, asynchronous).ConfigureAwait(false); break;
            }
        }

        private void AdmitPreparedDefinitionStorage(long bytes, LythonSourceSpan span)
        {
            _context.MemoryGovernor.Reserve(bytes, span);
            _context.MemoryGovernor.Commit(bytes);
        }

        private Dictionary<LoweredExpression, LoweredExpression> CaptureDefinitionInputs(IReadOnlyList<LoweredExpression> inputs)
        {
            var result = new Dictionary<LoweredExpression, LoweredExpression>(inputs.Count);
            for (var i = inputs.Count - 1; i >= 0; i--)
                result[inputs[i]] = new LoweredCapturedExpression(inputs[i].Syntax, Pop(ref _stack, inputs[i].Span));
            return result;
        }
    }
}
