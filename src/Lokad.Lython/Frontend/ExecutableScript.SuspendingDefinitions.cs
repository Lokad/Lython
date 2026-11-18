namespace Lokad.Lython.Frontend;

internal sealed record ExecutableStartFunctionDefaults(FunctionParameterLayout Layout) : ExecutableOperation;
internal readonly record struct ExecutableFunctionAnnotation(string Name, int Offset, LythonSourceSpan Span);
internal sealed record ExecutableCreateFunction(ExecutableFunctionBinding Binding, IReadOnlyList<ExecutableFunctionAnnotation> AnnotationInputs) : ExecutableOperation
{
    internal int InputCount => checked(Binding.DecoratorCount + AnnotationInputs.Count + Binding.ParameterLayout.Defaults.Length +
        (Binding.ParameterLayout.Defaults.Length == 0 ? 0 : 1));
}
internal sealed record ExecutableCreateLambda(LoweredLambdaExpression Expression) : ExecutableOperation
{
    internal ExecutableCodeObject CodeObject => Expression.Code;
}
internal sealed record ExecutableAnnotationsEnabled : ExecutableOperation;
internal sealed record ExecutableStartClassHeader(int Count) : ExecutableOperation;
internal sealed record ExecutableCreateClass(LoweredClassDefinitionStatement Definition, bool ExpandedHeader) : ExecutableOperation;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileFunctionDefinition(LoweredFunctionDefinitionStatement definition, int block, bool yielding)
        {
            var binding = PrepareFunctionBinding(definition);
            foreach (var decorator in definition.Decorators) block = CompileExpression(decorator, block);
            var layout = binding.ParameterLayout;
            if (layout.Defaults.Length != 0)
            {
                EmitOperation(new ExecutableStartFunctionDefaults(layout), definition.Span, block);
                foreach (var slot in layout.Defaults)
                    block = CompileExpression(definition.Parameters[slot.ParameterIndex].DefaultValue.RequireNotNull(), block);
            }
            if (!yielding || definition.TypeParameters is not null || !binding.HasAnnotations)
            {
                EmitOperation(new ExecutableCreateFunction(binding, []), definition.Span, block);
                return block;
            }
            var annotations = new List<(string Name, LoweredExpression Expression, int Order)>();
            foreach (var parameter in definition.Parameters)
                if (parameter.Annotation is { } annotation)
                    annotations.Add((parameter.Name, annotation, FunctionParameterLayout.AnnotationOrder(parameter.Kind)));
            if (definition.ReturnAnnotation is { } result)
                annotations.Add(("return", result, int.MaxValue));
            var skipped = CreateBlock();
            var after = CreateBlock();
            EmitOperation(new ExecutableAnnotationsEnabled(), definition.Span, block);
            AddInstruction(block, ExecutableInstruction.JumpIfFalse(skipped, definition.Span));
            foreach (var annotation in annotations) block = CompileExpression(annotation.Expression, block);
            var sites = annotations.Select((input, i) => (input.Order, Site: new ExecutableFunctionAnnotation(input.Name, i, input.Expression.Span)))
                .OrderBy(entry => entry.Order).Select(entry => entry.Site).ToArray();
            EmitOperation(new ExecutableCreateFunction(binding, sites), definition.Span, block);
            AddInstruction(block, ExecutableInstruction.Jump(after, definition.Span));
            EmitOperation(new ExecutableCreateFunction(binding, []), definition.Span, skipped);
            AddInstruction(skipped, ExecutableInstruction.Jump(after, definition.Span));
            return after;
        }

        private int CompileLambda(LoweredLambdaExpression lambda, int block)
        {
            foreach (var slot in lambda.ParameterLayout.Defaults)
                block = CompileExpression(lambda.Parameters[slot.ParameterIndex].DefaultValue.RequireNotNull(), block);
            if (lambda.PreparedCode is null)
                lambda.CompleteCompilation(CompileFunctionCode("<lambda>", lambda.ParameterLayout,
                    lambda.FunctionBody, lambda.ScopeFacts, generator: lambda.IsGenerator));
            EmitOperation(new ExecutableCreateLambda(lambda), lambda.Span, block);
            return block;
        }

        private int CompileSuspendingClass(LoweredClassDefinitionStatement definition, int block)
        {
            foreach (var decorator in definition.Decorators) block = CompileExpression(decorator, block);
            if (definition.TypeParameters is null)
            {
                EmitOperation(new ExecutableStartClassHeader(definition.HeaderArguments.Count), definition.Span, block);
                foreach (var argument in definition.HeaderArguments)
                {
                    block = CompileExpression(argument.Expression, block);
                    EmitOperation(new ExecutableAppendCall(argument.Form), argument.Expression.Span, block);
                }
            }
            EmitOperation(new ExecutableCreateClass(definition, definition.TypeParameters is null), definition.Span, block);
            return block;
        }
    }
}
