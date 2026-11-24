namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileAssert(AssertStatementSyntax statement, int block)
        {
            block = CompileExpression(statement.Condition, block);
            var failure = CreateBlock();
            var after = CreateBlock();
            AddInstruction(block, ExecutableInstruction.JumpIfFalse(failure, statement.Condition.Span));
            AddInstruction(block, ExecutableInstruction.Jump(after, statement.Span));
            if (statement.Message is not null) failure = CompileExpression(statement.Message, failure);
            AddInstruction(failure, ExecutableInstruction.FailAssertion(statement.Message is not null, statement.Span));
            return after;
        }

        private int? CompileRaise(RaiseStatementSyntax statement, int block)
        {
            if (statement.Expression is null)
                AddInstruction(block, ExecutableInstruction.ReraiseException(statement.Span));
            else
            {
                block = CompileExpression(statement.Expression, block);
                if (statement.CauseExpression is not null) block = CompileExpression(statement.CauseExpression, block);
                AddInstruction(block, ExecutableInstruction.Raise(statement.CauseExpression is not null, statement.Span));
            }
            return null;
        }

        private int CompileDelete(ExpressionSyntax expression, int block)
        {
            if (expression is ParenthesizedExpressionSyntax parenthesized)
                return CompileDelete(parenthesized.Inner, block);
            var items = expression switch
            {
                TupleLiteralExpressionSyntax tuple => tuple.Items,
                ListLiteralExpressionSyntax list => list.Items,
                _ => null,
            };
            if (items is not null)
            {
                foreach (var item in items) block = CompileDelete(item.Expression, block);
                return block;
            }
            switch (expression)
            {
                case IdentifierExpressionSyntax name:
                    // Deletion alone captures a nonlocal just like a read/store;
                    // its defining frame can have relinquished executable slots.
                    if (_scopeFacts.IsNonlocal(name.Name) && HasEnclosingBinding(name.Name))
                        InternClosure(name.Name);
                    AddInstruction(block, ExecutableInstruction.DeleteName(InternName(name.Name), expression.Span));
                    break;
                case MemberExpressionSyntax member:
                    block = CompileExpression(member.Target, block);
                    AddInstruction(block, ExecutableInstruction.DeleteMember(InternName(member.MemberName), expression.Span));
                    break;
                case SubscriptExpressionSyntax item:
                    block = CompileExpression(item.Target, block);
                    block = CompileExpression(item.Index, block);
                    AddInstruction(block, ExecutableInstruction.DeleteSubscript(expression.Span));
                    break;
                case SliceExpressionSyntax slice:
                    block = CompileExpression(slice.Target, block);
                    var parts = ExecutableSliceParts.None;
                    if (slice.Start is not null)
                    {
                        block = CompileExpression(slice.Start, block);
                        parts |= ExecutableSliceParts.Start;
                    }
                    if (slice.End is not null)
                    {
                        block = CompileExpression(slice.End, block);
                        parts |= ExecutableSliceParts.End;
                    }
                    if (slice.Step is not null)
                    {
                        block = CompileExpression(slice.Step, block);
                        parts |= ExecutableSliceParts.Step;
                    }
                    AddInstruction(block, ExecutableInstruction.DeleteSlice(parts, expression.Span));
                    break;
                default: throw new InvalidOperationException("Invalid delete target.");
            }
            return block;
        }
    }
}
