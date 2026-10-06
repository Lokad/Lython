namespace Lokad.Lython.Frontend;

internal sealed record ExecutableFailAssertion(bool HasMessage) : ExecutableOperation;
internal sealed record ExecutableRaise(bool HasCause) : ExecutableOperation;
internal sealed record ExecutableDeletePreparedTarget(AssignmentTargetSyntax Target) : ExecutableOperation;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileSuspendingAssert(LoweredAssertStatement statement, int block)
        {
            block = CompileExpression(statement.Condition, block);
            var failure = CreateBlock();
            var after = CreateBlock();
            AddInstruction(block, ExecutableInstruction.JumpIfFalse(failure, statement.Condition.Span));
            AddInstruction(block, ExecutableInstruction.Jump(after, statement.Span));
            if (statement.Message is not null) failure = CompileExpression(statement.Message, failure);
            EmitOperation(new ExecutableFailAssertion(statement.Message is not null), statement.Span, failure);
            // The operation throws. A structural jump also terminates this block.
            AddInstruction(failure, ExecutableInstruction.Jump(after, statement.Span));
            return after;
        }

        private int CompileSuspendingRaise(LoweredRaiseStatement statement, int block)
        {
            if (statement.Expression is null)
                AddInstruction(block, ExecutableInstruction.ReraiseException(statement.Span));
            else
            {
                block = CompileExpression(statement.Expression, block);
                if (statement.CauseExpression is not null) block = CompileExpression(statement.CauseExpression, block);
                EmitOperation(new ExecutableRaise(statement.CauseExpression is not null), statement.Span, block);
            }
            return block;
        }

        private int CompileSuspendingDelete(ExpressionSyntax expression, int block)
        {
            if (expression is ParenthesizedExpressionSyntax parenthesized)
                return CompileSuspendingDelete(parenthesized.Inner, block);
            var items = expression switch
            {
                TupleLiteralExpressionSyntax tuple => tuple.Items,
                ListLiteralExpressionSyntax list => list.Items,
                _ => null,
            };
            if (items is not null)
            {
                foreach (var item in items) block = CompileSuspendingDelete(item.Expression, block);
                return block;
            }
            AssignmentTargetSyntax target = expression switch
            {
                IdentifierExpressionSyntax name => new NameAssignmentTargetSyntax(name.Name, name.Span),
                MemberExpressionSyntax member => new MemberAssignmentTargetSyntax(member.Target, member.MemberName, member.Span),
                SubscriptExpressionSyntax item => new SubscriptAssignmentTargetSyntax(item.Target, item.Index, item.Span),
                SliceExpressionSyntax slice => new SliceAssignmentTargetSyntax(slice.Target, slice.Start, slice.End, slice.Step, slice.Span),
                _ => throw new InvalidOperationException("Invalid delete target."),
            };
            foreach (var read in AssignmentTargetFacts.Reads(target))
                block = CompileExpression(LoweredScript.LowerExpression(read), block);
            EmitOperation(new ExecutableDeletePreparedTarget(target), expression.Span, block);
            return block;
        }
    }
}
