using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

internal sealed record ExecutableStartFunctionDefaults(FunctionParameterLayout Layout) : ExecutableOperation;
internal sealed record ExecutableAppendFunctionDefault(int StorageDepth, int Index) : ExecutableOperation;
internal sealed record ExecutableCreateFunction(ExecutableCodeObject CodeObject, FunctionParameterLayout ParameterLayout,
    int DecoratorCount, bool IsGenerator, int AnnotationOperandCount, LoweredFunctionAnnotation[] DeferredAnnotations,
    IReadOnlyList<LoweredTypeParameter>? TypeParameters, PyString[]? PostponedAnnotations,
    ExecutableInstruction[]? EagerAnnotationReads) : ExecutableOperation
{
    internal int InputCount => checked(DecoratorCount + AnnotationOperandCount + ParameterLayout.DefaultStorageCount);
}
internal readonly record struct ExecutableLambdaBinding(
    ExecutableCodeObject CodeObject, FunctionParameterLayout Layout, bool IsGenerator, bool NamespaceClosure);

internal sealed record ExecutableCreateLambda(ExecutableLambdaBinding Binding) : ExecutableOperation
{
    internal ExecutableCodeObject CodeObject => Binding.CodeObject;
    internal int InputCount => Binding.Layout.DefaultStorageCount;
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
        // Each declaration owns its prepared body or deferred binding.
        // Intern guest values and reusable operations elsewhere.
        private void EmitDefinition(ExecutableOperation operation, LythonSourceSpan span, int block)
            => AddInstruction(block, ExecutableInstruction.ApplyOperation(AppendConstant(operation), span));

        private int CompileFunctionDefinition(FunctionDefinitionStatementSyntax definition, int block, bool yielding)
        {
            var typeParameters = LoweredScript.LowerTypeParameters(definition.TypeParameters);
            var code = PrepareFunctionCode(definition);
            var emitAnnotations = (_completeLexicalParent || yielding) && typeParameters is null;
            var annotations = emitAnnotations ? PrepareFunctionAnnotations(definition,
                static (key, expression) => (Key: key, Expression: expression)) : [];
            var deferredAnnotations = emitAnnotations ? [] : PrepareFunctionAnnotations(definition,
                static (key, expression) => new LoweredFunctionAnnotation(key, expression,
                    expression.PostponedAnnotation is null ? LoweredScript.LowerExpression(expression) : null));
            code.DefinitionAnnotationKeys = emitAnnotations
                ? PyDict.RecordKeys.PrepareDefinition(annotations.Length, annotations, static (inputs, index) => inputs[index].Key,
                    _compilation.CancellationToken, definition.Span)
                : PyDict.RecordKeys.PrepareDefinition(deferredAnnotations.Length, deferredAnnotations, static (inputs, index) => inputs[index].Key,
                    _compilation.CancellationToken, definition.Span);
            foreach (var decorator in definition.Decorators) block = CompileExpression(decorator, block);
            block = CompileFunctionDefaults(definition.Parameters,
                definition.ParameterLayout, definition.Span, block);
            PyString[]? postponed = null;
            ExecutableInstruction[]? reads = null;
            // Lexical preparation attaches postponed text to every annotation in the declaration.
            if (annotations.Length != 0 && annotations[0].Expression.PostponedAnnotation is not null)
            {
                var bytes = 32L + 16L * annotations.Length;
                var governor = _compilation.Governor;
                governor?.Reserve(bytes, definition.Span);
                try
                {
                    _compilation.CancellationToken.ThrowIfCancellationRequested();
                    postponed = new PyString[annotations.Length];
                    for (var i = 0; i < postponed.Length; i++)
                    {
                        if ((i & 63) == 0) _compilation.CancellationToken.ThrowIfCancellationRequested();
                        var text = annotations[i].Expression.PostponedAnnotation.RequireNotNull();
                        postponed[i] = (PyString)_buffers.Constants[InternConstant(text)]!;
                    }
                    governor?.Commit(bytes);
                }
                catch { governor?.ReleaseReserved(bytes); throw; }
            }
            else
            {
                var firstBlock = block;
                var first = _buffers.Blocks[block].Count;
                foreach (var (_, expression) in annotations) block = CompileExpression(expression, block);
                if (firstBlock == block) reads = PackFunctionAnnotationReads(block, first, annotations.Length);
            }
            // The creation operation retains lowered annotations only when
            // it still evaluates them in the definition's type scope.
            EmitDefinition(new ExecutableCreateFunction(code, definition.ParameterLayout, definition.Decorators.Count,
                definition.IsGenerator, postponed is null && reads is null ? annotations.Length : 0,
                deferredAnnotations, typeParameters, postponed, reads), definition.Span, block);
            // Module declarations update their live namespace and optional slot.
            var name = definition.Name;
            if (_parameterLayout is null)
                AddInstruction(block, ExecutableInstruction.StoreName(InternName(name), definition.Span));
            else
                CompileStoreBoundName(name, definition.Span, block);
            return block;
        }

