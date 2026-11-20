namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileGeneratorExpression(LoweredGeneratorExpression expression, int block, bool joinedTrimmedSplit = false)
        {
            if (expression.PreparedCode is null)
            {
                var builder = new Builder(_compilation, FunctionParameterLayout.Create([]), this, GeneratorFacts(expression),
                    completeLexicalParent: _completeLexicalParent, moduleCode: _moduleCode, generator: true);
                expression.CompleteCompilation(builder.CompileCodeObject("<genexpr>", [], expression));
            }
            var code = expression.Code;
            block = CompileExpression(expression.Clauses[0].Iterable, block);
            EmitOperation(new ExecutableCreateGeneratorExpression(code, expression.Clauses[0].Iterable.Span, joinedTrimmedSplit), expression.Span, block);
            return block;
        }

        private static bool IsTrimmedSplitGenerator(LoweredGeneratorExpression expression)
            => expression.Clauses is [{ Condition: null, Target: LoopNameTargetSyntax variable,
                    Iterable: LoweredCallExpression
                    {
                        Target: LoweredMemberExpression { Member.MemberName: "split" },
                        Arguments: [{ Kind: CallArgumentKind.Positional }],
                    } }]
                && expression.ItemExpression is LoweredCallExpression
                {
                    Target: LoweredMemberExpression { Member.MemberName: "rstrip", Target: LoweredIdentifierExpression item },
                    Arguments.Count: 0,
                }
                && item.Identifier.Name == variable.Name;

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
            _buffers.ComprehensionNames.Push(facts.LocalNames);
            try
            {
                block = CompileComprehensionBody(clauses, item, value, span, block, yielding: false);
                EmitOperation(new ExecutableFinishComprehension(), span, block);
                return block;
            }
            finally { _buffers.ComprehensionNames.Pop(); }
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
                var exit = CreateBlock();
                loops[index] = (head, exit);
                AddInstruction(block, ExecutableInstruction.Jump(head, clause.Span));
                AddInstruction(head, ExecutableInstruction.ForNext(exit, clause.Iterable.Span));
                var targetExit = CompileStoreTarget(clause.StoreTarget, clause.Iterable.Span, head, clause.Iterable.Span);
                block = targetExit;
                if (clause.Condition is not null)
                {
                    block = CompileExpression(clause.Condition, block);
                    AddInstruction(block, ExecutableInstruction.JumpIfFalse(head, clause.Condition.Span));
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
