namespace Lokad.Lython.Frontend;

internal sealed record ExecutableCreateFunction(LoweredFunctionDefinitionStatement Definition, IReadOnlyList<LoweredExpression> Inputs) : ExecutableOperation;
internal sealed record ExecutableCreateLambda(LoweredLambdaExpression Expression, IReadOnlyList<LoweredFunctionParameter> Parameters) : ExecutableOperation;
internal sealed record ExecutableAnnotationsEnabled : ExecutableOperation;
internal sealed record ExecutableStartClassHeader(int Count) : ExecutableOperation;
internal sealed record ExecutableCreateClass(LoweredClassDefinitionStatement Definition, bool ExpandedHeader) : ExecutableOperation;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileSuspendingFunction(LoweredFunctionDefinitionStatement definition, int block)
        {
            var inputs = definition.Decorators.Concat(definition.Parameters.Where(p => p.DefaultValue is not null).Select(p => p.DefaultValue!)).ToArray();
            foreach (var input in inputs) block = CompileExpression(input, block);
            var annotations = definition.TypeParameters is not null ? [] :
                definition.Parameters.Where(p => p.Annotation is not null).Select(p => p.Annotation!)
                    .Concat(definition.ReturnAnnotation is null ? [] : new[] { definition.ReturnAnnotation }).ToArray();
            if (annotations.Length == 0)
            {
                EmitOperation(new ExecutableCreateFunction(definition, inputs), definition.Span, block);
                return block;
            }
            var skipped = CreateBlock();
            var after = CreateBlock();
            EmitOperation(new ExecutableAnnotationsEnabled(), definition.Span, block);
            AddInstruction(block, ExecutableInstruction.JumpIfFalse(skipped, definition.Span));
            foreach (var annotation in annotations) block = CompileExpression(annotation, block);
            EmitOperation(new ExecutableCreateFunction(definition, inputs.Concat(annotations).ToArray()), definition.Span, block);
            AddInstruction(block, ExecutableInstruction.Jump(after, definition.Span));
            EmitOperation(new ExecutableCreateFunction(definition, inputs), definition.Span, skipped);
            AddInstruction(skipped, ExecutableInstruction.Jump(after, definition.Span));
            return after;
        }

        private int CompileSuspendingLambda(LoweredLambdaExpression lambda, int block)
        {
            var parameters = lambda.Lambda.Parameters.Select(p => new LoweredFunctionParameter(p.Name, p.Kind, null,
                p.DefaultValue is null ? null : LoweredScript.LowerExpression(p.DefaultValue))).ToArray();
            foreach (var parameter in parameters)
                if (parameter.DefaultValue is not null) block = CompileExpression(parameter.DefaultValue, block);
            EmitOperation(new ExecutableCreateLambda(lambda, parameters), lambda.Span, block);
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
