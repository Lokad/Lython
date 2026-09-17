using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

internal sealed partial class ExecutableScript
{
    private sealed partial class Builder
    {
        private int CompileExpression(LoweredExpression expression, int currentBlock)
        {
            switch (expression)
            {
                case LoweredParenthesizedExpression parenthesized:
                    currentBlock = CompileExpression(parenthesized.Inner, currentBlock);
                    return currentBlock;

                case LoweredStringLiteralExpression text:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyString.FromString(text.Literal.Value)), text.Span));
                    return currentBlock;

                case LoweredBytesLiteralExpression bytes:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(new PyBytes(bytes.Literal.Value.ToArray())), bytes.Span));
                    return currentBlock;

                case LoweredIntegerLiteralExpression integer:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyNumberOps.ParseInteger(integer.Literal.ValueText)), integer.Span));
                    return currentBlock;

                case LoweredFloatLiteralExpression floating:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyNumberOps.ParseFloat(floating.Literal.ValueText)), floating.Span));
                    return currentBlock;

                case LoweredBooleanLiteralExpression boolean:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(boolean.Literal.Value), boolean.Span));
                    return currentBlock;

                case LoweredNoneLiteralExpression none:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyNone.Instance), none.Span));
                    return currentBlock;

                case LoweredEllipsisLiteralExpression ellipsis:
                    AddInstruction(currentBlock, ExecutableInstruction.LoadConst(InternConstant(PyEllipsis.Instance), ellipsis.Span));
                    return currentBlock;

                case LoweredIdentifierExpression identifier:
                    CompileLoadIdentifier(identifier.Identifier.Name, identifier.Span, currentBlock);
                    return currentBlock;

                case LoweredMemberExpression member:
                    currentBlock = CompileExpression(member.Target, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.LoadMember(InternName(member.Member.MemberName), AllocateMemberCache(), member.Span));
                    return currentBlock;

                case LoweredCallExpression call:
                    if (!TryCompileCallExpression(call, currentBlock, out currentBlock))
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(call), call.Span));
                    }
                    return currentBlock;

                case LoweredListLiteralExpression list:
                    if (list.List.HasUnpacking)
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(list), list.Span));
                        return currentBlock;
                    }

                    foreach (var item in list.Items)
                    {
                        currentBlock = CompileExpression(item.Expression, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeList(list.Items.Count, list.Span));
                    return currentBlock;

                case LoweredTupleLiteralExpression tuple:
                    if (tuple.Tuple.HasUnpacking)
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(tuple), tuple.Span));
                        return currentBlock;
                    }

                    foreach (var item in tuple.Items)
                    {
                        currentBlock = CompileExpression(item.Expression, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeTuple(tuple.Items.Count, tuple.Span));
                    return currentBlock;

                case LoweredSetLiteralExpression set:
                    if (set.Set.HasUnpacking)
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(set), set.Span));
                        return currentBlock;
                    }

                    foreach (var item in set.Items)
                    {
                        currentBlock = CompileExpression(item.Expression, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeSet(set.Items.Count, set.Span));
                    return currentBlock;

                case LoweredDictLiteralExpression dict:
                    if (dict.Items.Any(item => item.IsUnpacking))
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(dict), dict.Span));
                        return currentBlock;
                    }

                    foreach (var item in dict.Items)
                    {
                        currentBlock = CompileExpression(item.Key, currentBlock);
                        currentBlock = CompileExpression(item.Value, currentBlock);
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.MakeDict(dict.Items.Count, dict.Span));
                    return currentBlock;

                case LoweredSubscriptExpression subscript:
                    currentBlock = CompileExpression(subscript.Target, currentBlock);
                    currentBlock = CompileExpression(subscript.Index, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Subscript(subscript.Span));
                    return currentBlock;

                case LoweredSliceExpression slice:
                    currentBlock = CompileExpression(slice.Target, currentBlock);
                    var parts = ExecutableSliceParts.None;
                    if (slice.Start is not null)
                    {
                        currentBlock = CompileExpression(slice.Start, currentBlock);
                        parts |= ExecutableSliceParts.Start;
                    }
                    if (slice.End is not null)
                    {
                        currentBlock = CompileExpression(slice.End, currentBlock);
                        parts |= ExecutableSliceParts.End;
                    }
                    if (slice.Step is not null)
                    {
                        currentBlock = CompileExpression(slice.Step, currentBlock);
                        parts |= ExecutableSliceParts.Step;
                    }
                    AddInstruction(currentBlock, ExecutableInstruction.Slice(parts, slice.Span));
                    return currentBlock;

                case LoweredBinaryExpression binary:
                    if (binary.Binary.Operator is BinaryOperatorSyntax.Or or BinaryOperatorSyntax.And)
                    {
                        AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(binary), binary.Span));
                        return currentBlock;
                    }

                    currentBlock = CompileExpression(binary.Left, currentBlock);
                    currentBlock = CompileExpression(binary.Right, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Binary(MapBinaryOperator(binary.Binary.Operator), binary.Span));
                    return currentBlock;

                case LoweredUnaryExpression unary:
                    currentBlock = CompileExpression(unary.Operand, currentBlock);
                    AddInstruction(currentBlock, ExecutableInstruction.Unary(MapUnaryOperator(unary.Unary.Operator), unary.Span));
                    return currentBlock;

                case LoweredConditionalExpression conditional:
                    return CompileConditionalExpression(conditional, currentBlock);

                default:
                    AddInstruction(currentBlock, ExecutableInstruction.EvaluateFallbackExpression(InternExpressionFallback(expression), expression.Span));
                    return currentBlock;
            }
        }

        private bool TryCompileCallExpression(LoweredCallExpression call, int currentBlock, out int exitBlock)
        {
            exitBlock = currentBlock;
            foreach (var argument in call.Arguments)
            {
                if (argument.Kind is CallArgumentKind.StarredList or CallArgumentKind.StarredDictionary)
                {
                    return false;
                }
            }

            exitBlock = CompileExpression(call.Target, exitBlock);
            foreach (var argument in call.Arguments)
            {
                exitBlock = CompileExpression(argument.Expression, exitBlock);
            }

            AddInstruction(exitBlock, ExecutableInstruction.Call(InternCallSite(call), AllocateCallCache(), call.Span));
            return true;
        }

        // Conditional expressions branch natively instead of paying one
        // lowered-dispatch state machine per evaluation. The shape mirrors
        // CompileIfStatement: the condition value feeds JumpIfFalse, and
        // exactly one side leaves its value for the join, so both edges
        // arrive with the same stack depth.
        private int CompileConditionalExpression(LoweredConditionalExpression conditional, int currentBlock)
        {
            currentBlock = CompileExpression(conditional.Condition, currentBlock);

            var consequentBlock = CreateBlock();
            var alternativeBlock = CreateBlock();
            var joinBlock = CreateBlock();

            AddInstruction(currentBlock, ExecutableInstruction.JumpIfFalse(alternativeBlock, conditional.Condition.Span));
            AddInstruction(currentBlock, ExecutableInstruction.Jump(consequentBlock, conditional.Conditional.Span));

            var consequentExit = CompileExpression(conditional.Consequent, consequentBlock);
            if (!IsTerminated(consequentExit))
            {
                AddInstruction(consequentExit, ExecutableInstruction.Jump(joinBlock, conditional.Consequent.Span));
            }

            var alternativeExit = CompileExpression(conditional.Alternative, alternativeBlock);
            if (!IsTerminated(alternativeExit))
            {
                AddInstruction(alternativeExit, ExecutableInstruction.Jump(joinBlock, conditional.Alternative.Span));
            }

            return joinBlock;
        }

        private static ExecutableBinaryOperator MapBinaryOperator(BinaryOperatorSyntax op)
            => op switch
            {
                BinaryOperatorSyntax.Or => ExecutableBinaryOperator.Or,
                BinaryOperatorSyntax.And => ExecutableBinaryOperator.And,
                BinaryOperatorSyntax.Add => ExecutableBinaryOperator.Add,
                BinaryOperatorSyntax.Subtract => ExecutableBinaryOperator.Subtract,
                BinaryOperatorSyntax.Multiply => ExecutableBinaryOperator.Multiply,
                BinaryOperatorSyntax.Divide => ExecutableBinaryOperator.Divide,
                BinaryOperatorSyntax.FloorDivide => ExecutableBinaryOperator.FloorDivide,
                BinaryOperatorSyntax.Modulo => ExecutableBinaryOperator.Modulo,
                BinaryOperatorSyntax.Power => ExecutableBinaryOperator.Power,
                BinaryOperatorSyntax.BitwiseOr => ExecutableBinaryOperator.BitwiseOr,
                BinaryOperatorSyntax.BitwiseXor => ExecutableBinaryOperator.BitwiseXor,
                BinaryOperatorSyntax.BitwiseAnd => ExecutableBinaryOperator.BitwiseAnd,
                BinaryOperatorSyntax.LeftShift => ExecutableBinaryOperator.LeftShift,
                BinaryOperatorSyntax.RightShift => ExecutableBinaryOperator.RightShift,
                BinaryOperatorSyntax.Equal => ExecutableBinaryOperator.Equal,
                BinaryOperatorSyntax.NotEqual => ExecutableBinaryOperator.NotEqual,
                BinaryOperatorSyntax.Less => ExecutableBinaryOperator.Less,
                BinaryOperatorSyntax.LessEqual => ExecutableBinaryOperator.LessEqual,
                BinaryOperatorSyntax.Greater => ExecutableBinaryOperator.Greater,
                BinaryOperatorSyntax.GreaterEqual => ExecutableBinaryOperator.GreaterEqual,
                BinaryOperatorSyntax.Is => ExecutableBinaryOperator.Is,
                BinaryOperatorSyntax.IsNot => ExecutableBinaryOperator.IsNot,
                BinaryOperatorSyntax.In => ExecutableBinaryOperator.In,
                BinaryOperatorSyntax.NotIn => ExecutableBinaryOperator.NotIn,
                _ => throw new ExecutableLoweringFallbackException($"Executable IR lowering does not support binary operator {op}."),
            };

        private static ExecutableUnaryOperator MapUnaryOperator(UnaryOperatorSyntax op)
            => op switch
            {
                UnaryOperatorSyntax.Not => ExecutableUnaryOperator.Not,
                UnaryOperatorSyntax.Plus => ExecutableUnaryOperator.Plus,
                UnaryOperatorSyntax.Minus => ExecutableUnaryOperator.Minus,
                UnaryOperatorSyntax.BitwiseNot => ExecutableUnaryOperator.BitwiseNot,
                _ => throw new ExecutableLoweringFallbackException($"Executable IR lowering does not support unary operator {op}."),
            };

        private static ExecutableAugmentedOperator MapAugmentedAssignmentOperator(AugmentedAssignmentOperatorSyntax op)
            => op switch
            {
                AugmentedAssignmentOperatorSyntax.Add => ExecutableAugmentedOperator.Add,
                AugmentedAssignmentOperatorSyntax.Subtract => ExecutableAugmentedOperator.Subtract,
                AugmentedAssignmentOperatorSyntax.Multiply => ExecutableAugmentedOperator.Multiply,
                AugmentedAssignmentOperatorSyntax.Divide => ExecutableAugmentedOperator.Divide,
                AugmentedAssignmentOperatorSyntax.FloorDivide => ExecutableAugmentedOperator.FloorDivide,
                AugmentedAssignmentOperatorSyntax.Modulo => ExecutableAugmentedOperator.Modulo,
                AugmentedAssignmentOperatorSyntax.Power => ExecutableAugmentedOperator.Power,
                AugmentedAssignmentOperatorSyntax.BitwiseOr => ExecutableAugmentedOperator.BitwiseOr,
                AugmentedAssignmentOperatorSyntax.BitwiseXor => ExecutableAugmentedOperator.BitwiseXor,
                AugmentedAssignmentOperatorSyntax.BitwiseAnd => ExecutableAugmentedOperator.BitwiseAnd,
                AugmentedAssignmentOperatorSyntax.LeftShift => ExecutableAugmentedOperator.LeftShift,
                AugmentedAssignmentOperatorSyntax.RightShift => ExecutableAugmentedOperator.RightShift,
                _ => throw new ExecutableLoweringFallbackException($"Executable IR lowering does not support augmented assignment operator {op}."),
            };

    }
}
