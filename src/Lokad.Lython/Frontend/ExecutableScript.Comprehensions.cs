namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileComprehension(ScopeDirectiveFacts facts,
            IReadOnlyList<LoweredComprehensionClause> clauses, LoweredExpression item,
            LoweredExpression? value, ExecutableDisplayKind kind, LythonSourceSpan span, int block)
        {
            // The outer iterable belongs to the caller's scope. All remaining
            // expressions stay in this executable frame under the existing
            // comprehension binding lifetime, rather than a new invocation.
            block = CompileExpression(clauses[0].Iterable, block);
            EmitOperation(new ExecutableStartComprehension(kind, facts), span, block);
            AddInstruction(block, ExecutableInstruction.GetIter(clauses[0].Iterable.Span));
            _comprehensionNames.Push(facts.LocalNames);
            try
            {
                var loops = new (int Head, int Exit)[clauses.Count];
                for (var index = 0; index < clauses.Count; index++)
                {
                    var clause = clauses[index];
                    if (index != 0)
                    {
                        block = CompileExpression(clause.Iterable, block);
                        AddInstruction(block, ExecutableInstruction.GetIter(clause.Iterable.Span));
                    }
                    var head = CreateBlock();
                    var body = CreateBlock();
                    var exit = CreateBlock();
                    loops[index] = (head, exit);
                    AddInstruction(block, ExecutableInstruction.Jump(head, clause.Span));
                    AddInstruction(head, ExecutableInstruction.ForNext(exit, clause.Iterable.Span));
                    var targetExit = CompileLoopTarget(clause.Target, clause.Iterable.Span, head);
                    AddInstruction(targetExit, ExecutableInstruction.Jump(body, clause.Span));
                    block = body;
                    if (clause.Condition is not null)
                    {
                        block = CompileExpression(clause.Condition, block);
                        var accepted = CreateBlock();
                        AddInstruction(block, ExecutableInstruction.JumpIfFalse(head, clause.Condition.Span));
                        AddInstruction(block, ExecutableInstruction.Jump(accepted, clause.Condition.Span));
                        block = accepted;
                    }
                }
                block = CompileExpression(item, block);
                if (value is not null)
                {
                    block = CompileExpression(value, block);
                    EmitOperation(new ExecutableStoreDictionaryItem(false, clauses.Count), item.Span, block);
                }
                else EmitOperation(new ExecutableAppendDisplay(false, clauses.Count), item.Span, block);
                AddInstruction(block, ExecutableInstruction.Jump(loops[^1].Head, span));
                for (var index = clauses.Count - 2; index >= 0; index--)
                    AddInstruction(loops[index + 1].Exit, ExecutableInstruction.Jump(loops[index].Head, span));
                EmitOperation(new ExecutableFinishComprehension(), span, loops[0].Exit);
                return loops[0].Exit;
            }
            finally { _comprehensionNames.Pop(); }
        }
    }
}
