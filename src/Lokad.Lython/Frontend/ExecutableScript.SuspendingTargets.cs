namespace Lokad.Lython.Frontend;

internal sealed record ExecutableUnpackValues(UnpackingLayout Layout) : ExecutableOperation;
internal sealed record ExecutableStorePreparedTarget(AssignmentTargetSyntax Target, bool ValueLast) : ExecutableOperation;
internal sealed record ExecutableReadPreparedTarget(AssignmentTargetSyntax Target) : ExecutableOperation;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileSuspendingStoreTarget(AssignmentTargetSyntax target, LythonSourceSpan span, int block)
        {
            if (target is UnpackingAssignmentTargetGroupSyntax group)
            {
                EmitOperation(new ExecutableUnpackValues(UnpackingLayout.FromTargets(group.Targets)), span, block);
                foreach (var child in group.Targets)
                    block = CompileStoreTarget(AssignmentTargetFacts.FromUnpacking(child), child.Span, block);
                return block;
            }
            foreach (var read in AssignmentTargetFacts.Reads(target))
                block = CompileExpression(LoweredScript.LowerExpression(read), block);
            EmitOperation(new ExecutableStorePreparedTarget(target, ValueLast: false), span, block);
            return block;
        }

        private int CompileSuspendingLoopTarget(LoopTargetSyntax target, LythonSourceSpan span, int block)
        {
            switch (target)
            {
                case LoopNameTargetSyntax name: CompileStoreBoundName(name.Name, span, block); return block;
                case LoopStarredTargetSyntax star: CompileStoreBoundName(star.Name, span, block); return block;
                case LoopStoreTargetSyntax store: return CompileStoreTarget(store.Target, span, block);
                case LoopTupleTargetSyntax tuple:
                    var starIndex = -1;
                    for (var i = 0; i < tuple.Items.Count; i++)
                        if (AssignmentTargetFacts.IsStarred(tuple.Items[i])) starIndex = i;
                    EmitOperation(new ExecutableUnpackValues(new UnpackingLayout(tuple.Items.Count, starIndex)), span, block);
                    foreach (var child in tuple.Items) block = CompileSuspendingLoopTarget(child, span, block);
                    return block;
                default: throw new InvalidOperationException("Unknown loop target.");
            }
        }

        private int CompileSuspendingAugmentedAssignment(LoweredAugmentedAssignmentStatement statement, int block)
        {
            foreach (var read in AssignmentTargetFacts.Reads(statement.Target.Syntax))
                block = CompileExpression(LoweredScript.LowerExpression(read), block);
            EmitOperation(new ExecutableReadPreparedTarget(statement.Target.Syntax), statement.Span, block);
            block = CompileExpression(statement.Expression, block);
            AddInstruction(block, ExecutableInstruction.Augmented(MapAugmentedAssignmentOperator(statement.Assignment.Operator), statement.Span));
            EmitOperation(new ExecutableStorePreparedTarget(statement.Target.Syntax, ValueLast: true), statement.Span, block);
            return block;
        }
    }
}
