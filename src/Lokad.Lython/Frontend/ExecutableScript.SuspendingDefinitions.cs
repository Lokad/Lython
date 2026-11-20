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
internal sealed record ExecutableCreateClass(LoweredClassDefinitionStatement Definition, bool ExpandedHeader,
    int PreparedBaseCount) : ExecutableOperation
{
    internal int DirectHeaderCount => !ExpandedHeader && Definition.TypeParameters is null
        ? Definition.HeaderArguments.Count : 0;
}

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileFunctionDefinition(LoweredFunctionDefinitionStatement definition, int block, bool yielding)
        {
            var binding = PrepareFunctionBinding(definition);
            foreach (var decorator in definition.Decorators) block = CompileExpression(decorator, block);
            block = CompileFunctionDefaults(definition.DefaultValues, binding.ParameterLayout, definition.Span, block);
            if ((!_completeLexicalParent && !yielding) || definition.TypeParameters is not null ||
                binding.Annotations.Length == 0)
            {
                EmitDefinition(block);
                return block;
            }
            foreach (var annotation in binding.Annotations)
            {
                var expression = annotation.Expression;
                if (expression.Syntax.PostponedAnnotation is { } text)
                    AddInstruction(block, ExecutableInstruction.LoadConst(InternConstant(text), expression.Span));
                else block = CompileExpression(expression, block);
            }
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
            block = CompileFunctionDefaults(lambda.DefaultValues, lambda.ParameterLayout, lambda.Span, block);
            if (lambda.PreparedCode is null)
                lambda.CompleteCompilation(CompileFunctionCode("<lambda>", lambda.ParameterLayout,
                    lambda.FunctionBody, lambda.ScopeFacts, generator: lambda.IsGenerator,
                    qualifiedName: lambda.Lambda.PreparedQualifiedName));
            EmitOperation(new ExecutableCreateLambda(lambda), lambda.Span, block);
            return block;
        }

        private int CompileFunctionDefaults(LoweredExpression[] defaults,
            FunctionParameterLayout layout, LythonSourceSpan span, int block)
        {
            if (layout.Defaults.Length == 0) return block;
            var keys = PyDict.RecordKeys.PrepareDefinition(layout.Defaults.Length - layout.PositionalDefaultCount,
                layout, static (parameters, index) => parameters.KeywordDefaultName(
                    parameters.Defaults[parameters.PositionalDefaultCount + index].KeywordIndex),
                _compilationCancellation, span);
            EmitOperation(new ExecutableStartFunctionDefaults(layout, keys), span, block);
            for (var i = 0; i < defaults.Length; i++)
            {
                var expression = defaults[i];
                block = CompileExpression(expression, block);
                EmitOperation(new ExecutableAppendFunctionDefault(layout.Defaults[i]), expression.Span, block);
            }
            return block;
        }

        private int CompileClassDefinition(LoweredClassDefinitionStatement definition, int block)
        {
            foreach (var decorator in definition.Decorators) block = CompileExpression(decorator, block);
            var expandedHeader = false;
            var baseCount = 0;
            if (definition.TypeParameters is null)
            {
                expandedHeader = definition.HeaderArguments.Any(argument => argument.Form.Kind is
                    CallArgumentKind.StarredList or CallArgumentKind.StarredDictionary);
                if (expandedHeader)
                    EmitOperation(new ExecutableStartClassHeader(definition.HeaderArguments.Count), definition.Span, block);
                foreach (var argument in definition.HeaderArguments)
                {
                    block = CompileExpression(argument.Expression, block);
                    if (expandedHeader) EmitOperation(new ExecutableAppendCall(argument.Form), argument.Expression.Span, block);
                    else if (argument.Form.Kind == CallArgumentKind.Positional) baseCount++;
                }
            }
            EmitOperation(new ExecutableCreateClass(definition, expandedHeader, baseCount), definition.Span, block);
            return block;
        }
    }
}
