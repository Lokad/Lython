namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileGeneratorExpression(GeneratorExpressionSyntax syntax, int block, bool joinedTrimmedSplit = false)
        {
            var expression = (LoweredGeneratorExpression)LoweredScript.LowerExpression(syntax);
            if (expression.PreparedCode is null)
            {
                var builder = new Builder(_compilation, FunctionParameterLayout.Create([]), this, GeneratorFacts(expression),
                    completeLexicalParent: _completeLexicalParent, moduleCode: _moduleCode, generator: true);
                expression.CompleteCompilation(builder.CompileCodeObject("<genexpr>", [], expression));
            }
            var code = expression.Code;
            block = CompileExpression(syntax.Clauses[0].Iterable, block);
            EmitDefinition(new ExecutableCreateGeneratorExpression(code, syntax.Clauses[0].Iterable.Span, joinedTrimmedSplit), expression.Span, block);
            return block;
        }

        private static bool IsTrimmedSplitGenerator(GeneratorExpressionSyntax expression)
            => expression.Clauses is [{ Condition: null, Target: LoopNameTargetSyntax variable,
                    Iterable: CallExpressionSyntax
                    {
                        Target: MemberExpressionSyntax { MemberName: "split" },
                        Arguments: [{ Kind: CallArgumentKind.Positional }],
                    } }]
                && expression.ItemExpression is CallExpressionSyntax
                {
                    Target: MemberExpressionSyntax { MemberName: "rstrip", Target: IdentifierExpressionSyntax item },
                    Arguments.Count: 0,
                }
                && item.Name == variable.Name;

        private int CompileComprehension(ComprehensionExpressionSyntax syntax, ExpressionSyntax item,
            ExpressionSyntax? value, ExecutableDisplayKind kind, LythonSourceSpan span, int block)
        {
            var facts = syntax.PreparedScopeFacts.RequireNotNull();
            var clauses = syntax.Clauses;
            var forwardsBinding = kind == ExecutableDisplayKind.List && _parameterLayout is not null && !_generator &&
                _buffers.ComprehensionNames.Count == 0 && facts.LocalBindingOrder.Length == 1 &&
                clauses is [{ Target: LoopNameTargetSyntax variable, Condition: null }] &&
                CanForwardComprehensionItem(item, variable.Name);
            // The outer iterable belongs to the caller's scope. All remaining
            // expressions stay in this executable frame under the existing
            // comprehension binding lifetime, rather than a new invocation.
            block = CompileExpression(clauses[0].Iterable, block);
            EmitOperation(new ExecutableStartComprehension(kind, facts, forwardsBinding), span, block);
            AddInstruction(block, ExecutableInstruction.GetIter(clauses[0].Iterable.Span));
            _buffers.ComprehensionNames.Push(facts.LocalNames);
            try
            {
                block = CompileComprehensionBody(clauses, item, value, span, block, yielding: false, forwardsBinding);
                EmitOperation(new ExecutableFinishComprehension(), span, block);
                return block;
            }
            finally { _buffers.ComprehensionNames.Pop(); }
        }

        // The item must start with exactly one use of the binding and have no
        // nested scopes, stores or branches. Other operands are constants.
        private bool CanForwardComprehensionItem(ExpressionSyntax item, string name)
        {
            while (true)
            {
                PythonTokenization.CheckCancellation(_compilation.CancellationToken, 0);
                switch (item)
                {
                    case IdentifierExpressionSyntax identifier: return identifier.Name == name;
                    case ParenthesizedExpressionSyntax parenthesized: item = parenthesized.Inner; break;
                    case SubscriptExpressionSyntax subscript when IsProjectionConstant(subscript.Index): item = subscript.Target; break;
                    case BinaryExpressionSyntax binary when binary.Operator is not (BinaryOperatorSyntax.And or BinaryOperatorSyntax.Or)
                        && IsProjectionConstant(binary.Right): item = binary.Left; break;
                    case UnaryExpressionSyntax unary: item = unary.Operand; break;
                    default: return false;
                }
            }
        }

        private static bool IsProjectionConstant(ExpressionSyntax expression)
            => expression is StringLiteralExpressionSyntax or IntegerLiteralExpressionSyntax or BytesLiteralExpressionSyntax
                or FloatLiteralExpressionSyntax or BooleanLiteralExpressionSyntax or NoneLiteralExpressionSyntax;

        private int CompileComprehensionBody(IReadOnlyList<ComprehensionClauseSyntax> clauses,
            ExpressionSyntax item, ExpressionSyntax? value, LythonSourceSpan span, int block, bool yielding, bool forwardsBinding = false)
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
                if (forwardsBinding)
                {
                    EmitOperation(new ExecutableForwardComprehensionItem(), clause.Iterable.Span, head);
                    block = head;
                }
                else
                {
                    var store = LoweredStoreTarget.FromLoop(clause.Target, clause.Iterable.Span);
                    block = CompileStoreTarget(store, clause.Iterable.Span, head, clause.Iterable.Span);
                }
                if (clause.Condition is not null)
                {
                    block = CompileExpression(clause.Condition, block);
                    AddInstruction(block, ExecutableInstruction.JumpIfFalse(head, clause.Condition.Span));
                }
            }
            var itemBlock = _buffers.Blocks[block];
            var first = itemBlock.Count;
            block = CompileExpression(item, block);
            if (forwardsBinding)
            {
                // ForwardComprehensionItem leaves the leading identifier's
                // ordinary operand on the stack alongside the scope binding.
                System.Diagnostics.Debug.Assert(itemBlock[first].OpCode == ExecutableOpCode.LoadName);
                itemBlock.RemoveAt(first);
            }
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
