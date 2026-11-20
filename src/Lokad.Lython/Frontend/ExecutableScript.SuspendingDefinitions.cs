using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal sealed record ExecutableStartFunctionDefaults(FunctionParameterLayout Layout) : ExecutableOperation;
internal sealed record ExecutableAppendFunctionDefault(FunctionParameterLayout.DefaultSlot Default) : ExecutableOperation;
internal sealed record ExecutableCreateFunction(ExecutableCodeObject CodeObject, FunctionParameterLayout ParameterLayout,
    int DecoratorCount, bool IsGenerator, int AnnotationOperandCount, LoweredFunctionAnnotation[] DeferredAnnotations,
    IReadOnlyList<LoweredTypeParameter>? TypeParameters) : ExecutableOperation
{
    internal int InputCount => checked(DecoratorCount + AnnotationOperandCount + (ParameterLayout.Defaults.Length == 0 ? 0 : 1));
}
internal readonly record struct ExecutableLambdaBinding(
    ExecutableCodeObject CodeObject, FunctionParameterLayout Layout, bool IsGenerator, bool NamespaceClosure);

internal sealed record ExecutableCreateLambda(ExecutableLambdaBinding Binding) : ExecutableOperation
{
    internal ExecutableCodeObject CodeObject => Binding.CodeObject;
    internal int InputCount => Binding.Layout.Defaults.Length == 0 ? 0 : 1;
}
internal sealed record ExecutableStartClassHeader(int Count) : ExecutableOperation;
internal sealed record ExecutableCreateClass(ClassDefinitionStatementSyntax Syntax, ExecutableCodeObject Body,
    IReadOnlyList<LoweredTypeParameter>? TypeParameters, IReadOnlyList<LoweredCallArgument> GenericHeaderArguments,
    bool ExpandedHeader, int PreparedBaseCount) : ExecutableOperation
{
    internal LythonSourceSpan Span => Syntax.Span;
    internal int DirectHeaderCount => !ExpandedHeader && TypeParameters is null ? Syntax.HeaderArguments.Count : 0;
}

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileFunctionDefinition(FunctionDefinitionStatementSyntax definition, int block, bool yielding)
        {
            var annotations = LoweredScript.LowerFunctionAnnotations(definition);
            var typeParameters = LoweredScript.LowerTypeParameters(definition.TypeParameters);
            var code = PrepareFunctionCode(definition, annotations);
            foreach (var decorator in definition.Decorators) block = CompileExpression(decorator, block);
            block = CompileFunctionDefaults(LoweredScript.LowerDefaults(definition.Parameters, definition.ParameterLayout),
                definition.ParameterLayout, definition.Span, block);
            var annotationOperandCount = (_completeLexicalParent || yielding) && definition.TypeParameters is null
                ? annotations.Length : 0;
            if (annotationOperandCount != 0)
            {
                foreach (var annotation in annotations)
                {
                    var expression = annotation.Expression;
                    if (expression.Syntax.PostponedAnnotation is { } text)
                        AddInstruction(block, ExecutableInstruction.LoadConst(InternConstant(text), expression.Span));
                    else block = CompileExpression(expression, block);
                }
            }
            // The creation operation retains lowered annotations only when
            // it still evaluates them in the definition's type scope.
            EmitOperation(new ExecutableCreateFunction(code, definition.ParameterLayout, definition.Decorators.Count,
                definition.IsGenerator, annotationOperandCount,
                annotationOperandCount == 0 ? annotations : [], typeParameters), definition.Span, block);
            // Module declarations update their live namespace and optional slot.
            var name = definition.Name;
            if (_parameterLayout is null)
                AddInstruction(block, ExecutableInstruction.StoreName(InternName(name), definition.Span));
            else
                CompileStoreBoundName(name, definition.Span, block);
            return block;
        }

        private int CompileLambda(LoweredLambdaExpression lambda, int block)
        {
            block = CompileFunctionDefaults(lambda.DefaultValues, lambda.ParameterLayout, lambda.Span, block);
            if (lambda.PreparedCode is null && _classBody)
                CompileNamespaceLambda(lambda, _compilation);
            else if (lambda.PreparedCode is null)
                lambda.CompleteCompilation(CompileFunctionCode("<lambda>", lambda.ParameterLayout,
                    [], lambda.ScopeFacts, generator: lambda.IsGenerator,
                    qualifiedName: lambda.Lambda.PreparedQualifiedName, lambda: lambda));
            EmitOperation(new ExecutableCreateLambda(lambda.Binding), lambda.Span, block);
            return block;
        }

        private int CompileFunctionDefaults(LoweredExpression[] defaults,
            FunctionParameterLayout layout, LythonSourceSpan span, int block)
        {
            if (layout.Defaults.Length == 0) return block;
            layout.PrepareKeywordDefaultKeys(_compilation.CancellationToken, span);
            EmitOperation(new ExecutableStartFunctionDefaults(layout), span, block);
            for (var i = 0; i < defaults.Length; i++)
            {
                var expression = defaults[i];
                block = CompileExpression(expression, block);
                EmitOperation(new ExecutableAppendFunctionDefault(layout.Defaults[i]), expression.Span, block);
            }
            return block;
        }

        private int CompileClassDefinition(ClassDefinitionStatementSyntax syntax, int block)
        {
            var typeParameters = LoweredScript.LowerTypeParameters(syntax.TypeParameters);
            var genericHeader = typeParameters is null ? [] : syntax.HeaderArguments.Select(argument =>
                new LoweredCallArgument(argument.Form, LoweredScript.LowerExpression(argument.Expression))).ToArray();
            var body = new Builder(_compilation, null, null, ScopeDirectiveFactsCollector.ForClass(syntax), classBody: true)
                .CompileCodeObject(syntax.DeclaredName, syntax.Body);
            foreach (var decorator in syntax.Decorators) block = CompileExpression(decorator, block);
            var expandedHeader = false;
            var baseCount = 0;
            if (typeParameters is null)
            {
                expandedHeader = syntax.HeaderArguments.Any(argument => argument.Form.Kind is
                    CallArgumentKind.StarredList or CallArgumentKind.StarredDictionary);
                if (expandedHeader)
                    EmitOperation(new ExecutableStartClassHeader(syntax.HeaderArguments.Count), syntax.Span, block);
                foreach (var argument in syntax.HeaderArguments)
                {
                    block = CompileExpression(argument.Expression, block);
                    if (expandedHeader) EmitOperation(new ExecutableAppendCall(argument.Form), argument.Expression.Span, block);
                    else if (argument.Form.Kind == CallArgumentKind.Positional) baseCount++;
                }
            }
            EmitOperation(new ExecutableCreateClass(syntax, body, typeParameters, genericHeader, expandedHeader, baseCount), syntax.Span, block);
            return block;
        }
    }
}
