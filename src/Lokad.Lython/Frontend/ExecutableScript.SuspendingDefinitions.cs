using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal sealed record ExecutableStartFunctionDefaults(FunctionParameterLayout Layout, PyDict.RecordKeys? KeywordKeys) : ExecutableOperation;
internal sealed record ExecutableAppendFunctionDefault(FunctionParameterLayout.DefaultSlot Default) : ExecutableOperation;
internal sealed record ExecutableCreateFunction(ExecutableFunctionBinding Binding, bool AnnotationsOnStack = false) : ExecutableOperation
{
    internal int InputCount => checked(Binding.DecoratorCount + (AnnotationsOnStack ? Binding.Annotations.Length : 0) +
        (Binding.ParameterLayout.Defaults.Length == 0 ? 0 : 1));
}
internal sealed record ExecutableCreateLambda(LoweredLambdaExpression Expression) : ExecutableOperation
{
    internal ExecutableCodeObject CodeObject => Expression.Code;
    internal int InputCount => Expression.ParameterLayout.Defaults.Length == 0 ? 0 : 1;
}
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
            block = CompileFunctionDefaults(definition.Parameters, binding.ParameterLayout, definition.Span, block);
            // Future annotations already carry their source spelling. Their
            // mode belongs to this definition and needs no execution-time test.
            if ((!_completeLexicalParent && !yielding) || definition.TypeParameters is not null ||
                binding.Annotations.Length == 0 || binding.Annotations[0].Expression.Syntax.PostponedAnnotationText is not null)
            {
                EmitDefinition(block);
                return block;
            }
            foreach (var annotation in binding.Annotations) block = CompileExpression(annotation.Expression, block);
            EmitDefinition(block, annotationsOnStack: true);
            return block;

            void EmitDefinition(int target, bool annotationsOnStack = false)
            {
                EmitOperation(new ExecutableCreateFunction(binding, annotationsOnStack), definition.Span, target);
                // Module declarations update their live namespace as well as
                // their optional slot, including across import completion.
                if (_parameterLayout is null)
                    AddInstruction(target, ExecutableInstruction.StoreName(InternName(binding.Name), definition.Span));
                else
                    CompileStoreBoundName(binding.Name, definition.Span, target);
            }
        }

        private int CompileLambda(LoweredLambdaExpression lambda, int block)
        {
            block = CompileFunctionDefaults(lambda.Parameters, lambda.ParameterLayout, lambda.Span, block);
            if (lambda.PreparedCode is null)
                lambda.CompleteCompilation(CompileFunctionCode("<lambda>", lambda.ParameterLayout,
                    lambda.FunctionBody, lambda.ScopeFacts, generator: lambda.IsGenerator,
                    qualifiedName: lambda.Lambda.PreparedQualifiedName));
            EmitOperation(new ExecutableCreateLambda(lambda), lambda.Span, block);
            return block;
        }

        private int CompileFunctionDefaults(IReadOnlyList<LoweredFunctionParameter> parameters,
            FunctionParameterLayout layout, LythonSourceSpan span, int block)
        {
            if (layout.Defaults.Length == 0) return block;
            var keys = PyDict.RecordKeys.PrepareDefinition(layout.Defaults.Length - layout.PositionalDefaultCount,
                layout, static (parameters, index) => parameters.KeywordDefaultName(
                    parameters.Defaults[parameters.PositionalDefaultCount + index].KeywordIndex),
                _compilationCancellation, span);
            EmitOperation(new ExecutableStartFunctionDefaults(layout, keys), span, block);
            foreach (var slot in layout.Defaults)
            {
                var expression = parameters[slot.ParameterIndex].DefaultValue.RequireNotNull();
                block = CompileExpression(expression, block);
                EmitOperation(new ExecutableAppendFunctionDefault(slot), expression.Span, block);
            }
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
