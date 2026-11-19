namespace Lokad.Lython.Frontend;

internal sealed record ExecutableStartFunctionDefaults(ExecutableFunctionBinding Binding) : ExecutableOperation;
internal sealed record ExecutableCreateFunction(ExecutableFunctionBinding Binding, bool AnnotationsOnStack = false) : ExecutableOperation
{
    internal int InputCount => checked(Binding.DecoratorCount + (AnnotationsOnStack ? Binding.Annotations.Length : 0) + Binding.ParameterLayout.Defaults.Length +
        (Binding.ParameterLayout.Defaults.Length == 0 ? 0 : 1));
}
internal sealed record ExecutableCreateLambda(LoweredLambdaExpression Expression, int KeywordRecordCache) : ExecutableOperation
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
                EmitOperation(new ExecutableStartFunctionDefaults(binding), definition.Span, block);
                foreach (var slot in layout.Defaults)
                    block = CompileExpression(definition.Parameters[slot.ParameterIndex].DefaultValue.RequireNotNull(), block);
            }
            if ((!_completeLexicalParent && !yielding) || definition.TypeParameters is not null || binding.Annotations.Length == 0)
            {
                EmitOperation(new ExecutableCreateFunction(binding), definition.Span, block);
                return block;
            }
            var skipped = CreateBlock();
            var after = CreateBlock();
            EmitOperation(new ExecutableAnnotationsEnabled(), definition.Span, block);
            AddInstruction(block, ExecutableInstruction.JumpIfFalse(skipped, definition.Span));
            foreach (var annotation in binding.Annotations) block = CompileExpression(annotation.Expression, block);
            EmitOperation(new ExecutableCreateFunction(binding, AnnotationsOnStack: true), definition.Span, block);
            AddInstruction(block, ExecutableInstruction.Jump(after, definition.Span));
            EmitOperation(new ExecutableCreateFunction(binding), definition.Span, skipped);
            AddInstruction(skipped, ExecutableInstruction.Jump(after, definition.Span));
            return after;
        }

        private int CompileLambda(LoweredLambdaExpression lambda, int block)
        {
            foreach (var slot in lambda.ParameterLayout.Defaults)
                block = CompileExpression(lambda.Parameters[slot.ParameterIndex].DefaultValue.RequireNotNull(), block);
            if (lambda.PreparedCode is null)
                lambda.CompleteCompilation(CompileFunctionCode("<lambda>", lambda.ParameterLayout,
                    lambda.FunctionBody, lambda.ScopeFacts, generator: lambda.IsGenerator,
                    qualifiedName: lambda.Lambda.PreparedQualifiedName));
            var recordCache = lambda.ParameterLayout.HasKeywordDefaults ? AllocateLookupCache() : -1;
            EmitOperation(new ExecutableCreateLambda(lambda, recordCache), lambda.Span, block);
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
