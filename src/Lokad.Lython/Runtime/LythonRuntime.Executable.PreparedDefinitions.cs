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
                    _stack.Push(!context.PostponedAnnotations || HasTypeParameterScope(context));
                    break;
                case ExecutableCreateFunction function:
                    using (var storage = context.MemoryGovernor.ReserveTemporary(256L + 160L * function.Inputs.Count + 128L * function.Definition.Parameters.Count, span))
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
                        RetainPreparedDefinitionStorage(definition.Parameters, 64L + 160L * definition.Parameters.Count, span);
                        RetainPreparedDefinitionStorage(definition, 128L + 64L * definition.Decorators.Count, span);
                        if (asynchronous) await ExecuteLoweredFunctionDefinitionAsync(definition, context).ConfigureAwait(false);
                        else ExecuteLoweredFunctionDefinition(definition, context);
                        SyncExecutableLocalsFromContext(codeObject, locals, localCells, context);
                        LoweredExpression Capture(LoweredExpression input) => captured.TryGetValue(input, out var value) ? value : input;
                    }
                    break;
                case ExecutableCreateLambda lambda:
                    using (var storage = context.MemoryGovernor.ReserveTemporary(256L + 256L * lambda.Parameters.Count, span))
                    {
                        var captured = CaptureDefinitionInputs(lambda.Parameters.Where(p => p.DefaultValue is not null).Select(p => p.DefaultValue!).ToArray());
                        var expression = lambda.Expression with
                        {
                            PreparedParameters = lambda.Parameters.Select(p => p with
                            {
                                DefaultValue = p.DefaultValue is null ? null : captured[p.DefaultValue],
                            }).ToArray(),
                        };
                        RetainPreparedDefinitionStorage(expression.PreparedParameters!, 64L + 160L * lambda.Parameters.Count, span);
                        PushObserved(asynchronous ? await CreateLoweredLambdaAsync(expression, context).ConfigureAwait(false)
                            : CreateLoweredLambda(expression, context), span);
                    }
                    break;
                case ExecutableStartClassHeader header:
                    _stack.Push(new CallExpansion.CallArgumentAccumulator(header.Count, context, ClassHeaderExpansionTarget, retained: true, deferSingleStar: false));
                    break;
                case ExecutableCreateClass create:
                    using (var accumulator = create.ExpandedHeader ? (CallExpansion.CallArgumentAccumulator)Pop(_stack, span) : null)
                    using (var storage = context.MemoryGovernor.ReserveTemporary(256L + 128L * create.Definition.Decorators.Count, span))
                    {
                        var definition = create.Definition;
                        var decorators = new LoweredExpression[definition.Decorators.Count];
                        for (var i = decorators.Length - 1; i >= 0; i--)
                            decorators[i] = new LoweredCapturedExpression(definition.Decorators[i].Syntax, Pop(_stack, span));
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
                        if (asynchronous) await ExecuteLoweredClassDefinitionAsync(definition, context).ConfigureAwait(false);
                        else ExecuteLoweredClassDefinition(definition, context);
                        SyncExecutableLocalsFromContext(codeObject, locals, localCells, context);
                    }
                    break;
                default: await ExecutePreparedTargetOperationAsync(operation, span, asynchronous).ConfigureAwait(false); break;
            }
        }

        private void RetainPreparedDefinitionStorage(object owner, long bytes, LythonSourceSpan span)
        {
            context.MemoryGovernor.Reserve(bytes, span);
            context.MemoryGovernor.Commit(bytes);
            context.Services.State.CallTemporaries.TrackFreshMutable(owner, bytes, span);
        }

        private Dictionary<LoweredExpression, LoweredExpression> CaptureDefinitionInputs(IReadOnlyList<LoweredExpression> inputs)
        {
            var result = new Dictionary<LoweredExpression, LoweredExpression>(inputs.Count);
            for (var i = inputs.Count - 1; i >= 0; i--)
                result[inputs[i]] = new LoweredCapturedExpression(inputs[i].Syntax, Pop(_stack, inputs[i].Span));
            return result;
        }
    }
}