        private ExecutableInstruction[]? PackFunctionAnnotationReads(int block, int first, int count)
        {
            if (count < 2) return null;
            var instructions = _buffers.Blocks[block];
            if (instructions.Count - first != count) return null;
            for (var i = first; i < instructions.Count; i++)
            {
                if ((i & 63) == 0) _compilation.CancellationToken.ThrowIfCancellationRequested();
                if (instructions[i].OpCode is not (ExecutableOpCode.LoadGlobal or ExecutableOpCode.LoadConst)) return null;
            }

            // Definition creation consumes these direct reads before capture
            // admission. Nontrivial expressions keep their executable route.
            var reads = new ExecutableInstruction[count];
            instructions.CopyTo(first, reads, 0, count);
            instructions.RemoveRange(first, count);
            return reads;
        }

        private static T[] PrepareFunctionAnnotations<T>(FunctionDefinitionStatementSyntax definition,
            Func<PyString, ExpressionSyntax, T> prepare)
        {
            var count = definition.ReturnAnnotation is null ? 0 : 1;
            var positionalCount = 0;
            var positionalOnlyCount = 0;
            foreach (var parameter in definition.Parameters)
            {
                if (parameter.Annotation is null) continue;
                count++;
                if (parameter.Kind == FunctionParameterKind.Positional) positionalCount++;
                else if (parameter.Kind == FunctionParameterKind.PositionalOnly) positionalOnlyCount++;
            }
            if (count == 0) return [];

            // Exchange the two positional groups for Python's annotation order.
            // The remaining parameter kinds already appear in evaluation order.
            var annotations = new T[count];
            var nextPositional = 0;
            var nextPositionalOnly = positionalCount;
            var nextOther = positionalCount + positionalOnlyCount;
            foreach (var parameter in definition.Parameters)
            {
                if (parameter.Annotation is not { } expression) continue;
                var index = parameter.Kind switch
                {
                    FunctionParameterKind.Positional => nextPositional++,
                    FunctionParameterKind.PositionalOnly => nextPositionalOnly++,
                    _ => nextOther++,
                };
                annotations[index] = prepare(PyString.FromString(parameter.Name), expression);
            }
            if (definition.ReturnAnnotation is { } result) annotations[^1] = prepare(PyString.FromString("return"), result);
            return annotations;
        }

        private int CompileLambda(LoweredLambdaExpression lambda, int block)
        {
            block = CompileFunctionDefaults(lambda.Lambda.Parameters, lambda.ParameterLayout, lambda.Span, block);
            if (lambda.PreparedCode is null && _classBody)
                CompileNamespaceLambda(lambda, _compilation);
            else if (lambda.PreparedCode is null)
                lambda.CompleteCompilation(CompileFunctionCode("<lambda>", lambda.ParameterLayout,
                    [], lambda.ScopeFacts, generator: lambda.IsGenerator,
                    qualifiedName: lambda.Lambda.PreparedQualifiedName, lambda: lambda));
            EmitDefinition(new ExecutableCreateLambda(lambda.Binding), lambda.Span, block);
            return block;
        }

        private int CompileFunctionDefaults(IReadOnlyList<FunctionParameterSyntax> parameters,
            FunctionParameterLayout layout, LythonSourceSpan span, int block)
        {
            if (layout.Defaults.Length == 0) return block;
            layout.PrepareKeywordDefaultKeys(_compilation.CancellationToken, span);
            EmitOperation(new ExecutableStartFunctionDefaults(layout), span, block);
            for (var i = 0; i < layout.Defaults.Length; i++)
            {
                var slot = layout.Defaults[i];
                var expression = parameters[slot.ParameterIndex].DefaultValue.RequireNotNull();
                block = CompileExpression(expression, block);
                var positional = slot.KeywordIndex < 0;
                var index = positional ? i : layout.KeywordDefaultAt(slot.KeywordIndex).RecordPosition;
                EmitOperation(new ExecutableAppendFunctionDefault(positional ? layout.DefaultStorageCount : 1, index),
                    expression.Span, block);
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
            EmitDefinition(new ExecutableCreateClass(syntax, body, typeParameters, genericHeader, expandedHeader, baseCount), syntax.Span, block);
            return block;
        }
    }
}
