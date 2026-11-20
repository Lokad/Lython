namespace Lokad.Lython.Frontend;

internal sealed record ExecutableStoreModuleAnnotation(string Name) : ExecutableOperation;
internal sealed record ExecutableBindTypeAlias(LoweredTypeAlias Alias) : ExecutableOperation;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileModuleAnnotation(AnnotatedAssignmentStatementSyntax statement, int block)
        {
            var name = statement.IsSimple && statement.Target is NameAssignmentTargetSyntax target
                ? target.Name : null;
            if (statement.Annotation.PostponedAnnotation is { } text)
            {
                if (name is null) return block;
                AddInstruction(block, ExecutableInstruction.LoadConst(InternConstant(text), statement.Annotation.Span));
                EmitOperation(new ExecutableStoreModuleAnnotation(name), statement.Span, block);
                return block;
            }

            block = CompileExpression(statement.Annotation, block);
            if (name is null) AddInstruction(block, ExecutableInstruction.PopTop(statement.Annotation.Span));
            else EmitOperation(new ExecutableStoreModuleAnnotation(name), statement.Span, block);
            return block;
        }
    }
}
