namespace Lokad.Lython.Frontend;

internal sealed record ExecutableStoreModuleAnnotation(string Name) : ExecutableOperation;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileModuleAnnotation(LoweredAnnotatedAssignmentStatement statement, int block)
        {
            var name = statement.Assignment.IsSimple && statement.Assignment.Target is NameAssignmentTargetSyntax target
                ? target.Name : null;
            if (statement.Annotation.Syntax.PostponedAnnotation is { } text)
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
