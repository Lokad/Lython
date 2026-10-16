namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileGeneratorExpression(LoweredGeneratorExpression expression, int block)
        {
            var locals = new HashSet<string>(expression.ScopeFacts.LocalNames, StringComparer.Ordinal) { ".0" };
            var facts = expression.ScopeFacts with { LocalNames = locals };
            var builder = new Builder([], ChildClosureCandidates(), facts) { _generator = true };
            var code = builder.CompileCodeObject("<genexpr>", [], expression);
            block = CompileExpression(expression.Clauses[0].Iterable, block);
            EmitOperation(new ExecutableCreateGeneratorExpression(code, expression.Clauses[0].Iterable.Span), expression.Span, block);
            return block;
        }

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
                block = CompileComprehensionBody(clauses, item, value, span, block, yielding: false);
                EmitOperation(new ExecutableFinishComprehension(), span, block);
                return block;
            }
            finally { _comprehensionNames.Pop(); }
        }

        private int CompileComprehensionBody(IReadOnlyList<LoweredComprehensionClause> clauses,
            LoweredExpression item, LoweredExpression? value, LythonSourceSpan span, int block, bool yielding)
        {
            // The eager caller or the retained generator frame already owns
            // the acquired outer iterator on this value stack.
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
            if (yielding)
            {
                AddInstruction(block, ExecutableInstruction.Yield(false, item.Span));
                AddInstruction(block, ExecutableInstruction.PopTop(item.Span));
            }
            else if (value is not null)
            {
                block = CompileExpression(value, block);
                EmitOperation(new ExecutableStoreDictionaryItem(false, clauses.Count), item.Span, block);
            }
            else EmitOperation(new ExecutableAppendDisplay(false, clauses.Count), item.Span, block);
            AddInstruction(block, ExecutableInstruction.Jump(loops[^1].Head, span));
            for (var index = clauses.Count - 2; index >= 0; index--)
                AddInstruction(loops[index + 1].Exit, ExecutableInstruction.Jump(loops[index].Head, span));
            return loops[0].Exit;
        }
    }
}
