namespace Lokad.Lython.Frontend;

internal sealed record ExecutablePostponedAnnotation(ExpressionSyntax Annotation) : ExecutableOperation;
internal sealed record ExecutableStoreModuleAnnotation(string Name) : ExecutableOperation;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileModuleAnnotation(LoweredAnnotatedAssignmentStatement statement, int block)
        {
            var postponed = CreateBlock();
            var after = CreateBlock();
            var name = statement.Assignment.IsSimple && statement.Assignment.Target is NameAssignmentTargetSyntax target
                ? target.Name : null;
            EmitOperation(new ExecutableAnnotationsEnabled(), statement.Span, block);
            AddInstruction(block, ExecutableInstruction.JumpIfFalse(postponed, statement.Span));
            block = CompileExpression(statement.Annotation, block);
            if (name is null) AddInstruction(block, ExecutableInstruction.PopTop(statement.Annotation.Span));
            else EmitOperation(new ExecutableStoreModuleAnnotation(name), statement.Span, block);
            AddInstruction(block, ExecutableInstruction.Jump(after, statement.Span));
            if (name is not null)
            {
                EmitOperation(new ExecutablePostponedAnnotation(statement.Annotation.Syntax), statement.Annotation.Span, postponed);
                EmitOperation(new ExecutableStoreModuleAnnotation(name), statement.Span, postponed);
            }
            AddInstruction(postponed, ExecutableInstruction.Jump(after, statement.Span));
            return after;
        }
    }
}
