namespace Lokad.Lython.Frontend;

internal sealed record ExecutableUnpackValues(UnpackingLayout Layout) : ExecutableOperation;
internal sealed record ExecutableStorePreparedTarget(AssignmentTargetSyntax Target, bool ValueLast) : ExecutableOperation;
internal sealed record ExecutableStoreSubscript(LythonSourceSpan TargetSpan) : ExecutableOperation;
internal sealed record ExecutableStoreMember(string MemberName, LythonSourceSpan TargetSpan) : ExecutableOperation;
internal sealed record ExecutableStoreSlice(LythonSourceSpan TargetSpan) : ExecutableOperation;
internal sealed record ExecutableReadPreparedTarget(AssignmentTargetSyntax Target) : ExecutableOperation;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileSuspendingStoreTarget(AssignmentTargetSyntax target, LythonSourceSpan span, int block)
        {
            foreach (var read in AssignmentTargetFacts.Reads(target))
                block = CompileExpression(LoweredScript.LowerExpression(read), block);
            EmitOperation(new ExecutableStorePreparedTarget(target, ValueLast: false), span, block);
            return block;
        }

        private int CompileLoopTarget(LoopTargetSyntax target, LythonSourceSpan span, int block)
        {
            switch (target)
            {
                case LoopNameTargetSyntax name: CompileLoopBinding(name.Name, span, block); return block;
                case LoopStarredTargetSyntax star: CompileLoopBinding(star.Name, span, block); return block;
                case LoopStoreTargetSyntax store: return CompileStoreTarget(store.Target, span, block);
                case LoopTupleTargetSyntax tuple:
                    var starIndex = -1;
                    for (var i = 0; i < tuple.Items.Count; i++)
                        if (AssignmentTargetFacts.IsStarred(tuple.Items[i])) starIndex = i;
                    EmitOperation(new ExecutableUnpackValues(new UnpackingLayout(tuple.Items.Count, starIndex)), span, block);
                    foreach (var child in tuple.Items) block = CompileLoopTarget(child, span, block);
                    return block;
                default: throw new InvalidOperationException("Unknown loop target.");
            }
        }

        private void CompileLoopBinding(string name, LythonSourceSpan span, int block)
        {
            if (_comprehensionNames.Any(names => names.Contains(name)))
            {
                // The resolved store selects inlined bindings before caller directives.
                AddInstruction(block, ExecutableInstruction.StoreGlobal(InternName(name), span));
            }
            else CompileStoreBoundName(name, span, block);
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
